using AutoPBR.Core.Models;
using AutoPBR.Core.Models.RuleExpressions;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class ConversionEffectiveTagsExpressionTests
{
    [Fact]
    public void ComputeResolutionAppliesBuiltInEntityUvRuleToRemoveSprite()
    {
        var rules = TagRulePresets.Default;
        var result = ConversionEffectiveTags.ComputeResolution(
            textureName: "leaf",
            ruleRelativeKey: @"\minecraft\textures\entity\leaf",
            texturePath: null,
            rules,
            sem: null,
            includeDictionaryEvidence: false,
            deferSemanticMl: false,
            added: null,
            removed: null);

        Assert.DoesNotContain(FlagTagResolver.Sprite2DId, result.EffectiveIds, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComputeResolutionAppliesCustomExpressionOverride()
    {
        var custom = new TagRule
        {
            Id = "custom_expr_rule",
            DisplayName = "Custom Expr Rule",
            Kind = TagRuleKind.Flag
        };
        TagRuleExpressionAccessor.SetExpression(custom, new RuleExpressionDefinition
        {
            Id = "set_invert_height",
            Condition = new RuleConditionNode { Type = RuleConditionNodeType.NameContains, Value = "ore" },
            Actions = [new RuleActionDefinition { Type = RuleActionType.SetInvertHeight, BoolValue = false }]
        });
        var rules = TagRulePresets.Default.Concat([custom]).ToList();
        var result = ConversionEffectiveTags.ComputeResolution(
            textureName: "iron_ore",
            ruleRelativeKey: @"\minecraft\textures\block\iron_ore",
            texturePath: null,
            rules,
            sem: null,
            includeDictionaryEvidence: false,
            deferSemanticMl: false,
            added: null,
            removed: null);

        Assert.True(result.OverrideDecisions.TryGetValue("invert_height", out var invertHeight));
        Assert.False(invertHeight);
    }
}
