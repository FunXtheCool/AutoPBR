using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Polar bear body uses <c>Rx(π/2)</c> like cow/chicken; preview Z/X must cluster with head/legs (not float along an axis).
/// </summary>
public sealed class PolarBearPreviewZClusterTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Theory]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png", "net.minecraft.client.model.animal.polarbear.PolarBearModel", 128, 64)]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear_baby.png", "net.minecraft.client.model.animal.polarbear.BabyPolarBearModel", 128, 64)]
    public void Catalog_mesh_body_clusters_with_head_and_legs_in_preview_space(
        string texturePath,
        string officialJvm,
        int atlasW,
        int atlasH)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var mesh));

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{officialJvm}.json");
        Assert.True(File.Exists(shardPath));
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvm, shard.RootElement);
        var options = GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = officialJvm };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        Assert.Equal(mesh.Elements.Count, partIds.Count);

        float headZ = 0f, bodyZ = 0f, limbZ = 0f;
        float headX = 0f, bodyX = 0f;
        var headN = 0;
        var bodyN = 0;
        var limbN = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var c = CornerCentroid(mesh.Elements[i]);
            var partId = partIds[i];
            if (string.Equals(partId, "head", StringComparison.Ordinal))
            {
                headZ += c.Z;
                headX += c.X;
                headN++;
            }
            else if (string.Equals(partId, "body", StringComparison.Ordinal))
            {
                bodyZ += c.Z;
                bodyX += c.X;
                bodyN++;
            }
            else if (partId.Contains("leg", StringComparison.Ordinal))
            {
                limbZ += c.Z;
                limbN++;
            }
        }

        Assert.True(bodyN > 0 && limbN > 0, $"{officialJvm}: body={bodyN} limb={limbN}");
        bodyZ /= bodyN;
        limbZ /= limbN;
        if (headN > 0)
        {
            headZ /= headN;
            headX /= headN;
            bodyX /= bodyN;
            // Adult polar bear head sits far forward (part Z ≈ −16) while the pitched torso centroid clusters with legs.
            var maxBodyHeadGap = string.Equals(
                officialJvm,
                "net.minecraft.client.model.animal.polarbear.PolarBearModel",
                StringComparison.Ordinal)
                ? 18f
                : 12f;
            Assert.True(
                MathF.Abs(bodyZ - headZ) <= maxBodyHeadGap,
                $"{officialJvm}: bodyZ={bodyZ:F3} headZ={headZ:F3} limbZ={limbZ:F3}");
            Assert.True(
                MathF.Abs(bodyX - headX) <= 6f,
                $"{officialJvm}: bodyX={bodyX:F3} headX={headX:F3}");
        }

        Assert.True(MathF.Abs(bodyZ - limbZ) <= 12f, $"{officialJvm}: bodyZ={bodyZ:F3} limbZ={limbZ:F3}");
    }

    private static Vector3 CornerCentroid(ModelElement el)
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

        return (wMin + wMax) * 0.5f;
    }
}
