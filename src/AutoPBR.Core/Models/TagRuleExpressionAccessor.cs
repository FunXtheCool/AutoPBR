using System.Reflection;
using AutoPBR.Core.Models.RuleExpressions;

namespace AutoPBR.Core.Models;

public static class TagRuleExpressionAccessor
{
    private static readonly PropertyInfo? ExpressionProperty = typeof(TagRule).GetProperty("Expression");

    public static RuleExpressionDefinition? GetExpression(TagRule rule) =>
        ExpressionProperty?.GetValue(rule) as RuleExpressionDefinition;

    public static void SetExpression(TagRule rule, RuleExpressionDefinition? expression)
    {
        if (ExpressionProperty?.CanWrite == true)
        {
            ExpressionProperty.SetValue(rule, expression);
        }
    }
}
