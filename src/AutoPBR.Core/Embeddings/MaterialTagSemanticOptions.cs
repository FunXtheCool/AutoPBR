using AutoPBR.Contracts.Ml;

namespace AutoPBR.Core.Embeddings;

/// <summary>Host-provided options for optional MiniLM-based material tag suggestions in Explore.</summary>
public sealed class MaterialTagSemanticOptions
{
    public bool Enabled { get; init; }
    public double MinSimilarity { get; init; } = 0.25;

    /// <summary>
    /// When the best match score is below this cosine similarity, only <c>unknown</c> is returned (material ML).
    /// </summary>
    public double CertaintyThreshold { get; init; } = 0.35;

    /// <summary>
    /// After the top match, further tags must score within this cosine distance of the best score (and meet
    /// <see cref="CertaintyThreshold"/> on their own). Prevents low-confidence filler tags when <see cref="MaxTags"/> &gt; 1.
    /// </summary>
    public double AdditionalTagMaxGapFromBest { get; init; } = 0.13;

    public int MaxTags { get; init; } = 3;
    public MaterialTagSemanticMatcher? Matcher { get; init; }

    /// <summary>When true, look up dictionary definitions and blend their semantic evidence with local MiniLM scoring.</summary>
    public bool DictionaryEvidenceEnabled { get; init; }

    /// <summary>Weight of dictionary evidence in fused score (0..1). 0 keeps local MiniLM only.</summary>
    public double DictionaryEvidenceWeight { get; init; } = 0.35;

    /// <summary>Minimum cosine score for dictionary evidence to contribute to final fused score.</summary>
    public double DictionaryMinEvidenceScore { get; init; } = 0.18;

    /// <summary>Dictionary request timeout in milliseconds per lookup.</summary>
    public int DictionaryRequestTimeoutMs { get; init; } = 900;

    /// <summary>Dictionary language for lookup, e.g. "en".</summary>
    public string DictionaryLanguageCode { get; init; } = "en";

    /// <summary>Provider used for dictionary definition lookups. Null disables dictionary evidence.</summary>
    public IDictionaryDefinitionProvider? DictionaryProvider { get; init; }
}
