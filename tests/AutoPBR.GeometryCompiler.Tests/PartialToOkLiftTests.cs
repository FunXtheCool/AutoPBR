using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AutoPBR.Tests.TestSupport;


using Xunit.Abstractions;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>
/// T0 jar lift + T2 committed-shard probe for partial→ok promotion list (geometry_ir_partial_to_ok_promotion_jvm.txt).
/// </summary>
[Collection(nameof(GeometryLiftSerialDefinition))]
public sealed class PartialToOkLiftTests(ITestOutputHelper output)
{
    private static readonly string[] PromotionJvmNames =
        GeometryIrTestTierSupport.LoadOfficialJvmNames(
            GeometryIrTestTierSupport.FindRepoRoot(),
            "geometry_ir_partial_to_ok_promotion_jvm.txt").ToArray();

    [Theory]
    [MemberData(nameof(FormerPartialModelNames))]
    public void Former_partial_models_pass_strict_shard_validation(string officialJvmName)
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap();
        var factoryMethod = ResolveFactoryMethod(jar, officialJvmName);
        Assert.True(GeometryLiftPipeline.TryLiftRoots(javap, jar, null, officialJvmName, factoryMethod,
                out var roots, out var notes),
            string.Join("; ", notes));

        var shard = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["officialJvmName"] = officialJvmName,
            ["extractionStatus"] = "ok",
            ["roots"] = roots,
            ["liftSummary"] = GeometryIrLiftSummaryBuilder.BuildFromRoots(roots)
        };

        var tree = GeometryIrLiftTreeValidator.ValidateRoots(roots, officialJvmName);
        var validation = GeometryIrStructuralValidator.ValidateShard(shard, officialJvmName,
            new GeometryIrStructuralValidator.Options(Strict: true));
        var allIssues = validation.Issues.Concat(tree.Issues).ToList();
        validation = new GeometryIrStructuralValidator.Result(validation.IsValid && tree.IsValid, allIssues);
        if (!validation.IsValid)
        {
            foreach (var issue in validation.Issues.Take(8))
            {
                output.WriteLine($"{issue.Code}: {issue.Message}");
            }
        }

        Assert.True(validation.IsValid, string.Join("; ", validation.Issues.Select(i => $"{i.Code}: {i.Message}")));
    }

    public static IEnumerable<object[]> FormerPartialModelNames() =>
        PromotionJvmNames.Select(m => new object[] { m });

    [Fact]
    public void BabySquidModel_loop_lift_sets_tentacle_y_rotation()
    {
        var jar = ResolveClientJar();
        Assert.True(BytecodeGeometryMeshLift.TryLiftFromJar(jar,
            "net.minecraft.client.model.animal.squid.BabySquidModel",
            "createBodyLayer",
            maps: null,
            out var roots,
            out _,
            out _));
        var tentacle = FindPartById(roots, "tentacle0");
        Assert.NotNull(tentacle);
        var rot = tentacle["pose"]!["rotationEulerRad"]!.AsArray();
        Assert.InRange(rot[1]!.GetValue<double>(), 1.5, 1.65);
    }

    [Theory]
    [MemberData(nameof(FormerPartialModelNames))]
    public void Committed_shard_file_is_ok_when_present(string jvm)
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(path, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var shard = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var validation = GeometryIrStructuralValidator.ValidateShard(shard, jvm,
            new GeometryIrStructuralValidator.Options(Strict: true));
        Assert.True(validation.IsValid, string.Join("; ", validation.Issues.Select(i => $"{i.Code}: {i.Message}")));
    }

    [Fact]
    public void HumanoidModel_mesh_concat_contains_createMesh_and_fload_for_pose_map()
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null,
                "net.minecraft.client.model.HumanoidModel",
                "createMesh",
                out var resolved));
        Assert.Contains("PartDefinition.addOrReplaceChild", resolved.MeshConcat, StringComparison.Ordinal);
        Assert.Contains("PartPose.offset", resolved.MeshConcat, StringComparison.Ordinal);
        Assert.Contains("fload_1", resolved.MeshConcat, StringComparison.Ordinal);
    }

    [Fact]
    public void HumanoidModel_lift_has_no_unknown_fload_pose_warnings()
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftFromJar(jar,
                "net.minecraft.client.model.HumanoidModel",
                "createMesh",
                maps: null,
                out var roots,
                out _,
                out _),
            "HumanoidModel mesh lift failed.");
        AssertNoPoseWarning(roots, "unknown_fload_zeroed");
    }

    [Fact]
    public void AdultCatModel_resolves_feline_mesh_host_and_lifts()
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null,
                "net.minecraft.client.model.animal.feline.AdultCatModel",
                "createBodyLayer",
                out var resolved),
            "Expected AdultFelineModel mesh host for AdultCatModel.");
        Assert.Equal("net.minecraft.client.model.animal.feline.AdultFelineModel", resolved.HostJvmName);
        Assert.True(BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.True(CountCuboids(roots) > 0);
    }

    [Fact]
    public void AdultCatModel_head_includes_main_skin_cuboid()
    {
        var jar = ResolveClientJar();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(JavapLocator.FindJavap(), jar, null,
                "net.minecraft.client.model.animal.feline.AdultCatModel", "createBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));
        var head = FindPartById(attempt.Roots, "head");
        Assert.NotNull(head);
        var cuboids = head!["cuboids"]!.AsArray();
        Assert.True(cuboids.Count >= 4, $"expected head main+nose+ears, got {cuboids.Count}");
        Assert.Contains(cuboids, c =>
            c is JsonObject o &&
            string.Equals((string?)o["textureKey"], "#main", StringComparison.Ordinal) &&
            o["from"] is JsonArray from &&
            from.Count >= 3 &&
            Math.Abs(from[0]!.GetValue<double>() - (-2.5)) < 0.01 &&
            Math.Abs(from[2]!.GetValue<double>() - (-3)) < 0.01);
    }

    [Fact]
    public void AdultCatModel_pipeline_lift_with_tree_repair()
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap();
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, null,
                "net.minecraft.client.model.animal.feline.AdultCatModel", "createBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));
        Assert.True(CountCuboids(attempt.Roots) > 0);
        var tail2 = FindPartById(attempt.Roots, "tail2");
        Assert.NotNull(tail2);
        Assert.Null(FindPartById(attempt.Roots, "tail1")?["children"]?.AsArray()
            ?.FirstOrDefault(n => n is JsonObject o && string.Equals((string?)o["id"], "tail2", StringComparison.Ordinal)));
        Assert.NotNull(FindPartById(attempt.Roots, "tail2"));
    }

    [Fact]
    public void Feline_setupAnim_wrappers_delegate_mesh_from_feline_hosts()
    {
        var root = Program.FindRepoRoot();
        foreach (var (wrapper, source, expectedBodyY) in new[]
                 {
                     ("net.minecraft.client.model.animal.feline.AdultCatModel",
                         "net.minecraft.client.model.animal.feline.AdultFelineModel",
                         12f),
                     ("net.minecraft.client.model.animal.feline.AdultOcelotModel",
                         "net.minecraft.client.model.animal.feline.AdultFelineModel",
                         12f),
                     ("net.minecraft.client.model.animal.feline.BabyCatModel",
                         "net.minecraft.client.model.animal.feline.BabyFelineModel",
                         20.5f),
                     ("net.minecraft.client.model.animal.feline.BabyOcelotModel",
                         "net.minecraft.client.model.animal.feline.BabyFelineModel",
                         20.5f),
                 })
        {
            var path = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{wrapper}.json");
            Assert.True(File.Exists(path), $"missing shard: {wrapper}");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var shard = doc.RootElement;
            Assert.Equal("ok", shard.GetProperty("extractionStatus").GetString());
            Assert.Equal(source, shard.GetProperty("delegatedFromOfficialJvmName").GetString());
            var rootsNode = JsonNode.Parse(shard.GetProperty("roots").GetRawText())!.AsArray();
            var body = FindPartById(rootsNode, "body");
            Assert.NotNull(body);
            var ty = body!["pose"]!["translation"]![1]!.GetValue<double>();
            Assert.True(Math.Abs(ty - expectedBodyY) < 0.01, $"{wrapper}: body Y={ty}, expected {expectedBodyY}");
        }
    }

    [Fact]
    public void PiglinHeadModel_resolves_skull_host_and_lifts_ok()
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null,
                "net.minecraft.client.model.object.skull.PiglinHeadModel",
                "createBodyLayer",
                out var resolved));
        Assert.Equal("net.minecraft.client.model.object.skull.SkullModel", resolved.HostJvmName);
        Assert.Contains("addOrReplaceChild", resolved.MeshConcat, StringComparison.Ordinal);
        Assert.True(BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.True(CountCuboids(roots) > 0, string.Join("; ", notes));
    }

    [Fact]
    public void ChestModel_disassembler_direction_and_util_lines_match_mask_regexes()
    {
        var direction = "     13: getstatic #97 // Field net.minecraft.core.Direction.WEST:Lnet/minecraft/core/Direction;";
        var util = "     16: invokestatic #87 // Method net.minecraft.util.Util.allOfEnumExcept:(Ljava/lang/Enum;)Ljava/util/Set;";
        Assert.Matches(new Regex(@"getstatic\s+#\d+\s+//\s*Field\s+[\w$./]*Direction\.(\w+):"), direction);
        Assert.Matches(new Regex(@"invokestatic\s+#\d+\s+//\s*Method\s+[\w$./]*Util\.allOfEnumExcept:"), util);
    }

    [Fact]
    public void ChestModel_set_addBox_resolves_mask_from_full_mesh_concat()
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null,
                "net.minecraft.client.model.object.chest.ChestModel",
                "createDoubleBodyRightLayer",
                out var resolved));
        var wide = JavapFloatGeometryMeshLift.FoldJavapWrappedBytecodeLinesForTests(
            resolved.MeshConcat.Split('\n').Select(l => l.TrimEnd('\r')).ToList());
        var addBoxIdx = wide.FindIndex(l =>
            l.Contains("addBox:(FFFFFFLjava/util/Set", StringComparison.Ordinal));
        Assert.True(addBoxIdx > 0, "expected direction-mask addBox in chest concat");
        var globalIdx = JavapFloatGeometryMeshLift.FindMeshWideLineIndexForTests(wide, wide[addBoxIdx]);
        Assert.True(globalIdx >= 0, $"mesh-wide line index not found for: {wide[addBoxIdx]}");
        var maskResult = JavapFloatGeometryMeshLift.TryParseDirectionFaceMaskForAddBoxForTests(
            wide, addBoxIdx, addBoxIdx - 1, wide, out var faceMask);
        Assert.Equal(DirectionMaskParseResult.ParsedFaces, maskResult);
        Assert.NotNull(faceMask);
        Assert.NotEmpty(faceMask);
        var invokeDesc = JavapFloatGeometryMeshLift.MergeJavapCommentContinuationForTests(wide, addBoxIdx);
        Assert.Contains("Ljava/util/Set", invokeDesc, StringComparison.Ordinal);
        Assert.True(JavapMeshBytecodeProfiles.IsNamedOrObfuscatedFloatAddBoxLine(wide[addBoxIdx], out _));
    }

    [Fact]
    public void ChestModel_double_body_layer_parses_allOfEnumExcept_masks()
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeMeshResolution.TryResolve(jar, null,
                "net.minecraft.client.model.object.chest.ChestModel",
                "createDoubleBodyRightLayer",
                out var resolved));
        Assert.Contains("allOfEnumExcept", resolved.MeshConcat, StringComparison.Ordinal);
        Assert.True(BytecodeGeometryMeshLift.TryLiftConcat(resolved.MeshConcat, null, out var roots, out var notes),
            string.Join("; ", notes));
        var masked = WalkCuboids(roots).Where(c => c["faceMask"] is JsonArray { Count: not 0 }).ToList();
        Assert.NotEmpty(masked);
        Assert.All(masked, c => Assert.Equal("exact", (string?)c["liftKind"]));
    }

    [Fact]
    public void ChestModel_direction_masked_boxes_lift_as_exact()
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftFromJar(jar,
                "net.minecraft.client.model.object.chest.ChestModel",
                "createDoubleBodyRightLayer",
                maps: null,
                out var roots,
                out _,
                out _),
            "ChestModel mesh lift failed.");
        foreach (var c in WalkCuboids(roots))
        {
            Assert.Equal("exact", (string?)c["liftKind"]);
        }
    }

    private static void AssertNoPoseWarning(JsonArray roots, string code)
    {
        foreach (var pose in WalkPoses(roots))
        {
            if (pose["liftWarnings"] is not JsonArray w)
            {
                continue;
            }

            Assert.DoesNotContain(w, n => string.Equals(n?.GetValue<string>(), code, StringComparison.Ordinal));
        }
    }

    private static IEnumerable<JsonObject> WalkPoses(JsonArray parts)
    {
        foreach (var n in parts)
        {
            if (n is not JsonObject o)
            {
                continue;
            }

            if (o["pose"] is JsonObject pose)
            {
                yield return pose;
            }

            if (o["children"] is JsonArray ch)
            {
                foreach (var p in WalkPoses(ch))
                {
                    yield return p;
                }
            }
        }
    }

    private static IEnumerable<JsonObject> WalkCuboids(JsonArray roots)
    {
        foreach (var part in WalkParts(roots))
        {
            if (part["cuboids"] is not JsonArray cuboids)
            {
                continue;
            }

            foreach (var c in cuboids.OfType<JsonObject>())
            {
                yield return c;
            }
        }
    }

    private static IEnumerable<JsonObject> WalkParts(JsonArray parts)
    {
        foreach (var n in parts)
        {
            if (n is not JsonObject o)
            {
                continue;
            }

            yield return o;
            if (o["children"] is JsonArray ch)
            {
                foreach (var child in WalkParts(ch))
                {
                    yield return child;
                }
            }
        }
    }

    private static JsonObject? FindPartById(JsonArray parts, string id)
    {
        foreach (var n in parts)
        {
            if (n is not JsonObject o)
            {
                continue;
            }

            if (string.Equals((string?)o["id"], id, StringComparison.Ordinal))
            {
                return o;
            }

            if (o["children"] is JsonArray ch)
            {
                var found = FindPartById(ch, id);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static int CountCuboids(JsonArray roots)
    {
        var n = 0;
        foreach (var _ in WalkCuboids(roots))
        {
            n++;
        }

        return n;
    }

    private static string ResolveFactoryMethod(string clientJar, string officialJvmName)
    {
        foreach (var host in MeshHostClassCandidates.Enumerate(officialJvmName))
        {
            if (!ClientJarIO.TryResolveJarEntry(clientJar, host, null, out _, out var bytes))
            {
                continue;
            }

            return MeshFactoryMethodResolver.Resolve(null, officialJvmName, "createBodyLayer", bytes);
        }

        return "createBodyLayer";
    }

    private static string ResolveClientJar()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        Assert.True(File.Exists(path), $"Missing client.jar at {path}");
        return path;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AutoPBR.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find repo root.");
    }
}
