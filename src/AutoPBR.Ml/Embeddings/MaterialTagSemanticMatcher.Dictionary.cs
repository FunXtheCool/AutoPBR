using System.Text.RegularExpressions;
using AutoPBR.Contracts.Ml;
using AutoPBR.Contracts;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Matches texture titles/paths to material tag ids using MiniLM embeddings (cosine similarity to prototypes).
/// </summary>
public sealed partial class MaterialTagSemanticMatcher
{
    private DictionaryEvidence BuildDictionaryEvidence(
        string queryText,
        List<MaterialTagRuleDescriptor> materialRules,
        Dictionary<string, List<string>> prototypesByRuleId,
        bool dictionaryEvidenceEnabled,
        IDictionaryDefinitionProvider? dictionaryProvider,
        double dictionaryEvidenceWeight,
        double dictionaryMinEvidenceScore,
        int dictionaryRequestTimeoutMs,
        string dictionaryLanguageCode)
    {
        if (!dictionaryEvidenceEnabled || dictionaryProvider is null || materialRules.Count == 0)
        {
            return DictionaryEvidence.Empty;
        }

        var terms = BuildDictionaryTerms(queryText);
        if (terms.Count == 0)
        {
            return DictionaryEvidence.Empty;
        }

        var termWeights = ComputeDoorTrapdoorDictionaryTermWeights(terms);
        var hasDoorOrTrapdoor = terms.Any(static t =>
            t.Equals("door", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("trapdoor", StringComparison.OrdinalIgnoreCase));

        const int maxDefsPerTerm = 12;
        const int maxEmbeddingDefs = 48;
        var timeout = TimeSpan.FromMilliseconds(Math.Clamp(dictionaryRequestTimeoutMs, 100, 5000));

        // One bucket per query term: every lemma variant is queried; caps are per-term so later terms are not starved.
        var buckets = new List<(string Term, List<string> Defs, float Weight)>();
        for (var ti = 0; ti < terms.Count; ti++)
        {
            var term = terms[ti];
            var termDefs = LookupDefinitionsWithVariants(
                dictionaryProvider,
                dictionaryLanguageCode,
                term,
                timeout,
                maxDefsPerTerm);
            if (termDefs.Count > 0)
            {
                buckets.Add((term, termDefs, termWeights[ti]));
            }
        }

        // Phrase-level lookup as an extra bucket (round-robin interleaved with per-term defs).
        var phrase = string.Join(' ', terms);
        if (!string.IsNullOrWhiteSpace(phrase))
        {
            var phraseGot = dictionaryProvider.GetDefinitions(dictionaryLanguageCode, phrase, timeout, out _);
            var phraseList = phraseGot
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxDefsPerTerm)
                .ToList();
            if (phraseList.Count > 0)
            {
                buckets.Add(($"(phrase) {phrase}", phraseList, 1f));
            }
        }

        var debugTermBlocks = new List<DictionaryTermDebugBlock>();
        foreach (var term in terms)
        {
            List<string>? termDefList = null;
            foreach (var (t, defList, _) in buckets)
            {
                if (string.Equals(t, term, StringComparison.OrdinalIgnoreCase))
                {
                    termDefList = defList;
                    break;
                }
            }

            if (termDefList is null || termDefList.Count == 0)
            {
                continue;
            }

            // Up to four synonymous gloss lines per term for debug UI.
            var lines = termDefList
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();
            if (lines.Count > 0)
            {
                debugTermBlocks.Add(new DictionaryTermDebugBlock(term, lines));
            }
        }

        var weightedDefs = InterleaveWeightedDefinitionBuckets(buckets, maxEmbeddingDefs);
        if (weightedDefs.Count == 0)
        {
            return new DictionaryEvidence(
                terms,
                [],
                new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                false,
                0f,
                debugTermBlocks);
        }

        var weightedDefVecs = new List<(float[] Vec, float Weight)>(weightedDefs.Count);
        var defs = new List<string>(weightedDefs.Count);
        var defVectors = _engine.EmbedTexts(weightedDefs.Select(static wd => wd.Def).ToList());
        foreach (var (def, w) in weightedDefs)
        {
            defs.Add(def);
            var vec = defVectors.GetValueOrDefault(def);
            if (vec is not null)
            {
                weightedDefVecs.Add((vec, w));
            }
        }

        if (weightedDefVecs.Count == 0)
        {
            return new DictionaryEvidence(
                terms,
                defs,
                new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                false,
                0f,
                debugTermBlocks);
        }

        var minEvidence = (float)Math.Clamp(dictionaryMinEvidenceScore, -1.0, 1.0);
        var scoreByRule = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in materialRules)
        {
            if (!prototypesByRuleId.TryGetValue(rule.Id, out var protos))
            {
                continue;
            }

            if (protos.Count == 0)
            {
                continue;
            }

            var best = float.MinValue;
            foreach (var phraseProto in protos)
            {
                var protoVec = GetOrEmbedPrototype(phraseProto);
                if (protoVec is null)
                {
                    continue;
                }

                foreach (var (defVec, termW) in weightedDefVecs)
                {
                    var s = Dot(defVec, protoVec) * termW;
                    if (s > best)
                    {
                        best = s;
                    }
                }
            }

            if (best >= minEvidence)
            {
                scoreByRule[rule.Id] = best;
            }
        }

        if (hasDoorOrTrapdoor && materialRules.Any(r => r.Id.Equals("wood", StringComparison.OrdinalIgnoreCase)))
        {
            var bonus = WoodDictionaryBiasWhenDoorOrTrapdoor;
            if (scoreByRule.TryGetValue("wood", out var woodScore))
            {
                scoreByRule["wood"] = Math.Min(1f, woodScore + bonus);
            }
            else
            {
                scoreByRule["wood"] = Math.Max(minEvidence, bonus);
            }
        }

        var weight = (float)Math.Clamp(dictionaryEvidenceWeight, 0.0, 1.0);
        var applied = scoreByRule.Count > 0 && weight > 0f;
        return new DictionaryEvidence(terms, defs, scoreByRule, applied, weight, debugTermBlocks);
    }
}
