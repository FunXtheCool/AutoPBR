using System.Collections.ObjectModel;

using AutoPBR.App.Lang;
using AutoPBR.App.Services.Rulesets;
using AutoPBR.Core.Models;
using AutoPBR.Core.Models.RuleExpressions;
using AutoPBR.Core.RuleAssembly;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPBR.App.ViewModels.Rulesets;

public partial class RulesetsViewModel : ObservableObject
{
    private readonly Action _onRulesChanged;
    private readonly HashSet<string> _expandedKeywordRows = new(StringComparer.OrdinalIgnoreCase);

    public RulesetsViewModel(Action onRulesChanged)
    {
        _onRulesChanged = onRulesChanged;
        RefreshPresentation();
    }

    public ObservableCollection<CustomTagRuleEntry> CustomTagRules { get; } = new();
    public ObservableCollection<RuleDisplayItemViewModel> BuiltInTags { get; } = new();
    public ObservableCollection<RuleDisplayItemViewModel> BuiltInFlags { get; } = new();
    public ObservableCollection<RuleDisplayItemViewModel> BuiltInRules { get; } = new();
    public ObservableCollection<RuleDisplayItemViewModel> SystemComputedRules { get; } = new();

    public IReadOnlyList<string> TagRuleKindOptions { get; } = ["Material", "Flag"];
    public IReadOnlyList<RuleConditionNodeType> ConditionTypeOptions { get; } =
        Enum.GetValues<RuleConditionNodeType>();
    public IReadOnlyList<RuleActionType> ActionTypeOptions { get; } =
        Enum.GetValues<RuleActionType>();
    public IReadOnlyList<string> AvailableTagIds => GetAvailableIds(TagRuleKind.Material);
    public IReadOnlyList<string> AvailableFlagIds => GetAvailableIds(TagRuleKind.Flag);

    public IReadOnlyList<TagRule> GetEffectiveTagRules() => EffectiveRuleSetBuilder.Build(CustomTagRules);

    public void RefreshPresentation()
    {
        ReplaceRows(BuiltInTags, RulesetPresentationBuilder.BuildBuiltInTagRows(IsKeywordRowExpanded));
        ReplaceRows(BuiltInFlags, RulesetPresentationBuilder.BuildBuiltInFlagRows(IsKeywordRowExpanded));
        ReplaceRows(BuiltInRules, RulesetPresentationBuilder.BuildBuiltInRuleRows());
        ReplaceRows(SystemComputedRules, RulesetPresentationBuilder.BuildSystemRows());
    }

    [RelayCommand]
    private void ToggleKeywordExpansion(RuleDisplayItemViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var key = BuildKeywordRowKey(row);
        if (!_expandedKeywordRows.Remove(key))
        {
            _expandedKeywordRows.Add(key);
        }

        RefreshPresentation();
    }

    public void ReplaceCustomRules(IEnumerable<CustomTagRuleEntry> entries)
    {
        CustomTagRules.Clear();
        foreach (var entry in entries)
        {
            CustomTagRules.Add(entry);
        }
    }

    [RelayCommand]
    private void AddCustomTag()
    {
        CustomTagRules.Add(new CustomTagRuleEntry
        {
            Id = BuildUniqueId("custom_tag"),
            DisplayName = Resources.CustomTagDefaultDisplayName,
            Kind = nameof(TagRuleKind.Material),
            Keywords = "keyword"
        });
        _onRulesChanged();
    }

