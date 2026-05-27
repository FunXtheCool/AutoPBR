using System.Collections.Concurrent;

using AutoPBR.App.Models;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

using Avalonia.Threading;

namespace AutoPBR.App.Services;

internal sealed partial class ExploreTreeController
{
    public SemanticMatchDebugReport? GetSemanticMatchDebugReport(string archivePath)
    {
        var storageKey = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return null;
        }

        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        if (sem is not { Enabled: true, Matcher: { } matcher })
        {
            return null;
        }

        var ruleKey = RuleRelativeKeyFromStorageKey(storageKey);
        var name = Path.GetFileNameWithoutExtension(archivePath);
        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var materialRules = rules.Where(r => r.Kind == TagRuleKind.Material).ToList();
        return matcher.MatchDebug(
            name,
            ruleKey,
            materialRules,
            sem.DictionaryEvidenceEnabled,
            sem.DictionaryProvider,
            sem.DictionaryEvidenceWeight,
            sem.DictionaryMinEvidenceScore,
            sem.DictionaryRequestTimeoutMs,
            sem.DictionaryLanguageCode);
    }

    public string? GetSemanticMatchDebugText(string archivePath)
    {
        var storageKey = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return null;
        }

        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        if (sem is not { Enabled: true, Matcher: { } matcher })
        {
            return null;
        }

        var ruleKey = RuleRelativeKeyFromStorageKey(storageKey);
        var name = Path.GetFileNameWithoutExtension(archivePath);
        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var materialRules = rules.Where(r => r.Kind == TagRuleKind.Material).ToList();
        var flagRules = rules.Where(r => r.Kind == TagRuleKind.Flag).ToList();
        var rulesById = rules
            .GroupBy(static r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.OrdinalIgnoreCase);

        var isNumericOptifineTile = IsNumericOnlyOptifineTile(name, ruleKey);
        var includeDictionaryEvidence = sem.DictionaryEvidenceEnabled;
        var deferSemanticMl = isNumericOptifineTile;
        var query = MaterialTagSemanticQuery.Build(name, ruleKey);
        var heuristicMaterialIds = TagRulePresets.GetMatchingMaterialTagIds(name, ruleKey, rules).ToList();
        var usedSemanticMl = false;
        List<string> semanticRawIds;
        string materialSelectionPath;
        SemanticMatchDebugReport? semanticReport = null;

        if (deferSemanticMl)
        {
            semanticRawIds = TagRulePresets.GetMatchingMaterialTagIds(name, ruleKey, rules).ToList();
            materialSelectionPath = "keyword-only (numeric OptiFine tile deferred ML)";
        }
        else if (heuristicMaterialIds.Count > 0)
        {
            semanticRawIds = heuristicMaterialIds;
            materialSelectionPath = "keyword-only (material keyword hit)";
        }
        else
        {
            semanticRawIds = matcher.Match(
                name,
                ruleKey,
                materialRules,
                sem.MinSimilarity,
                sem.MaxTags,
                sem.CertaintyThreshold,
                sem.AdditionalTagMaxGapFromBest,
                includeDictionaryEvidence && sem.DictionaryEvidenceEnabled,
                sem.DictionaryProvider,
                sem.DictionaryEvidenceWeight,
                sem.DictionaryMinEvidenceScore,
                sem.DictionaryRequestTimeoutMs,
                sem.DictionaryLanguageCode).ToList();
            usedSemanticMl = true;
            materialSelectionPath = "MiniLM semantic (fused score ranking)";
            semanticReport = GetSemanticMatchDebugReport(archivePath);
        }

        var postProcessedMaterialIds = MaterialTagMlPostProcessor.Apply(
            name,
            ruleKey,
            semanticRawIds,
            materialRules,
            sem.MaxTags);
        var postAdded = postProcessedMaterialIds.Except(semanticRawIds, StringComparer.OrdinalIgnoreCase).ToList();
        var postRemoved = semanticRawIds.Except(postProcessedMaterialIds, StringComparer.OrdinalIgnoreCase).ToList();

        var autoFlagIds = FlagTagResolver.Resolve(name, ruleKey, flagRules).ToList();
        var flagsBeforeWeighted = autoFlagIds.ToList();
        MaterialTagSemanticResolution.AppendWeightedUnweightedFlags(autoFlagIds, sem, deferSemanticMl, usedSemanticMl);
        var weightedFlag = autoFlagIds.FirstOrDefault(id =>
            id.Equals(FlagTagResolver.WeightedId, StringComparison.OrdinalIgnoreCase) ||
            id.Equals(FlagTagResolver.UnweightedId, StringComparison.OrdinalIgnoreCase));

        var autoIds = postProcessedMaterialIds.Concat(autoFlagIds).ToList();
        var added = _tagAdded.GetValueOrDefault(storageKey);
        var removed = _tagRemoved.GetValueOrDefault(storageKey);
        var removedArr = removed is null ? [] : removed.ToList();
        var addedArr = added is null ? [] : added.ToList();
        var effectiveIds = autoIds
            .Except(removedArr, StringComparer.OrdinalIgnoreCase)
            .Union(addedArr, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if ((effectiveIds.Contains("organic", StringComparer.OrdinalIgnoreCase) ||
             effectiveIds.Contains("plant", StringComparer.OrdinalIgnoreCase)) &&
            effectiveIds.Contains(FlagTagResolver.BlockId, StringComparer.OrdinalIgnoreCase) &&
            !name.Contains("block", StringComparison.OrdinalIgnoreCase))
        {
            effectiveIds = effectiveIds
                .Where(id => !id.Equals(FlagTagResolver.BlockId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        MaterialTagSemanticResolution.AppendTwoDSpriteFlagIfNeeded(effectiveIds, removed);

        var inheritedAdded = new List<string>();
        if (isNumericOptifineTile && includeDictionaryEvidence)
        {
            var parentRuleKey = GetParentRuleRelativeKey(ruleKey);
            if (!string.IsNullOrEmpty(parentRuleKey))
            {
                var inheritedMaterialIds = GetOrComputeOptifineFolderMaterialHintIds(parentRuleKey, rules, sem);
                HashSet<string>? removedSet = null;
                if (removed is { Count: > 0 })
                {
                    removedSet = new HashSet<string>(removed, StringComparer.OrdinalIgnoreCase);
                }

                foreach (var inheritedId in inheritedMaterialIds)
                {
                    if (removedSet is not null && removedSet.Contains(inheritedId))
                    {
                        continue;
                    }

                    if (!effectiveIds.Contains(inheritedId, StringComparer.OrdinalIgnoreCase))
                    {
                        effectiveIds.Add(inheritedId);
                        inheritedAdded.Add(inheritedId);
                    }
                }
            }
        }

        var finalMaterials = effectiveIds
            .Where(id => rulesById.TryGetValue(id, out var rule) && rule.Kind == TagRuleKind.Material)
            .ToList();
        var finalFlags = effectiveIds
            .Where(id => rulesById.TryGetValue(id, out var rule) && rule.Kind == TagRuleKind.Flag)
            .ToList();

        return SemanticTagDebugReportBuilder.Build(new SemanticTagDebugRenderModel
        {
            FileName = Path.GetFileName(archivePath),
            RuleKey = ruleKey,
            Query = query,
            SemanticEnabled = sem.Enabled,
            DictionaryEvidenceEnabled = sem.DictionaryEvidenceEnabled,
            DictionaryEvidenceWeight = sem.DictionaryEvidenceWeight,
            MinSimilarity = sem.MinSimilarity,
            CertaintyThreshold = sem.CertaintyThreshold,
            AdditionalTagMaxGapFromBest = sem.AdditionalTagMaxGapFromBest,
            MaxTags = sem.MaxTags,
            IsNumericOptifineTile = isNumericOptifineTile,
            MaterialSelectionPath = materialSelectionPath,
            HeuristicMaterialIds = heuristicMaterialIds,
            SemanticReport = semanticReport,
            SemanticRawIds = semanticRawIds,
            PostProcessedMaterialIds = postProcessedMaterialIds,
            PostAdded = postAdded,
            PostRemoved = postRemoved,
            FlagsBeforeWeighted = flagsBeforeWeighted,
            WeightedFlag = weightedFlag,
            ManualAdded = addedArr,
            ManualRemoved = removedArr,
            InheritedAdded = inheritedAdded,
            FinalMaterials = finalMaterials,
            FinalFlags = finalFlags,
            RulesById = rulesById
        });
    }
}
