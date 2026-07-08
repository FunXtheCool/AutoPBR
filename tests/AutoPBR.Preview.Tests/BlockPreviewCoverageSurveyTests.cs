using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Preview.Tests;

public sealed class BlockPreviewCoverageSurveyTests
{
    private static readonly BlockTextureParityPreviewShape[] ComplexShapes =
    [
        BlockTextureParityPreviewShape.ThinPlate,
        BlockTextureParityPreviewShape.DoorHalf,
        BlockTextureParityPreviewShape.CakeWedge,
        BlockTextureParityPreviewShape.CakeSlice,
        BlockTextureParityPreviewShape.CactusCross,
        BlockTextureParityPreviewShape.FenceWithLink,
        BlockTextureParityPreviewShape.RailTrack,
        BlockTextureParityPreviewShape.CrossSprite,
    ];

    [Fact]
    public void Complex_catalog_shapes_do_not_fall_back_to_uniform_cube()
    {
        var paths = BlockTextureParityCatalog.GetSynthesizableCubePreviewPaths();
        foreach (var shape in ComplexShapes)
        {
            var shapePaths = paths.Where(p => BlockTextureParityCatalog.ResolveRule(p)?.PreviewShape == shape).ToList();
            Assert.NotEmpty(shapePaths);
            foreach (var path in shapePaths)
            {
                var rule = BlockTextureParityCatalog.ResolveRule(path);
                Assert.NotNull(rule);
                Assert.Equal(shape, rule!.PreviewShape);
                Assert.NotEqual(BlockTextureParityPreviewShape.UniformCube, rule.PreviewShape);
                Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out _, out _));
                Assert.NotEmpty(merged.Elements);
            }
        }
    }

    [Fact]
    public void PackModelJsonOnly_paths_remain_non_synthesizable()
    {
        const string path = "assets/minecraft/textures/block/activator_rail_on.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.PackModelJsonOnly, rule!.PreviewShape);
        Assert.False(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out _, out _, out _, out _));
    }

    [Fact]
    public void Archive_inventory_classifies_parity_and_json_coverage()
    {
        var paths = new[]
        {
            "assets/minecraft/textures/block/grass_block_side.png",
            "assets/minecraft/textures/block/oak_trapdoor.png",
            "assets/minecraft/models/block/custom_block.json",
        };
        var inventory = ArchiveModelInventoryBuilder.BuildFromArchivePaths(paths);
        Assert.Equal(BlockPreviewCoverageKind.ParityCatalogSynthesizable,
            inventory.BlockTextureCoverageByPath["assets/minecraft/textures/block/grass_block_side.png"]);
        Assert.Equal(BlockPreviewCoverageKind.ParityCatalogSynthesizable,
            inventory.BlockTextureCoverageByPath["assets/minecraft/textures/block/oak_trapdoor.png"]);
        Assert.Contains("assets/minecraft/models/block/custom_block.json", inventory.BlockModelJsonPaths);
    }
}
