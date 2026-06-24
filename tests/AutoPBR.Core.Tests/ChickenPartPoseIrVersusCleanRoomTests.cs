using System.Numerics;
using System.Text.Json;

using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Xunit;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Explores <b>part pose</b> from vanilla geometry IR vs CleanRoom <see cref="ModelElement.LocalToParent"/>
/// after stripping the preview LER mirror (<c>scale(-1,-1,1)</c>).
/// </summary>
/// <remarks>
/// Geometry IR lists <c>beak</c> / <c>red_thing</c> as root siblings with zero translation; in Java they are head children.
/// CleanRoom folds head look into the same <c>headPose</c> matrix for head, beak, and wattle cuboids, so merged
/// bind-pose matrices for those three elements match <b>head</b> IR pose, not the beak/wattle IR rows.
/// </remarks>
public sealed partial class ChickenPartPoseIrVersusCleanRoomTests
{
    private static string ContentPath(params string[] segments) =>
        Path.Combine([GeometryIrTestTierSupport.FindRepoRoot(), .. segments]);

    /// <summary>Matches <see cref="CleanRoomEntityModelRuntime"/> <c>EntityParityTemplate.Er</c> (Z then Y then X).</summary>
    private static Matrix4x4 Er(float xRad, float yRad, float zRad) =>
        Matrix4x4.Multiply(
            Matrix4x4.Multiply(Matrix4x4.CreateRotationZ(zRad), Matrix4x4.CreateRotationY(yRad)),
            Matrix4x4.CreateRotationX(xRad));

    private static Matrix4x4 Mul(Matrix4x4 a, Matrix4x4 b) => Matrix4x4.Multiply(a, b);

    private static Matrix4x4 Translation(float x, float y, float z) => Matrix4x4.CreateTranslation(x, y, z);

    /// <summary>Production uses <c>Er × T</c> in <see cref="GeometryIrMeshEmitter"/> (Explore GPU parity, 2026-05-28).</summary>
    private static Matrix4x4 MatrixFromIrPartPose(JsonElement pose)
    {
        var t = pose.GetProperty("translation");
        var r = pose.GetProperty("rotationEulerRad");
        var tx = (float)t[0].GetDouble();
        var ty = (float)t[1].GetDouble();
        var tz = (float)t[2].GetDouble();
        var rx = (float)r[0].GetDouble();
        var ry = (float)r[1].GetDouble();
        var rz = (float)r[2].GetDouble();
        return Mul(Er(rx, ry, rz), Translation(tx, ty, tz));
    }

