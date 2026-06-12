using AutoPBR.App.Rendering.OpenGL;

using AutoPBR.PreviewGpuAssets;

namespace AutoPBR.App.Tests;

public sealed class PreviewCloudNoiseTextureGeneratorTests
{
    [Fact]
    public void GenerateRgba8_ProducesExpectedShapeVolume()
    {
        var rgba = PreviewCloudNoiseTextureGenerator.GenerateRgba8();
        Assert.Equal(PreviewCloudNoiseTextureGenerator.Size * PreviewCloudNoiseTextureGenerator.Size *
                     PreviewCloudNoiseTextureGenerator.Size * 4, rgba.Length);
        AssertChannelHasVariance(rgba, channel: 0); // Perlin-Worley base
        AssertChannelHasVariance(rgba, channel: 1); // Worley octave
    }

    [Fact]
    public void GenerateDetailRgba8_ProducesExpectedDetailVolume()
    {
        var rgba = PreviewCloudNoiseTextureGenerator.GenerateDetailRgba8();
        Assert.Equal(PreviewCloudNoiseTextureGenerator.DetailSize * PreviewCloudNoiseTextureGenerator.DetailSize *
                     PreviewCloudNoiseTextureGenerator.DetailSize * 4, rgba.Length);
        AssertChannelHasVariance(rgba, channel: 0);
    }

    [Fact]
    public void CoverageMapGenerator_ProducesWeatherChannels()
    {
        var map = PreviewCloudCoverageMapGenerator.GenerateRgba8();
        Assert.Equal(PreviewCloudCoverageMapGenerator.Size * PreviewCloudCoverageMapGenerator.Size * 4, map.Length);
        AssertChannelHasVariance(map, channel: 0); // coverage
        AssertChannelHasVariance(map, channel: 1); // cloud type
    }

    private static void AssertChannelHasVariance(byte[] rgba, int channel)
    {
        var min = byte.MaxValue;
        var max = byte.MinValue;
        for (var i = channel; i < rgba.Length; i += 4)
        {
            min = Math.Min(min, rgba[i]);
            max = Math.Max(max, rgba[i]);
        }

        Assert.True(max - min > 40, $"channel {channel} expected variance, got min={min} max={max}");
    }
}
