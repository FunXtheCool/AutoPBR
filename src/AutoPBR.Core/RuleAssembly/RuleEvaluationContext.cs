using AutoPBR.Core.Models.RuleExpressions;

namespace AutoPBR.Core.RuleAssembly;

public sealed class RuleEvaluationContext
{
    public required string TextureName { get; init; }
    public required string RuleRelativeKey { get; init; }
    public required HashSet<string> EffectiveIds { get; init; }
    public required HashSet<string> MaterialIds { get; init; }
    public required HashSet<string> FlagIds { get; init; }
    public required bool IsWeighted { get; init; }
    public Dictionary<string, bool> OverrideDecisions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool StopProcessing { get; set; }

    public void ApplyAction(RuleActionDefinition action)
    {
        switch (action.Type)
        {
            case RuleActionType.AddTag:
            case RuleActionType.AddFlag:
                if (!string.IsNullOrWhiteSpace(action.Value))
                {
                    EffectiveIds.Add(action.Value.Trim());
                }
                break;
            case RuleActionType.RemoveTag:
            case RuleActionType.RemoveFlag:
                if (!string.IsNullOrWhiteSpace(action.Value))
                {
                    EffectiveIds.Remove(action.Value.Trim());
                }
                break;
            case RuleActionType.SetInvertHeight:
                OverrideDecisions["invert_height"] = action.BoolValue ?? true;
                break;
            case RuleActionType.SetInvertSpecular:
                OverrideDecisions["invert_specular"] = action.BoolValue ?? true;
                break;
            case RuleActionType.StopProcessing:
                StopProcessing = true;
                break;
        }
    }
}
