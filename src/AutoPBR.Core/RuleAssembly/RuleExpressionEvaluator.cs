using System.Text.RegularExpressions;
using AutoPBR.Core.Models.RuleExpressions;

namespace AutoPBR.Core.RuleAssembly;

public static class RuleExpressionEvaluator
{
    public static IReadOnlyDictionary<string, bool> Evaluate(
        RuleEvaluationContext context,
        IReadOnlyList<RuleExpressionDefinition> expressions)
    {
        foreach (var expression in expressions
                     .Where(e => e.Enabled)
                     .OrderByDescending(e => e.Priority)
                     .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (context.StopProcessing)
            {
                break;
            }

            if (!EvaluateNode(context, expression.Condition))
            {
                continue;
            }

            foreach (var action in expression.Actions)
            {
                context.ApplyAction(action);
            }

            if (expression.StopProcessing)
            {
                context.StopProcessing = true;
            }
        }

        return context.OverrideDecisions;
    }

    private static bool EvaluateNode(RuleEvaluationContext context, RuleConditionNode? node)
    {
        if (node is null)
        {
            return true;
        }

        return node.Type switch
        {
            RuleConditionNodeType.All => node.Children.All(c => EvaluateNode(context, c)),
            RuleConditionNodeType.Any => node.Children.Any(c => EvaluateNode(context, c)),
            RuleConditionNodeType.Not => node.Children.Count > 0 && !EvaluateNode(context, node.Children[0]),
            RuleConditionNodeType.HasTag => HasId(context.MaterialIds, node.Value),
            RuleConditionNodeType.HasFlag => HasId(context.FlagIds, node.Value),
            RuleConditionNodeType.PathContains => Contains(context.RuleRelativeKey, node.Value),
            RuleConditionNodeType.NameContains => Contains(context.TextureName, node.Value),
            RuleConditionNodeType.PathRegex => RegexMatch(context.RuleRelativeKey, node.Value),
            RuleConditionNodeType.NameRegex => RegexMatch(context.TextureName, node.Value),
            RuleConditionNodeType.IsWeighted => context.IsWeighted,
            RuleConditionNodeType.IsUnweighted => !context.IsWeighted,
            _ => false
        };
    }

    private static bool HasId(HashSet<string> values, string? value) =>
        !string.IsNullOrWhiteSpace(value) && values.Contains(value.Trim());

    private static bool Contains(string source, string? needle) =>
        !string.IsNullOrWhiteSpace(needle) && source.Contains(needle.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool RegexMatch(string source, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(source, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
