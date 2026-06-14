using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Repairs known flat IR trees after lift, before strict validation (mirrors Core parity repair).
/// </summary>
internal static class GeometryIrLiftTreeRepair
{
    private static readonly (string ChildId, string ParentId)[] ReparentRules =
    [
        ("beak", "head"),
        ("red_thing", "head"),
        ("hat", "head"),
        ("nose", "head"),
        ("mole", "head"),
        ("top_gills", "head"),
        ("left_gills", "head"),
        ("right_gills", "head"),
        ("right_leg_r1", "right_hind_leg"),
        ("left_leg_r1", "left_hind_leg"),
        ("rods", "body"),
        ("tail2", "tail1"),
        ("wind_bottom", "wind_body"),
        ("wind_mid", "wind_bottom"),
        ("wind_top", "wind_mid"),
    ];

    /// <param name="hoistStandardQuadrupedLegsToRoot">
    /// When true (bytecode lift / reference-aligned shards), hoists hind/front legs from under <c>body</c> to mesh root.
    /// Javap segment lifts keep nested binding order for hierarchy probes.
    /// </param>
    public static JsonArray Apply(JsonArray roots, bool hoistStandardQuadrupedLegsToRoot = true)
    {
        foreach (var root in roots)
        {
            if (root is not JsonObject ro)
            {
                continue;
            }

            if (ro["children"] is JsonArray rootKids)
            {
                UnwrapNestedDefinitionRoot(rootKids);
                RemoveDegenerateCuboidsFromForest(rootKids);
                foreach (var (childId, parentId) in ReparentRules)
                {
                    if (ShouldSkipFelineFlatTailReparent(childId, parentId, rootKids))
                    {
                        continue;
                    }

                    ReparentFlatPart(rootKids, childId, parentId);
                }

                RemoveCowHornCuboidsWhenHornsAreChildParts(rootKids);
                RemoveCowHornCuboidsFromMergedHead(rootKids);
                RemoveRootSiblingWhenNested(rootKids);
                CollapseInnerBodyUnderBody(rootKids);
                RemoveOptionalSaddleHarnessParts(rootKids);
                RemovePlayerMeshInternalParts(rootKids);
                HoistFelineFlatTail2ToRoot(rootKids);
                if (hoistStandardQuadrupedLegsToRoot)
                {
                    HoistStandardQuadrupedLegsFromBody(rootKids);
                }

                RemoveDuplicatePartIdsPreferCuboids(rootKids);
            }

            DeduplicateNestedPartIds(ro);
        }

        return roots;
    }