    [RelayCommand]
    private void AddCustomRule()
    {
        var defaultTagId = AvailableTagIds.Count > 0 ? AvailableTagIds[0] : "unknown";
        var defaultFlagId = AvailableFlagIds.Count > 0 ? AvailableFlagIds[0] : "weighted";
        var id = BuildUniqueId("custom_rule");
        var entry = new CustomTagRuleEntry
        {
            Id = id,
            DisplayName = Resources.CustomRuleDefaultDisplayName,
            Kind = nameof(TagRuleKind.Flag),
            Expression = new RuleExpressionDefinition
            {
                Id = $"{id}_expr",
                DisplayName = Resources.CustomRuleDefaultDisplayName,
                Condition = new RuleConditionNode
                {
                    Type = RuleConditionNodeType.All,
                    Children = [new RuleConditionNode { Type = RuleConditionNodeType.HasTag, Value = defaultTagId }]
                },
                Actions = [new RuleActionDefinition { Type = RuleActionType.AddFlag, Value = defaultFlagId }]
            }
        };
        UpdateExpressionSummary(entry);
        CustomTagRules.Add(entry);
        _onRulesChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveCustomTagRule))]
    private void RemoveCustomTagRule(CustomTagRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        CustomTagRules.Remove(entry);
        _onRulesChanged();
    }

    private static bool CanRemoveCustomTagRule(CustomTagRuleEntry? entry) => entry is not null;

    [RelayCommand(CanExecute = nameof(CanMoveCustomTagRuleUp))]
    private void MoveCustomTagRuleUp(CustomTagRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var i = CustomTagRules.IndexOf(entry);
        if (i <= 0)
        {
            return;
        }

        CustomTagRules.RemoveAt(i);
        CustomTagRules.Insert(i - 1, entry);
        _onRulesChanged();
    }

    private bool CanMoveCustomTagRuleUp(CustomTagRuleEntry? entry) =>
        entry is not null && CustomTagRules.IndexOf(entry) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveCustomTagRuleDown))]
    private void MoveCustomTagRuleDown(CustomTagRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var i = CustomTagRules.IndexOf(entry);
        if (i < 0 || i >= CustomTagRules.Count - 1)
        {
            return;
        }

        CustomTagRules.RemoveAt(i);
        CustomTagRules.Insert(i + 1, entry);
        _onRulesChanged();
    }

    private bool CanMoveCustomTagRuleDown(CustomTagRuleEntry? entry) =>
        entry is not null &&
        CustomTagRules.IndexOf(entry) >= 0 &&
        CustomTagRules.IndexOf(entry) < CustomTagRules.Count - 1;

    [RelayCommand]
    private void RefreshTagRules() => _onRulesChanged();

    [RelayCommand]
    private void AddKeywordToken(CustomTagRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        AddCsvToken(entry, isKeywords: true, "keyword");
    }

    [RelayCommand]
    private void RemoveKeywordToken(CustomTagRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var tokens = SplitTokens(entry.Keywords);
        var token = tokens.Count > 0 ? tokens[^1] : null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        RemoveCsvToken(entry, isKeywords: true, token);
    }

    [RelayCommand]
    private void AddSemanticHintToken(CustomTagRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        AddCsvToken(entry, isKeywords: false, "hint");
    }

    [RelayCommand]
    private void RemoveSemanticHintToken(CustomTagRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var tokens = SplitTokens(entry.SemanticHints);
        var token = tokens.Count > 0 ? tokens[^1] : null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        RemoveCsvToken(entry, isKeywords: false, token);
    }

    [RelayCommand]
    private void AddExpressionCondition(CustomTagRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        EnsureExpression(entry);
        if (entry.Expression?.Condition is not { } condition)
        {
            return;
        }

        var defaultTagId = AvailableTagIds.Count > 0 ? AvailableTagIds[0] : "unknown";
        condition.Children.Add(new RuleConditionNode { Type = RuleConditionNodeType.HasTag, Value = defaultTagId });
        UpdateExpressionSummary(entry);
        TouchEntry(entry);
        _onRulesChanged();
    }

    [RelayCommand]
    private void AddExpressionAction(CustomTagRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        EnsureExpression(entry);
        if (entry.Expression is null)
        {
            return;
        }

        var defaultFlagId = AvailableFlagIds.Count > 0 ? AvailableFlagIds[0] : "weighted";
        entry.Expression.Actions.Add(new RuleActionDefinition { Type = RuleActionType.AddFlag, Value = defaultFlagId });
        UpdateExpressionSummary(entry);
        TouchEntry(entry);
        _onRulesChanged();
    }

    [RelayCommand]
    private void RemoveExpressionCondition(RuleConditionNode? node)
    {
        if (node is null)
        {
            return;
        }

        var owner = CustomTagRules.FirstOrDefault(x => x.Expression?.Condition?.Children.Contains(node) == true);
        if (owner?.Expression?.Condition is null)
        {
            return;
        }

        owner.Expression.Condition.Children.Remove(node);
        UpdateExpressionSummary(owner);
        TouchEntry(owner);
        _onRulesChanged();
    }

    [RelayCommand]
    private void RemoveExpressionAction(RuleActionDefinition? action)
    {
        if (action is null)
        {
            return;
        }

        var owner = CustomTagRules.FirstOrDefault(x => x.Expression?.Actions.Contains(action) == true);
        if (owner?.Expression is null)
        {
            return;
        }

        owner.Expression.Actions.Remove(action);
        UpdateExpressionSummary(owner);
        TouchEntry(owner);
        _onRulesChanged();
    }

    [RelayCommand]
    private void EnsureExpression(CustomTagRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        entry.Expression ??= new RuleExpressionDefinition
        {
            Id = string.IsNullOrWhiteSpace(entry.Id) ? BuildUniqueId("custom_expr") : $"{entry.Id}_expr",
            DisplayName = entry.DisplayName,
            Condition = new RuleConditionNode { Type = RuleConditionNodeType.All },
            Actions = []
        };
        UpdateExpressionSummary(entry);
        TouchEntry(entry);
        _onRulesChanged();
    }

    public static IReadOnlyList<string> SplitTokens(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void AddCsvToken(CustomTagRuleEntry entry, bool isKeywords, string defaultValue)
    {
        var tokens = SplitTokens(isKeywords ? entry.Keywords : entry.SemanticHints).ToList();
        tokens.Add(defaultValue);
        var next = string.Join(", ", tokens.Distinct(StringComparer.OrdinalIgnoreCase));
        if (isKeywords)
        {
            entry.Keywords = next;
        }
        else
        {
            entry.SemanticHints = next;
        }

        TouchEntry(entry);
        _onRulesChanged();
    }

    private void RemoveCsvToken(CustomTagRuleEntry entry, bool isKeywords, string token)
    {
        var tokens = SplitTokens(isKeywords ? entry.Keywords : entry.SemanticHints)
            .Where(t => !t.Equals(token, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var next = string.Join(", ", tokens);
        if (isKeywords)
        {
            entry.Keywords = next;
        }
        else
        {
            entry.SemanticHints = next;
        }

        TouchEntry(entry);
        _onRulesChanged();
    }

    private void TouchEntry(CustomTagRuleEntry entry)
    {
        var index = CustomTagRules.IndexOf(entry);
        if (index < 0)
        {
            return;
        }

        CustomTagRules[index] = entry;
    }

    private static void UpdateExpressionSummary(CustomTagRuleEntry entry)
    {
        if (entry.Expression is null)
        {
            return;
        }

        var conditionCount = CountConditionNodes(entry.Expression.Condition);
        var actionCount = entry.Expression.Actions.Count;
        entry.Expression.DisplayName = $"Conditions: {conditionCount}, Actions: {actionCount}";
    }

    private static int CountConditionNodes(RuleConditionNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        return 1 + node.Children.Sum(CountConditionNodes);
    }

    private List<string> GetAvailableIds(TagRuleKind kind)
    {
        var builtIns = kind == TagRuleKind.Material
            ? TagRulePresets.DefaultMaterials.Select(t => t.Id)
            : TagRulePresets.DefaultFlags.Select(t => t.Id);

        var custom = CustomTagRules
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) &&
                        string.Equals(x.Kind, kind.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id.Trim());

        return builtIns
            .Concat(custom)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string BuildUniqueId(string prefix)
    {
        var i = 1;
        while (true)
        {
            var candidate = $"{prefix}_{i}";
            if (CustomTagRules.All(x => !candidate.Equals(x.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
            i++;
        }
    }

    private static void ReplaceRows<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private bool IsKeywordRowExpanded(string rowId) => _expandedKeywordRows.Contains(rowId);

    private static string BuildKeywordRowKey(RuleDisplayItemViewModel row) =>
        row.Source switch
        {
            RuleDisplaySource.BuiltInTag => $"tag:{row.Id.Trim()}",
            RuleDisplaySource.BuiltInRule => $"rule:{row.Id.Trim()}",
            RuleDisplaySource.SystemComputed => $"system:{row.Id.Trim()}",
            _ => $"{row.Source}:{row.Id.Trim()}"
        };
}
