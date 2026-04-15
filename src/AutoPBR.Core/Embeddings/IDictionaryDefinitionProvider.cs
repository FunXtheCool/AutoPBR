namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Provides dictionary definitions for semantic enrichment of material matching.
/// Implementations should be resilient and return an empty list on transient failures.
/// </summary>
public interface IDictionaryDefinitionProvider
{
    IReadOnlyList<string> GetDefinitions(
        string languageCode,
        string lookupTerm,
        TimeSpan timeout,
        out string? diagnostic);
}
