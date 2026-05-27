using System.Collections.Concurrent;

using AutoPBR.App.Models;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

using Avalonia.Threading;

namespace AutoPBR.App.Services;

internal sealed partial class ExploreTreeController
{
    /// <param name="archivePath">Archive entry path for the texture being evaluated.</param>
    /// <param name="includeDictionaryEvidence">Whether dictionary evidence should influence semantic material scoring.</param>
    /// <param name="deferSemanticMl">When true with semantic ML enabled, use keyword material tags only (fast); full ML runs later on a background thread.</param>
    private List<(string Id, string DisplayName, TagRuleKind Kind)> ComputeEffectiveTags(
        string archivePath,
        bool includeDictionaryEvidence,
        bool deferSemanticMl = false)
    {
        var storageKey = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return [];
        }

        var ruleKey = RuleRelativeKeyFromStorageKey(storageKey);
        var name = Path.GetFileNameWithoutExtension(archivePath);
        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        var isNumericOptifineTile = IsNumericOnlyOptifineTile(name, ruleKey);
        if (isNumericOptifineTile)
        {
            deferSemanticMl = true;
        }

        if (deferSemanticMl && !includeDictionaryEvidence)
        {
            deferSemanticMl = ShouldDeferSemanticMl(archivePath);
        }
        var added = _tagAdded.GetValueOrDefault(storageKey);
        var removed = _tagRemoved.GetValueOrDefault(storageKey);
        var effectiveIds = ConversionEffectiveTags.ComputeEffectiveTagIds(
            name,
            ruleKey,
            texturePath: null,
            rules,
            sem,
            includeDictionaryEvidence,
            deferSemanticMl,
            added,
            removed);
        var orderedEffectiveIds = new List<string>(effectiveIds);
        var effective = new HashSet<string>(effectiveIds, StringComparer.OrdinalIgnoreCase);
        if (isNumericOptifineTile && includeDictionaryEvidence)
        {
            var parentRuleKey = GetParentRuleRelativeKey(ruleKey);
            if (!string.IsNullOrEmpty(parentRuleKey))
            {
                var inheritedMaterialIds = GetOrComputeOptifineFolderMaterialHintIds(parentRuleKey, rules, sem);
                if (inheritedMaterialIds.Count > 0)
                {
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

                        if (effective.Add(inheritedId))
                        {
                            orderedEffectiveIds.Add(inheritedId);
                        }
                    }
                }
            }
        }

        var result = new List<(string Id, string DisplayName, TagRuleKind Kind)>();
        var rulesById = rules
            .GroupBy(static r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in orderedEffectiveIds)
        {
            if (!effective.Contains(id) || !seen.Add(id))
            {
                continue;
            }

            if (!rulesById.TryGetValue(id, out var rule))
            {
                continue;
            }

            result.Add((rule.Id, rule.DisplayName, rule.Kind));
        }

        _effectiveTagIdsByStorageKey[storageKey] = result.Select(static t => t.Id).ToList();
        return result;
    }

    private bool ShouldDeferSemanticMl(string archivePath)
    {
        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        if (sem is not { Enabled: true, Matcher: not null })
        {
            return false;
        }

        var storageKey = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return true;
        }

        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var ruleKey = RuleRelativeKeyFromStorageKey(storageKey);
        var name = Path.GetFileNameWithoutExtension(archivePath);
        if (IsNumericOnlyOptifineTile(name, ruleKey))
        {
            return true;
        }

        var heuristicMaterialIds = TagRulePresets.GetMatchingMaterialTagIds(name, ruleKey, rules);
        if (heuristicMaterialIds.Count > 0)
        {
            return false;
        }

        var materialRuleIds = new HashSet<string>(
            rules.Where(r => r.Kind == TagRuleKind.Material).Select(r => r.Id),
            StringComparer.OrdinalIgnoreCase);
        if (_tagAdded.TryGetValue(storageKey, out var addedSet))
        {
            lock (addedSet)
            {
                foreach (var id in addedSet)
                {
                    if (materialRuleIds.Contains(id))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool IsNumericOnlyOptifineTile(string textureName, string ruleRelativeKey)
    {
        if (!IsOptifineRuleRelativeKey(ruleRelativeKey))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(textureName))
        {
            return false;
        }

        foreach (var c in textureName)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOptifineRuleRelativeKey(string ruleRelativeKey) =>
        ruleRelativeKey.Contains("\\optifine\\", StringComparison.OrdinalIgnoreCase);

    private static string? GetParentRuleRelativeKey(string ruleRelativeKey)
    {
        var lastSlash = ruleRelativeKey.LastIndexOf('\\');
        if (lastSlash <= 0)
        {
            return null;
        }

        return ruleRelativeKey[..lastSlash];
    }

    private static string GetRuleRelativeLeafName(string ruleRelativeKey)
    {
        var lastSlash = ruleRelativeKey.LastIndexOf('\\');
        if (lastSlash < 0 || lastSlash == ruleRelativeKey.Length - 1)
        {
            return ruleRelativeKey;
        }

        return ruleRelativeKey[(lastSlash + 1)..];
    }

    private IReadOnlyList<string> GetOrComputeOptifineFolderMaterialHintIds(
        string folderRuleKey,
        IReadOnlyList<TagRule> rules,
        MaterialTagSemanticOptions? sem)
    {
        if (string.IsNullOrWhiteSpace(folderRuleKey) || !IsOptifineRuleRelativeKey(folderRuleKey))
        {
            return [];
        }

        return _optifineFolderMaterialHintIdsByRuleKey.GetOrAdd(folderRuleKey, key =>
        {
            var materialRuleIds = new HashSet<string>(
                rules.Where(r => r.Kind == TagRuleKind.Material).Select(r => r.Id),
                StringComparer.OrdinalIgnoreCase);
            if (materialRuleIds.Count == 0)
            {
                return [];
            }

            var folderName = GetRuleRelativeLeafName(key);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return [];
            }

            var heuristicMaterialIds = TagRulePresets.GetMatchingMaterialTagIds(folderName, key, rules);
            if (heuristicMaterialIds.Count > 0)
            {
                return heuristicMaterialIds
                    .Where(materialRuleIds.Contains)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (sem is not { Enabled: true, Matcher: not null })
            {
                return [];
            }

            var inferredMaterialIds = ConversionEffectiveTags.ComputeEffectiveTagIds(
                folderName,
                key,
                texturePath: null,
                rules,
                sem,
                includeDictionaryEvidence: sem.DictionaryEvidenceEnabled,
                deferSemanticMl: false,
                added: null,
                removed: null)
                .Where(materialRuleIds.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return inferredMaterialIds;
        });
    }

    void IArchiveNodeHost.ApplyManualTagToggle(string archivePath, string tagId, bool wantApplied)
    {
        var key = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (wantApplied)
        {
            RemoveTagIdForPath(_tagRemoved, key, tagId);
            if (!EffectiveTagsContainId(archivePath, tagId))
            {
                var addSet = _tagAdded.GetOrAdd(key, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                lock (addSet)
                {
                    addSet.Add(tagId);
                }
            }
        }
        else
        {
            RemoveTagIdForPath(_tagAdded, key, tagId);
            if (EffectiveTagsContainId(archivePath, tagId))
            {
                var remSet = _tagRemoved.GetOrAdd(key, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                lock (remSet)
                {
                    remSet.Add(tagId);
                }
            }
        }

        PersistTagOverrides();
        PersistEffectiveTagCache();
        _effectiveTagCache[archivePath] = ComputeEffectiveTags(archivePath, includeDictionaryEvidence: true);
        _finalSemanticTagPaths[archivePath] = 0;
        RefreshExploreTagFilterCacheEntry(archivePath);
        var node = FindNodeByFullPath(archivePath);
        node?.RefreshDisplayTags();
        ApplyExploreFilterIfNeeded();
    }

    private bool EffectiveTagsContainId(string archivePath, string tagId)
    {
        var tags = ComputeEffectiveTags(archivePath, includeDictionaryEvidence: true);
        return tags.Any(t => string.Equals(t.Id, tagId, StringComparison.OrdinalIgnoreCase));
    }

    private static void RemoveTagIdForPath(
        ConcurrentDictionary<string, HashSet<string>> dict,
        string key,
        string tagId)
    {
        if (!dict.TryGetValue(key, out var set))
        {
            return;
        }

        lock (set)
        {
            set.Remove(tagId);
        }

        if (set.Count == 0)
        {
            dict.TryRemove(key, out _);
        }
    }
}
