namespace AutoPBR.Core.Models;

/// <summary>User-defined material tag (settings + optional JSON for CLI).</summary>
public sealed class CustomTagRuleEntry
{
    /// <summary>When false, this rule is excluded from effective rules (legend and explore).</summary>
    public bool Enabled { get; set; } = true;

    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";

    /// <summary><see cref="TagRuleKind.Material"/> or <see cref="TagRuleKind.Flag"/>.</summary>
    public string Kind { get; set; } = "Material";

    /// <summary>Comma-separated keywords; match when texture name or path contains any (case-insensitive).</summary>
    public string Keywords { get; set; } = "";

    /// <summary>When true, keywords use whole-word/token matching (see <see cref="TagRule.KeywordsMatchWholeWord"/>).</summary>
    public bool KeywordsMatchWholeWord { get; set; }

    /// <summary>Comma-separated English phrases for MiniLM semantic matching (optional).</summary>
    public string SemanticHints { get; set; } = "";

    public TagRule ToTagRule()
    {
        var keywords = SplitCommaList(Keywords);
        var hints = SplitCommaList(SemanticHints);
        var kind = TagRuleKind.Material;
        if (!string.IsNullOrWhiteSpace(Kind) && Enum.TryParse(Kind.Trim(), ignoreCase: true, out TagRuleKind parsed))
        {
            kind = parsed;
        }

        return new TagRule
        {
            Id = Id.Trim(),
            DisplayName = DisplayName.Trim(),
            Kind = kind,
            Keywords = keywords,
            KeywordsMatchWholeWord = KeywordsMatchWholeWord,
            SemanticHints = hints
        };
    }

    private static List<string> SplitCommaList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToList();
    }
}
