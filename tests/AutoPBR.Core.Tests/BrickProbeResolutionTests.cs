using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class BrickProbeResolutionTests
{
    [Theory]
    [InlineData(16, 2, 2)]
    [InlineData(32, 2, 2)]
    [InlineData(64, 4, 4)]
    [InlineData(128, 6, 6)]
    [InlineData(256, 8, 11)]
    public void MortarBrickFaceDilateRadiusScalesWithMinDim(int minDim, int minInclusive, int maxInclusive)
    {
        var r = BrickProbeResolution.MortarBrickFaceDilateRadius(minDim);
        Assert.InRange(r, minInclusive, maxInclusive);
    }

    [Theory]
    [InlineData(16, 3, 1, 3)]
    [InlineData(64, 3, 1, 4)]
    [InlineData(256, 3, 3, 16)]
    public void GetTopHatRadiiIncludesResolutionScaledCap(int minDim, int userMax, int minRadius, int maxRadius)
    {
        var radii = BrickProbeResolution.GetTopHatRadii(minDim, userMax);
        Assert.NotEmpty(radii);
        Assert.All(radii, r => Assert.InRange(r, minRadius, maxRadius));
        Assert.Equal(radii, radii.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void GetTopHatRadii256UsesCoarseScalesWhenUserDefault()
    {
        var radii = BrickProbeResolution.GetTopHatRadii(256, AutoPBRDefaults.DefaultBrickMortarTopHatMaxRadius);
        Assert.Contains(radii, r => r >= 8);
    }
}
