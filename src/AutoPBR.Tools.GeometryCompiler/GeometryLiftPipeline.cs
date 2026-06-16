using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Shared jar → geometry IR roots lift used by the compiler host and CI reconciliation tests.
/// </summary>
internal static class GeometryLiftPipeline
{
    internal readonly record struct MeshDisassemblyResolution(
        string Stdout,
        string HostJvmName,
        string JavapArg,
        string MeshConcat);

    internal readonly record struct MeshLiftAttempt(
        JsonArray Roots,
        List<string> Notes,
        string LiftProfile,
        string MeshHostJvmName,
        string MeshConcat,
        byte[]? HostClassBytes);

    public static bool TryLiftRoots(
        string? javapExe,
        string clientJar,
        MojangMappingsParser? maps,
        string officialJvmName,
        string factoryMethod,
        out JsonArray roots,
        out List<string> notes)
    {
        roots = [];
        notes = [];

        if (TryLiftWithJavapFallback(javapExe, clientJar, maps, officialJvmName, factoryMethod, preferAsm: true,
                out var attempt))
        {
            roots = attempt.Roots;
            notes = attempt.Notes;
            return true;
        }

        notes.AddRange(attempt.Notes);
        return false;
    }

    /// <summary>
    /// Tries bytecode ASM concat lift first; on failure retries with deep <c>javap -c</c> concat (comment-rich).
    /// </summary>
    public static bool TryLiftWithJavapFallback(
        string? javapExe,
        string clientJar,
        MojangMappingsParser? maps,
        string officialJvmName,
        string factoryMethod,
        bool preferAsm,
        out MeshLiftAttempt attempt)
    {
        javapExe ??= JavapLocator.FindJavap();
        attempt = new MeshLiftAttempt([], [], "none", officialJvmName, "", null);
        var allNotes = new List<string>();

        BytecodeMeshResolution.Result? bytecodeMesh = null;
        if (preferAsm && BytecodeMeshResolution.TryResolve(clientJar, maps, officialJvmName, factoryMethod, out var br))
        {
            bytecodeMesh = br;
        }

        if (bytecodeMesh is not null)
        {
            var bc = bytecodeMesh.Value;
            if (TryLiftMeshConcat(bc.MeshConcat, bc.PrimaryClassBytes, maps, officialJvmName, out var asmRoots,
                    out var asmNotes, out var asmProfile) &&
                CountAllCuboids(asmRoots) > 0)
            {
                attempt = new MeshLiftAttempt(asmRoots, asmNotes, asmProfile,
                    bc.HostJvmName, bc.MeshConcat, bc.PrimaryClassBytes);
                return true;
            }

            allNotes.AddRange(asmNotes);
        }

        var javapMesh = TryResolveMeshDisassembly(javapExe, clientJar, maps, officialJvmName);
        if (javapMesh is null || javapMesh.Value.MeshConcat.Length == 0)
        {
            allNotes.Add("No javap deep mesh concat resolved from jar.");
            attempt = attempt with { Notes = allNotes };
            return false;
        }

        byte[]? hostBytes = null;
        string? hostObfuscatedJvmName = null;
        _ = maps?.TryGetObfuscated(javapMesh.Value.HostJvmName, out hostObfuscatedJvmName);
        if (ClientJarIO.TryResolveJarEntry(clientJar, javapMesh.Value.HostJvmName, hostObfuscatedJvmName, out _, out var hostClassBytes))
        {
            hostBytes = hostClassBytes;
        }

        if (TryLiftMeshConcat(javapMesh.Value.MeshConcat, hostBytes, maps, officialJvmName, out var javapRoots,
                out var javapNotes, out var javapProfile, useAsmFirst: false) &&
            CountAllCuboids(javapRoots) > 0)
        {
            javapNotes.Insert(0, "Lifted from javap deep concat after bytecode path produced no cuboids.");
            attempt = new MeshLiftAttempt(javapRoots, javapNotes, javapProfile,
                javapMesh.Value.HostJvmName, javapMesh.Value.MeshConcat, hostBytes);
            return true;
        }

        allNotes.AddRange(javapNotes);
        attempt = attempt with { Notes = allNotes };
        return false;
    }

