using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// Matches textures to tag rules by keyword and merges tag overrides into work item overrides.
/// </summary>
internal static class TagRuleApplicator
{
    /// <summary>
    /// Returns tag ids whose keywords match the texture name or relative key (case-insensitive contains).
    /// </summary>
    public static IReadOnlyList<string> GetMatchingTagIds(string name, string relativeKey, IReadOnlyList<TagRule> rules)
    {
        if (rules.Count == 0)
        {
            return [];
        }

        var combined = name + "\0" + relativeKey;
        var list = new List<string>();
        foreach (var rule in rules)
        {
            if (string.IsNullOrEmpty(rule.Id) || rule.Keywords.Count == 0)
            {
                continue;
            }

            foreach (var keyword in rule.Keywords)
            {
                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                if (combined.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(rule.Id);
                    break;
                }
            }
        }

        return list;
    }

    /// <summary>
    /// Merges overrides from the given tag rules (by id) into the target overrides.
    /// Only properties that are "set" on the source (non-default) are copied.
    /// </summary>
    public static void MergeTagOverridesInto(
        TextureOverrides target,
        IReadOnlyList<TagRule> rules,
        IReadOnlyList<string> tagIds)
    {
        if (rules.Count == 0 || tagIds.Count == 0)
        {
            return;
        }

        var idSet = new HashSet<string>(tagIds, StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            if (!idSet.Contains(rule.Id))
            {
                continue;
            }

            MergeOne(target, rule.Overrides);
        }
    }

    private static void MergeOne(TextureOverrides target, TextureOverrides source)
    {
        if (source.InvertHeight)
        {
            target.InvertHeight = true;
        }

        if (source.InvertSpecular)
        {
            target.InvertSpecular = true;
        }

        if (source.InvertNormalRed)
        {
            target.InvertNormalRed = true;
        }

        if (source.InvertNormalGreen)
        {
            target.InvertNormalGreen = true;
        }

        if (source.NormalIntensity.HasValue)
        {
            target.NormalIntensity = source.NormalIntensity;
        }

        if (source.HeightIntensity.HasValue)
        {
            target.HeightIntensity = source.HeightIntensity;
        }

        if (source.HeightBrightness.HasValue)
        {
            target.HeightBrightness = source.HeightBrightness;
        }

        if (source.FastSpecular.HasValue)
        {
            target.FastSpecular = source.FastSpecular;
        }

        if (source.CustomSpecularRules is not null)
        {
            target.CustomSpecularRules = source.CustomSpecularRules;
        }
    }
}
