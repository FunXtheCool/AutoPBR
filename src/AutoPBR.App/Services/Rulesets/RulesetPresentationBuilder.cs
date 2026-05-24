using AutoPBR.App.Lang;
using AutoPBR.App.ViewModels.Rulesets;
using AutoPBR.Core;
using AutoPBR.Core.Models;
using AutoPBR.Core.Models.RuleExpressions;

namespace AutoPBR.App.Services.Rulesets;

public static class RulesetPresentationBuilder
{
    private const int KeywordPreviewLimit = 5;

    public static IReadOnlyList<RuleDisplayItemViewModel> BuildBuiltInTagRows(Func<string, bool>? isExpanded = null)
    {
        return TagRulePresets.DefaultMaterials
            .Select(r =>
            {
                var rowKey = $"tag:{r.Id}";
                return new RuleDisplayItemViewModel
                {
                    Id = r.Id,
                    DisplayName = r.DisplayName,
                    Kind = r.Kind.ToString(),
                    Keywords = string.Join(", ", r.Keywords),
                    SemanticHints = string.Join(", ", r.SemanticHints),
                    KeywordTokens = r.Keywords.ToList(),
                    VisibleKeywordTokens = BuildVisibleKeywordTokens(rowKey, r.Keywords, isExpanded),
                    KeywordsExpanded = IsExpanded(rowKey, isExpanded),
                    HiddenKeywordCount = CountHiddenKeywords(rowKey, r.Keywords, isExpanded),
                    MoreKeywordsLabel = string.Format(LocalizedStrings.RulesetsMoreCount, CountHiddenKeywords(rowKey, r.Keywords, isExpanded)),
                    SemanticHintTokens = r.SemanticHints.ToList(),
                    Source = RuleDisplaySource.BuiltInTag,
                    IsEditable = false
                };
            })
            .ToList();
    }

    public static IReadOnlyList<RuleDisplayItemViewModel> BuildBuiltInFlagRows(Func<string, bool>? isExpanded = null)
    {
        return TagRulePresets.DefaultFlags
            .Where(r => !IsSystemComputedRule(r.Id))
            .Select(r =>
            {
                var rowKey = $"flag:{r.Id}";
                return new RuleDisplayItemViewModel
                {
                    Id = r.Id,
                    DisplayName = r.DisplayName,
                    Kind = r.Kind.ToString(),
                    Keywords = string.Join(", ", r.Keywords),
                    SemanticHints = string.Join(", ", r.SemanticHints),
                    KeywordTokens = r.Keywords.ToList(),
                    VisibleKeywordTokens = BuildVisibleKeywordTokens(rowKey, r.Keywords, isExpanded),
                    KeywordsExpanded = IsExpanded(rowKey, isExpanded),
                    HiddenKeywordCount = CountHiddenKeywords(rowKey, r.Keywords, isExpanded),
                    MoreKeywordsLabel = string.Format(LocalizedStrings.RulesetsMoreCount, CountHiddenKeywords(rowKey, r.Keywords, isExpanded)),
                    SemanticHintTokens = r.SemanticHints.ToList(),
                    Source = RuleDisplaySource.BuiltInTag,
                    IsEditable = false
                };
            })
            .ToList();
    }

    public static IReadOnlyList<RuleDisplayItemViewModel> BuildBuiltInRuleRows()
    {
        return TagRulePresets.BuiltInExpressions
            .Select(r => new RuleDisplayItemViewModel
            {
                Id = r.Id,
                DisplayName = r.DisplayName,
                Kind = "Expression",
                ExpressionText = ToPseudoExpression(r),
                Source = RuleDisplaySource.BuiltInRule,
                IsEditable = false
            })
            .ToList();
    }

    public static IReadOnlyList<RuleDisplayItemViewModel> BuildSystemRows()
    {
        return TagRulePresets.DefaultFlags
            .Where(r => IsSystemComputedRule(r.Id))
            .Select(r => new RuleDisplayItemViewModel
            {
                Id = r.Id,
                DisplayName = r.DisplayName,
                Kind = r.Kind.ToString(),
                Keywords = "Computed by semantic pipeline",
                SemanticHints = "Read-only system marker",
                Source = RuleDisplaySource.SystemComputed,
                IsEditable = false
            })
            .ToList();
    }

    private static bool IsSystemComputedRule(string id) =>
        id.Equals(FlagTagResolver.WeightedId, StringComparison.OrdinalIgnoreCase) ||
        id.Equals(FlagTagResolver.UnweightedId, StringComparison.OrdinalIgnoreCase);

    private static bool IsExpanded(string id, Func<string, bool>? isExpanded) => isExpanded?.Invoke(id) == true;

    private static IReadOnlyList<string> BuildVisibleKeywordTokens(
        string id,
        IReadOnlyList<string> tokens,
        Func<string, bool>? isExpanded)
    {
        if (IsExpanded(id, isExpanded) || tokens.Count <= KeywordPreviewLimit)
        {
            return tokens.ToList();
        }

        return tokens.Take(KeywordPreviewLimit).ToList();
    }

    private static int CountHiddenKeywords(
        string id,
        IReadOnlyList<string> tokens,
        Func<string, bool>? isExpanded)
    {
        if (IsExpanded(id, isExpanded))
        {
            return 0;
        }

        return Math.Max(0, tokens.Count - KeywordPreviewLimit);
    }

    public static string ToPseudoExpression(RuleExpressionDefinition expression)
    {
        var condition = FormatCondition(expression.Condition);
        var actions = expression.Actions.Count == 0
            ? "(no actions)"
            : string.Join("; ", expression.Actions.Select(FormatAction));
        return $"IF {condition} THEN {actions}";
    }

    private static string FormatCondition(RuleConditionNode? node)
    {
        if (node is null)
        {
            return "TRUE";
        }

        return node.Type switch
        {
            RuleConditionNodeType.All => "(" + string.Join(" AND ", node.Children.Select(FormatCondition)) + ")",
            RuleConditionNodeType.Any => "(" + string.Join(" OR ", node.Children.Select(FormatCondition)) + ")",
            RuleConditionNodeType.Not => "NOT " + FormatCondition(node.Children.FirstOrDefault()),
            RuleConditionNodeType.HasTag => $"has tag \"{node.Value}\"",
            RuleConditionNodeType.HasFlag => $"has flag \"{node.Value}\"",
            RuleConditionNodeType.PathContains => $"path contains \"{node.Value}\"",
            RuleConditionNodeType.NameContains => $"name contains \"{node.Value}\"",
            RuleConditionNodeType.PathRegex => $"path regex \"{node.Value}\"",
            RuleConditionNodeType.NameRegex => $"name regex \"{node.Value}\"",
            RuleConditionNodeType.IsWeighted => "is weighted",
            RuleConditionNodeType.IsUnweighted => "is unweighted",
            _ => "unknown"
        };
    }

    private static string FormatAction(RuleActionDefinition action) =>
        action.Type switch
        {
            RuleActionType.AddTag => $"add tag \"{action.Value}\"",
            RuleActionType.AddFlag => $"add flag \"{action.Value}\"",
            RuleActionType.RemoveTag => $"remove tag \"{action.Value}\"",
            RuleActionType.RemoveFlag => $"remove flag \"{action.Value}\"",
            RuleActionType.SetInvertHeight => $"set invert_height = {(action.BoolValue ?? true)}",
            RuleActionType.SetInvertSpecular => $"set invert_specular = {(action.BoolValue ?? true)}",
            RuleActionType.StopProcessing => "stop processing",
            _ => "unknown action"
        };
}
