using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;
using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

public sealed class GhastPreviewAttachmentTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    private const string MonsterJvm = "net.minecraft.client.model.monster.ghast.GhastModel";
    private const string HappyJvm = "net.minecraft.client.model.animal.ghast.HappyGhastModel";
    private const string MonsterTexturePath = "assets/minecraft/textures/entity/ghast/ghast.png";
    private const string HappyTexturePath = "assets/minecraft/textures/entity/ghast/happy_ghast.png";

    private readonly ITestOutputHelper _output;

    public GhastPreviewAttachmentTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Ghast_family_tentacle_bind_pose_reorients_javap_plusY_box_to_model_space_hang_down()
    {
        var y0 = 0f;
        var y1 = 8f;
        Assert.True(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            MonsterJvm, "tentacle0", ref y0, ref y1));
        Assert.Equal(-8f, y0);
        Assert.Equal(0f, y1);

        y0 = 0f;
        y1 = 5f;
        Assert.True(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            HappyJvm, "tentacle0", ref y0, ref y1));
        Assert.Equal(-5f, y0);
        Assert.Equal(0f, y1);

        y0 = 0f;
        y1 = 5f;
        Assert.False(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            "net.minecraft.client.model.animal.ghast.HappyGhastHarnessModel", "harness", ref y0, ref y1));
        Assert.Equal(0f, y0);
        Assert.Equal(5f, y1);

        Assert.Equal(0.4f + 0.2f * MathF.Sin(2f), GeometryIrEmitPolicy.ComputeGhastAnimateTentaclesXRot(2, 0f), 3);
    }

    [Fact]
    public void Monster_ghast_runtime_mesh_body_and_tentacles_match_reference_java_landmarks()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{MonsterJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            MonsterTexturePath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.True(bind.Elements.Count >= 10, "body + 9 tentacles");

        AssertWorldAabbClose(bind.Elements[0], new Vector3(-8f, -74.456f, -8f), new Vector3(8f, -58.456f, 8f), 0.08f);
        AssertWorldAabbClose(bind.Elements[1], new Vector3(-4.75f, -67.456f, -6f), new Vector3(-2.75f, -59.456f, -4f), 0.08f);

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(MonsterJvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 32) with { OfficialJvmName = MonsterJvm });
        var (bodyMaxY, tentacleMinY, hullGap) = MeasureBodyTentacleHullGap(bind, partIds);
        _output.WriteLine($"bodyMaxY={bodyMaxY:F4} tentacleMinY={tentacleMinY:F4} hullGap={hullGap:F4}");
        Assert.True(hullGap < 0.15f, $"tentacle hull should hang from body (gap={hullGap:F3})");
    }

    [Fact]
    public void Happy_ghast_runtime_mesh_tentacles_hang_from_body_shell()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{HappyJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            HappyTexturePath,
            Profile26,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.True(bind.Elements.Count >= 10, "body + 9 tentacles");

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(HappyJvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = HappyJvm });
        var (bodyMaxY, tentacleMinY, hullGap) = MeasureBodyTentacleHullGap(bind, partIds);
        _output.WriteLine($"happy bodyMaxY={bodyMaxY:F4} tentacleMinY={tentacleMinY:F4} hullGap={hullGap:F4}");
        Assert.True(hullGap < 0.15f, $"happy ghast tentacles should hang from body (gap={hullGap:F3})");
    }

    [Fact]
    public void Ghast_shooting_variant_uses_monster_ghast_geometry_ir()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{MonsterJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/ghast/ghast_shooting.png",
            Profile26,
            0f,
            0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains(MonsterJvm, provenance.Detail, StringComparison.Ordinal);
        Assert.True(bind.Elements.Count >= 10);
    }

    [Fact]
    public void Ghast_family_uv_footprint_uses_vanilla_texOffs_unfold_sizes()
    {
        var y0 = 0f;
        var y1 = 8f;
        Assert.True(GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            MonsterJvm, "tentacle0", ref y0, ref y1));
        var uw = -1;
        var uh = -1;
        var ud = -1;
        Assert.True(GeometryIrEmitPolicy.TryApplyGhastFamilyCuboidUvFootprint(
            MonsterJvm, "body", y0, y1, ref uw, ref uh, ref ud));
        Assert.Equal(16, uw);
        Assert.Equal(16, uh);
        Assert.Equal(16, ud);

        uw = 8;
        uh = 8;
        ud = 8;
        Assert.True(GeometryIrEmitPolicy.TryApplyGhastFamilyCuboidUvFootprint(
            HappyJvm, "tentacle1", y0, y1, ref uw, ref uh, ref ud));
        Assert.Equal(2, uw);
        Assert.Equal(8, uh);
        Assert.Equal(2, ud);
    }

    [Fact]
    public void Monster_ghast_runtime_gpu_mesh_tentacles_hang_below_body_shell()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{MonsterJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            MonsterTexturePath,
            Profile26,
            0f,
            0f,
            out var bind,
            out var provenance,
            applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(MonsterJvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(64, 32) with { OfficialJvmName = MonsterJvm });

        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [MonsterTexturePath] = 0 };
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase) { [MonsterTexturePath] = (64, 32) };
        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            bind, "minecraft", pathToIdx, texSizes, out var gpuVerts, out _, out _));

        var (bodyMaxY, tentacleMinY, hullGap) = MeasureBodyTentaclePreviewHullGap(gpuVerts, partIds);
        _output.WriteLine($"gpu bodyMaxY={bodyMaxY:F4} tentacleMinY={tentacleMinY:F4} hullGap={hullGap:F4}");
        Assert.True(hullGap < 0.15f, $"gpu tentacles should hang from body (gap={hullGap:F3})");
    }

    private static (float BodyMaxY, float TentacleMinY, float HullGap) MeasureBodyTentaclePreviewHullGap(
        ReadOnlySpan<float> gpuVerts,
        IReadOnlyList<string> partIds)
    {
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var bodyMaxY = float.NegativeInfinity;
        var tentacleMinY = float.PositiveInfinity;
        for (var i = 0; i + stride - 1 < gpuVerts.Length; i += stride)
        {
            var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(gpuVerts[i + 12]);
            if (bi < 0 || bi >= partIds.Count)
            {
                continue;
            }

            var preview = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(
                new Vector3(gpuVerts[i], gpuVerts[i + 1], gpuVerts[i + 2]));
            var id = partIds[bi];
            if (string.Equals(id, "body", StringComparison.OrdinalIgnoreCase))
            {
                bodyMaxY = MathF.Max(bodyMaxY, preview.Y);
            }
            else if (id.StartsWith("tentacle", StringComparison.OrdinalIgnoreCase))
            {
                tentacleMinY = MathF.Min(tentacleMinY, preview.Y);
            }
        }

        return (bodyMaxY, tentacleMinY, tentacleMinY - bodyMaxY);
    }

    private static (float BodyMaxY, float TentacleMinY, float HullGap) MeasureBodyTentacleHullGap(
        MergedJavaBlockModel mesh,
        IReadOnlyList<string> partIds)
    {
        var bodyMaxY = float.NegativeInfinity;
        var tentacleMinY = float.PositiveInfinity;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var id = partIds[i];
            TransformWorldCorners(mesh.Elements[i], out var min, out var max);
            if (id.Contains("body", StringComparison.Ordinal) && !id.Contains("inner", StringComparison.Ordinal))
            {
                bodyMaxY = MathF.Max(bodyMaxY, max.Y);
            }
            else if (id.StartsWith("tentacle", StringComparison.OrdinalIgnoreCase))
            {
                tentacleMinY = MathF.Min(tentacleMinY, min.Y);
            }
        }

        return (bodyMaxY, tentacleMinY, tentacleMinY - bodyMaxY);
    }

    private static void AssertWorldAabbClose(ModelElement el, Vector3 expectedMin, Vector3 expectedMax, float eps)
    {
        TransformWorldCorners(el, out var min, out var max);
        var detail = $"expectedMin={expectedMin} actualMin={min} expectedMax={expectedMax} actualMax={max}";
        Assert.True(MathF.Abs(expectedMin.X - min.X) <= eps, detail);
        Assert.True(MathF.Abs(expectedMin.Y - min.Y) <= eps, detail);
        Assert.True(MathF.Abs(expectedMin.Z - min.Z) <= eps, detail);
        Assert.True(MathF.Abs(expectedMax.X - max.X) <= eps, detail);
        Assert.True(MathF.Abs(expectedMax.Y - max.Y) <= eps, detail);
        Assert.True(MathF.Abs(expectedMax.Z - max.Z) <= eps, detail);
    }

    private static void TransformWorldCorners(ModelElement el, out Vector3 min, out Vector3 max)
    {
        var m = el.LocalToParent;
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        var fx = el.From[0];
        var fy = el.From[1];
        var fz = el.From[2];
        var tx = el.To[0];
        var ty = el.To[1];
        var tz = el.To[2];
        ReadOnlySpan<(float x, float y, float z)> c =
        [
            (fx, fy, fz), (tx, fy, fz), (fx, ty, fz), (tx, ty, fz),
            (fx, fy, tz), (tx, fy, tz), (fx, ty, tz), (tx, ty, tz),
        ];
        foreach (var p in c)
        {
            var w = Vector3.Transform(new Vector3(p.x, p.y, p.z), m);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
    }
}
