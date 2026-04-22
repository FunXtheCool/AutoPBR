using AutoPBR.Core.Models;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class TagRuleApplicatorTests
{
    [Fact]
    public void OreKeywordWholeWordDoesNotMatchForests()
    {
        var ids = TagRuleApplicator.GetMatchingTagIds(
            "forests",
            @"\minecraft\textures\block\forests",
            TagRulePresets.Default,
            TagRuleKind.Flag);

        Assert.DoesNotContain(FlagTagResolver.OreId, ids, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void OreKeywordWholeWordMatchesIronOre()
    {
        var ids = TagRuleApplicator.GetMatchingTagIds(
            "iron_ore",
            @"\minecraft\textures\block\iron_ore",
            TagRulePresets.Default,
            TagRuleKind.Flag);

        Assert.Contains(FlagTagResolver.OreId, ids, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void OreKeywordWholeWordMatchesFilenameOre()
    {
        var ids = TagRuleApplicator.GetMatchingTagIds(
            "ore",
            @"\minecraft\textures\block\raw_gold",
            TagRulePresets.Default,
            TagRuleKind.Flag);

        Assert.Contains(FlagTagResolver.OreId, ids, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubstringMatchStillWorksWhenWholeWordFalse()
    {
        var rules = new List<TagRule>
        {
            new TagRule
            {
                Id = "testflag",
                DisplayName = "Test",
                Kind = TagRuleKind.Flag,
                Keywords = ["ore"],
                KeywordsMatchWholeWord = false
            }
        };

        var ids = TagRuleApplicator.GetMatchingTagIds("forests", @"\a\b\forests", rules, TagRuleKind.Flag);
        Assert.Contains("testflag", ids, StringComparer.OrdinalIgnoreCase);
    }
}
