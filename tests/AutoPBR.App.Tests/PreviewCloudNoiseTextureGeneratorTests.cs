using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class PreviewCloudNoiseTextureGeneratorTests
{
    [Fact]
    public void GenerateRgba8_ProducesExpectedVolume()
    {
        var rgba = PreviewCloudNoiseTextureGenerator.GenerateRgba8();
        Assert.Equal(PreviewCloudNoiseTextureGenerator.Size * PreviewCloudNoiseTextureGenerator.Size *
                     PreviewCloudNoiseTextureGenerator.Size * 4, rgba.Length);
        Assert.Contains(rgba, b => b > 0);
    }

    [Fact]
    public void CoverageMapGenerator_ProducesFullGrid()
    {
        var map = PreviewCloudCoverageMapGenerator.GenerateR8();
        Assert.Equal(PreviewCloudCoverageMapGenerator.Size * PreviewCloudCoverageMapGenerator.Size, map.Length);
        Assert.Contains(map, b => b > 10);
        Assert.Contains(map, b => b < 245);
    }
}
