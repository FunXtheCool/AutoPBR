using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;
using AutoPBR.Core.Models.RuleExpressions;
using AutoPBR.Core.RuleAssembly;
using SixLabors.ImageSharp;

namespace AutoPBR.Core;

/// <summary>
/// Matches Explore tag resolution so conversion/preview use the same effective ids as the Resource Explorer
/// (materials, path flags, weighted/unweighted, organic/block heuristic, 2D Sprite).
/// </summary>
public static class ConversionEffectiveTags
{
    public sealed class EffectiveTagResolution
    {
        public required List<string> EffectiveIds { get; init; }
        public required IReadOnlyDictionary<string, bool> OverrideDecisions { get; init; }
    }

    /// <summary>
    /// Effective tag ids for a texture (materials + flags), including auto <c>sprite_2d</c> when applicable.
    /// </summary>
    public static List<string> ComputeEffectiveTagIds(
        string textureName,
        string ruleRelativeKey,
        string? texturePath,
        IReadOnlyList<TagRule> rules,
        MaterialTagSemanticOptions? sem,
        bool includeDictionaryEvidence,
        bool deferSemanticMl,
        IReadOnlyCollection<string>? added,
        IReadOnlyCollection<string>? removed)
        => ComputeResolution(
            textureName,
            ruleRelativeKey,
            texturePath,
            rules,
            sem,
            includeDictionaryEvidence,
            deferSemanticMl,
            added,
            removed).EffectiveIds;

    public static EffectiveTagResolution ComputeResolution(
        string textureName,
        string ruleRelativeKey,
        string? texturePath,
        IReadOnlyList<TagRule> rules,
        MaterialTagSemanticOptions? sem,
        bool includeDictionaryEvidence,
        bool deferSemanticMl,
        IReadOnlyCollection<string>? added,
        IReadOnlyCollection<string>? removed)
    {
        var flagRules = rules.Where(r => r.Kind == TagRuleKind.Flag).ToList();
        var (width, height) = TryGetImageDimensions(texturePath);
        var autoFlagIds = FlagTagResolver.Resolve(
                textureName,
                ruleRelativeKey,
                flagRules,
                new FlagTagResolver.ResolveContext(
                    ExplicitUvWrap: null,
                    TextureWidth: width,
                    TextureHeight: height))
            .ToList();
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

        var expressionRules = new List<RuleExpressionDefinition>(TagRulePresets.BuiltInExpressions);
        expressionRules.AddRange(
            rules.Select(TagRuleExpressionAccessor.GetExpression).Where(expression => expression is not null)!);

        var effectiveSet = new HashSet<string>(effectiveIds, StringComparer.OrdinalIgnoreCase);
        var materialSet = new HashSet<string>(
            effectiveSet.Where(id => rules.Any(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && r.Kind == TagRuleKind.Material)),
            StringComparer.OrdinalIgnoreCase);
        var flagSet = new HashSet<string>(
            effectiveSet.Where(id => rules.Any(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && r.Kind == TagRuleKind.Flag)),
            StringComparer.OrdinalIgnoreCase);
        var context = new RuleEvaluationContext
        {
            TextureName = textureName,
            RuleRelativeKey = ruleRelativeKey,
            EffectiveIds = effectiveSet,
            MaterialIds = materialSet,
            FlagIds = flagSet,
            IsWeighted = effectiveSet.Contains(FlagTagResolver.WeightedId)
        };
        var overrideDecisions = RuleExpressionEvaluator.Evaluate(context, expressionRules);
        effectiveIds = effectiveSet.ToList();

        MaterialTagSemanticResolution.AppendTwoDSpriteFlagIfNeeded(effectiveIds, removed);
        return new EffectiveTagResolution
        {
            EffectiveIds = effectiveIds,
            OverrideDecisions = new Dictionary<string, bool>(overrideDecisions, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static (int? Width, int? Height) TryGetImageDimensions(string? texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath) || !File.Exists(texturePath))
        {
            return (null, null);
        }

        try
        {
            var info = Image.Identify(texturePath);
            return (info.Width, info.Height);
        }
        catch
        {
            return (null, null);
        }
    }
}
