namespace AutoPBR.Core.Models.RuleExpressions;

public sealed class RuleExpressionDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public bool StopProcessing { get; set; }
    public RuleConditionNode? Condition { get; set; }
    public List<RuleActionDefinition> Actions { get; set; } = [];
    public bool IsBuiltIn { get; set; }
}
