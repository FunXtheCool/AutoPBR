using System.Text.RegularExpressions;
using AutoPBR.Core.Models;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Matches texture titles/paths to material tag ids using MiniLM embeddings (cosine similarity to prototypes).
/// </summary>
public sealed partial class MaterialTagSemanticMatcher
{
    /// <summary>
    /// Returns material tag ids from MiniLM similarity. Rules should be material-only (typically excluding <c>unknown</c> from prototypes).
    /// When the best score is below <paramref name="certaintyThreshold"/>, returns only <c>unknown</c>.
    /// The best tag must meet <paramref name="minSimilarity"/>. Additional tags (when <paramref name="maxTags"/> &gt; 1) must also meet
    /// <paramref name="certaintyThreshold"/> and stay within <paramref name="additionalTagMaxGapFromBest"/> cosine distance of the best score,
    /// so weak secondary guesses are not forced in just to fill slots.
    /// </summary>
    public IReadOnlyList<string> Match(
        string textureName,
        string ruleRelativeKey,
        IReadOnlyList<TagRule> rules,
        double minSimilarity,
        int maxTags,
        double certaintyThreshold,
        double additionalTagMaxGapFromBest = 0.13,
        bool dictionaryEvidenceEnabled = false,
        IDictionaryDefinitionProvider? dictionaryProvider = null,
        double dictionaryEvidenceWeight = 0.35,
        double dictionaryMinEvidenceScore = 0.18,
        int dictionaryRequestTimeoutMs = 900,
        string dictionaryLanguageCode = "en")
    {
        if (rules.Count == 0 || maxTags <= 0)
        {
            return [];
        }

        var queryText = MaterialTagSemanticQuery.Build(textureName, ruleRelativeKey);
        var query = _engine.EmbedText(queryText);
        if (query is null)
        {
            return [];
        }

        var min = (float)minSimilarity;
        var certainty = (float)certaintyThreshold;
        var gap = (float)additionalTagMaxGapFromBest;
        if (gap < 0f)
        {
            gap = 0f;
        }

        var perRule = new List<(string Id, float Score)>();
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

        var dictScoreByRule = dictionary.ScoreByRuleId;
        var weight = dictionary.Weight;

        foreach (var rule in materialRules)
        {
            var protos = prototypesByRuleId[rule.Id];
            if (protos.Count == 0)
            {
                continue;
            }

            var best = float.MinValue;
            foreach (var phrase in protos)
            {
                var vec = GetOrEmbedPrototype(phrase);
                if (vec is null)
                {
                    continue;
                }

                var s = Dot(query, vec);
                if (s > best)
                {
                    best = s;
                }
            }

            if (best > float.MinValue)
            {
                // When dictionary evidence is enabled, rules with no dictionary support should not keep
                // full base score, otherwise they can dominate even when dictionary strongly supports another rule.
                var fused = weight > 0f ? (1f - weight) * best : best;
                if (dictScoreByRule.TryGetValue(rule.Id, out var dictScore))
                {
                    fused = (1f - weight) * best + weight * dictScore;
                }

                perRule.Add((rule.Id, fused));
            }
        }

        perRule.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (perRule.Count == 0 || perRule[0].Score < certainty)
        {
            return ["unknown"];
        }

        var bestScore = perRule[0].Score;
        var result = new List<string>();
        foreach (var (id, score) in perRule)
        {
            if (result.Count >= maxTags)
            {
                break;
            }

            if (score < min)
            {
                continue;
            }

            if (result.Count == 0)
            {
                result.Add(id);
                continue;
            }

            // Do not add extra tags unless they are independently confident and nearly tied with the best match.
            if (score < certainty || score < bestScore - gap)
            {
                continue;
            }

            result.Add(id);
        }

        return result;
    }
}
