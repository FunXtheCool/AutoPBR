using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.Core.Tests;

public sealed class PreviewGrassColormapTintTests
{
    [Fact]
    public void ApplyTintToDiffuse_multiplies_grayscale_by_sampled_color()
    {
        var diffuse = new byte[] { 128, 128, 128, 255 };
        var tint = new Rgba32(100, 200, 50, 255);
        var tinted = PreviewGrassColormapTint.ApplyTintToDiffuse(diffuse, 1, 1, tint);
        Assert.Equal(4, tinted.Length);
        Assert.Equal((byte)50, tinted[0]);
        Assert.Equal((byte)100, tinted[1]);
        Assert.Equal((byte)25, tinted[2]);
        Assert.Equal((byte)255, tinted[3]);
    }

    [Fact]
    public void IsGrassColormapTintIndexPath_matches_grass_tint_targets()
    {
        Assert.True(PreviewGrassColormapTint.IsGrassColormapTintIndexPath(
            "assets/minecraft/textures/block/grass_block_top.png"));
        Assert.True(PreviewGrassColormapTint.IsGrassColormapTintIndexPath(
            "assets/minecraft/textures/block/grass_block_side_overlay.png"));
        Assert.False(PreviewGrassColormapTint.IsGrassColormapTintIndexPath(
            "assets/minecraft/textures/block/grass_block_side.png"));
        Assert.False(PreviewGrassColormapTint.IsGrassColormapTintIndexPath(
            "assets/minecraft/textures/block/grass_block_snow.png"));
    }

    [Fact]
    public void IsGrassSnowSideColormapTintIndexPath_matches_grass_block_snow_only()
    {
        Assert.True(PreviewGrassColormapTint.IsGrassSnowSideColormapTintIndexPath(
            "assets/minecraft/textures/block/grass_block_snow.png"));
        Assert.False(PreviewGrassColormapTint.IsGrassSnowSideColormapTintIndexPath(
            "assets/minecraft/textures/block/grass_block_top.png"));
    }

    [Fact]
    public void SampleSnowSideGrassTint_uses_cold_colormap_edge_at_warm_ui_temperature()
    {
        var colormap = new PreviewColormapImage
        {
            Width = 2,
            Height = 2,
            Rgba =
            [
                0, 0, 0, 255, 0, 0, 0, 255,
                0, 255, 0, 255, 255, 255, 255, 255,
            ],
        };

        var grass = PreviewGrassColormapTint.SampleGrassTint(colormap, temperature01: 1.0, downfall01: 0.0);
        var snowSide = PreviewGrassColormapTint.SampleSnowSideGrassTint(colormap, temperature01: 1.0, downfall01: 0.0);

        Assert.Equal(new Rgba32(0, 255, 0, 255), grass);
        Assert.Equal(new Rgba32(255, 255, 255, 255), snowSide);
    }

    [Fact]
    public void WithGrassTint_tints_side_overlay_diffuse()
    {
        var maps = new PreviewTextureMaps
        {
            Width = 1,
            Height = 1,
            DiffuseRgba = [128, 128, 128, 255],
        };
        var tinted = PreviewGrassColormapTint.WithGrassTint(
            maps,
            "assets/minecraft/textures/block/grass_block_side_overlay.png",
            new Rgba32(100, 200, 50, 255));
        Assert.Equal((byte)50, tinted.DiffuseRgba[0]);
        Assert.Equal((byte)100, tinted.DiffuseRgba[1]);
        Assert.Equal((byte)25, tinted.DiffuseRgba[2]);
    }
}
