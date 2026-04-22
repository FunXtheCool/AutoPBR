using System.Text.RegularExpressions;
using AutoPBR.Core.Models;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Matches texture titles/paths to material tag ids using MiniLM embeddings (cosine similarity to prototypes).
/// </summary>
public sealed class MaterialTagSemanticMatcher : IDisposable
{
    /// <summary>Multiplier on dictionary evidence from lookups of <c>door</c> / <c>trapdoor</c> (generic glosses dilute material).</summary>
    private const float DoorTrapdoorDictionaryPenalty = 0.35f;

    /// <summary>Boost for the term before <c>door</c> / <c>trapdoor</c> (e.g. wood/iron in the file name).</summary>
    private const float DoorPrecedingTermDictionaryBoost = 1.45f;

    /// <summary>Extra dictionary score for <c>wood</c> when the query includes door/trapdoor (Minecraft default).</summary>
    private const float WoodDictionaryBiasWhenDoorOrTrapdoor = 0.10f;

    private readonly MiniLmEmbeddingEngine _engine;
    private readonly Dictionary<string, float[]> _prototypeCache = new(StringComparer.OrdinalIgnoreCase);

    private MaterialTagSemanticMatcher(MiniLmEmbeddingEngine engine) => _engine = engine;

    public static MaterialTagSemanticMatcher? TryCreate(string? baseDirectory = null)
    {
        var engine = MiniLmEmbeddingEngine.TryCreate(baseDirectory);
        return engine is null ? null : new MaterialTagSemanticMatcher(engine);
    }

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
    private static List<string> BuildPrototypePhrases(TagRule rule, IReadOnlyList<TagRule> allRules)
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
    private static HashSet<string> CollectForeignMaterialTokens(TagRule current, IReadOnlyList<TagRule> allRules)
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

    private DictionaryEvidence BuildDictionaryEvidence(
        string queryText,
        List<TagRule> materialRules,
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

    private static float Dot(float[] a, float[] b)
    {
        var sum = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    public void Dispose() => _engine.Dispose();
}

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
