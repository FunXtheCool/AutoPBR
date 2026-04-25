using AutoPBR.Core.Models;
using AutoPBR.Core.RuleAssembly;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class EffectiveRuleSetBuilderTests
{
    [Fact]
    public void BuildWithoutCustomEntriesReturnsDefaultPresetInstance()
    {
        var effective = EffectiveRuleSetBuilder.Build([]);
        Assert.Same(TagRulePresets.Default, effective);
    }

    [Fact]
    public void BuildSkipsDisabledAndBlankIdCustomRules()
    {
        var custom = new List<CustomTagRuleEntry>
        {
            new() { Enabled = false, Id = "disabled_rule", DisplayName = "Disabled", Keywords = "stone" },
            new() { Enabled = true, Id = "  ", DisplayName = "Blank", Keywords = "wood" },
            new() { Enabled = true, Id = "custom_flag", DisplayName = "Custom Flag", Kind = "Flag", Keywords = "ctm" }
        };

        var effective = EffectiveRuleSetBuilder.Build(custom);
        var last = Assert.Single(effective, r => r.Id.Equals("custom_flag", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(TagRuleKind.Flag, last.Kind);
    }

    [Fact]
    public void BuildKeepsDeterministicOrderBuiltInThenCustom()
    {
        var custom = new List<CustomTagRuleEntry>
        {
            new() { Enabled = true, Id = "custom_a", DisplayName = "Custom A", Keywords = "a" },
            new() { Enabled = true, Id = "custom_b", DisplayName = "Custom B", Keywords = "b" }
        };

        var effective = EffectiveRuleSetBuilder.Build(custom);
        Assert.Equal("custom_a", effective[^2].Id);
        Assert.Equal("custom_b", effective[^1].Id);
    }
}
