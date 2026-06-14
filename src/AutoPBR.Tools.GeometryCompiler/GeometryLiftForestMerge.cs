using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Post-lift forest operations: overlay relocation, orphan reparenting, deduplication, and cuboid clearing.
/// </summary>
internal static class GeometryLiftForestMerge
{
    /// <summary>
    /// MeshTransformer overlay part ids (ears, chests, beak, …) and their canonical parent part ids.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> KnownNestedChildToParent { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["beak"] = "head",
            ["red_thing"] = "head",
            ["hat"] = "head",
            ["nose"] = "head",
            ["mole"] = "head",
            ["top_gills"] = "head",
            ["left_gills"] = "head",
            ["right_gills"] = "head",
            ["right_leg_r1"] = "right_hind_leg",
            ["left_leg_r1"] = "left_hind_leg",
            ["rods"] = "body",
            ["tail2"] = "tail1",
            ["left_chest"] = "body",
            ["right_chest"] = "body",
            ["left_ear"] = "head",
            ["right_ear"] = "head",
        };

    /// <summary>
    /// Runs multi-island post-merge steps on concatenated mesh factory output (reparent, dedupe overlays, relocate).
    /// </summary>
    public static void ApplyMultiIslandPostMerge(JsonArray rootChildren)
    {
        var index = PartForestIndex.Build(rootChildren);
        if (!index.ContainsAnyKnownOverlayPart())
        {
            return;
        }

        ReparentKnownNestedRootOrphans(rootChildren, index);
        if (RemoveDuplicateOverlayPartIds(index))
        {
            index.Rebuild(rootChildren);
        }

        RelocateOverlayPartsToKnownParents(index);
    }

    /// <summary>
    /// True when <paramref name="id"/> is a MeshTransformer overlay part handled by <see cref="KnownNestedChildToParent"/>.
    /// </summary>
    public static bool IsMeshTransformerOverlayPartId(string? id) =>
        !string.IsNullOrEmpty(id) && KnownNestedChildToParent.ContainsKey(id);

    /// <summary>
    /// Delegated factories sometimes emit duplicate PartDefinition segments in one island; last segment wins per id.
    /// </summary>
    public static JsonArray DeduplicateRootChildrenByPartIdLastWins(JsonArray rootChildren)
    {
        var lastById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var n in rootChildren)
        {
            if (n is not JsonObject o)
            {
                continue;
            }

            var id = (string?)o["id"];
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            if (!lastById.ContainsKey(id))
            {
                order.Add(id);
            }

            var clone = JsonNode.Parse(o.ToJsonString())!.AsObject();
            if (clone["children"] is JsonArray kids)
            {
                clone["children"] = DeduplicateRootChildrenByPartIdLastWins(kids);
            }

            lastById[id] = clone;
        }

        var deduped = new JsonArray();
        foreach (var id in order)
        {
            deduped.Add(lastById[id]);
        }

        return deduped;
    }

    public static bool TryFindPartObjectByIdInForest(JsonArray level, string id, out JsonObject? found)
    {
        found = null;
        foreach (var n in level)
        {
            if (n is not JsonObject o)
            {
                continue;
            }

            if (string.Equals((string?)o["id"], id, StringComparison.Ordinal))
            {
                found = o;
                return true;
            }

            if (o["children"] is JsonArray kids && TryFindPartObjectByIdInForest(kids, id, out found))
            {
                return true;
            }
        }

        return false;
    }

    public static void AttachPartUnderParent(JsonObject parentObj, JsonObject partNode, string partName)
    {
        if (parentObj["children"] is not JsonArray ch)
        {
            parentObj["children"] = new JsonArray();
            ch = parentObj["children"]!.AsArray();
        }

        for (var i = 0; i < ch.Count; i++)
        {
            if (ch[i] is JsonObject existing &&
                string.Equals((string?)existing["id"], partName, StringComparison.Ordinal))
            {
                ch[i] = partNode;
                return;
            }
        }

        ch.Add(partNode);
    }

    public static void ClearCuboidsRecursively(JsonArray parts) =>
        ClearCuboidsRecursivelyExcept(parts, null);

    public static void ClearCuboidsRecursivelyExcept(JsonArray parts, IReadOnlySet<string>? preservePartIds)
    {
        foreach (var n in parts)
        {
            if (n is not JsonObject part)
            {
                continue;
            }

            var id = (string?)part["id"];
            if (preservePartIds is not null && id is not null && preservePartIds.Contains(id))
            {
                continue;
            }

            part["cuboids"] = new JsonArray();
            if (part["children"] is JsonArray kids)
            {
                ClearCuboidsRecursivelyExcept(kids, preservePartIds);
            }
        }
    }

    /// <summary>
    /// MeshTransformer islands lift chests/ears at the forest root; reattach under body/head after base mesh merge.
    /// </summary>
    private static void ReparentKnownNestedRootOrphans(JsonArray rootChildren, PartForestIndex index)
    {
        foreach (var requiredParentId in new[] { "body", "head" })
        {
            var phaseMoved = false;
            while (true)
            {
                var moved = false;
                for (var i = rootChildren.Count - 1; i >= 0; i--)
                {
                    if (rootChildren[i] is not JsonObject child)
                    {
                        continue;
                    }

                    var id = (string?)child["id"];
                    if (string.IsNullOrEmpty(id) ||
                        !KnownNestedChildToParent.TryGetValue(id, out var parentId) ||
                        !string.Equals(parentId, requiredParentId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!index.TryGetFirst(parentId, out var parentObj) || parentObj is null)
                    {
                        continue;
                    }

                    if (index.TryGetOccurrences(id, out var occurrences))
                    {
                        foreach (var occ in occurrences.ToList())
                        {
                            RemoveOccurrenceNode(occ);
                        }
                    }

                    AttachPartUnderParent(parentObj, child, id);
                    moved = true;
                }

                if (!moved)
                {
                    break;
                }

                phaseMoved = true;
            }

            if (phaseMoved)
            {
                index.Rebuild(rootChildren);
            }
        }
    }

    private static void RelocateOverlayPartsToKnownParents(PartForestIndex index)
    {
        var removals = new List<PartForestIndex.Occurrence>();
        var attaches = new List<(JsonObject Node, string ChildId, JsonObject Parent)>();

        foreach (var (childId, parentId) in KnownNestedChildToParent)
        {
            if (ShouldSkipFelineFlatTailRelocate(childId, parentId, index))
            {
                continue;
            }

            if (!index.TryGetOccurrences(childId, out var hits) || hits.Count == 0)
            {
                continue;
            }

            if (index.IsOverlayPartNestedUnderParentId(childId, parentId))
            {
                continue;
            }

            if (!index.TryGetFirst(parentId, out var parentObj) || parentObj is null)
            {
                continue;
            }

            removals.AddRange(hits);
            attaches.Add((hits[^1].Node, childId, parentObj));
        }

        RemoveOccurrencesDescendingByIndex(removals);

        foreach (var (node, childId, parentObj) in attaches)
        {
            AttachPartUnderParent(parentObj, node, childId);
        }
    }

    private static bool ShouldSkipFelineFlatTailRelocate(string childId, string parentId, PartForestIndex index)
    {
        if (!string.Equals(childId, "tail2", StringComparison.Ordinal) ||
            !string.Equals(parentId, "tail1", StringComparison.Ordinal) ||
            !index.TryGetFirst("tail2", out var tail2) ||
            tail2 is null)
        {
            return false;
        }

        if (tail2["pose"]?["translation"] is not JsonArray tailTr || tailTr.Count < 3)
        {
            return false;
        }

        var tailY = tailTr[1]?.GetValue<double>() ?? 0;
        var tailZ = tailTr[2]?.GetValue<double>() ?? 0;
        return tailY > 18 && tailZ > 10;
    }

    private static bool RemoveDuplicateOverlayPartIds(PartForestIndex index)
    {
        var removals = new List<PartForestIndex.Occurrence>();

        foreach (var overlayId in KnownNestedChildToParent.Keys)
        {
            if (!index.TryGetOccurrences(overlayId, out var hits) || hits.Count <= 1)
            {
                continue;
            }

            var keepIndex = 0;
            var bestScore = OverlayPartAttachmentScore(hits[0].Node);
            for (var i = 1; i < hits.Count; i++)
            {
                var score = OverlayPartAttachmentScore(hits[i].Node);
                if (score > bestScore)
                {
                    bestScore = score;
                    keepIndex = i;
                }
            }

            var kept = hits[keepIndex];
            removals.AddRange(hits.Where(h => !ReferenceEquals(h.Node, kept.Node)));
        }

        if (removals.Count == 0)
        {
            return false;
        }

        RemoveOccurrencesDescendingByIndex(removals);
        return true;
    }

    /// <summary>
    /// Removes indexed occurrences without shifting later <see cref="PartForestIndex.Occurrence.Index"/> values in the same parent.
    /// </summary>
    private static void RemoveOccurrencesDescendingByIndex(IEnumerable<PartForestIndex.Occurrence> hits)
    {
        foreach (var group in hits.GroupBy(h => h.Parent))
        {
            foreach (var hit in group.OrderByDescending(h => h.Index))
            {
                var parent = hit.Parent;
                if (hit.Index >= 0 && hit.Index < parent.Count && ReferenceEquals(parent[hit.Index], hit.Node))
                {
                    parent.RemoveAt(hit.Index);
                    continue;
                }

                RemoveOccurrenceNode(hit);
            }
        }
    }

    private static void RemoveOccurrenceNode(PartForestIndex.Occurrence hit)
    {
        var parent = hit.Parent;
        for (var i = parent.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(parent[i], hit.Node))
            {
                parent.RemoveAt(i);
                return;
            }
        }
    }

    private static double OverlayPartAttachmentScore(JsonObject part)
    {
        if (part["pose"] is not JsonObject pose || pose["translation"] is not JsonArray trans || trans.Count < 3)
        {
            return 0;
        }

        static double Abs(JsonArray a, int i) => Math.Abs(a[i]!.GetValue<double>());
        return Abs(trans, 0) + Abs(trans, 1) + Abs(trans, 2);
    }

    /// <summary>
    /// One DFS over the lifted part forest: first node per id (lookup) and all parent/index locations (dedupe/relocate).
    /// Rebuilt after structural edits because <see cref="JsonArray"/> indices shift on remove.
    /// </summary>
    private sealed class PartForestIndex
    {
        internal readonly record struct Occurrence(JsonArray Parent, int Index, JsonObject Node);

        private readonly Dictionary<string, JsonObject> _firstById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Occurrence>> _occurrences = new(StringComparer.Ordinal);

        public static PartForestIndex Build(JsonArray roots)
        {
            var index = new PartForestIndex();
            index.Walk(roots);
            return index;
        }

        public void Rebuild(JsonArray roots)
        {
            _firstById.Clear();
            _occurrences.Clear();
            Walk(roots);
        }

        public bool TryGetFirst(string id, out JsonObject? found) => _firstById.TryGetValue(id, out found);

        public bool TryGetOccurrences(string id, out List<Occurrence> hits) =>
            _occurrences.TryGetValue(id, out hits!);

        public bool ContainsAnyKnownOverlayPart()
        {
            foreach (var overlayId in KnownNestedChildToParent.Keys)
            {
                if (_occurrences.ContainsKey(overlayId))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsOverlayPartNestedUnderParentId(string childId, string parentId)
        {
            if (!TryGetFirst(parentId, out var parentObj) || parentObj is null)
            {
                return false;
            }

            return ContainsNestedPartId(parentObj["children"] as JsonArray ?? new JsonArray(), childId);
        }

        private void Walk(JsonArray level)
        {
            for (var i = 0; i < level.Count; i++)
            {
                if (level[i] is not JsonObject part)
                {
                    continue;
                }

                var id = (string?)part["id"];
                if (!string.IsNullOrEmpty(id))
                {
                    _firstById.TryAdd(id, part);
                    if (!_occurrences.TryGetValue(id, out var list))
                    {
                        list = new List<Occurrence>();
                        _occurrences[id] = list;
                    }

                    list.Add(new Occurrence(level, i, part));
                }

                if (part["children"] is JsonArray kids)
                {
                    Walk(kids);
                }
            }
        }

        private static bool ContainsNestedPartId(JsonArray parts, string id)
        {
            foreach (var n in parts)
            {
                if (n is not JsonObject o)
                {
                    continue;
                }

                if (string.Equals((string?)o["id"], id, StringComparison.Ordinal))
                {
                    return true;
                }

                if (o["children"] is JsonArray kids && ContainsNestedPartId(kids, id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
