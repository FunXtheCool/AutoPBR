namespace AutoPBR.Core.Models;

using RuleExpressions;

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
    public List<string> KeywordTokens
    {
        get => SplitCommaList(Keywords);
        set => Keywords = JoinCommaList(value);
    }

    /// <summary>When true, keywords use whole-word/token matching (see <see cref="TagRule.KeywordsMatchWholeWord"/>).</summary>
    public bool KeywordsMatchWholeWord { get; set; }

    /// <summary>Comma-separated English phrases for MiniLM semantic matching (optional).</summary>
    public string SemanticHints { get; set; } = "";
    public List<string> SemanticHintTokens
    {
        get => SplitCommaList(SemanticHints);
        set => SemanticHints = JoinCommaList(value);
    }

    /// <summary>Optional expression (guided builder) evaluated at runtime.</summary>
    public RuleExpressionDefinition? Expression { get; set; }
    public bool HasExpression => Expression is not null;
    public bool IsTagDefinition => Expression is null;

    public TagRule ToTagRule()
    {
        var keywords = SplitCommaList(Keywords);
        var hints = SplitCommaList(SemanticHints);
        var kind = TagRuleKind.Material;
        if (!string.IsNullOrWhiteSpace(Kind) && Enum.TryParse(Kind.Trim(), ignoreCase: true, out TagRuleKind parsed))
        {
            kind = parsed;
        }

        var tagRule = new TagRule
        {
            Id = Id.Trim(),
            DisplayName = DisplayName.Trim(),
            Kind = kind,
            Keywords = keywords,
            KeywordsMatchWholeWord = KeywordsMatchWholeWord,
            SemanticHints = hints
        };
        TagRuleExpressionAccessor.SetExpression(tagRule, Expression);
        return tagRule;
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

    private static string JoinCommaList(IEnumerable<string> values) =>
        string.Join(", ", values
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));
}
