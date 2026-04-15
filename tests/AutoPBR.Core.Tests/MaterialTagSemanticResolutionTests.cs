using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class MaterialTagSemanticResolutionTests
{
    [Fact]
    public void AppendWeightedUnweightedFlags_SemanticDisabled_AddsUnweighted()
    {
        var flags = new List<string> { "block" };
        MaterialTagSemanticResolution.AppendWeightedUnweightedFlags(flags, null, deferSemanticMl: false, usedSemanticMl: true);
        Assert.Equal(new[] { "block", FlagTagResolver.UnweightedId }, flags);
    }

    [Fact]
    public void AppendWeightedUnweightedFlags_DeferSemanticMl_AddsUnweighted()
    {
        var sem = new MaterialTagSemanticOptions { Enabled = true, Matcher = null };
        var flags = new List<string>();
        MaterialTagSemanticResolution.AppendWeightedUnweightedFlags(flags, sem, deferSemanticMl: true, usedSemanticMl: true);
        Assert.Equal(new[] { FlagTagResolver.UnweightedId }, flags);
    }

    [Fact]
    public void AppendWeightedUnweightedFlags_ReplacesExistingWeightedUnweighted()
    {
        var flags = new List<string> { FlagTagResolver.WeightedId, "block" };
        MaterialTagSemanticResolution.AppendWeightedUnweightedFlags(flags, null, deferSemanticMl: false, usedSemanticMl: false);
        Assert.Equal(new[] { "block", FlagTagResolver.UnweightedId }, flags);
    }

    [Fact]
    public void AppendTwoDSpriteFlagIfNeeded_OrganicWithoutBlock_AddsSprite2D()
    {
        var ids = new List<string> { "plant", FlagTagResolver.ItemId };
        MaterialTagSemanticResolution.AppendTwoDSpriteFlagIfNeeded(ids, removedTagIds: null);
        Assert.Equal(new[] { "plant", FlagTagResolver.ItemId, FlagTagResolver.Sprite2DId }, ids);
    }

    [Fact]
    public void AppendTwoDSpriteFlagIfNeeded_WithBlock_DoesNotAdd()
    {
        var ids = new List<string> { "plant", FlagTagResolver.BlockId };
        MaterialTagSemanticResolution.AppendTwoDSpriteFlagIfNeeded(ids, null);
        Assert.Equal(new[] { "plant", FlagTagResolver.BlockId }, ids);
    }

    [Fact]
    public void AppendTwoDSpriteFlagIfNeeded_NoPlant_DoesNotAdd()
    {
        var ids = new List<string> { "wood", FlagTagResolver.ItemId };
        MaterialTagSemanticResolution.AppendTwoDSpriteFlagIfNeeded(ids, null);
        Assert.Equal(new[] { "wood", FlagTagResolver.ItemId }, ids);
    }

    [Fact]
    public void AppendTwoDSpriteFlagIfNeeded_UserRemovedSprite_DoesNotAdd()
    {
        var ids = new List<string> { "plant", FlagTagResolver.ItemId };
        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { FlagTagResolver.Sprite2DId };
        MaterialTagSemanticResolution.AppendTwoDSpriteFlagIfNeeded(ids, removed);
        Assert.Equal(new[] { "plant", FlagTagResolver.ItemId }, ids);
    }
}
