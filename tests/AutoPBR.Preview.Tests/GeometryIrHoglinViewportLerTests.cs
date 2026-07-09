using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Hoglin body uses <c>PartPose.offsetAndRotation</c>; default LER fold (<c>S * LocalToParent</c>) matches T2 viewport.
/// Baby hoglin shares the same policy (see <c>BabyHoglinModel</c> assembly pilot).
/// </summary>
public sealed class GeometryIrHoglinViewportLerTests
{
    private const string HoglinJvm = "net.minecraft.client.model.monster.hoglin.HoglinModel";
    private const string BabyHoglinJvm = "net.minecraft.client.model.monster.hoglin.BabyHoglinModel";

    [Theory]
    [InlineData(HoglinJvm)]
    [InlineData(BabyHoglinJvm)]
    public void Hoglin_family_does_not_use_flat_offset_quadruped_ler_right_compose(string officialJvm)
    {
        Assert.False(EntityModelRuntime.UsesFlatPartPoseOffsetQuadrupedJvm(officialJvm));
        Assert.False(
            EntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
                officialJvm,
                "hoglinmodel",
                "assets/minecraft/textures/entity/hoglin/hoglin.png"));
    }

    [Fact]
    public void HoglinModel_column_pose_stack_root_orders_legs_below_head()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{HoglinJvm}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(HoglinJvm, doc.RootElement.Clone());

        var columnMesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test",
            new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2)),
            HoglinJvm,
            128,
            64,
            out var err0,
            geometryRootOverride: repaired);
        Assert.NotNull(columnMesh);
        Assert.Null(err0);

        var (columnHead, columnLeg) = MeasureHeadLegCentroidY(columnMesh!, repaired, 128, 64, HoglinJvm);
        Assert.True(columnLeg < columnHead + 0.1f, $"column root: legY={columnLeg:F3} headY={columnHead:F3}");
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.polarbear.PolarBearModel")]
    [InlineData("net.minecraft.client.model.animal.panda.PandaModel")]
    [InlineData("net.minecraft.client.model.animal.polarbear.BabyPolarBearModel")]
    [InlineData("net.minecraft.client.model.animal.panda.BabyPandaModel")]
    public void Panda_polar_bear_family_column_pose_stack_root_orders_legs_below_head(string officialJvm)
    {
        Assert.False(
            EntityModelRuntime.ResolveGeometryIrLerMirrorRightComposeLocalChain(
                officialJvm,
                stemLower: null,
                normalizedAssetPath: null));

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{officialJvm}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvm, doc.RootElement.Clone());

        var mesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test",
            new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2)),
            officialJvm,
            128,
            64,
            out var err,
            geometryRootOverride: repaired);
        Assert.NotNull(mesh);
        Assert.Null(err);

        var (headY, legY) = MeasureHeadLegCentroidY(mesh!, repaired, 128, 64, officialJvm);
        Assert.True(legY < headY, $"{officialJvm}: legY={legY:F3} headY={headY:F3}");
    }

    [Theory]
    [InlineData(HoglinJvm, 128, 64)]
    [InlineData(BabyHoglinJvm, 128, 64)]
    public void Hoglin_family_geometry_ir_viewport_legs_below_head(string officialJvm, int atlasW, int atlasH)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        if (!GeometryIrTestTierSupport.IsClientJarPresent(repo))
        {
            return;
        }

        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{officialJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(shardPath));
        var shardRoot = doc.RootElement.Clone();
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvm, shardRoot);

        var mesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test",
            new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2)),
            officialJvm,
            atlasW,
            atlasH,
            out var failure,
            geometryRootOverride: repaired);
        Assert.NotNull(mesh);
        Assert.Null(failure);

        var (headY, legY) = MeasureHeadLegCentroidY(mesh!, repaired, atlasW, atlasH, officialJvm);
        Assert.True(legY < headY + 0.1f, $"{officialJvm}: expected legs below head; legY={legY:F4} headY={headY:F4}");
    }

    private static (float HeadY, float LegY) MeasureHeadLegCentroidY(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        string officialJvm)
    {
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasW,
            AtlasHeight = atlasH,
            Fidelity = GeometryIrEmitFidelity.Parity,
            PreviewDegenerateAxisThickness = 0f,
            OfficialJvmName = officialJvm,
        };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);

        float headSum = 0f;
        var headCount = 0;
        float legSum = 0f;
        var legCount = 0;

        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            TransformWorldCorners(mesh.Elements[i], out var wMin, out var wMax);
            var cy = (wMin.Y + wMax.Y) * 0.5f;
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += cy;
                headCount++;
            }

            if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += cy;
                legCount++;
            }
        }

        Assert.True(headCount > 0 && legCount > 0);
        return (headSum / headCount, legSum / legCount);
    }

    private static void TransformWorldCorners(ModelElement el, out Vector3 wMin, out Vector3 wMax)
    {
        wMin = new Vector3(float.PositiveInfinity);
        wMax = new Vector3(float.NegativeInfinity);
        ReadOnlySpan<(float x, float y, float z)> corners =
        [
            (el.From[0], el.From[1], el.From[2]),
            (el.To[0], el.From[1], el.From[2]),
            (el.From[0], el.To[1], el.From[2]),
            (el.To[0], el.To[1], el.From[2]),
            (el.From[0], el.From[1], el.To[2]),
            (el.To[0], el.From[1], el.To[2]),
            (el.From[0], el.To[1], el.To[2]),
            (el.To[0], el.To[1], el.To[2]),
        ];
        foreach (var (x, y, z) in corners)
        {
            var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
            wMin = Vector3.Min(wMin, w);
            wMax = Vector3.Max(wMax, w);
        }
    }
}