    private static Dictionary<string, Matrix4x4> LoadChicken26IrBindPoseMatrices()
    {
        var path = ContentPath("docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.animal.chicken.AdultChickenModel.json");
        Assert.True(File.Exists(path), $"Missing test content: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var map = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
        foreach (var ch in doc.RootElement.GetProperty("roots")[0].GetProperty("children").EnumerateArray())
        {
            var id = ch.GetProperty("id").GetString()!;
            map[id] = MatrixFromIrPartPose(ch.GetProperty("pose"));
            if (!string.Equals(id, "head", StringComparison.Ordinal) ||
                !ch.TryGetProperty("children", out var headKids) ||
                headKids.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var sub in headKids.EnumerateArray())
            {
                var sid = sub.GetProperty("id").GetString()!;
                map[sid] = MatrixFromIrPartPose(sub.GetProperty("pose"));
            }
        }

        return map;
    }

    private static bool MatricesClose(in Matrix4x4 a, in Matrix4x4 b, float eps)
    {
        return Math.Abs(a.M11 - b.M11) <= eps && Math.Abs(a.M12 - b.M12) <= eps && Math.Abs(a.M13 - b.M13) <= eps &&
               Math.Abs(a.M14 - b.M14) <= eps &&
               Math.Abs(a.M21 - b.M21) <= eps && Math.Abs(a.M22 - b.M22) <= eps && Math.Abs(a.M23 - b.M23) <= eps &&
               Math.Abs(a.M24 - b.M24) <= eps &&
               Math.Abs(a.M31 - b.M31) <= eps && Math.Abs(a.M32 - b.M32) <= eps && Math.Abs(a.M33 - b.M33) <= eps &&
               Math.Abs(a.M34 - b.M34) <= eps &&
               Math.Abs(a.M41 - b.M41) <= eps && Math.Abs(a.M42 - b.M42) <= eps && Math.Abs(a.M43 - b.M43) <= eps &&
               Math.Abs(a.M44 - b.M44) <= eps;
    }

    private static (Matrix4x4 head, Matrix4x4 body) LoadColdChicken26IrHeadBodyPoses()
    {
        var path = ContentPath("docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.animal.chicken.ColdChickenModel.json");
        Assert.True(File.Exists(path), $"Missing test content: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Matrix4x4? headM = null, bodyM = null;
        foreach (var ch in doc.RootElement.GetProperty("roots")[0].GetProperty("children").EnumerateArray())
        {
            var id = ch.GetProperty("id").GetString()!;
            var m = MatrixFromIrPartPose(ch.GetProperty("pose"));
            if (string.Equals(id, "head", StringComparison.Ordinal))
            {
                headM = m;
            }
            else if (string.Equals(id, "body", StringComparison.Ordinal))
            {
                bodyM = m;
            }
        }

        Assert.True(headM.HasValue && bodyM.HasValue, "ColdChicken IR should define head and body poses");
        return (headM!.Value, bodyM!.Value);
    }

    /// <summary>
    /// <c>ColdChickenModel</c> IR DFS: head (2), beak, wattle, body (2), legs, wings.
    /// Stripped matrices for head/body cuboids match IR part poses at bind; legs/wings include preview idle drivers.
    /// </summary>
    [Fact]
    public void Chicken_cold_catalog_uses_runtime_geometry_ir()
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/chicken/chicken_cold.png";
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: 0f, animationTimeSeconds: 0f, out var mesh, out var provenance));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains("ColdChickenModel", provenance.Detail, StringComparison.Ordinal);
        Assert.Equal(10, mesh.Elements.Count);
    }

    [Fact]
    public void Cold_chicken_bind_pose_stripped_head_body_match_cold_ir()
    {
        GeometryIrParityPolicy.ResetForTests();
        var (headPoseIr, bodyPoseIr) = LoadColdChicken26IrHeadBodyPoses();
        const string jvm = "net.minecraft.client.model.animal.chicken.ColdChickenModel";
        var geometryRoot = LoadRepairedGeometryRoot(jvm);
        var merged = CleanRoomEntityModelRuntime.TryBuildGeometryIrModelSpaceParityMeshForTests(
            "entity/chicken/chicken_cold", jvm, 64, 32, geometryRoot, out _);
        Assert.NotNull(merged);
        Assert.Equal(10, merged!.Elements.Count);

        const float eps = 1e-4f;
        Assert.True(MatricesClose(merged.Elements[0].LocalToParent, headPoseIr, eps), "cold head main");
        Assert.True(MatricesClose(merged.Elements[1].LocalToParent, headPoseIr, eps), "cold hood");
        Assert.True(MatricesClose(merged.Elements[2].LocalToParent, headPoseIr, eps), "cold beak under head");
        Assert.True(MatricesClose(merged.Elements[3].LocalToParent, headPoseIr, eps), "cold wattle under head");
        Assert.True(MatricesClose(merged.Elements[4].LocalToParent, bodyPoseIr, eps), "cold body main");
        Assert.True(MatricesClose(merged.Elements[5].LocalToParent, bodyPoseIr, eps), "cold body crest");
    }

    [Fact]
    public void Head_pitch_preview_mesh_keeps_body_Z_clustered_with_head()
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new CleanRoomEntityModelRuntime();
        const string path = "assets/minecraft/textures/entity/chicken/chicken.png";
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        var pitch = 0.11f;
        var idle = pitch / (8f * (MathF.PI / 180f));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idlePhase01: idle, animationTimeSeconds: 0f, out var merged));
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        const string jvm = "net.minecraft.client.model.animal.chicken.AdultChickenModel";
        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json")));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var options = GeometryIrMeshEmitOptions.ForParity(64, 32) with { OfficialJvmName = jvm };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        float headZ = 0f, bodyZ = 0f;
        var headN = 0;
        var bodyN = 0;
        for (var i = 0; i < merged.Elements.Count; i++)
        {
            var cz = ChickenPreviewZClusterTests.CornerCentroidZ(merged.Elements[i]);
            if (partIds[i].Contains("head", StringComparison.Ordinal))
            {
                headZ += cz;
                headN++;
            }
            else if (string.Equals(partIds[i], "body", StringComparison.Ordinal))
            {
                bodyZ += cz;
                bodyN++;
            }
        }

        Assert.True(headN > 0 && bodyN > 0);
        Assert.True(MathF.Abs(bodyZ / bodyN - headZ / headN) <= 8f,
            $"pitched head should not detach body on Z: bodyZ={bodyZ / bodyN:F3} headZ={headZ / headN:F3}");
    }
}
