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
    public void IsGrassColormapTintIndexPath_matches_grass_block_top()
    {
        Assert.True(PreviewGrassColormapTint.IsGrassColormapTintIndexPath(
            "assets/minecraft/textures/block/grass_block_top.png"));
        Assert.False(PreviewGrassColormapTint.IsGrassColormapTintIndexPath(
            "assets/minecraft/textures/block/grass_block_side.png"));
    }
}
