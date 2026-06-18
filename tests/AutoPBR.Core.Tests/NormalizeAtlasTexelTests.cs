using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class NormalizeAtlasTexelTests
{
    [Theory]
    [InlineData(64, 64, 64)]
    [InlineData(32, 64, 32)]
    [InlineData(0, 64, 0)]
    [InlineData(130, 128, 128)]
    [InlineData(368, 256, 256)]
    [InlineData(-6, 32, 26)]
    public void Preserves_in_range_and_clamps_or_wraps_out_of_range(float px, int atlasSize, float expected)
    {
        Assert.Equal(expected, MinecraftModelBaker.NormalizeAtlasTexel(px, atlasSize));
    }
}
