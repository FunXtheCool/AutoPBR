using System.Collections.Concurrent;
using AutoPBR.Core.Models;
using SixLabors.ImageSharp;

namespace AutoPBR.Core;

/// <summary>
/// Scans extracted resource packs to build TextureWorkItem lists for conversion.
/// </summary>
internal static partial class TextureScanner
{
    private static TagComputationResult BuildTagComputationResultFromEffectiveIds(
        ScanCandidate candidate,
        IReadOnlyCollection<string> effectiveIds,
        IReadOnlyDictionary<string, bool>? overrideDecisions = null)
    {
        var effective = new HashSet<string>(effectiveIds, StringComparer.OrdinalIgnoreCase);
        var invertSpecular = effective.Contains("brick");
        var invertHeight = OreCoalTextureRules.ShouldInvertHeight(candidate.Name, candidate.RelativePathNoExt);
        if (overrideDecisions is not null)
        {
            if (overrideDecisions.TryGetValue("invert_specular", out var invSpec))
            {
                invertSpecular = invSpec;
            }

            if (overrideDecisions.TryGetValue("invert_height", out var invHeight))
            {
                invertHeight = invHeight;
            }
        }

        return new TagComputationResult(
            effective.Contains(FlagTagResolver.Sprite2DId),
            effective.Contains("organic") ||
            effective.Contains("plant") ||
            AutoPBRDefaults.PlantTextureKeys.Contains(candidate.RelativePathNoExt),
            effective.Contains("brick"),
            effective.Contains(FlagTagResolver.UvWrapId),
            invertSpecular,
            invertHeight,
            effective.ToList());
    }

    private static TagComputationResult ComputeTagsForCandidate(
        ScanCandidate candidate,
        AutoPBROptions options,
        IReadOnlyList<TagRule> rules,
        bool deferSemanticMl,
        IReadOnlyCollection<string>? inheritedMaterialTagIds = null)
    {
        IReadOnlyCollection<string>? added = null;
        IReadOnlyCollection<string>? removed = null;
        if (options.ManualTagOverrides?.TryGetValue(candidate.RelativePathNoExt, out var o) == true)
        {
            added = o.Added;
            removed = o.Removed;
        }

        var sem = options.SemanticOptions;
        var includeDict = sem?.DictionaryEvidenceEnabled ?? false;
        var resolution = ConversionEffectiveTags.ComputeResolution(
            candidate.Name,
            candidate.RelativePathNoExt,
            candidate.File,
            rules,
            sem,
            includeDict,
            deferSemanticMl,
            added,
            removed);
        var effective = new HashSet<string>(resolution.EffectiveIds, StringComparer.OrdinalIgnoreCase);
        if (inheritedMaterialTagIds is { Count: > 0 })
        {
            HashSet<string>? removedSet = null;
            if (removed is { Count: > 0 })
            {
                removedSet = new HashSet<string>(removed, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var inheritedId in inheritedMaterialTagIds)
            {
                if (removedSet is not null && removedSet.Contains(inheritedId))
                {
                    continue;
                }

                effective.Add(inheritedId);
            }
        }

        return BuildTagComputationResultFromEffectiveIds(candidate, effective, resolution.OverrideDecisions);
    }

    private static Dictionary<string, IReadOnlyCollection<string>> BuildNumericOptifineFolderMaterialHints(
        IReadOnlyList<ScanCandidate> candidates,
        AutoPBROptions options,
        IReadOnlyList<TagRule> rules,
        CancellationToken cancellationToken)
    {
        var hints = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
        var sem = options.SemanticOptions;
        if (sem is not { Enabled: true, Matcher: not null })
        {
            return hints;
        }

        var includeDict = sem.DictionaryEvidenceEnabled;
        var materialRuleIds = new HashSet<string>(
            rules.Where(r => r.Kind == TagRuleKind.Material).Select(r => r.Id),
            StringComparer.OrdinalIgnoreCase);
        if (materialRuleIds.Count == 0)
        {
            return hints;
        }

        var parentFolders = candidates
            .Where(static c => IsNumericOnlyOptifineTile(c.Name, c.RelativePathNoExt))
            .Select(static c => GetParentRelativePathNoExt(c.RelativePathNoExt))
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => p!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var folderKey in parentFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderName = GetLeafSegment(folderKey);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                continue;
            }

            var heuristicMaterialIds = TagRuleApplicator.GetMatchingTagIds(
                folderName,
                folderKey,
                rules,
                TagRuleKind.Material);
            if (heuristicMaterialIds.Count > 0)
            {
                hints[folderKey] = heuristicMaterialIds
                    .Where(materialRuleIds.Contains)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                continue;
            }

            var inferredIds = ConversionEffectiveTags.ComputeEffectiveTagIds(
                folderName,
                folderKey,
                texturePath: null,
                rules,
                sem,
                includeDict,
                deferSemanticMl: false,
                added: null,
                removed: null);
            var inferredMaterialIds = inferredIds
                .Where(materialRuleIds.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (inferredMaterialIds.Length > 0)
            {
                hints[folderKey] = inferredMaterialIds;
            }
        }

        return hints;
    }
}
