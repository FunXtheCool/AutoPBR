using System.Numerics;

namespace AutoPBR.Core.Tests;

public sealed class BlockDoorPreviewPairingTests
{
    [Fact]
    public void Acacia_door_blockstate_pair_rebakes_to_stacked_preview_panels()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string texturePath = "assets/minecraft/textures/block/acacia_door_top.png";
        Assert.True(JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
            source,
            texturePath,
            out var paths,
            out var ns));
        Assert.Equal(2, paths.Count);
        Assert.Contains(paths, p => p.Contains("bottom_left", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(paths, p => p.Contains("top_left", StringComparison.OrdinalIgnoreCase));

        Assert.True(MinecraftModelMerger.TryMergeMany(source, paths, out var merged));
        Assert.Equal(2, merged.Elements.Count);
        Assert.True(merged.Elements.All(e => e.To[1] - e.From[1] > 15f));

        var rebaked = BlockDoorPreviewPairing.TryNormalizeMergedDoorToPreviewPair(texturePath, ns, ref merged);
        Assert.True(rebaked);
        Assert.Equal(2, merged.Elements.Count);
        Assert.Equal(16f, merged.Elements[0].To[1]);
        Assert.Equal(0f, merged.Elements[1].From[1]);
        Assert.Equal(16f, merged.Elements[1].To[1]);
        Assert.Equal(16f, merged.Elements[1].LocalToParent.M42);
        Assert.Contains("acacia_door_bottom", merged.Textures["bottom"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("acacia_door_top", merged.Textures["top"], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("#bottom", merged.Elements[0].Faces["west"].TextureKey);
        Assert.Equal("#top", merged.Elements[1].Faces["west"].TextureKey);
    }

    [Fact]
    public void Rebaked_acacia_door_bakes_distinct_west_face_uv_regions()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string texturePath = "assets/minecraft/textures/block/acacia_door_top.png";
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
        Assert.True(MinecraftModelBaker.TryBake(merged, ns, pathToIdx, texSizes, out var verts, out _, out var batches));
        Assert.Equal(2, batches.Select(b => b.MaterialIndex).Distinct().Count());
        Assert.True(verts.Length > 0);
    }

    [Fact]
    public void Single_full_height_door_panel_is_rebaked()
    {
        using var fixture = new BlockModelMergerTests.BlockModelFixture();
        fixture.Write(
            "assets/minecraft/models/block/oak_door_bottom.json",
            """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 3],
                  "faces": {
                    "north": { "texture": "#bottom", "uv": [0, 0, 16, 16] }
                  }
                }
              ],
              "textures": { "bottom": "minecraft:block/oak_door_bottom" }
            }
            """);

        var source = new DirectoryAssetSource(fixture.Root);
        Assert.True(MinecraftModelMerger.TryMerge(
            source,
            "assets/minecraft/models/block/oak_door_bottom.json",
            out var merged));

        var rebaked = BlockDoorPreviewPairing.TryNormalizeMergedDoorToPreviewPair(
            "assets/minecraft/textures/block/oak_door_bottom.png",
            "minecraft",
            ref merged);

        Assert.True(rebaked);
        Assert.Equal(2, merged.Elements.Count);
    }
}
