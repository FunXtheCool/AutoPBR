using AutoPBR.Core.Models;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Deterministic cleanup after MiniLM material tags. Resolves wood+plant confusion: timber paths drop
/// plant; leaf/leaves paths drop wood. The organic (id plant) material tag is intended for plants/coral/grass, not lumber.
/// Paths whose name or relative key matches the Ore flag keyword <c>ore</c> as a whole token ensure
/// <c>stone</c> so ore blocks keep stone-like LabPBR heuristics while preserving semantic material tags.
/// </summary>
public static class MaterialTagMlPostProcessor
{
    private static readonly string[] TimberPathOrNameHints =
    [
        "plank",
        "log",
        "bark",
        "stripped",
        "hyphae",
        "stem",
        "fence",
        "gate",
        "trapdoor",
        "door",
        "sign",
        "slab",
        "stairs",
        "button",
        "pressure_plate",
        "boat",
        "chest",
        "barrel",
        "bookshelf",
        "lectern",
        "loom",
        "composter",
        "crafting_table",
        "stick",
        "ladder",
        "campfire",
        "jukebox",
        "note_block",
        "beehive",
        "bee_nest",
        "wooden",
        "_wood",
        "scaffolding"
    ];

    private static readonly string[] LeafPathOrNameHints =
    [
        "leaf",
        "leaves",
        "azalea",
        "mangrove_roots"
    ];

    /// <summary>
    /// Post-processes ML material tags:
    /// <list type="bullet">
    ///   <item>Drop <c>plant</c> when the path/title looks like timber (unless a plant keyword matches).</item>
    ///   <item>Drop <c>wood</c> when the path/title looks like leaves/leaf (unless a wood keyword matches).</item>
    /// </list>
    /// When <paramref name="maxMaterialTags"/> is set (MiniLM path), the list is trimmed to that many ids in order
    /// so inserts like forced <c>plant</c> cannot exceed the user's max ML material tag count. On ore paths (whole-word
    /// <c>ore</c>), when both <c>stone</c> and <c>gem</c> or <c>metal</c> are present, the cap is raised by one so
    /// gem/metal ores can keep stone without dropping either tag. Coal (whole-word <c>coal</c> in name/path) does not
    /// use this extra slot.
    /// </summary>
    public static List<string> Apply(
        string textureName,
        string ruleRelativeKey,
        IReadOnlyList<string> materialTagIds,
        IReadOnlyList<TagRule> materialRules,
        int? maxMaterialTags = null)
    {
        if (materialTagIds.Count == 0)
        {
            return [];
        }

        var pathBelow = TagRuleApplicator.PathBelowNamespace(ruleRelativeKey);
        var combined = textureName + "\0" + pathBelow;

        var result = materialTagIds.ToList();
        result = EnsurePlantForLeafHints(result, combined);

        result = TryDropTag(result, "plant", combined, TimberPathOrNameHints, materialRules);
        result = TryDropTag(result, "wood", combined, LeafPathOrNameHints, materialRules);

        result = EnsureStoneForOrePaths(result, combined);

        if (maxMaterialTags is > 0 and var cap && result.Count > cap)
        {
            return ClampToMaxMaterialTags(result, combined, cap);
        }

        return result;
    }

    private static List<string> ClampToMaxMaterialTags(
        List<string> result,
        string combined,
        int cap)
    {
        var orePath = TagRuleApplicator.KeywordMatches(combined, "ore", wholeWord: true);
        var coalContext = TagRuleApplicator.KeywordMatches(combined, "coal", wholeWord: true);
        var hasStone = Contains(result, "stone");
        var hasGem = Contains(result, "gem");
        var hasMetal = Contains(result, "metal");
        var allowOreStonePair = orePath && !coalContext && hasStone && (hasGem || hasMetal);
        var effectiveCap = allowOreStonePair ? cap + 1 : cap;

        if (result.Count <= effectiveCap)
        {
            return result;
        }

        if (!allowOreStonePair)
        {
            return result.GetRange(0, cap);
        }

        return TrimMaterialTagsToCap(result, effectiveCap);
    }

