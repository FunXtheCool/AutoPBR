using AutoPBR.Core.Models;

namespace AutoPBR.Core.RuleAssembly;

/// <summary>
/// Builds effective rule sets by combining built-in presets and enabled custom rules.
/// </summary>
public static class EffectiveRuleSetBuilder
{
    public static IReadOnlyList<TagRule> Build(IReadOnlyList<CustomTagRuleEntry>? customEntries)
    {
        if (customEntries is null || customEntries.Count == 0)
        {
            return TagRulePresets.Default;
        }

        var custom = customEntries
            .Where(c => c.Enabled)
            .Select(c => c.ToTagRule())
            .Where(r => !string.IsNullOrWhiteSpace(r.Id))
            .ToList();

        return custom.Count == 0
            ? TagRulePresets.Default
            : TagRulePresets.Default.Concat(custom).ToList();
    }
}
