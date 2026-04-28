using AutoPBR.Core.Atlas;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class AtlasTilingTests
{
    [Fact]
    public void TryInferPlan_FindsPowerOfTwoTileGrid()
    {
        var plan = AtlasTiling.TryInferPlan(64, 128);

        Assert.True(plan.IsAtlas);
        Assert.Equal(64, plan.TileSize);
        Assert.Equal(1, plan.Columns);
        Assert.Equal(2, plan.Rows);
    }

    [Fact]
    public void Decide_ExplicitDisable_WinsOverGeometry()
    {
        var plan = AtlasTiling.Decide(64, 128, explicitAtlasEnabled: false);

        Assert.False(plan.IsAtlas);
        Assert.Equal(AtlasDecisionReason.ExplicitDisabled, plan.Reason);
    }

    [Fact]
    public void Decide_WithPreferredTileSize_DoesNotSplitStandardTile()
    {
        var plan = AtlasTiling.Decide(32, 32, preferredTileSize: 32);

        Assert.False(plan.IsAtlas);
    }

    [Fact]
    public void Decide_WithPreferredTileSize_SplitsAlignedAtlas()
    {
        var plan = AtlasTiling.Decide(64, 128, preferredTileSize: 32);

        Assert.True(plan.IsAtlas);
        Assert.Equal(32, plan.TileSize);
        Assert.Equal(2, plan.Columns);
        Assert.Equal(4, plan.Rows);
    }
}