    /// <summary>Keeps <c>stone</c>, <c>gem</c>, and <c>metal</c> first (when present), then remaining ids in original order.</summary>
    private static List<string> TrimMaterialTagsToCap(List<string> result, int effectiveCap)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var seen = new HashSet<string>(comparer);
        var ordered = new List<string>(Math.Min(effectiveCap, result.Count));

        void AddIfPresent(string tagId)
        {
            if (ordered.Count >= effectiveCap)
            {
                return;
            }

            foreach (var id in result)
            {
                if (id.Equals(tagId, StringComparison.OrdinalIgnoreCase) && seen.Add(id))
                {
                    ordered.Add(id);
                    return;
                }
            }
        }

        AddIfPresent("stone");
        AddIfPresent("gem");
        AddIfPresent("metal");
        foreach (var id in result)
        {
            if (ordered.Count >= effectiveCap)
            {
                break;
            }

            if (seen.Add(id))
            {
                ordered.Add(id);
            }
        }

        return ordered;
    }

    /// <summary>
    /// When the texture title/path matches the Ore flag (<c>ore</c> as a whole word, same as Explore), ensure
    /// <c>stone</c> is present so ores align with stone material rules while preserving matched tags.
    /// </summary>
    private static List<string> EnsureStoneForOrePaths(List<string> ids, string combined)
    {
        if (!TagRuleApplicator.KeywordMatches(combined, "ore", wholeWord: true))
        {
            return ids;
        }

        if (Contains(ids, "stone"))
        {
            return ids;
        }

        var withoutUnknown = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            if (!id.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                withoutUnknown.Add(id);
            }
        }

        if (withoutUnknown.Count == 0)
        {
            return ["stone"];
        }

        withoutUnknown.Insert(0, "stone");
        return withoutUnknown;
    }

    /// <summary>
    /// If <paramref name="tagIdToDrop"/> is present and <paramref name="combined"/> matches any of
    /// <paramref name="pathHints"/>, drop the tag — unless the tag's own rule keywords also match.
    /// </summary>
    private static List<string> TryDropTag(
        List<string> ids,
        string tagIdToDrop,
        string combined,
        string[] pathHints,
        IReadOnlyList<TagRule> materialRules)
    {
        if (!Contains(ids, tagIdToDrop))
        {
            return ids;
        }

        if (!AnyHintMatches(combined, pathHints))
        {
            return ids;
        }

        var rule = FindRule(materialRules, tagIdToDrop);
        if (rule is not null && KeywordHit(combined, rule.Keywords))
        {
            return ids;
        }

        var result = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            if (!id.Equals(tagIdToDrop, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(id);
            }
        }

        if (result.Count == 0)
        {
            result.Add("unknown");
        }

        return result;
    }

    private static bool Contains(List<string> ids, string target)
    {
        foreach (var id in ids)
        {
            if (id.Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> EnsurePlantForLeafHints(List<string> ids, string combined)
    {
        if (!AnyHintMatches(combined, LeafPathOrNameHints))
        {
            return ids;
        }

        if (Contains(ids, "plant"))
        {
            return ids;
        }

        var result = new List<string>(ids.Count + 1);
        foreach (var id in ids)
        {
            if (!id.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(id);
            }
        }

        result.Insert(0, "plant");
        return result;
    }

    private static TagRule? FindRule(IReadOnlyList<TagRule> materialRules, string ruleId)
    {
        foreach (var r in materialRules)
        {
            if (r.Id.Equals(ruleId, StringComparison.OrdinalIgnoreCase) && r.Kind == TagRuleKind.Material)
            {
                return r;
            }
        }

        return null;
    }

    private static bool KeywordHit(string combined, IReadOnlyList<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                continue;
            }

            if (combined.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AnyHintMatches(string combined, string[] hints)
    {
        foreach (var hint in hints)
        {
            if (combined.Contains(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
