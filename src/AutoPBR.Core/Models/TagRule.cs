namespace AutoPBR.Core.Models;

using AutoPBR.Contracts;
using AutoPBR.Core.Models.RuleExpressions;

/// <summary>
/// Tag definition: <see cref="TagRuleKind.Material"/> uses keywords / MiniLM; <see cref="TagRuleKind.Flag"/> uses path rules and optional keywords.
/// </summary>
public sealed class TagRule
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public TagRuleKind Kind { get; init; } = TagRuleKind.Material;

    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>
    /// When true, each <see cref="Keywords"/> entry must match as a whole token (not as a substring inside a longer
    /// letter/number run). Underscores and path separators count as boundaries (e.g. <c>iron_ore</c> matches <c>ore</c>;
    /// <c>forests</c> does not).
    /// </summary>
    public bool KeywordsMatchWholeWord { get; init; }

    /// <summary>Short English phrases used only for semantic (embedding) matching; optional.</summary>
    public IReadOnlyList<string> SemanticHints { get; init; } = [];

    /// <summary>
    /// Optional expression rule attached to this tag/flag rule. When provided, runtime expression evaluation can apply
    /// condition + actions in addition to keyword/semantic matching behavior.
    /// </summary>
    public RuleExpressionDefinition? Expression { get; init; }
}
