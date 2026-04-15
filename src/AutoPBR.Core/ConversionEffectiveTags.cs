using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// Matches Explore tag resolution so conversion/preview use the same effective ids as the Resource Explorer
/// (materials, path flags, weighted/unweighted, plant/block heuristic, 2D Sprite).
/// </summary>
public static class ConversionEffectiveTags
{
    /// <summary>
    /// Effective tag ids for a texture (materials + flags), including auto <c>sprite_2d</c> when applicable.
    /// </summary>
    public static List<string> ComputeEffectiveTagIds(
        string textureName,
        string ruleRelativeKey,
        IReadOnlyList<TagRule> rules,
        MaterialTagSemanticOptions? sem,
        bool includeDictionaryEvidence,
        bool deferSemanticMl,
        IReadOnlyCollection<string>? added,
        IReadOnlyCollection<string>? removed)
    {
        var flagRules = rules.Where(r => r.Kind == TagRuleKind.Flag).ToList();
        var autoFlagIds = FlagTagResolver.Resolve(textureName, ruleRelativeKey, flagRules).ToList();
        var autoMaterialIds = MaterialTagSemanticResolution.ResolveMaterialTags(
            textureName,
            ruleRelativeKey,
            rules,
            sem,
            deferSemanticMl,
            includeDictionaryEvidence,
            out var usedSemanticMl);
        MaterialTagSemanticResolution.AppendWeightedUnweightedFlags(autoFlagIds, sem, deferSemanticMl, usedSemanticMl);

        var autoIds = autoMaterialIds.Concat(autoFlagIds).ToList();
        var removedArr = removed ?? [];
        var addedArr = added ?? [];
        var effectiveIds = autoIds
            .Except(removedArr, StringComparer.OrdinalIgnoreCase)
            .Union(addedArr, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (effectiveIds.Contains("plant", StringComparer.OrdinalIgnoreCase) &&
            effectiveIds.Contains(FlagTagResolver.BlockId, StringComparer.OrdinalIgnoreCase) &&
            !textureName.Contains("block", StringComparison.OrdinalIgnoreCase))
        {
            effectiveIds = effectiveIds
                .Where(id => !id.Equals(FlagTagResolver.BlockId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        MaterialTagSemanticResolution.AppendTwoDSpriteFlagIfNeeded(effectiveIds, removed);
        return effectiveIds;
    }
}
