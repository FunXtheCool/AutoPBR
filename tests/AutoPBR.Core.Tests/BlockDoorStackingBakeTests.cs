using System.Numerics;

namespace AutoPBR.Core.Tests;

public sealed class BlockDoorStackingBakeTests
{
    [Fact]
    public void Rebaked_door_halves_stack_on_Y_axis()
    {
        var textures = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bottom"] = "minecraft:block/acacia_door_bottom",
            ["top"] = "minecraft:block/acacia_door_top",
        };
        var merged = VanillaBlockDoorHalfBuilder.BuildPair(textures);
        Assert.Equal(16f, merged.Elements[0].To[1]);
        Assert.Equal(0f, merged.Elements[1].From[1]);
        Assert.Equal(16f, merged.Elements[1].To[1]);
        Assert.True(Matrix4x4.Identity.Equals(merged.Elements[0].LocalToParent));
        Assert.Equal(16f, merged.Elements[1].LocalToParent.M42);

        var bottomTex = "assets/minecraft/textures/block/acacia_door_bottom.png";
        var topTex = "assets/minecraft/textures/block/acacia_door_top.png";
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [bottomTex] = 0,
            [topTex] = 1,
        };
        var texSizes = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase)
        {
            [bottomTex] = (16, 16),
            [topTex] = (16, 16),
        };
        Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out var indices, out var batches));

        var bottomMaxY = float.MinValue;
        var topMinY = float.MaxValue;
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        CollectBatchYExtents(verts, indices, batches, materialIndex: 0, ref bottomMaxY, ref minX, ref maxX, isMax: true);
        CollectBatchYExtents(verts, indices, batches, materialIndex: 1, ref topMinY, ref minX, ref maxX, isMax: false);

        Assert.True(bottomMaxY <= 0.51f, $"bottom max Y {bottomMaxY}");
        Assert.True(topMinY >= 0.49f, $"top min Y {topMinY}");
        Assert.True(maxX - minX < 0.25f, $"door spread on X {minX}..{maxX}");
    }

    [Fact]
    public void Merged_vanilla_door_pair_before_rebake_overlaps_on_Y()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string texturePath = "assets/minecraft/textures/block/acacia_door_top.png";
        Assert.True(JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
            source,
            texturePath,
            out var paths,
            out _));
        Assert.True(MinecraftModelMerger.TryMergeMany(source, paths, out var merged));
        Assert.Equal(2, merged.Elements.Count);
        Assert.True(merged.Elements.All(e => e.To[1] - e.From[1] > 15f));
    }

    [Fact]
    public void Resolver_rebaked_acacia_door_bakes_stacked_on_Y()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string texturePath = "assets/minecraft/textures/block/acacia_door_bottom.png";
        Assert.True(JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
            source,
            texturePath,
            out var paths,
            out var ns));
        Assert.True(MinecraftModelMerger.TryMergeMany(source, paths, out var merged));
        Assert.True(BlockDoorPreviewPairing.TryNormalizeMergedDoorToPreviewPair(texturePath, ns, ref merged));

        var bottomTex = "assets/minecraft/textures/block/acacia_door_bottom.png";
        var topTex = "assets/minecraft/textures/block/acacia_door_top.png";
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [bottomTex] = 0,
            [topTex] = 1,
        };
        var texSizes = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase)
        {
            [bottomTex] = (16, 16),
            [topTex] = (16, 16),
        };
        Assert.True(MinecraftModelBaker.TryBake(merged, ns, pathToIdx, texSizes, out var verts, out var indices, out var batches));

        var bottomMaxY = float.MinValue;
        var topMinY = float.MaxValue;
        var minZ = float.MaxValue;
        var maxZ = float.MinValue;
        CollectBatchYExtents(verts, indices, batches, materialIndex: 0, ref bottomMaxY, ref minZ, ref maxZ, isMax: true);
        CollectBatchYExtents(verts, indices, batches, materialIndex: 1, ref topMinY, ref minZ, ref maxZ, isMax: false);

        Assert.True(bottomMaxY <= 0.51f, $"bottom max Y {bottomMaxY}");
        Assert.True(topMinY >= 0.49f, $"top min Y {topMinY}");
    }

    [Fact]
    public void Rebaked_door_uses_production_material_order_bottom_below_top()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string texturePath = "assets/minecraft/textures/block/acacia_door_bottom.png";
        Assert.True(JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
            source,
            texturePath,
            out var paths,
            out var ns));
        Assert.True(MinecraftModelMerger.TryMergeMany(source, paths, out var merged));
        Assert.True(BlockDoorPreviewPairing.TryNormalizeMergedDoorToPreviewPair(texturePath, ns, ref merged));

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, ns);
        Assert.Equal(2, ordered.Count);
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
        }

        var texSizes = ordered.ToDictionary(
            p => p,
            _ => (16, 16),
            StringComparer.OrdinalIgnoreCase);
        Assert.True(MinecraftModelBaker.TryBake(merged, ns, pathToIdx, texSizes, out var verts, out var indices, out var batches));

        var bottomPath = ordered.First(p => p.Contains("door_bottom", StringComparison.OrdinalIgnoreCase));
        var topPath = ordered.First(p => p.Contains("door_top", StringComparison.OrdinalIgnoreCase));
        var bottomIdx = pathToIdx[bottomPath];
        var topIdx = pathToIdx[topPath];
        var bottomAvgY = AverageBatchY(verts, indices, batches, bottomIdx);
        var topAvgY = AverageBatchY(verts, indices, batches, topIdx);
        Assert.True(bottomAvgY < topAvgY, $"bottom avg Y {bottomAvgY} should be below top avg Y {topAvgY}");
    }

    private static float AverageBatchY(
        float[] verts,
        uint[] indices,
        List<PreviewDrawBatch> batches,
        int materialIndex)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var sum = 0f;
        var count = 0;
        foreach (var batch in batches)
        {
            if (batch.MaterialIndex != materialIndex)
            {
                continue;
            }

            for (var i = batch.FirstIndex; i < batch.FirstIndex + batch.IndexCount; i++)
            {
                sum += verts[(int)indices[i] * stride + 1];
                count++;
            }
        }

        return count == 0 ? float.NaN : sum / count;
    }

    private static void CollectBatchYExtents(
        float[] verts,
        uint[] indices,
        List<PreviewDrawBatch> batches,
        int materialIndex,
        ref float yExtent,
        ref float minX,
        ref float maxX,
        bool isMax)
    {
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        foreach (var batch in batches)
        {
            if (batch.MaterialIndex != materialIndex)
            {
                continue;
            }

            for (var i = batch.FirstIndex; i < batch.FirstIndex + batch.IndexCount; i++)
            {
                var vi = (int)indices[i];
                var y = verts[vi * stride + 1];
                var x = verts[vi * stride];
                if (isMax)
                {
                    yExtent = MathF.Max(yExtent, y);
                }
                else
                {
                    yExtent = MathF.Min(yExtent, y);
                }

                minX = MathF.Min(minX, x);
                maxX = MathF.Max(maxX, x);
            }
        }
    }
}
