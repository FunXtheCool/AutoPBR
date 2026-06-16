using System.Collections.Concurrent;
using System.Text.Json;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Reads <c>reference_java</c> part parentage from optional repo-local bakes for hierarchy-aware repair.
/// </summary>
internal static class GeometryIrReferenceHierarchy
{
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>?> ParentMapCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Expected parent part id from reference_java (<c>""</c> = mesh root sibling). Returns false when no reference bake is available.
    /// </summary>
    internal static bool TryGetExpectedParentId(string? officialJvmName, string partId, out string? parentId)
    {
        parentId = null;
        if (string.IsNullOrWhiteSpace(officialJvmName) || string.IsNullOrEmpty(partId))
        {
            return false;
        }

        if (!TryGetReferenceParentMap(officialJvmName, out var map) || map is null ||
            !map.TryGetValue(partId, out parentId))
        {
            return false;
        }

        return true;
    }

    internal static bool TryBuildReferenceParentMap(JsonElement referenceRoot, out Dictionary<string, string> parentByPartId)
    {
        parentByPartId = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!referenceRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var outer in roots.EnumerateArray())
        {
            if (!outer.TryGetProperty("children", out var outerKids) || outerKids.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var ch in outerKids.EnumerateArray())
            {
                if (ch.TryGetProperty("id", out var idEl) &&
                    string.Equals(idEl.GetString(), "root", StringComparison.Ordinal) &&
                    ch.TryGetProperty("children", out var innerKids) &&
                    innerKids.ValueKind == JsonValueKind.Array)
                {
                    WalkReferenceParents(innerKids, parentId: "", parentByPartId);
                    return parentByPartId.Count > 0;
                }
            }

            WalkReferenceParents(outerKids, parentId: "", parentByPartId);
            return parentByPartId.Count > 0;
        }

        return false;
    }

    private static bool TryGetReferenceParentMap(string officialJvmName, out Dictionary<string, string>? map)
    {
        map = ParentMapCache.GetOrAdd(officialJvmName, static jvm =>
        {
            if (!TryLoadReferenceRoot(jvm, out var root))
            {
                return null;
            }

            return TryBuildReferenceParentMap(root, out var built) ? built : null;
        });

        return map is not null;
    }

    private static bool TryLoadReferenceRoot(string officialJvmName, out JsonElement root) =>
        GeometryIrReferenceBakePaths.TryLoadReferenceRoot(officialJvmName, out root);

    private static void WalkReferenceParents(
        JsonElement siblings,
        string parentId,
        Dictionary<string, string> parentByPartId)
    {
        foreach (var part in siblings.EnumerateArray())
        {
            if (!part.TryGetProperty("id", out var idEl))
            {
                continue;
            }

            var id = idEl.GetString();
            if (string.IsNullOrEmpty(id) || string.Equals(id, "root", StringComparison.Ordinal))
            {
                continue;
            }

            parentByPartId[id] = parentId;
            if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                WalkReferenceParents(children, id, parentByPartId);
            }
        }
    }
}
