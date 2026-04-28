using AutoPBR.Core.Models;
using AutoPBR.Core.Atlas;

namespace AutoPBR.Core;

/// <summary>
/// Path-derived flags (block/item/entity/armor/UV wrap) plus optional keyword-based custom flag rules.
/// </summary>
public static class FlagTagResolver
{
    public readonly record struct ResolveContext(bool? ExplicitUvWrap, int? TextureWidth, int? TextureHeight);

    public const string BlockId = "block";
    public const string ItemId = "item";
    public const string EntityId = "entity";
    public const string ArmorId = "armor";
    public const string UvWrapId = "uv_wrap";
    public const string NoUvWrapId = "no_uv_wrap";

    /// <summary>Keyword-matched: file name or path contains <c>ore</c> as a whole token (e.g. iron_ore, deepslate_gold_ore; not &quot;forests&quot;).</summary>
    public const string OreId = "ore";

    /// <summary>MiniLM/dictionary material resolution ran (no keyword heuristic hit).</summary>
    public const string WeightedId = "weighted";

    /// <summary>Keyword/heuristic materials only; ML embedding skipped for speed.</summary>
    public const string UnweightedId = "unweighted";

    /// <summary>Organic material without <see cref="BlockId"/> — flat/item-style sprite (assigned in Explore).</summary>
    public const string Sprite2DId = "sprite_2d";

    /// <summary>
    /// Resolves flag tag ids from resource path and optional <paramref name="flagRules"/> with keywords.
    /// </summary>
    public static IReadOnlyList<string> Resolve(
        string textureName,
        string relativeKey,
        IReadOnlyList<TagRule> flagRules,
        ResolveContext context = default)
    {
        var path = TagRuleApplicator.PathBelowNamespace(relativeKey);
        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var combined = textureName + "\0" + path;

        // Keys look like "namespace\textures\block\..." — first segment below namespace is usually "textures".
        var i = 0;
        if (i < segments.Length && segments[i].Equals("textures", StringComparison.OrdinalIgnoreCase))
        {
            i++;
        }

        var textureKindFolder = i < segments.Length ? segments[i] : "";
        var ids = new List<string>();

        if (textureKindFolder.Equals("block", StringComparison.OrdinalIgnoreCase) ||
            textureKindFolder.Equals("blocks", StringComparison.OrdinalIgnoreCase))
        {
            ids.Add(BlockId);
        }

        if (textureKindFolder.Equals("item", StringComparison.OrdinalIgnoreCase) ||
            textureKindFolder.Equals("items", StringComparison.OrdinalIgnoreCase))
        {
            ids.Add(ItemId);
        }

        var isEntityFolder = textureKindFolder.Equals("entity", StringComparison.OrdinalIgnoreCase) ||
                             textureKindFolder.Equals("entities", StringComparison.OrdinalIgnoreCase);

        if (isEntityFolder)
        {
            ids.Add(EntityId);
        }

        var isArmorPath = segments.Any(s => s.Equals("armor", StringComparison.OrdinalIgnoreCase)) ||
                          normalized.Contains("/armor/", StringComparison.OrdinalIgnoreCase);

        if (isArmorPath)
        {
            ids.Add(ArmorId);
        }

        var keywordHits = TagRuleApplicator.GetMatchingTagIds(textureName, relativeKey, flagRules, TagRuleKind.Flag);
        foreach (var id in keywordHits)
        {
            ids.Add(id);
        }

        // UV wrap hybrid resolution:
        // 1) explicit override from caller/rules
        // 2) legacy path/folder heuristics
        // 3) conservative geometry evidence from atlas inference
        var explicitUvDecision = context.ExplicitUvWrap;
        if (!explicitUvDecision.HasValue)
        {
            if (keywordHits.Contains(NoUvWrapId, StringComparer.OrdinalIgnoreCase))
            {
                explicitUvDecision = false;
            }
            else if (keywordHits.Contains(UvWrapId, StringComparer.OrdinalIgnoreCase))
            {
                explicitUvDecision = true;
            }
        }

        var hasLegacyUvEvidence =
            combined.Contains("ctm", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("connect", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("optifine", StringComparison.OrdinalIgnoreCase) ||
            isEntityFolder ||
            isArmorPath;

        var hasAtlasGeometryEvidence =
            context.TextureWidth is > 0 &&
            context.TextureHeight is > 0 &&
            context.TextureWidth.Value != context.TextureHeight.Value &&
            AtlasTiling.TryInferPlan(context.TextureWidth.Value, context.TextureHeight.Value).IsAtlas;

        var shouldApplyUv = explicitUvDecision ?? (hasLegacyUvEvidence || hasAtlasGeometryEvidence);
        if (shouldApplyUv)
        {
            ids.Add(UvWrapId);
        }
        else
        {
            ids.RemoveAll(id => id.Equals(UvWrapId, StringComparison.OrdinalIgnoreCase));
        }

        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
