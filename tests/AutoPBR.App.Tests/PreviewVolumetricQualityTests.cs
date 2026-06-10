using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Tests;

public sealed class PreviewVolumetricQualityTests
{
    [Theory]
    [InlineData(0, 8, 24, 12, 0f, 0f, 0f, 0f, 0f, 0f)]
    [InlineData(1, 4, 32, 20, 2.8f, 0.28f, 0.42f, 1, 0.35f, 0.45f)]
    [InlineData(2, 3, 48, 24, 4.2f, 0.38f, 0.55f, 2, 0.42f, 0.55f)]
    public void Resolve_ReturnsExpectedProfile(
        int quality,
        int divisor,
        int minSize,
        int slices,
        float depthExp,
        float froxelTemporal,
        float cloudTemporal,
        int cloudQuality,
        float volumeTemporal,
        float upsampleTemporal)
    {
        var profile = PreviewVolumetricQuality.Resolve(quality);

        Assert.Equal(divisor, profile.FroxelDivisor);
        Assert.Equal(minSize, profile.FroxelMinSize);
        Assert.Equal(slices, profile.FroxelSlices);
        Assert.Equal(depthExp, profile.FroxelDepthExp);
        Assert.Equal(froxelTemporal, profile.FroxelTemporal3DWeight);
        Assert.Equal(cloudTemporal, profile.CloudTemporalWeight);
        Assert.Equal(cloudQuality, profile.CloudQuality);
        Assert.Equal(volumeTemporal, profile.VolumeIntegrateTemporalWeight);
        Assert.Equal(upsampleTemporal, profile.UpsampleTemporalWeight);
    }

    [Theory]
    [InlineData(-1, 8)]
    [InlineData(99, 3)]
    public void Resolve_ClampsOutOfRangeQuality(int quality, int expectedDivisor)
    {
        var profile = PreviewVolumetricQuality.Resolve(quality);
        Assert.Equal(expectedDivisor, profile.FroxelDivisor);
    }

    [Fact]
    public void ResolveFroxelDimensions_RespectsMinimumSize()
    {
        var profile = PreviewVolumetricQuality.Resolve(0);
        Assert.Equal(25, profile.ResolveFroxelWidth(200));
        Assert.Equal(24, profile.ResolveFroxelHeight(100));
        Assert.Equal(24, profile.ResolveFroxelWidth(120));
    }
}
