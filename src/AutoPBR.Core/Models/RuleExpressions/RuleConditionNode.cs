namespace AutoPBR.Core.Models.RuleExpressions;

public enum RuleConditionNodeType
{
    All,
    Any,
    Not,
    HasTag,
    HasFlag,
    PathContains,
    NameContains,
    PathRegex,
    NameRegex,
    IsWeighted,
    IsUnweighted
}

public sealed class RuleConditionNode
{
    public RuleConditionNodeType Type { get; set; }
    public string? Value { get; set; }
    public List<RuleConditionNode> Children { get; set; } = [];
}
