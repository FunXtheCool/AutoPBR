using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// Piglin family (26.1.2): <c>AbstractPiglinModel.addHead</c> ears must lift and match Java reference bakes.
/// </summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class GeometryIrPiglinReferenceTests
{
    private static readonly (string Jvm, int RefPartCount, bool StrictReference)[] PilotPiglins =
    [
        ("net.minecraft.client.model.monster.piglin.AdultPiglinModel", 15, true),
        ("net.minecraft.client.model.monster.piglin.AdultZombifiedPiglinModel", 15, true),
        ("net.minecraft.client.model.monster.piglin.BabyPiglinModel", 12, true),
        ("net.minecraft.client.model.monster.piglin.BabyZombifiedPiglinModel", 12, true),
    ];

    private static string? ClientJar =>
        File.Exists(Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar"))
            ? Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar")
            : null;

    [Theory]
    [MemberData(nameof(PilotCases))]
    public void Lifted_shard_matches_reference_cuboids_and_piglin_ears(string jvm, int refPartCount, bool strictReference)
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        var refPath = Path.Combine(Program.FindRepoRoot(), "tools", "MinecraftGeometryReference", "reference-output",
            $"{jvm}.json");
        Assert.True(File.Exists(refPath), $"missing reference bake: {jvm}");

        Assert.True(GeometryLiftPipeline.TryLiftRoots(GeometryJavapLocator.FindJavap(), jar, null, jvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));

        var shard = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["officialJvmName"] = jvm,
            ["extractionStatus"] = "ok",
            ["roots"] = roots,
            ["liftSummary"] = GeometryIrLiftSummaryBuilder.BuildFromRoots(roots)
        };

        var head = FindPartById(roots, "head");
        Assert.NotNull(head);
        var headKids = head["children"]!.AsArray();
        AssertChildPartId(headKids, "left_ear");
        AssertChildPartId(headKids, "right_ear");

        if (strictReference)
        {
            using var irDoc = System.Text.Json.JsonDocument.Parse(shard.ToJsonString());
            var entry = GeometryIrLiftQualityReport.AnalyzeShard(jvm, "ok", irDoc.RootElement, Program.FindRepoRoot());
            Assert.True(entry.ReferenceCuboidsMatch, entry.ReferenceCompareMessage);
            Assert.Equal(refPartCount, CountParts(irDoc.RootElement));
        }
        else
        {
            Assert.InRange(CountParts(System.Text.Json.JsonDocument.Parse(shard.ToJsonString()).RootElement),
                refPartCount - 2, refPartCount + 2);
        }
    }

    [Fact]
    public void AbstractPiglin_addHead_extracts_from_companion_class_bytes()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string sig =
            " addHead(Lnet/minecraft/client/model/geom/builders/CubeDeformation;Lnet/minecraft/client/model/geom/builders/MeshDefinition;);";
        ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.monster.piglin.AbstractPiglinModel", null, out _, out var bytes);
        var block = BytecodeGeometryMeshLift.TryExtractMethodBlockFromClass(bytes, sig);
        Assert.NotNull(block);
        Assert.True(JavapMeshBytecodeProfiles.ContainsMeshSignals(block));
    }

    [Fact]
    public void AdultPiglin_createBodyLayer_bytecode_includes_addHead_invoke()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.piglin.AdultPiglinModel";
        ClientJarIO.TryResolveJarEntry(jar, jvm, null, out _, out var bytes);
        var layer = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(bytes, ["createBodyLayer"], out _);
        var refs = JavapMeshBytecodeProfiles.EnumerateInvokeStaticMeshRefs(layer).ToList();
        Assert.Contains(refs, r => string.Equals(r.Method, "addHead", StringComparison.Ordinal));
    }

    [Fact]
    public void Null_owner_addHead_sig_extracts_from_abstract_companion()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.piglin.AdultPiglinModel";
        ClientJarIO.TryResolveJarEntry(jar, jvm, null, out _, out var hostBytes);
        var layer = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(hostBytes, ["createBodyLayer"], out _);
        var addHeadRef = JavapMeshBytecodeProfiles.EnumerateInvokeStaticMeshRefs(layer)
            .First(r => string.Equals(r.Method, "addHead", StringComparison.Ordinal));
        Assert.Equal(jvm, addHeadRef.OwnerJarSimple);
        var sig = $" {addHeadRef.Method}({addHeadRef.ArgsInner});";
        ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.monster.piglin.AbstractPiglinModel", null, out _, out var abstractBytes);
        Assert.Null(BytecodeGeometryMeshLift.TryExtractMethodBlockFromClass(hostBytes, sig));
        Assert.NotNull(BytecodeGeometryMeshLift.TryExtractMethodBlockFromClass(abstractBytes, sig));
    }

    [Fact]
    public void BuildMeshConcatDeep_for_AdultZombified_includes_addHead_and_lifts_ears()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.piglin.AdultZombifiedPiglinModel";
        ClientJarIO.TryResolveJarEntry(jar, jvm, null, out _, out var hostBytes);
        var concat = BytecodeMeshResolution.BuildMeshConcatDeep(jar, null, jvm, hostBytes, "createBodyLayer");
        const string sig =
            " addHead(Lnet/minecraft/client/model/geom/builders/CubeDeformation;Lnet/minecraft/client/model/geom/builders/MeshDefinition;);";
        ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.monster.piglin.AbstractPiglinModel", null, out _, out var abstractBytes);
        var addHeadBody = BytecodeGeometryMeshLift.TryExtractMethodBlockFromClass(abstractBytes, sig);
        Assert.NotNull(addHeadBody);
        Assert.Contains(addHeadBody, concat, StringComparison.Ordinal);
        Assert.True(BytecodeGeometryMeshLift.TryLiftConcat(concat, null, out var roots, out var notes),
            string.Join("; ", notes));
        var head = FindPartById(roots, "head");
        Assert.NotNull(head);
        AssertChildPartId(head["children"]!.AsArray(), "left_ear");
    }

    [Fact]
    public void AdultZombified_delegated_mesh_concat_includes_addHead_and_lifts_ears()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.piglin.AdultZombifiedPiglinModel";
        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, jvm, "createBodyLayer", out var resolved));
        Assert.Contains("addHead", resolved.MeshConcat, StringComparison.Ordinal);
        var islands = resolved.MeshConcat
            .Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        Assert.True(islands.Count >= 3, $"island count={islands.Count}");
        Assert.True(BytecodeMeshResolution.TryResolve(jar, null,
            "net.minecraft.client.model.monster.piglin.AdultPiglinModel", "createBodyLayer", out var adultResolved));
        Assert.Contains(adultResolved.MeshConcat, resolved.MeshConcat, StringComparison.Ordinal);
        Assert.True(GeometryLiftPipeline.TryLiftRoots(GeometryJavapLocator.FindJavap(), jar, null, jvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));
        var head = FindPartById(roots, "head");
        Assert.NotNull(head);
        AssertChildPartId(head["children"]!.AsArray(), "left_ear");
    }

    [Fact]
    public void BuildMeshConcatDeep_appends_abstract_addHead_after_host_layer()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.piglin.AdultPiglinModel";
        ClientJarIO.TryResolveJarEntry(jar, jvm, null, out _, out var hostBytes);
        ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.monster.piglin.AbstractPiglinModel", null, out _, out var abstractBytes);
        var addHeadIsland = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(abstractBytes, ["addHead"], out var ok);
        Assert.True(ok);
        var concat = BytecodeMeshResolution.BuildMeshConcatDeep(jar, null, jvm, hostBytes, "createBodyLayer");
        Assert.Contains(addHeadIsland, concat, StringComparison.Ordinal);
    }

    [Fact]
    public void AdultPiglin_mesh_concat_includes_liftable_addHead_island()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.monster.piglin.AbstractPiglinModel", null, out _, out var abstractBytes);
        var addHeadIsland = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(abstractBytes, ["addHead"], out var ok);
        Assert.True(ok);
        Assert.True(BytecodeMeshResolution.TryResolve(jar, null,
            "net.minecraft.client.model.monster.piglin.AdultPiglinModel", "createBodyLayer", out var resolved));
        Assert.Contains(addHeadIsland, resolved.MeshConcat, StringComparison.Ordinal);
    }

    [Fact]
    public void AdultPiglin_addHead_island_lifts_nested_ears()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.piglin.AdultPiglinModel";
        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, jvm, "createBodyLayer", out var resolved));
        var islands = resolved.MeshConcat
            .Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        var liftedAddHead = islands.FirstOrDefault(i =>
        {
            if (!JavapFloatGeometryMeshLift.TryLift(i, out var islandRoots, out _))
            {
                return false;
            }

            var h = FindPartById(islandRoots, "head");
            return h?["children"] is JsonArray kids &&
                   kids.Any(n => n is JsonObject j && string.Equals((string?)j["id"], "left_ear", StringComparison.Ordinal));
        });
        Assert.NotNull(liftedAddHead);
    }

    [Fact]
    public void AdultPiglin_mesh_concat_places_addHead_after_createBodyLayer()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.piglin.AdultPiglinModel";
        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, jvm, "createBodyLayer", out var resolved));
        var islands = resolved.MeshConcat
            .Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        Assert.True(islands.Count >= 2, "expected delegated mesh + addHead islands");
        Assert.Contains("addHead", resolved.MeshConcat, StringComparison.Ordinal);
        var islandCount = resolved.MeshConcat.Split(
            JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(islandCount >= 3, $"expected PlayerModel + createBodyLayer + addHead islands, got {islandCount}");
        var addHeadIdx = -1;
        var layerIdx = -1;
        for (var i = 0; i < islands.Count; i++)
        {
            if (addHeadIdx < 0 &&
                islands[i].Contains("addHead", StringComparison.Ordinal) &&
                islands[i].Contains("addOrReplaceChild", StringComparison.Ordinal))
            {
                addHeadIdx = i;
            }

            if (layerIdx < 0 &&
                islands[i].Contains("HumanoidModel.createMesh", StringComparison.Ordinal) &&
                !islands[i].Contains("addHead", StringComparison.Ordinal))
            {
                layerIdx = i;
            }
            else if (layerIdx < 0 &&
                     islands[i].Contains("invokestatic", StringComparison.Ordinal) &&
                     islands[i].Contains("LayerDefinition.create", StringComparison.Ordinal) &&
                     islands[i].Contains("getstatic", StringComparison.Ordinal) &&
                     !islands[i].Contains("left_ear", StringComparison.Ordinal))
            {
                layerIdx = i;
            }
        }
        Assert.True(addHeadIdx > layerIdx, "addHead island should follow createBodyLayer / PlayerModel mesh");
    }

    public static IEnumerable<object[]> PilotCases() =>
        PilotPiglins.Select(t => new object[] { t.Jvm, t.RefPartCount, t.StrictReference });

    private static JsonObject? FindPartById(JsonArray roots, string id)
    {
        foreach (var node in roots)
        {
            if (node is not JsonObject root)
            {
                continue;
            }

            var found = Walk(root, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static JsonObject? Walk(JsonObject part, string id)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            return part;
        }

        if (part["children"] is not JsonArray kids)
        {
            return null;
        }

        foreach (var ch in kids)
        {
            if (ch is JsonObject co && Walk(co, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static void AssertChildPartId(JsonArray parts, string id) =>
        Assert.Contains(parts, n => n is JsonObject j && string.Equals((string?)j["id"], id, StringComparison.Ordinal));

    private static int CountParts(System.Text.Json.JsonElement doc)
    {
        var n = 0;
        if (!doc.TryGetProperty("roots", out var roots))
        {
            return 0;
        }

        foreach (var root in roots.EnumerateArray())
        {
            n += WalkPartCount(root);
        }

        return n;
    }

    private static int WalkPartCount(System.Text.Json.JsonElement part)
    {
        var n = 1;
        if (part.TryGetProperty("children", out var kids))
        {
            foreach (var ch in kids.EnumerateArray())
            {
                n += WalkPartCount(ch);
            }
        }

        return n;
    }
}
