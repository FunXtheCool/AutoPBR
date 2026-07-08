using AutoPBR.Core;
using AutoPBR.Preview;
using Xunit;

namespace AutoPBR.Preview.Tests;

public sealed class PreviewPathPolicyTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/item/diamond.png", true)]
    [InlineData(@"\minecraft\textures\item\diamond", true)]
    [InlineData(@"\minecraft\item\acacia_boat", true)]
    [InlineData("assets/minecraft/textures/block/stone.png", false)]
    [InlineData(@"\minecraft\block\stone", false)]
    [InlineData("assets/minecraft/textures/item/generated/apple.png", true)]
    public void IsItemTexturePath_detects_item_folder(string path, bool expected) =>
        Assert.Equal(expected, PreviewPathPolicy.IsItemTexturePath(path));

    [Fact]
    public void ShouldUseFlatItemPlane_requires_sprite_flag_and_item_path()
    {
        Assert.True(PreviewPathPolicy.ShouldUseFlatItemPlane(
            "assets/minecraft/textures/item/diamond.png",
            sprite2DFoliageTarget: true));
        Assert.False(PreviewPathPolicy.ShouldUseFlatItemPlane(
            "assets/minecraft/textures/block/grass.png",
            sprite2DFoliageTarget: true));
        Assert.False(PreviewPathPolicy.ShouldUseFlatItemPlane(
            "assets/minecraft/textures/item/diamond.png",
            sprite2DFoliageTarget: false));
    }

    [Fact]
    public void IsItemFlatSpriteExempt_skips_block_entity_and_armor_flags()
    {
        Assert.True(PreviewPathPolicy.IsItemFlatSpriteExempt([FlagTagResolver.ItemId, FlagTagResolver.BlockId]));
        Assert.True(PreviewPathPolicy.IsItemFlatSpriteExempt([FlagTagResolver.ItemId, FlagTagResolver.EntityId]));
        Assert.True(PreviewPathPolicy.IsItemFlatSpriteExempt([FlagTagResolver.ItemId, FlagTagResolver.ArmorId]));
        Assert.False(PreviewPathPolicy.IsItemFlatSpriteExempt([FlagTagResolver.ItemId, "wood"]));
    }
}
