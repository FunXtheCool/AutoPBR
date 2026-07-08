namespace AutoPBR.Contracts.Ml;

/// <summary>Material/flag rule fields needed for ONNX tag matching (no expression graph).</summary>
public sealed record MaterialTagRuleDescriptor(
    string Id,
    string DisplayName,
    Contracts.TagRuleKind Kind,
    IReadOnlyList<string> Keywords,
    bool KeywordsMatchWholeWord,
    IReadOnlyList<string> SemanticHints);
