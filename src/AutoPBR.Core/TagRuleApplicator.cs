using System.Text.RegularExpressions;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// Matches textures to tag rules by keyword (file name and path below the pack namespace).
/// </summary>
internal static class TagRuleApplicator
{
    /// <summary>
    /// Unicode letter/number runs count as one token; other characters (including <c>_</c>, <c>/</c>, <c>\</c>) are boundaries.
    /// </summary>
    internal static bool KeywordMatches(string haystack, string keyword, bool wholeWord)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return false;
        }

        if (!wholeWord)
        {
            return haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        var escaped = Regex.Escape(keyword);
        // Avoid $@"..." — '{' in \p{L} would start string interpolation.
        return Regex.IsMatch(
            haystack,
            "(?<![\\p{L}\\p{N}])" + escaped + "(?![\\p{L}\\p{N}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Returns tag ids whose keywords match the texture file name or the relative path <strong>excluding</strong>
    /// the first segment (the <c>assets/&lt;namespace&gt;/</c> root / mod id). That way a keyword like <c>metal</c>
    /// still matches filenames such as <c>metal_ingot</c>, but not the mod folder name <c>mythicmetals</c>.
    /// </summary>
    /// <summary>Keyword substring matches for rules of <paramref name="kind"/> (material vs flag).</summary>
    public static IReadOnlyList<string> GetMatchingTagIds(
        string name,
        string relativeKey,
        IReadOnlyList<TagRule> rules,
        TagRuleKind kind)
    {
        if (rules.Count == 0)
        {
            return [];
        }

        var pathBelowNamespace = PathBelowNamespace(relativeKey);
        var combined = name + "\0" + pathBelowNamespace;
        var list = new List<string>();
        foreach (var rule in rules)
        {
            if (rule.Kind != kind || string.IsNullOrEmpty(rule.Id) || rule.Keywords.Count == 0)
            {
                continue;
            }

            foreach (var keyword in rule.Keywords)
            {
                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                if (KeywordMatches(combined, keyword, rule.KeywordsMatchWholeWord))
                {
                    list.Add(rule.Id);
                    break;
                }
            }
        }

        return list;
    }

    /// <summary>Everything after the first path segment of <paramref name="relativeKey"/> (the namespace / mod root).</summary>
    internal static string PathBelowNamespace(string relativeKey)
    {
        if (string.IsNullOrEmpty(relativeKey))
        {
            return string.Empty;
        }

        var parts = relativeKey.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
        {
            return string.Empty;
        }

        return string.Join("\\", parts.Skip(1));
    }
}
