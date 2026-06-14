using System.Text.Json;
using System.Text.Json.Nodes;


namespace AutoPBR.Tools.GeometryCompiler;

internal sealed partial class GeometryCompilerHost
{
    private static bool TryResolveLiftedAtlasDimensions(
        string? meshText,
        GeometryLiftPipeline.MeshDisassemblyResolution? meshDisasm,
        out int textureWidth,
        out int textureHeight)
    {
        textureWidth = 0;
        textureHeight = 0;
        if (!string.IsNullOrEmpty(meshText) &&
            LayerDefinitionAtlasSizeProbe.TryRead(meshText, out textureWidth, out textureHeight))
        {
            return true;
        }

        if (meshDisasm is { Stdout.Length: > 0 } disasm &&
            LayerDefinitionAtlasSizeProbe.TryRead(disasm.Stdout, out textureWidth, out textureHeight))
        {
            return true;
        }

        return false;
    }

    private static bool IsPackageInfoStub(string officialJvmName) =>
        officialJvmName.EndsWith(".package-info", StringComparison.Ordinal);

    private static int CountAllCuboids(JsonArray roots)
    {
        var n = 0;
        foreach (var node in roots)
        {
            if (node is not JsonObject p)
            {
                continue;
            }

            if (p["cuboids"] is JsonArray c)
            {
                n += c.Count;
            }

            if (p["children"] is JsonArray ch)
            {
                n += CountAllCuboids(ch);
            }
        }

        return n;
    }

    /// <summary>Removes duplicate string entries (e.g. repeated javap probe warnings across regenerations).</summary>
    private static void DedupeExtractionNotes(JsonObject root)
    {
        if (root["extractionNotes"] is not JsonArray a || a.Count < 2)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var next = new JsonArray();
        foreach (var n in a)
        {
            if (n is JsonValue jv && jv.TryGetValue<string>(out var s))
            {
                if (!seen.Add(s))
                {
                    continue;
                }
            }

            next.Add(n!.DeepClone());
        }

        root["extractionNotes"] = next;
    }

    /// <summary>
    /// Picks an on-disk JSON file to use as the merge base, or returns <c>null</c> to synthesize a minimal schema-compliant shard.
    /// </summary>
    private bool ShouldUseExistingShardAsMergeTemplate(ReadOnlySpan<byte> classBytes, string officialJvmName)
    {
        if (officialJvmName.Contains('$', StringComparison.Ordinal) ||
            IsPackageInfoStub(officialJvmName) ||
            JvmClassFileParser.IsInterface(classBytes))
        {
            return false;
        }

        return ProguardMeshFactoryDetection.HasResolvableMeshFactory(_maps, officialJvmName, classBytes);
    }

    private static void FinalizeShardExtractionStatus(JsonObject json, string officialJvmName)
    {
        if (json["roots"] is not JsonArray roots || CountAllCuboids(roots) == 0)
        {
            if (string.Equals((string?)json["extractionStatus"], "heuristic", StringComparison.Ordinal))
            {
                json["extractionStatus"] = "partial";
            }

            return;
        }

        if (json.ContainsKey("delegatedFromOfficialJvmName"))
        {
            json["extractionStatus"] = "ok";
            return;
        }

        StripHandmadeExtractionNotes(json);
        json["extractionStatus"] = "ok";
        var strictValidation = GeometryIrStructuralValidator.ValidateShard(json,
            officialJvmName,
            new GeometryIrStructuralValidator.Options(Strict: true));
        var treeValidation = GeometryIrLiftTreeValidator.ValidateRoots(roots, officialJvmName);
        var mergeIssues = strictValidation.Issues.Concat(treeValidation.Issues).ToList();
        var ok = strictValidation.IsValid && treeValidation.IsValid;
        ok = GeometryIrReferenceBakeGate.Apply(officialJvmName, json, ok, mergeIssues);
        json["extractionStatus"] = ok ? "ok" : "partial";
    }

    private static void StripHandmadeExtractionNotes(JsonObject json)
    {
        if (json["extractionNotes"] is not JsonArray notes || notes.Count == 0)
        {
            return;
        }

        var kept = new JsonArray();
        foreach (var n in notes)
        {
            if (n is not JsonValue jv || !jv.TryGetValue<string>(out var s))
            {
                kept.Add(n!.DeepClone());
                continue;
            }

            if (s.Contains("CleanRoom", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Derived from CleanRoom", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("temporarily mirrored", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Synced from CleanRoom", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("hand-synced", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("flattened approximations", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            kept.Add(JsonValue.Create(s));
        }

        json["extractionNotes"] = kept;
    }

    private string ResolveFactoryMethodFromMeshHosts(string officialJvmName, string requested,
        ReadOnlySpan<byte> classBytes)
    {
        var resolved = MeshFactoryMethodResolver.Resolve(_maps, officialJvmName, requested, classBytes);
        foreach (var host in MeshHostClassCandidates.Enumerate(officialJvmName))
        {
            if (!ClientJarIO.TryResolveJarEntry(_clientJar, host, null, out _, out var hostBytes))
            {
                continue;
            }

            if (BytecodeMeshResolution.ShouldSkipMeshHostWithoutPrimaryFactory(host, hostBytes, requested, _maps))
            {
                continue;
            }

            return MeshFactoryMethodResolver.Resolve(_maps, officialJvmName, requested, hostBytes);
        }

        return resolved;
    }

    private void ApplyNonMeshShardFinalization(JsonObject json, ReadOnlySpan<byte> classBytes, string officialJvmName,
        bool liftSucceeded, MojangMappingsParser? maps, string factoryMethod)
    {
        if (liftSucceeded)
        {
            return;
        }

        if (GeometryIrDelegatedMeshCopy.TryApply(_outDir, _versionLabel, officialJvmName, json, out _))
        {
            return;
        }

        var hasDelegatedMeshHost = _useAsmLift &&
            BytecodeMeshResolution.TryResolve(_clientJar, maps, officialJvmName, factoryMethod, out _);
        if (GeometryIrNonMeshClassifier.TryClassify(officialJvmName, classBytes, maps, out var skipKind, out var skipNote) ||
            JvmClassFileParser.IsInterface(classBytes) ||
            officialJvmName.Contains('$', StringComparison.Ordinal) ||
            IsPackageInfoStub(officialJvmName) ||
            ProguardMeshFactoryDetection.IsMeshDefinitionTransformerOnly(maps, officialJvmName) ||
            ProguardMeshFactoryDetection.IsMeshDefinitionTransformerOnly(classBytes, maps) ||
            (!ProguardMeshFactoryDetection.HasResolvableMeshFactory(maps, officialJvmName, classBytes) &&
             !hasDelegatedMeshHost))
        {
            json["extractionStatus"] = "skipped";
            json["roots"] = CreateSkippedRoots();
            var note = !string.IsNullOrEmpty(skipNote)
                ? skipNote
                : ProguardMeshFactoryDetection.IsMeshDefinitionTransformerOnly(maps, officialJvmName) ||
                  ProguardMeshFactoryDetection.IsMeshDefinitionTransformerOnly(classBytes, maps)
                    ? $"MeshDefinition transformer on {officialJvmName} (e.g. apply(MeshDefinition)); no static part-tree factory to lift."
                    : $"No static mesh factory on {officialJvmName}; structural lift skipped (interface, inner class, or non-mesh type).";
            json["extractionNotes"] = new JsonArray(note);
            if (!string.IsNullOrEmpty(skipKind))
            {
                json["skipKind"] = skipKind;
            }
        }
    }
}
