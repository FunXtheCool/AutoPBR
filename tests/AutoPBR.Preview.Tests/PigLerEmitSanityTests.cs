using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

public sealed class PigLerEmitSanityTests
{
    private const string PigJvm = "net.minecraft.client.model.animal.pig.PigModel";

    [Fact]
    public void Pig_parity_emit_folds_ler_with_column_pose_stack_root_scale()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{PigJvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(PigJvm, shard.RootElement);
        var plan = EntityModelRuntime.ResolveGeometryIrParityEmitPlan(
            PigJvm,
            "pig",
            "assets/minecraft/textures/entity/pig/pig.png",
            deferLivingEntityRendererUntilAfterMotionPasses: false);
        Assert.Equal(EntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot, plan.Basis);
        Assert.True(plan.ApplyPostLivingEntityRendererBasis);
        Assert.Equal(Matrix4x4.Identity, plan.EmitRootTransform);

        var mesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/pig/pig",
            new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2)),
            PigJvm,
            64,
            64,
            out var failure,
            geometryRootOverride: geometryRoot);
        Assert.NotNull(mesh);
        Assert.Null(failure);

        var options = GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = PigJvm };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        float headSum = 0f, legSum = 0f, bodySum = 0f;
        var headN = 0;
        var legN = 0;
        var bodyN = 0;
        for (var i = 0; i < mesh!.Elements.Count; i++)
        {
            var partId = partIds[i];
            var cy = CornerCentroidY(mesh.Elements[i]);
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += cy;
                headN++;
            }
            else if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += cy;
                legN++;
            }
            else if (partId.Contains("body", StringComparison.OrdinalIgnoreCase))
            {
                bodySum += cy;
                bodyN++;
            }
        }

        Assert.True(headN > 0 && legN > 0 && bodyN > 0);
        var headY = headSum / headN;
        var legY = legSum / legN;
        var bodyY = bodySum / bodyN;
        Assert.True(legY < headY, $"LER preview: legY={legY:F3} headY={headY:F3} bodyY={bodyY:F3}");
    }

    private static float CornerCentroidY(ModelElement el)
    {
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
        var wMin = new Vector3(float.PositiveInfinity);
        var wMax = new Vector3(float.NegativeInfinity);
        foreach (var (x, y, z) in corners)
        {
            var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
            wMin = Vector3.Min(wMin, w);
            wMax = Vector3.Max(wMax, w);
        }

        return (wMin.Y + wMax.Y) * 0.5f;
    }
}
