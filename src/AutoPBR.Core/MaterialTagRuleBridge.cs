using AutoPBR.Contracts.Ml;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

public static class MaterialTagRuleBridge
{
    public static MaterialTagRuleDescriptor ToDescriptor(this TagRule rule) =>
        new(
            rule.Id,
            rule.DisplayName,
            rule.Kind,
            rule.Keywords,
            rule.KeywordsMatchWholeWord,
            rule.SemanticHints);

    public static List<MaterialTagRuleDescriptor> ToDescriptors(this IEnumerable<TagRule> rules) =>
        rules.Select(r => r.ToDescriptor()).ToList();
}