    /// <summary>
    /// Java reference bakes omit optional saddle harness parts from equine <c>createBodyLayer</c> (donkey/mule chest variants).
    /// Dedicated <c>*SaddleModel.createSaddleLayer</c> lifts keep harness geometry as the primary output.
    /// </summary>
    private static void RemoveOptionalSaddleHarnessParts(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "head_parts", out _) ||
            !TryFindPartById(rootChildren, "left_chest", out _))
        {
            return;
        }

        RemovePartIdsFromForest(rootChildren,
        [
            "saddle",
            "head_saddle",
            "mouth_saddle_wrap",
            "left_saddle_mouth",
            "right_saddle_mouth",
            "left_saddle_line",
            "right_saddle_line",
            "reins",
            "bridle",
            "harness",
        ]);
    }

    /// <summary>
    /// <c>PlayerModel.createMesh</c> bytecode keeps waist/feet/inner_body parts that Java reference bakes omit
    /// (piglin, player, drowned, …). Frog feet are real parts and are kept (no jacket/pants overlay kit).
    /// </summary>
    private static void RemovePlayerMeshInternalParts(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "jacket", out _) ||
            !TryFindPartById(rootChildren, "left_pants", out _))
        {
            return;
        }

        RemovePartIdsFromForest(rootChildren, ["waist", "inner_body", "left_foot", "right_foot"]);
    }

    private static void RemovePartIdsFromForest(JsonArray parts, IReadOnlyList<string> idsToRemove)
    {
        for (var i = parts.Count - 1; i >= 0; i--)
        {
            if (parts[i] is not JsonObject part)
            {
                continue;
            }

            var id = (string?)part["id"];
            if (!string.IsNullOrEmpty(id) &&
                idsToRemove.Any(removeId => string.Equals(id, removeId, StringComparison.Ordinal)))
            {
                parts.RemoveAt(i);
                continue;
            }

            if (part["children"] is JsonArray kids)
            {
                RemovePartIdsFromForest(kids, idsToRemove);
            }
        }
    }

    /// <summary>
    /// Java reference bakes omit <c>inner_body</c> under <c>body</c> (HappyGhast); bytecode lift keeps it. Hoist cuboids into body.
    /// </summary>
    private static void CollapseInnerBodyUnderBody(JsonArray rootChildren)
    {
        foreach (var n in rootChildren)
        {
            if (n is not JsonObject part)
            {
                continue;
            }

            CollapseInnerBodyRecursive(part);
        }

        if (TryFindPartById(rootChildren, "body", out _))
        {
            RemovePartIdsFromForest(rootChildren, ["inner_body"]);
        }
    }

    private static void CollapseInnerBodyRecursive(JsonObject part)
    {
        if (string.Equals((string?)part["id"], "body", StringComparison.Ordinal))
        {
            RepairBodyForReferenceBakeAlignment(part);
        }

        if (part["children"] is not JsonArray kids)
        {
            return;
        }

        foreach (var n in kids)
        {
            if (n is JsonObject co)
            {
                CollapseInnerBodyRecursive(co);
            }
        }
    }

    private static void RepairBodyForReferenceBakeAlignment(JsonObject bodyPart)
    {
        if (bodyPart["children"] is not JsonArray bodyKids)
        {
            return;
        }

        var hasInnerBody = false;
        foreach (var child in bodyKids)
        {
            if (child is JsonObject ch &&
                string.Equals((string?)ch["id"], "inner_body", StringComparison.Ordinal))
            {
                hasInnerBody = true;
                break;
            }
        }

        if (hasInnerBody)
        {
            TrimInnerBodyFleeceCuboidFromBody(bodyPart);
        }

        for (var i = bodyKids.Count - 1; i >= 0; i--)
        {
            if (bodyKids[i] is JsonObject ch &&
                string.Equals((string?)ch["id"], "inner_body", StringComparison.Ordinal))
            {
                bodyKids.RemoveAt(i);
                break;
            }
        }
    }

    private static void TrimInnerBodyFleeceCuboidFromBody(JsonObject bodyPart)
    {
        if (bodyPart["cuboids"] is not JsonArray bodyCuboids || bodyCuboids.Count < 2)
        {
            return;
        }

        for (var i = bodyCuboids.Count - 1; i >= 0; i--)
        {
            if (bodyCuboids[i] is not JsonObject cub)
            {
                continue;
            }

            if (cub["uvOrigin"] is JsonArray uv && uv.Count >= 2 &&
                Math.Abs(uv[1]!.GetValue<double>() - 32) < 0.01)
            {
                bodyCuboids.RemoveAt(i);
            }
        }
    }

    private static void RemoveCowHornCuboidsWhenHornsAreChildParts(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "head", out var head) || head is null ||
            head["children"] is not JsonArray headKids ||
            !HeadHasHornChildren(headKids) ||
            head["cuboids"] is not JsonArray cuboids)
        {
            return;
        }

        for (var i = cuboids.Count - 1; i >= 0; i--)
        {
            if (cuboids[i] is not JsonObject cuboid)
            {
                continue;
            }

            var textureKey = (string?)cuboid["textureKey"];
            if (string.Equals(textureKey, "#right_horn", StringComparison.Ordinal) ||
                string.Equals(textureKey, "#left_horn", StringComparison.Ordinal))
            {
                cuboids.RemoveAt(i);
            }
        }
    }

    private static void RemoveCowHornCuboidsFromMergedHead(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "head", out var head) || head is null ||
            head["cuboids"] is not JsonArray cuboids ||
            cuboids.Count <= 4)
        {
            return;
        }

        RemoveHornTextureCuboids(cuboids);
    }

    private static void RemoveHornTextureCuboids(JsonArray cuboids)
    {
        for (var i = cuboids.Count - 1; i >= 0; i--)
        {
            if (cuboids[i] is not JsonObject cuboid)
            {
                continue;
            }

            var textureKey = (string?)cuboid["textureKey"];
            if (string.Equals(textureKey, "#right_horn", StringComparison.Ordinal) ||
                string.Equals(textureKey, "#left_horn", StringComparison.Ordinal))
            {
                cuboids.RemoveAt(i);
            }
        }
    }

    private static bool HeadHasHornChildren(JsonArray headKids)
    {
        var hasRight = false;
        var hasLeft = false;
        foreach (var kid in headKids)
        {
            if (kid is not JsonObject child)
            {
                continue;
            }

            hasRight |= string.Equals((string?)child["id"], "right_horn", StringComparison.Ordinal);
            hasLeft |= string.Equals((string?)child["id"], "left_horn", StringComparison.Ordinal);
        }

        return hasRight && hasLeft;
    }

    private static bool ShouldSkipFelineFlatTailReparent(string childId, string parentId, JsonArray rootChildren)
    {
        if (!string.Equals(childId, "tail2", StringComparison.Ordinal) ||
            !string.Equals(parentId, "tail1", StringComparison.Ordinal))
        {
            return false;
        }

        return UsesFelineFlatRootAbsoluteTailBake(rootChildren);
    }

    private static bool UsesFelineFlatRootAbsoluteTailBake(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "tail2", out var tail2) || tail2 is null ||
            tail2["pose"]?["translation"] is not JsonArray tailTr ||
            tailTr.Count < 3)
        {
            return false;
        }

        var tailY = tailTr[1]?.GetValue<double>() ?? 0;
        var tailZ = tailTr[2]?.GetValue<double>() ?? 0;
        return tailY > 18 && tailZ > 10;
    }

    private static void HoistFelineFlatTail2ToRoot(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "tail1", out var tail1) || tail1 is null ||
            tail1["children"] is not JsonArray tail1Kids)
        {
            return;
        }

        JsonObject? tail2 = null;
        var tail2Idx = -1;
        for (var i = 0; i < tail1Kids.Count; i++)
        {
            if (tail1Kids[i] is JsonObject o && string.Equals((string?)o["id"], "tail2", StringComparison.Ordinal))
            {
                tail2 = o;
                tail2Idx = i;
                break;
            }
        }

        if (tail2 is null || tail2Idx < 0 || !UsesFelineFlatRootAbsoluteTailBake(rootChildren))
        {
            return;
        }

        rootChildren.Add(tail2.DeepClone());
        tail1Kids.RemoveAt(tail2Idx);
    }

    private static void ReparentFlatPart(JsonArray rootChildren, string childId, string parentId)
    {
        JsonObject? childNode = null;
        var childIdx = -1;
        for (var i = 0; i < rootChildren.Count; i++)
        {
            if (rootChildren[i] is JsonObject o && string.Equals((string?)o["id"], childId, StringComparison.Ordinal))
            {
                childNode = o;
                childIdx = i;
                break;
            }
        }

        if (childNode is null || childIdx < 0)
        {
            return;
        }

        if (!TryFindPartById(rootChildren, parentId, out var parentNode) || parentNode is null)
        {
            return;
        }

        if (parentNode["children"] is not JsonArray parentKids)
        {
            parentKids = [];
            parentNode["children"] = parentKids;
        }

        if (parentKids.Any(n => n is JsonObject o && string.Equals((string?)o["id"], childId, StringComparison.Ordinal)))
        {
            rootChildren.RemoveAt(childIdx);
            return;
        }

        parentKids.Add(childNode.DeepClone());
        rootChildren.RemoveAt(childIdx);
    }

    private static void HoistStandardQuadrupedLegsFromBody(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "body", out var body) || body is null ||
            body["children"] is not JsonArray bodyKids)
        {
            return;
        }

        for (var i = bodyKids.Count - 1; i >= 0; i--)
        {
            if (bodyKids[i] is not JsonObject child ||
                child["id"]?.GetValue<string>() is not { } id ||
                !IsStandardQuadrupedLegPartId(id))
            {
                continue;
            }

            rootChildren.Add(child.DeepClone());
            bodyKids.RemoveAt(i);
        }
    }

    private static bool IsStandardQuadrupedLegPartId(string id) =>
        id is "right_hind_leg" or "left_hind_leg" or "right_front_leg" or "left_front_leg";

    /// <summary>
    /// PartDefinition trees name the root part <c>root</c>; <see cref="GeometryLiftOutputAssembly.WrapSyntheticRoot"/>
    /// adds another <c>root</c> wrapper — hoist the inner children so part ids are unique.
    /// </summary>
    private static void UnwrapNestedDefinitionRoot(JsonArray rootChildren)
    {
        for (var i = rootChildren.Count - 1; i >= 0; i--)
        {
            if (rootChildren[i] is not JsonObject part ||
                !string.Equals((string?)part["id"], "root", StringComparison.Ordinal) ||
                part["children"] is not JsonArray kids)
            {
                continue;
            }

            rootChildren.RemoveAt(i);
            if (kids.Count == 0)
            {
                continue;
            }

            for (var j = 0; j < kids.Count; j++)
            {
                rootChildren.Insert(i + j, JsonNode.Parse(kids[j]!.ToJsonString())!);
            }
        }
    }

    private static void RemoveDegenerateCuboidsFromForest(JsonArray parts)
    {
        foreach (var n in parts)
        {
            if (n is JsonObject part)
            {
                RemoveDegenerateCuboidsFromPart(part);
            }
        }
    }

    private static void RemoveDegenerateCuboidsFromPart(JsonObject part)
    {
        if (part["cuboids"] is JsonArray cuboids)
        {
            for (var i = cuboids.Count - 1; i >= 0; i--)
            {
                if (cuboids[i] is JsonObject c && IsDegenerateLiftCuboid(c))
                {
                    cuboids.RemoveAt(i);
                }
            }
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var kid in kids.OfType<JsonObject>())
            {
                RemoveDegenerateCuboidsFromPart(kid);
            }
        }
    }

    /// <summary>Drop zero-thickness boxes and mis-lifted float constants (e.g. π as a coordinate).</summary>
    private static bool IsDegenerateLiftCuboid(JsonObject cuboid)
    {
        if (!TryReadVec3(cuboid["from"], out var fx, out var fy, out var fz) ||
            !TryReadVec3(cuboid["to"], out var tx, out var ty, out var tz))
        {
            return true;
        }

        var dx = Math.Abs(tx - fx);
        var dy = Math.Abs(ty - fy);
        var dz = Math.Abs(tz - fz);
        const double eps = 1e-3;
        // Mojang uses zero-thickness boxes for flat fins / arrow shafts; drop only line/point degenerates.
        var thinDims = (dx < eps ? 1 : 0) + (dy < eps ? 1 : 0) + (dz < eps ? 1 : 0);
        if (thinDims >= 2)
        {
            return true;
        }

        return false;
    }

    private static bool TryReadVec3(JsonNode? node, out double x, out double y, out double z)
    {
        x = y = z = 0;
        if (node is not JsonArray arr || arr.Count < 3)
        {
            return false;
        }

        return arr[0] is JsonValue vx && vx.TryGetValue(out x) &&
               arr[1] is JsonValue vy && vy.TryGetValue(out y) &&
               arr[2] is JsonValue vz && vz.TryGetValue(out z);
    }

    private sealed record PartOccurrence(string Id, JsonArray Parent, int Index, JsonObject Node);

    private static void RemoveDuplicatePartIdsPreferCuboids(JsonArray rootChildren)
    {
        var occurrences = new List<PartOccurrence>();
        CollectPartOccurrences(rootChildren, occurrences);
        foreach (var group in occurrences.GroupBy(o => o.Id, StringComparer.Ordinal))
        {
            var items = group.ToList();
            if (items.Count <= 1)
            {
                continue;
            }

            var keep = items
                .OrderByDescending(o => DirectCuboidCount(o.Node))
                .ThenBy(o => occurrences.IndexOf(o))
                .First();
            foreach (var remove in items.Where(o => !ReferenceEquals(o.Node, keep.Node))
                         .OrderByDescending(o => o.Index))
            {
                if (remove.Index >= 0 &&
                    remove.Index < remove.Parent.Count &&
                    ReferenceEquals(remove.Parent[remove.Index], remove.Node))
                {
                    remove.Parent.RemoveAt(remove.Index);
                    continue;
                }

                for (var i = remove.Parent.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(remove.Parent[i], remove.Node))
                    {
                        remove.Parent.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }

    private static void CollectPartOccurrences(JsonArray parts, List<PartOccurrence> occurrences)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] is not JsonObject part ||
                part["id"]?.GetValue<string>() is not { } id)
            {
                continue;
            }

            occurrences.Add(new PartOccurrence(id, parts, i, part));
            if (part["children"] is JsonArray kids)
            {
                CollectPartOccurrences(kids, occurrences);
            }
        }
    }

    private static int DirectCuboidCount(JsonObject part) =>
        part["cuboids"] is JsonArray cuboids ? cuboids.Count : 0;

    private static int CountCuboids(JsonObject part)
    {
        var count = part["cuboids"] is JsonArray cuboids ? cuboids.Count : 0;
        if (part["children"] is JsonArray kids)
        {
            foreach (var kid in kids.OfType<JsonObject>())
            {
                count += CountCuboids(kid);
            }
        }

        return count;
    }

    /// <summary>Drop a root sibling when the same part id already exists deeper (e.g. hat on head + hat at root).</summary>
    private static void RemoveRootSiblingWhenNested(JsonArray rootChildren)
    {
        for (var i = rootChildren.Count - 1; i >= 0; i--)
        {
            if (rootChildren[i] is not JsonObject o || o["id"]?.GetValue<string>() is not { } id)
            {
                continue;
            }

            var foundNested = false;
            foreach (var n in rootChildren)
            {
                if (n is JsonObject other && other != o && PartTreeContainsId(other, id))
                {
                    foundNested = true;
                    break;
                }
            }

            if (foundNested)
            {
                rootChildren.RemoveAt(i);
            }
        }
    }

    private static bool TryFindPartById(JsonArray parts, string id, out JsonObject? found)
    {
        foreach (var n in parts)
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

            if (o["children"] is JsonArray kids && TryFindPartById(kids, id, out found))
            {
                return true;
            }
        }

        found = null;
        return false;
    }

    private static bool PartTreeContainsId(JsonObject part, string id)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            return true;
        }

        if (part["children"] is not JsonArray kids)
        {
            return false;
        }

        foreach (var ch in kids)
        {
            if (ch is JsonObject co && PartTreeContainsId(co, id))
            {
                return true;
            }
        }

        return false;
    }

    private static void DeduplicateNestedPartIds(JsonObject part)
    {
        if (part["children"] is not JsonArray kids)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = kids.Count - 1; i >= 0; i--)
        {
            if (kids[i] is not JsonObject child)
            {
                continue;
            }

            var id = (string?)child["id"];
            if (string.IsNullOrEmpty(id))
            {
                DeduplicateNestedPartIds(child);
                continue;
            }

            if (!seen.Add(id))
            {
                kids.RemoveAt(i);
                continue;
            }

            DeduplicateNestedPartIds(child);
        }
    }
}
