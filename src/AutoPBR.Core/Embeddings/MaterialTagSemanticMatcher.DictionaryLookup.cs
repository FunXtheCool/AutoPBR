using System.Text.RegularExpressions;
using AutoPBR.Contracts.Ml;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Matches texture titles/paths to material tag ids using MiniLM embeddings (cosine similarity to prototypes).
/// </summary>
public sealed partial class MaterialTagSemanticMatcher
{
    /// <summary>
    /// Down-weights <c>door</c> / <c>trapdoor</c> glosses; boosts the preceding term so material words (e.g. oak, iron) dominate.
    /// </summary>
    private static float[] ComputeDoorTrapdoorDictionaryTermWeights(List<string> terms)
    {
        var w = new float[terms.Count];
        for (var i = 0; i < terms.Count; i++)
        {
            w[i] = 1f;
        }

        for (var i = 0; i < terms.Count; i++)
        {
            var t = terms[i];
            if (t.Equals("door", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("trapdoor", StringComparison.OrdinalIgnoreCase))
            {
                w[i] *= DoorTrapdoorDictionaryPenalty;
                if (i > 0)
                {
                    w[i - 1] *= DoorPrecedingTermDictionaryBoost;
                }
            }
        }

        for (var i = 0; i < w.Length; i++)
        {
            w[i] = Math.Clamp(w[i], 0.15f, 2.5f);
        }

        return w;
    }

    /// <summary>Round-robin merge of definition lines; each line carries its source bucket weight for <see cref="Dot"/> scaling.</summary>
    private static List<(string Def, float Weight)> InterleaveWeightedDefinitionBuckets(
        List<(string Term, List<string> Defs, float Weight)> buckets,
        int maxTotal)
    {
        if (buckets.Count == 0 || maxTotal <= 0)
        {
            return [];
        }

        var merged = new List<(string Def, float Weight)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maxLen = 0;
        foreach (var (_, defs, _) in buckets)
        {
            if (defs.Count > maxLen)
            {
                maxLen = defs.Count;
            }
        }

        for (var round = 0; round < maxLen && merged.Count < maxTotal; round++)
        {
            foreach (var (_, defs, termWeight) in buckets)
            {
                if (round >= defs.Count)
                {
                    continue;
                }

                var d = defs[round].Trim();
                if (d.Length == 0 || !seen.Add(d))
                {
                    continue;
                }

                merged.Add((d, termWeight));
                if (merged.Count >= maxTotal)
                {
                    return merged;
                }
            }
        }

        return merged;
    }

    /// <summary>Calls the dictionary for every lemma variant and merges distinct gloss lines (per-term cap).</summary>
    private static List<string> LookupDefinitionsWithVariants(
        IDictionaryDefinitionProvider provider,
        string languageCode,
        string term,
        TimeSpan timeout,
        int maxDefinitionsPerTerm)
    {
        var defs = new List<string>();
        foreach (var variant in BuildDictionaryVariants(term))
        {
            var got = provider.GetDefinitions(languageCode, variant, timeout, out _);
            foreach (var g in got)
            {
                if (!string.IsNullOrWhiteSpace(g))
                {
                    defs.Add(g.Trim());
                }
            }
        }

        return defs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxDefinitionsPerTerm))
            .ToList();
    }

    private static List<string> BuildDictionaryVariants(string term)
    {
        var t = term.Trim().ToLowerInvariant();
        if (t.Length == 0)
        {
            return [];
        }

        var variants = new List<string> { t };
        if (t.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && t.Length > 3)
        {
            variants.Add(t[..^3] + "y");
        }

        if (t.EndsWith("ves", StringComparison.OrdinalIgnoreCase) && t.Length > 3)
        {
            variants.Add(t[..^3] + "f");
            variants.Add(t[..^3] + "fe");
        }

        if (t.EndsWith("es", StringComparison.OrdinalIgnoreCase) && t.Length > 2)
        {
            variants.Add(t[..^2]);
        }

        if (t.EndsWith("s", StringComparison.OrdinalIgnoreCase) && t.Length > 1)
        {
            variants.Add(t[..^1]);
        }

        return variants
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> BuildDictionaryTerms(string queryText) =>
        MaterialTagSemanticQuery.ExtractOrderedDictionaryTerms(queryText).ToList();
}
