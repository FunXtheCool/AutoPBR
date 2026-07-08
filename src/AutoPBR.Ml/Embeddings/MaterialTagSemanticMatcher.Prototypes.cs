using System.Text.RegularExpressions;
using AutoPBR.Contracts.Ml;
using AutoPBR.Contracts;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Matches texture titles/paths to material tag ids using MiniLM embeddings (cosine similarity to prototypes).
/// </summary>
public sealed partial class MaterialTagSemanticMatcher
{
    private float[]? GetOrEmbedPrototype(string phrase)
    {
        if (_prototypeCache.TryGetValue(phrase, out var cached))
        {
            return cached;
        }

        var e = _engine.EmbedText(phrase);
        if (e is not null)
        {
            _prototypeCache[phrase] = e;
        }

        return e;
    }

    /// <summary>
    /// Prototype phrases for embedding: display name and semantic hints, excluding any phrase that uses
    /// another material rule's id, display-name word, or keyword (avoids cross-tag contamination, e.g. "stone bricks" on stone vs brick).
    /// </summary>
    private static List<string> BuildPrototypePhrases(MaterialTagRuleDescriptor rule, IReadOnlyList<MaterialTagRuleDescriptor> allRules)
    {
        var foreignTokens = CollectForeignMaterialTokens(rule, allRules);
        var list = new List<string>();

        void TryAdd(string? phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
            {
                return;
            }

            var t = phrase.Trim();
            if (PhraseContainsForeignMaterialToken(t, foreignTokens))
            {
                return;
            }

            list.Add(t);
        }

        TryAdd(rule.DisplayName);
        foreach (var h in rule.SemanticHints)
        {
            TryAdd(h);
        }

        if (list.Count == 0 && !string.IsNullOrWhiteSpace(rule.Id) && !rule.Id.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            TryAdd(rule.Id);
        }

        return list;
    }

    /// <summary>Tokens from other material rules (id, keywords, display-name words) used to reject overlapping prototype phrases.</summary>
    private static HashSet<string> CollectForeignMaterialTokens(MaterialTagRuleDescriptor current, IReadOnlyList<MaterialTagRuleDescriptor> allRules)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in allRules)
        {
            if (r.Kind != TagRuleKind.Material)
            {
                continue;
            }

            if (r.Id.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
                r.Id.Equals(current.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddForeignMaterialToken(set, r.Id);
            foreach (var k in r.Keywords)
            {
                AddForeignMaterialToken(set, k);
            }

            if (string.IsNullOrWhiteSpace(r.DisplayName))
            {
                continue;
            }

            foreach (var w in r.DisplayName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddForeignMaterialToken(set, w);
            }
        }

        return set;
    }

    private static void AddForeignMaterialToken(HashSet<string> set, string raw)
    {
        var t = raw.Trim();
        if (t.Length < 3)
        {
            return;
        }

        set.Add(t.ToLowerInvariant());
    }

    private static bool PhraseContainsForeignMaterialToken(string phrase, HashSet<string> foreignTokens)
    {
        if (foreignTokens.Count == 0)
        {
            return false;
        }

        foreach (var tok in foreignTokens)
        {
            if (tok.Length < 3)
            {
                continue;
            }

            var escaped = Regex.Escape(tok) + "s?";

            if (Regex.IsMatch(phrase, @"\b" + escaped + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return true;
            }
        }

        return false;
    }
    private void PrimePrototypeCache(IEnumerable<string> phrases)
    {
        var missing = phrases
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => p.Trim())
            .Where(p => !_prototypeCache.ContainsKey(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var embedded = _engine.EmbedTexts(missing);
        foreach (var kv in embedded)
        {
            _prototypeCache[kv.Key] = kv.Value;
        }
    }
}
