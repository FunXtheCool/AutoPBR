using System.Globalization;
using System.Text;

using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Services;

internal static class SemanticTagDebugReportBuilder
{
    public static string Build(SemanticTagDebugRenderModel model)
    {
        var sb = new StringBuilder();
        AppendInvariantLine(sb, $"File:  {model.FileName}");
        AppendInvariantLine(sb, $"Rule key: \"{model.RuleKey}\"");
        AppendInvariantLine(sb, $"Query: \"{model.Query}\"");
        sb.AppendLine();
        sb.AppendLine("── Resolution inputs ──");
        AppendInvariantLine(sb, $"Semantic enabled: {model.SemanticEnabled} | Dictionary evidence: {model.DictionaryEvidenceEnabled} (weight={model.DictionaryEvidenceWeight:0.00})");
        AppendInvariantLine(sb, $"Thresholds: minSimilarity={model.MinSimilarity:0.###}, certainty={model.CertaintyThreshold:0.###}, gapFromBest={model.AdditionalTagMaxGapFromBest:0.###}, maxTags={model.MaxTags}");
        AppendInvariantLine(sb, $"Numeric OptiFine tile: {model.IsNumericOptifineTile}");
        AppendInvariantLine(sb, $"Material selection path: {model.MaterialSelectionPath}");
        sb.AppendLine();
        sb.AppendLine("── Stage 1: Keyword material hits ──");
        sb.AppendLine(ListOrNone(model.HeuristicMaterialIds, model.RulesById));
        sb.AppendLine();
        sb.AppendLine("── Stage 2: Semantic candidate ranking (best/dict/fused) ──");
        if (model.SemanticReport is null)
        {
            sb.AppendLine("MiniLM ranking not used for final material selection on this file.");
        }
        else
        {
            var dictionaryTerms = model.SemanticReport.DictionaryTerms is { Count: > 0 }
                ? string.Join(", ", model.SemanticReport.DictionaryTerms)
                : "(none)";
            AppendInvariantLine(sb, $"Dictionary terms: {dictionaryTerms}");
            foreach (var entry in model.SemanticReport.Entries)
            {
                var dictScore = entry.DictionaryBestScore > float.MinValue
                    ? entry.DictionaryBestScore.ToString("F4", CultureInfo.InvariantCulture)
                    : "n/a";
                var fused = entry.FusedScore > float.MinValue
                    ? entry.FusedScore.ToString("F4", CultureInfo.InvariantCulture)
                    : "n/a";
                var displayName = entry.DisplayName;
                var bestPhrase = entry.BestPhrase;
                AppendInvariantLine(sb,
                    $"  {displayName,-16} best={entry.BestScore:F4} dict={dictScore} fused={fused} <- \"{bestPhrase}\"");
            }
        }

        sb.AppendLine();
        sb.AppendLine("── Stage 3: Material IDs before/after post-processing ──");
        AppendInvariantLine(sb, $"Before post-process: {ListOrNone(model.SemanticRawIds, model.RulesById)}");
        AppendInvariantLine(sb, $"After  post-process: {ListOrNone(model.PostProcessedMaterialIds, model.RulesById)}");
        AppendInvariantLine(sb, $"Post-process added:  {ListOrNone(model.PostAdded, model.RulesById)}");
        AppendInvariantLine(sb, $"Post-process removed:{ListOrNone(model.PostRemoved, model.RulesById)}");
        sb.AppendLine();
        sb.AppendLine("── Stage 4: Flag reasoning ──");
        AppendInvariantLine(sb, $"Path-derived flags:  {ListOrNone(model.FlagsBeforeWeighted, model.RulesById)}");
        AppendInvariantLine(sb, $"Weighted marker:     {ListOrNone(model.WeightedFlag is null ? [] : [model.WeightedFlag], model.RulesById)}");
        sb.AppendLine();
        sb.AppendLine("── Stage 5: Manual and inherited adjustments ──");
        AppendInvariantLine(sb, $"Manual added:        {ListOrNone(model.ManualAdded, model.RulesById)}");
        AppendInvariantLine(sb, $"Manual removed:      {ListOrNone(model.ManualRemoved, model.RulesById)}");
        AppendInvariantLine(sb, $"Inherited materials: {ListOrNone(model.InheritedAdded, model.RulesById)}");
        sb.AppendLine();
        sb.AppendLine("── Final effective tags ──");
        AppendInvariantLine(sb, $"Materials: {ListOrNone(model.FinalMaterials, model.RulesById)}");
        AppendInvariantLine(sb, $"Flags:     {ListOrNone(model.FinalFlags, model.RulesById)}");
        if (model.FinalMaterials.Count > model.MaxTags)
        {
            sb.AppendLine();
            AppendInvariantLine(sb,
                $"Note: final material count ({model.FinalMaterials.Count}) exceeds maxTags ({model.MaxTags}) because maxTags is applied during semantic selection, while post-processing/manual/inherited rules can intentionally add material tags afterward.");
        }

        return sb.ToString();
    }

    private static string ListOrNone(IEnumerable<string> ids, IReadOnlyDictionary<string, TagRule> allRules)
    {
        var list = ids.ToList();
        return list.Count == 0
            ? "(none)"
            : string.Join(", ", list.Select(id => IdWithName(id, allRules)));
    }

    private static string IdWithName(string id, IReadOnlyDictionary<string, TagRule> allRules)
    {
        if (allRules.TryGetValue(id, out var rule))
        {
            return $"{rule.DisplayName} ({rule.Id})";
        }

        return id;
    }

    private static void AppendInvariantLine(StringBuilder sb, FormattableString message) =>
        sb.AppendLine(FormattableString.Invariant(message));
}

internal sealed class SemanticTagDebugRenderModel
{
    public required string FileName { get; init; }
    public required string RuleKey { get; init; }
    public required string Query { get; init; }
    public required bool SemanticEnabled { get; init; }
    public required bool DictionaryEvidenceEnabled { get; init; }
    public required double DictionaryEvidenceWeight { get; init; }
    public required double MinSimilarity { get; init; }
    public required double CertaintyThreshold { get; init; }
    public required double AdditionalTagMaxGapFromBest { get; init; }
    public required int MaxTags { get; init; }
    public required bool IsNumericOptifineTile { get; init; }
    public required string MaterialSelectionPath { get; init; }
    public required IReadOnlyList<string> HeuristicMaterialIds { get; init; }
    public required SemanticMatchDebugReport? SemanticReport { get; init; }
    public required IReadOnlyList<string> SemanticRawIds { get; init; }
    public required IReadOnlyList<string> PostProcessedMaterialIds { get; init; }
    public required IReadOnlyList<string> PostAdded { get; init; }
    public required IReadOnlyList<string> PostRemoved { get; init; }
    public required IReadOnlyList<string> FlagsBeforeWeighted { get; init; }
    public required string? WeightedFlag { get; init; }
    public required IReadOnlyList<string> ManualAdded { get; init; }
    public required IReadOnlyList<string> ManualRemoved { get; init; }
    public required IReadOnlyList<string> InheritedAdded { get; init; }
    public required IReadOnlyList<string> FinalMaterials { get; init; }
    public required IReadOnlyList<string> FinalFlags { get; init; }
    public required IReadOnlyDictionary<string, TagRule> RulesById { get; init; }
}