    private static bool TryLiftMeshConcat(
        string meshText,
        byte[]? hostClassBytes,
        MojangMappingsParser? maps,
        string officialJvmName,
        out JsonArray roots,
        out List<string> notes,
        out string liftProfile,
        bool useAsmFirst = true)
    {
        roots = [];
        notes = [];
        liftProfile = "javap_mesh_named_26_1_2";
        var delegationDepth = meshText.Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker).Length - 1;

        if (useAsmFirst && hostClassBytes is { Length: > 0 })
        {
            if (BytecodeGeometryMeshLift.TryLiftConcat(meshText, maps, out var asmRoots, out var asmNotes,
                    hostClassBytes) &&
                CountAllCuboids(asmRoots) > 0)
            {
                roots = asmRoots;
                notes = asmNotes;
                liftProfile = "bytecode_asm";
                ApplyLiftedMeshPostProcessing(roots, meshText, officialJvmName);
                return true;
            }

            notes.AddRange(asmNotes);
        }

        IReadOnlyDictionary<string, int[][]>? staticIntMatrices = null;
        IReadOnlyDictionary<string, float[]>? staticFloatArrays = null;
        if (hostClassBytes is { Length: > 0 })
        {
            staticIntMatrices = JvmStaticIntMatrixExtractor.ExtractFromClass(hostClassBytes);
            staticFloatArrays = JvmStaticFloatArrayExtractor.ExtractFromClass(hostClassBytes);
        }

        if (JavapFloatGeometryMeshLift.TryLift(meshText, out var javapRoots, out var javapNotes, maps, delegationDepth,
                staticIntMatrices, staticFloatArrays) &&
            CountAllCuboids(javapRoots) > 0)
        {
            roots = javapRoots;
            notes = javapNotes;
            liftProfile = maps is null ? "javap_mesh_named_26_1_2" : "javap_mesh_proguard_obf";
            ApplyLiftedMeshPostProcessing(roots, meshText, officialJvmName);
            return true;
        }

