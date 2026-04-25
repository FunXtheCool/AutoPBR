namespace AutoPBR.Core.Models.RuleExpressions;

public enum RuleActionType
{
    AddTag,
    AddFlag,
    RemoveTag,
    RemoveFlag,
    SetInvertHeight,
    SetInvertSpecular,
    StopProcessing
}

public sealed class RuleActionDefinition
{
    public RuleActionType Type { get; set; }
    public string? Value { get; set; }
    public bool? BoolValue { get; set; }
}
