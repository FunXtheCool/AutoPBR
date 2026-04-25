namespace AutoPBR.App.ViewModels.Rulesets;

public enum RuleDisplaySource
{
    BuiltInTag,
    BuiltInRule,
    SystemComputed
}

public sealed class RuleDisplayItemViewModel
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Keywords { get; init; } = "";
    public string SemanticHints { get; init; } = "";
    public IReadOnlyList<string> KeywordTokens { get; init; } = [];
    public IReadOnlyList<string> VisibleKeywordTokens { get; init; } = [];
    public bool KeywordsExpanded { get; init; }
    public int HiddenKeywordCount { get; init; }
    public bool HasHiddenKeywords => HiddenKeywordCount > 0;
    public string MoreKeywordsLabel { get; init; } = "";
    public IReadOnlyList<string> SemanticHintTokens { get; init; } = [];
    public string ExpressionText { get; init; } = "";
    public RuleDisplaySource Source { get; init; }
    public bool IsEditable { get; init; }
}
