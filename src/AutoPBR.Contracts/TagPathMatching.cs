using System.Text.RegularExpressions;

namespace AutoPBR.Contracts;

/// <summary>Shared path and keyword matching helpers for tag rules.</summary>
public static class TagPathMatching
{
    /// <summary>
    /// Unicode letter/number runs count as one token; other characters (including <c>_</c>, <c>/</c>, <c>\</c>) are boundaries.
    /// </summary>
    public static bool KeywordMatches(string haystack, string keyword, bool wholeWord)
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
        return Regex.IsMatch(
            haystack,
            "(?<![\\p{L}\\p{N}])" + escaped + "(?![\\p{L}\\p{N}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>Everything after the first path segment of <paramref name="relativeKey"/> (the namespace / mod root).</summary>
    public static string PathBelowNamespace(string relativeKey)
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
