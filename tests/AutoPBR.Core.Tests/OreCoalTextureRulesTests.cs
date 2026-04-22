using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class OreCoalTextureRulesTests
{
    [Theory]
    [InlineData("coal_ore", @"\minecraft\textures\block\coal_ore", true)]
    [InlineData("deepslate_coal_ore", @"\minecraft\textures\block\deepslate_coal_ore", true)]
    [InlineData("iron_ore", @"\minecraft\textures\block\iron_ore", false)]
    [InlineData("deepslate_iron_ore", @"\minecraft\textures\block\deepslate_iron_ore", false)]
    [InlineData("forests", @"\minecraft\textures\block\forests", false)]
    public void ShouldInvertHeightMatchesWholeWordOreAndCoal(string name, string relativeKey, bool expected)
    {
        Assert.Equal(expected, OreCoalTextureRules.ShouldInvertHeight(name, relativeKey));
    }
}
