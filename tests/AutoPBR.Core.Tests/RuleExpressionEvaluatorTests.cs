using AutoPBR.Core.Models.RuleExpressions;
using AutoPBR.Core.RuleAssembly;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class RuleExpressionEvaluatorTests
{
    [Fact]
    public void EvaluateAddsAndRemovesIdsAndSetsOverrides()
    {
        var context = new RuleEvaluationContext
        {
            TextureName = "leaf",
            RuleRelativeKey = @"\minecraft\textures\entity\leaf",
            EffectiveIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "organic", "entity", "uv_wrap", "sprite_2d" },
            MaterialIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "organic" },
            FlagIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "entity", "uv_wrap", "sprite_2d" },
            IsWeighted = false
        };

        var expressions = new[]
        {
            new RuleExpressionDefinition
            {
                Id = "remove_sprite",
                Priority = 10,
                Condition = new RuleConditionNode
                {
                    Type = RuleConditionNodeType.All,
                    Children =
                    [
                        new RuleConditionNode { Type = RuleConditionNodeType.HasTag, Value = "organic" },
                        new RuleConditionNode { Type = RuleConditionNodeType.HasFlag, Value = "entity" },
                        new RuleConditionNode { Type = RuleConditionNodeType.HasFlag, Value = "uv_wrap" }
                    ]
                },
                Actions =
                [
                    new RuleActionDefinition { Type = RuleActionType.RemoveFlag, Value = "sprite_2d" },
                    new RuleActionDefinition { Type = RuleActionType.SetInvertSpecular, BoolValue = true }
                ]
            }
        };

        var overrides = RuleExpressionEvaluator.Evaluate(context, expressions);
        Assert.DoesNotContain("sprite_2d", context.EffectiveIds, StringComparer.OrdinalIgnoreCase);
        Assert.True(overrides.TryGetValue("invert_specular", out var invSpec) && invSpec);
    }
}