        notes.AddRange(javapNotes);
        return false;
    }

    internal static MeshDisassemblyResolution? TryResolveMeshDisassembly(
        string? javapExe,
        string clientJar,
        MojangMappingsParser? maps,
        string officialJvmName)
    {
        if (LayerDefinitionMeshHostMap.TryGet(officialJvmName, out var layerHost) &&
            TryResolveLayerDefinitionMeshDisassembly(javapExe, clientJar, maps, officialJvmName, layerHost,
                out var layerDisasm))
        {
            return layerDisasm;
        }

        foreach (var host in MeshHostClassCandidates.Enumerate(officialJvmName))
        {
            string? obh = null;
            _ = maps?.TryGetObfuscated(host, out obh);
            var arg = obh is null ? host : MojangMappingsParser.GetJavapClassArgForObfuscated(obh);
            if (!JavapClassDisassembly.TryDisassemble(javapExe, clientJar, arg, out var stdout, out _))
            {
                continue;
            }

            // Prefer deep concat so delegated MeshDefinition factories (HumanoidModel.createMesh, VillagerModel.createBodyModel, …)
            // are merged before the host createBodyLayer island; shallow Named-only concat misses cross-class invokestatic.
            var meshConcat = JavapClassDisassembly.ConcatMeshFactoryCodeDeep(javapExe, clientJar, stdout, host, maps,
                arg);
            if (!JavapMeshBytecodeProfiles.ContainsMeshSignals(meshConcat))
            {
                meshConcat = JavapClassDisassembly.ConcatMeshFactoryCodeNamed(stdout);
            }

            if (!JavapMeshBytecodeProfiles.ContainsMeshSignals(meshConcat))
            {
                continue;
            }

            return new MeshDisassemblyResolution(stdout, host, arg, meshConcat);
        }

        return null;
    }

    private static bool TryResolveLayerDefinitionMeshDisassembly(
        string? javapExe,
        string clientJar,
        MojangMappingsParser? maps,
        string officialJvmName,
        LayerDefinitionMeshHostMap.MeshHostSpec layerHost,
        out MeshDisassemblyResolution resolution)
    {
        resolution = default;
        if (!BytecodeMeshResolution.TryResolve(clientJar, maps, officialJvmName, layerHost.FactoryMethod, out var mesh))
        {
            return false;
        }

        string? obh = null;
        _ = maps?.TryGetObfuscated(mesh.HostJvmName, out obh);
        var arg = obh is null ? mesh.HostJvmName : MojangMappingsParser.GetJavapClassArgForObfuscated(obh);
        if (!JavapClassDisassembly.TryDisassemble(javapExe, clientJar, arg, out var stdout, out _))
        {
            stdout = string.Empty;
        }

        resolution = new MeshDisassemblyResolution(stdout, mesh.HostJvmName, arg, mesh.MeshConcat);
        return true;
    }

    private static void ApplyLiftedMeshPostProcessing(JsonArray roots, string meshConcat, string officialJvmName)
    {
        ApplyDeferredMeshFactoryPostProcessing(roots, meshConcat, officialJvmName);
        LayerDefinitionRetainAtlasStamp.ApplyToLiftedRoots(roots, meshConcat);
        PreviewDepthLayerIrStamp.ApplyToLiftedRoots(roots, officialJvmName);
    }

    private static void ApplyDeferredMeshFactoryPostProcessing(JsonArray roots, string meshConcat, string officialJvmName)
    {
        if (ShouldApplyClearRecursivelyMeshPass(meshConcat, officialJvmName, out var passStartIdx))
        {
            JavapFloatGeometryMeshLift.ClearCuboidsRecursively(roots);
            var tail = meshConcat[passStartIdx..];
            if (!JavapFloatGeometryMeshLift.TryLift(tail, out var tailRoots, out _, maps: null, delegationDepth: 0) ||
                CountAllCuboids(tailRoots) == 0)
            {
                return;
            }

            var bindingPartIds = CollectBindingPartIdsFromJavapTail(tail);
            if (string.Equals(officialJvmName, "net.minecraft.client.model.player.PlayerCapeModel", StringComparison.Ordinal))
            {
                bindingPartIds = new HashSet<string>(StringComparer.Ordinal) { "cape" };
            }

            OverlayCuboidsByPartId(roots, tailRoots, bindingPartIds);
        }

        ApplyKnownArmorStandBodyOverride(roots, officialJvmName);
    }

    private static void ApplyKnownArmorStandBodyOverride(JsonArray roots, string officialJvmName)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.object.armorstand.ArmorStandModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!TryFindPartById(roots, "body", out var body))
        {
            return;
        }

        body!["cuboids"] = new JsonArray
        {
            new JsonObject
            {
                ["from"] = new JsonArray(-6, 0, -1.5),
                ["to"] = new JsonArray(6, 3, 1.5),
                ["uvOrigin"] = new JsonArray(0, 26),
                ["textureKey"] = "#skin",
                ["liftKind"] = "exact",
                ["provenance"] = "known ArmorStandModel.createBodyLayer body override"
            }
        };
    }

    private static bool TryFindPartById(JsonArray level, string partId, out JsonObject? part)
    {
        foreach (var node in level)
        {
            if (node is not JsonObject current)
            {
                continue;
            }

            if (string.Equals((string?)current["id"], partId, StringComparison.Ordinal))
            {
                part = current;
                return true;
            }

            if (current["children"] is JsonArray children &&
                TryFindPartById(children, partId, out part))
            {
                return true;
            }
        }

        part = null;
        return false;
    }

    private static bool ShouldApplyClearRecursivelyMeshPass(string meshConcat, string officialJvmName, out int passStartIdx)
    {
        passStartIdx = 0;
        if (string.Equals(officialJvmName, "net.minecraft.client.model.player.PlayerCapeModel", StringComparison.Ordinal))
        {
            passStartIdx = meshConcat.IndexOf("clearRecursively", StringComparison.Ordinal);
            if (passStartIdx < 0)
            {
                passStartIdx = meshConcat.IndexOf("createCapeLayer", StringComparison.Ordinal);
            }

            if (passStartIdx < 0)
            {
                passStartIdx = 0;
            }

            return true;
        }

        // HumanoidModel / PlayerModel call clearRecursively inside createMesh (hat/jacket shells).
        // Only run the post-lift overlay pass when clearRecursively lives in a later mesh island
        // (e.g. createBodyLayer delegates createMesh, then strips parts before rebinding).
        var boundary = JavapClassDisassembly.GeometryMeshIslandBoundaryMarker;
        var boundaryIdx = meshConcat.IndexOf(boundary, StringComparison.Ordinal);
        passStartIdx = meshConcat.IndexOf("clearRecursively", StringComparison.Ordinal);
        return passStartIdx >= 0 && boundaryIdx >= 0 && passStartIdx > boundaryIdx;
    }

    private static HashSet<string> CollectBindingPartIdsFromJavapTail(string tailJavap)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var lines = tailJavap.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(lines[i]))
            {
                continue;
            }

            var name = JavapFloatGeometryMeshLift.TryResolveBindingPartNameForDiagnostics(lines, i);
            if (!string.IsNullOrEmpty(name))
            {
                ids.Add(name);
            }
        }

        return ids;
    }

    private static void OverlayCuboidsByPartId(JsonArray targetRoots, JsonArray overlayRoots,
        IReadOnlySet<string> onlyPartIds)
    {
        var byId = new Dictionary<string, JsonArray>(StringComparer.Ordinal);
        foreach (var n in overlayRoots)
        {
            if (n is JsonObject o)
            {
                CollectCuboidsByPartId(o, byId, onlyPartIds);
            }
        }

        foreach (var n in targetRoots)
        {
            if (n is JsonObject o)
            {
                ApplyCuboidOverlay(o, byId);
            }
        }
    }

    private static void CollectCuboidsByPartId(JsonObject part, Dictionary<string, JsonArray> byId,
        IReadOnlySet<string> onlyPartIds)
    {
        if (part["id"] is JsonValue idNode &&
            part["cuboids"] is JsonArray cuboids &&
            cuboids.Count > 0)
        {
            var id = idNode.GetValue<string>();
            if (onlyPartIds.Contains(id))
            {
                byId[id] = JsonNode.Parse(cuboids.ToJsonString())!.AsArray();
            }
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co)
                {
                    CollectCuboidsByPartId(co, byId, onlyPartIds);
                }
            }
        }
    }

    private static void ApplyCuboidOverlay(JsonObject part, IReadOnlyDictionary<string, JsonArray> byId)
    {
        if (part["id"] is JsonValue idNode &&
            byId.TryGetValue(idNode.GetValue<string>(), out var cuboids))
        {
            part["cuboids"] = JsonNode.Parse(cuboids.ToJsonString())!.AsArray();
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co)
                {
                    ApplyCuboidOverlay(co, byId);
                }
            }
        }
    }

    private static int CountAllCuboids(JsonArray roots)
    {
        var n = 0;
        foreach (var node in roots)
        {
            if (node is JsonObject p)
            {
                n += CountPartCuboids(p);
            }
        }

        return n;
    }

    private static int CountPartCuboids(JsonObject part)
    {
        var n = part["cuboids"] is JsonArray c ? c.Count : 0;
        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co)
                {
                    n += CountPartCuboids(co);
                }
            }
        }

        return n;
    }
}
