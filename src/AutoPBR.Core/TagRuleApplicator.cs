using System.Text.RegularExpressions;
using AutoPBR.Contracts;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// Matches textures to tag rules by keyword (file name and path below the pack namespace).
/// </summary>
internal static class TagRuleApplicator
{
    internal static bool KeywordMatches(string haystack, string keyword, bool wholeWord) =>
        TagPathMatching.KeywordMatches(haystack, keyword, wholeWord);

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

    internal static string PathBelowNamespace(string relativeKey) =>
        TagPathMatching.PathBelowNamespace(relativeKey);
}
