using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// Heuristic-first material tag resolution for Explore, and assignment of the <c>weighted</c> / <c>unweighted</c> flag tags.
/// </summary>
/// <remarks>
/// <para>
/// <b>Weighted vs Unweighted (flag tags).</b> Built-in flags <see cref="FlagTagResolver.WeightedId"/> and
/// <see cref="FlagTagResolver.UnweightedId"/> summarize whether MiniLM similarity ran for <b>material</b> tags on a texture.
/// They are informational only (like other Explore tags) and do not affect normals, height, or specular output.
/// Preset rules use empty keywords; the app assigns exactly one flag in code (<see cref="AppendWeightedUnweightedFlags"/>).
/// </para>
/// <para>
/// <b>Purpose.</b> When a title/path already matches material rules via keyword substring heuristics, the full
/// <c>MaterialTagSemanticMatcher.Match</c> pipeline is skipped to save work. When nothing matches, embeddings run.
/// </para>
/// <para>
/// <b><see cref="ResolveMaterialTags"/> order.</b>
/// (1) If semantic ML is unavailable (disabled, no matcher) or <c>deferSemanticMl</c> is true: material ids come from
/// <see cref="TagRulePresets.GetMatchingMaterialTagIds"/> plus <see cref="MaterialTagMlPostProcessor.Apply"/>;
/// <c>usedSemanticMl</c> is false.
/// (2) If semantic ML is on and not deferred: run material keyword heuristics via
/// <c>TagRuleApplicator.GetMatchingTagIds</c> for <see cref="TagRuleKind.Material"/>.
/// If any rule matches, use those ids (post-processor applied); <c>usedSemanticMl</c> is false and <c>Match</c> is skipped.
/// If none match, run <c>Match</c> (optional dictionary evidence); <c>usedSemanticMl</c> is true.
/// The post-processor may still adjust tags (e.g. low-confidence <c>unknown</c>); flags reflect whether MiniLM similarity ran, not every post-processing branch.
/// </para>
/// <para>
/// <b><see cref="AppendWeightedUnweightedFlags"/>.</b> Strips any existing weighted/unweighted entries, then appends exactly one:
/// <see cref="FlagTagResolver.UnweightedId"/> when semantic ML is off or there is no matcher;
/// <see cref="FlagTagResolver.UnweightedId"/> when <c>deferSemanticMl</c> is true (first-pass preview);
/// <see cref="FlagTagResolver.WeightedId"/> when semantic ML is on, not deferred, and <c>usedSemanticMl</c> is true;
/// otherwise <see cref="FlagTagResolver.UnweightedId"/> (heuristic-only material resolution).
/// Callers merge with path-derived flags and manual add/remove (e.g. Explore <c>ComputeEffectiveTags</c>).
/// </para>
/// <para>
/// <b>Other callers.</b> <c>TextureScanner</c> uses <see cref="ResolveMaterialTags"/> for material ids but does not append weighted/unweighted flags.
/// </para>
/// <para>
/// Long-form documentation: repository file <c>docs/semantic-material-weighted-unweighted.md</c>.
/// </para>
/// </remarks>
public static class MaterialTagSemanticResolution
{
    private static readonly HashSet<string> VanillaBedColorTextureNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
        "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black"
    };

    /// <summary>
    /// Resolves material tag ids: keyword rules first when ML is on and not deferred; otherwise keyword-only or full ML.
    /// </summary>
    /// <remarks>
    /// Sets <paramref name="usedSemanticMl"/> to true only when semantic ML is enabled, not deferred, keyword heuristics matched no material rule,
    /// and <c>MaterialTagSemanticMatcher.Match</c> runs. See class <see cref="MaterialTagSemanticResolution"/> remarks for weighted/unweighted.
    /// </remarks>
    public static List<string> ResolveMaterialTags(
        string textureName,
        string ruleRelativeKey,
        IReadOnlyList<TagRule> allRules,
        MaterialTagSemanticOptions? sem,
        bool deferSemanticMl,
        bool includeDictionaryEvidence,
        out bool usedSemanticMl)
    {
        usedSemanticMl = false;
        var materialRules = allRules.Where(r => r.Kind == TagRuleKind.Material).ToList();
        var materialDescriptors = materialRules.ToDescriptors();

        if (IsVanillaEntityBedColorTexture(textureName, ruleRelativeKey))
        {
            // Explicit unweighted override: vanilla entity bed color variants should be organic.
            return MaterialTagMlPostProcessor.Apply(
                textureName,
                ruleRelativeKey,
                ["organic"],
                materialDescriptors,
                sem is { Enabled: true, Matcher: not null } ? sem.MaxTags : null);
        }

        if (IsVanillaPlayerEntityTexture(ruleRelativeKey))
        {
            // Explicit unweighted override: player skins (wide/slim) should be organic; skip name-based dictionary ML.
            return MaterialTagMlPostProcessor.Apply(
                textureName,
                ruleRelativeKey,
                ["organic"],
                materialDescriptors,
                sem is { Enabled: true, Matcher: not null } ? sem.MaxTags : null);
        }

        if (sem is not { Enabled: true, Matcher: { } matcher } || deferSemanticMl)
        {
            var ids = TagRulePresets.GetMatchingMaterialTagIds(textureName, ruleRelativeKey, allRules).ToList();
            return MaterialTagMlPostProcessor.Apply(
                textureName,
                ruleRelativeKey,
                ids,
                materialDescriptors,
                sem is { Enabled: true, Matcher: not null } ? sem.MaxTags : null);
        }

        var heuristicIds = TagRuleApplicator.GetMatchingTagIds(textureName, ruleRelativeKey, allRules, TagRuleKind.Material);
        if (heuristicIds.Count > 0)
        {
            return MaterialTagMlPostProcessor.Apply(
                textureName,
                ruleRelativeKey,
                heuristicIds,
                materialDescriptors,
                sem.MaxTags);
        }

        usedSemanticMl = true;
        var mlIds = matcher.Match(
                textureName,
                ruleRelativeKey,
                materialDescriptors,
                sem.MinSimilarity,
                sem.MaxTags,
                sem.CertaintyThreshold,
                sem.AdditionalTagMaxGapFromBest,
                includeDictionaryEvidence && sem.DictionaryEvidenceEnabled,
                sem.DictionaryProvider,
                sem.DictionaryEvidenceWeight,
                sem.DictionaryMinEvidenceScore,
                sem.DictionaryRequestTimeoutMs,
                sem.DictionaryLanguageCode)
            .ToList();
        return MaterialTagMlPostProcessor.Apply(
            textureName,
            ruleRelativeKey,
            mlIds,
            materialDescriptors,
            sem.MaxTags);
    }

    /// <summary>
    /// Appends exactly one of Weighted/Unweighted: ML path vs keyword/heuristic-only (or semantic off / deferred preview).
    /// </summary>
    /// <remarks>
    /// Uses <see cref="FlagTagResolver.WeightedId"/> / <see cref="FlagTagResolver.UnweightedId"/>.
    /// See class <see cref="MaterialTagSemanticResolution"/> remarks for the full weighted/unweighted table and behavior.
    /// </remarks>
    public static void AppendWeightedUnweightedFlags(
        IList<string> flagIds,
        MaterialTagSemanticOptions? sem,
        bool deferSemanticMl,
        bool usedSemanticMl)
    {
        RemoveWeightedUnweighted(flagIds);
        if (sem is not { Enabled: true, Matcher: not null })
        {
            flagIds.Add(FlagTagResolver.UnweightedId);
            return;
        }

        if (deferSemanticMl)
        {
            flagIds.Add(FlagTagResolver.UnweightedId);
            return;
        }

        flagIds.Add(usedSemanticMl ? FlagTagResolver.WeightedId : FlagTagResolver.UnweightedId);
    }

    /// <summary>
    /// Adds <see cref="FlagTagResolver.Sprite2DId"/> for item textures (unless exempt) and for organic materials without the Block flag.
    /// </summary>
    /// <param name="effectiveTagIds">Effective tag ids after auto resolution and manual add/remove.</param>
    /// <param name="removedTagIds">When non-null and containing <see cref="FlagTagResolver.Sprite2DId"/>, the user hid this flag — do not re-add.</param>
    public static void AppendTwoDSpriteFlagIfNeeded(
        IList<string> effectiveTagIds,
        IReadOnlyCollection<string>? removedTagIds)
    {
        if (removedTagIds is not null &&
            removedTagIds.Contains(FlagTagResolver.Sprite2DId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (effectiveTagIds.Contains(FlagTagResolver.Sprite2DId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (effectiveTagIds.Contains(FlagTagResolver.ItemId, StringComparer.OrdinalIgnoreCase) &&
            !ItemFlatSpriteTagPolicy.IsItemFlatSpriteExempt(effectiveTagIds))
        {
            effectiveTagIds.Add(FlagTagResolver.Sprite2DId);
            return;
        }

        if (!ContainsOrganicMaterialTag(effectiveTagIds))
        {
            return;
        }

        if (effectiveTagIds.Contains(FlagTagResolver.BlockId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        // Entity UV textures are model-mapped surfaces; do not classify as flat 2D sprites.
        if (effectiveTagIds.Contains(FlagTagResolver.EntityId, StringComparer.OrdinalIgnoreCase) &&
            effectiveTagIds.Contains(FlagTagResolver.UvWrapId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        effectiveTagIds.Add(FlagTagResolver.Sprite2DId);
    }

    private static void RemoveWeightedUnweighted(IList<string> flagIds)
    {
        for (var i = flagIds.Count - 1; i >= 0; i--)
        {
            var id = flagIds[i];
            if (id.Equals(FlagTagResolver.WeightedId, StringComparison.OrdinalIgnoreCase) ||
                id.Equals(FlagTagResolver.UnweightedId, StringComparison.OrdinalIgnoreCase))
            {
                flagIds.RemoveAt(i);
            }
        }
    }

    private static bool ContainsOrganicMaterialTag(IEnumerable<string> effectiveTagIds)
    {
        foreach (var id in effectiveTagIds)
        {
            if (id.Equals("organic", StringComparison.OrdinalIgnoreCase) ||
                id.Equals("plant", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVanillaPlayerEntityTexture(string ruleRelativeKey)
    {
        if (string.IsNullOrWhiteSpace(ruleRelativeKey))
        {
            return false;
        }

        if (!ruleRelativeKey.StartsWith("\\minecraft\\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ruleRelativeKey.Contains("\\entity\\player\\wide\\", StringComparison.OrdinalIgnoreCase) ||
               ruleRelativeKey.Contains("\\entity\\player\\slim\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVanillaEntityBedColorTexture(string textureName, string ruleRelativeKey)
    {
        if (string.IsNullOrWhiteSpace(textureName) || string.IsNullOrWhiteSpace(ruleRelativeKey))
        {
            return false;
        }

        if (!ruleRelativeKey.StartsWith("\\minecraft\\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!ruleRelativeKey.Contains("\\entity\\bed\\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return VanillaBedColorTextureNames.Contains(textureName.Trim());
    }
}
