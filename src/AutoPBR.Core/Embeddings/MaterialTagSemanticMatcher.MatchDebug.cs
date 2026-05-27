using System.Text.RegularExpressions;
using AutoPBR.Core.Models;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Matches texture titles/paths to material tag ids using MiniLM embeddings (cosine similarity to prototypes).
/// </summary>
public sealed partial class MaterialTagSemanticMatcher
{
    /// <summary>
    /// Returns a detailed debug report: the query text, and for every rule the best cosine score and which
    /// prototype phrase produced it, sorted best-first. For investigating why the ML picks certain tags.
    /// </summary>
    public SemanticMatchDebugReport MatchDebug(
        string textureName,
        string ruleRelativeKey,
        IReadOnlyList<TagRule> rules,
        bool dictionaryEvidenceEnabled = false,
        IDictionaryDefinitionProvider? dictionaryProvider = null,
        double dictionaryEvidenceWeight = 0.35,
        double dictionaryMinEvidenceScore = 0.18,
        int dictionaryRequestTimeoutMs = 900,
        string dictionaryLanguageCode = "en")
    {
        var queryText = MaterialTagSemanticQuery.Build(textureName, ruleRelativeKey);
        var query = _engine.EmbedText(queryText);
        if (query is null)
        {
            return new SemanticMatchDebugReport(queryText, []);
        }

        var materialRules = rules
            .Where(r => !string.IsNullOrWhiteSpace(r.Id) && !r.Id.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var prototypesByRuleId = materialRules
            .ToDictionary(r => r.Id, r => BuildPrototypePhrases(r, rules), StringComparer.OrdinalIgnoreCase);
        PrimePrototypeCache(prototypesByRuleId.Values.SelectMany(static p => p));
        var dictionary = BuildDictionaryEvidence(
            queryText,
            materialRules,
            prototypesByRuleId,
            dictionaryEvidenceEnabled,
            dictionaryProvider,
            dictionaryEvidenceWeight,
            dictionaryMinEvidenceScore,
            dictionaryRequestTimeoutMs,
            dictionaryLanguageCode);

        var entries = new List<SemanticMatchDebugEntry>();
        foreach (var rule in materialRules)
        {
            var protos = prototypesByRuleId[rule.Id];
            if (protos.Count == 0)
            {
                continue;
            }

            var bestScore = float.MinValue;
            var bestPhrase = "";
            var phraseScores = new List<(string Phrase, float Score)>();
            foreach (var phrase in protos)
            {
                var vec = GetOrEmbedPrototype(phrase);
                if (vec is null)
                {
                    continue;
                }

                var s = Dot(query, vec);
                phraseScores.Add((phrase, s));
                if (s > bestScore)
                {
                    bestScore = s;
                    bestPhrase = phrase;
                }
            }

            phraseScores.Sort((a, b) => b.Score.CompareTo(a.Score));
            var dictScore = dictionary.ScoreByRuleId.GetValueOrDefault(rule.Id, float.MinValue);
            var fusedScore = bestScore > float.MinValue
                ? (dictScore > float.MinValue
                    ? (1f - dictionary.Weight) * bestScore + dictionary.Weight * dictScore
                    : (dictionary.Weight > 0f ? (1f - dictionary.Weight) * bestScore : bestScore))
                : float.MinValue;

            entries.Add(new SemanticMatchDebugEntry(rule.Id, rule.DisplayName, bestScore, bestPhrase, phraseScores, dictScore, fusedScore));
        }

        entries.Sort((a, b) => b.FusedScore.CompareTo(a.FusedScore));
        return new SemanticMatchDebugReport(
            queryText,
            entries,
            dictionary.Terms,
            dictionary.Definitions,
            dictionary.Applied,
            dictionary.Weight,
            dictionary.DebugTermBlocks);
    }
}
