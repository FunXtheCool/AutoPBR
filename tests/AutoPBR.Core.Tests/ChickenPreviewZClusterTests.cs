using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Chicken torso is pitched via <c>body</c> <c>Rx(π/2)</c>; LER column-root scale must keep body Z
/// clustered with head (−4), legs/wings (~0–1), not displaced along preview Z.
/// </summary>
public sealed class ChickenPreviewZClusterTests
{
    private static readonly (string TexturePath, string Jvm, int MinElements)[] Variants =
    [
        ("assets/minecraft/textures/entity/chicken/chicken_cold.png",
            "net.minecraft.client.model.animal.chicken.ColdChickenModel", 10),
        ("assets/minecraft/textures/entity/chicken/chicken_baby.png",
            "net.minecraft.client.model.animal.chicken.BabyChickenModel", 7),
    ];

    [Theory]
    [MemberData(nameof(VariantCases))]
    public void Catalog_mesh_body_Z_centroid_clusters_with_head_legs_and_wings(
        string texturePath,
        string officialJvm,
        int minElements)
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = new EntityModelRuntime();
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        Assert.True(runtime.TryBuildStaticMesh(texturePath, profile, idlePhase01: 0f, animationTimeSeconds: 0f, out var mesh));
        Assert.True(mesh.Elements.Count >= minElements, $"elements={mesh.Elements.Count}");

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{officialJvm}.json");
        Assert.True(File.Exists(shardPath));
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvm, shard.RootElement);
        var options = GeometryIrMeshEmitOptions.ForParity(64, 32) with { OfficialJvmName = officialJvm };
        var partIds = ResolvePartIdsForZCluster(officialJvm, geometryRoot, options, mesh.Elements.Count);

        float headZ = 0f, bodyZ = 0f, limbZ = 0f;
        var headN = 0;
        var bodyN = 0;
        var limbN = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            var cz = CornerCentroid(mesh.Elements[i]).Z;
            if (IsHeadFamily(partId))
            {
                headZ += cz;
                headN++;
            }
            else if (string.Equals(partId, "body", StringComparison.Ordinal))
            {
                bodyZ += cz;
                bodyN++;
            }
            else if (partId.Contains("leg", StringComparison.Ordinal) ||
                     partId.Contains("wing", StringComparison.Ordinal))
            {
                limbZ += cz;
                limbN++;
            }
        }

        Assert.True(bodyN > 0 && limbN > 0, $"{officialJvm}: body={bodyN} limb={limbN}");
        bodyZ /= bodyN;
        limbZ /= limbN;

        var isBaby = officialJvm.Contains("Baby", StringComparison.Ordinal);
        if (headN > 0)
        {
            headZ /= headN;
            var maxBodyHeadGap = isBaby ? 6f : 8f;
            Assert.True(
                MathF.Abs(bodyZ - headZ) <= maxBodyHeadGap,
                $"{officialJvm}: bodyZ={bodyZ:F3} headZ={headZ:F3} limbZ={limbZ:F3}");
            Assert.True(
                bodyZ > headZ - 10f,
                $"{officialJvm}: body should not sit far behind head; bodyZ={bodyZ:F3} headZ={headZ:F3}");
        }

        var maxBodyLimbGap = isBaby ? 6f : 8f;
        Assert.True(
            MathF.Abs(bodyZ - limbZ) <= maxBodyLimbGap,
            $"{officialJvm}: bodyZ={bodyZ:F3} headZ={(headN > 0 ? headZ : float.NaN):F3} limbZ={limbZ:F3}");
    }

    public static IEnumerable<object[]> VariantCases() =>
        Variants.Select(v => new object[] { v.TexturePath, v.Jvm, v.MinElements });

    private static List<string> ResolvePartIdsForZCluster(
        string officialJvm,
        JsonElement geometryRoot,
        GeometryIrMeshEmitOptions options,
        int meshElementCount)
    {
        _ = officialJvm;
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        Assert.Equal(meshElementCount, partIds.Count);
        return partIds;
    }

    private static bool IsHeadFamily(string partId) =>
        string.Equals(partId, "head", StringComparison.Ordinal) ||
        string.Equals(partId, "beak", StringComparison.Ordinal) ||
        string.Equals(partId, "red_thing", StringComparison.Ordinal);

    internal static float CornerCentroidZ(ModelElement el) => CornerCentroid(el).Z;

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
