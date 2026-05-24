using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Reparents IR parts to mirror <c>reference_java</c> hierarchy before world-pose walks.
/// </summary>
internal static class GeometryIrReferenceTopologyAlign
{
    public static JsonElement ApplyForWorldPoseCompare(JsonElement referenceRoot, JsonElement irShardRoot)
    {
        if (!TryBuildReferenceParentMap(referenceRoot, out var refParentByPartId) || refParentByPartId.Count == 0)
        {
            return irShardRoot;
        }

        var node = JsonNode.Parse(irShardRoot.GetRawText());
        if (node is not JsonObject doc || doc["roots"] is not JsonArray roots)
        {
            return irShardRoot;
        }

        foreach (var root in roots)
        {
            if (root is not JsonObject outerRoot || outerRoot["children"] is not JsonArray factoryKids)
            {
                continue;
            }

            AlignFactoryTree(factoryKids, refParentByPartId);
        }

        return JsonDocument.Parse(doc.ToJsonString()).RootElement;
    }

    private static void AlignFactoryTree(JsonArray factoryRootChildren, IReadOnlyDictionary<string, string> refParentByPartId)
    {
        foreach (var (partId, parentId) in refParentByPartId)
        {
            if (string.IsNullOrEmpty(partId) || string.Equals(partId, "root", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryDetachPart(factoryRootChildren, partId, out var detached) || detached is null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(parentId))
            {
                if (!IsDirectChild(factoryRootChildren, partId))
                {
                    factoryRootChildren.Add(detached);
                }

                continue;
            }

            if (!TryFindPart(factoryRootChildren, parentId, out var parentNode) || parentNode is null)
            {
                factoryRootChildren.Add(detached);
                continue;
            }

            parentNode["children"] ??= new JsonArray();
            if (parentNode["children"] is not JsonArray parentKids)
            {
                continue;
            }

            if (!IsDirectChild(parentKids, partId))
            {
                parentKids.Add(detached);
            }
        }
    }

    private static bool TryBuildReferenceParentMap(JsonElement referenceRoot, out Dictionary<string, string> parentByPartId)
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

    private static bool IsDirectChild(JsonArray siblings, string partId)
    {
        foreach (var n in siblings)
        {
            if (n is JsonObject o && string.Equals((string?)o["id"], partId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPart(JsonArray searchRoots, string partId, out JsonObject? found)
    {
        found = null;
        foreach (var n in searchRoots)
        {
            if (n is not JsonObject part)
            {
                continue;
            }

            if (string.Equals((string?)part["id"], partId, StringComparison.Ordinal))
            {
                found = part;
                return true;
            }

            if (part["children"] is JsonArray kids && TryFindPart(kids, partId, out found))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryDetachPart(JsonArray searchRoots, string partId, out JsonObject? detached)
    {
        detached = null;
        for (var i = 0; i < searchRoots.Count; i++)
        {
            if (searchRoots[i] is not JsonObject part)
            {
                continue;
            }

            if (string.Equals((string?)part["id"], partId, StringComparison.Ordinal))
            {
                detached = part;
                searchRoots.RemoveAt(i);
                return true;
            }

            if (part["children"] is JsonArray kids && TryDetachPart(kids, partId, out detached))
            {
                return true;
            }
        }

        return false;
    }
}
