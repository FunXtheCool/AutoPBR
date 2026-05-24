using AutoPBR.Core.Models.RuleExpressions;

using Avalonia.Data.Converters;

namespace AutoPBR.App.Converters;

public sealed class ConditionValueOptionsConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var type = values.Count > 0 && values[0] is RuleConditionNodeType t ? t : RuleConditionNodeType.HasTag;
        var tagIds = values.Count > 1 && values[1] is IEnumerable<string> tags ? tags : [];
        var flagIds = values.Count > 2 && values[2] is IEnumerable<string> flags ? flags : [];

        return type switch
        {
            RuleConditionNodeType.HasTag => tagIds.ToList(),
            RuleConditionNodeType.HasFlag => flagIds.ToList(),
            _ => tagIds.Concat(flagIds).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }
}

public sealed class ActionValueOptionsConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var type = values.Count > 0 && values[0] is RuleActionType t ? t : RuleActionType.AddTag;
        var tagIds = values.Count > 1 && values[1] is IEnumerable<string> tags ? tags : [];
        var flagIds = values.Count > 2 && values[2] is IEnumerable<string> flags ? flags : [];

        return type switch
        {
            RuleActionType.AddTag or RuleActionType.RemoveTag => tagIds.ToList(),
            RuleActionType.AddFlag or RuleActionType.RemoveFlag => flagIds.ToList(),
            _ => tagIds.Concat(flagIds).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }
}
