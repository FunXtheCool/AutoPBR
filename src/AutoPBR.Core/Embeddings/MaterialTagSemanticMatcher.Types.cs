namespace AutoPBR.Core.Embeddings;

/// <summary>Per-rule detail from the debug matcher.</summary>
public sealed record SemanticMatchDebugEntry(
    string RuleId,
    string DisplayName,
    float BestScore,
    string BestPhrase,
    IReadOnlyList<(string Phrase, float Score)> AllPhraseScores,
    float DictionaryBestScore = float.MinValue,
    float FusedScore = float.MinValue);

/// <summary>Per query-term dictionary gloss lines shown in the ML debug dialog.</summary>
public sealed record DictionaryTermDebugBlock(string Term, IReadOnlyList<string> DefinitionLines);

/// <summary>Full debug report for one texture.</summary>
public sealed record SemanticMatchDebugReport(
    string QueryText,
    IReadOnlyList<SemanticMatchDebugEntry> Entries,
    IReadOnlyList<string>? DictionaryTerms = null,
    IReadOnlyList<string>? DictionaryDefinitions = null,
    bool DictionaryEvidenceApplied = false,
    float DictionaryEvidenceWeight = 0f,
    IReadOnlyList<DictionaryTermDebugBlock>? DictionaryTermBlocks = null);

internal sealed record DictionaryEvidence(
    IReadOnlyList<string> Terms,
    IReadOnlyList<string> Definitions,
    IReadOnlyDictionary<string, float> ScoreByRuleId,
    bool Applied,
    float Weight,
    IReadOnlyList<DictionaryTermDebugBlock> DebugTermBlocks)
{
    public static DictionaryEvidence Empty { get; } =
        new([], [], new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase), false, 0f, []);
}
