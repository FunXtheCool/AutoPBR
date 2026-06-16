using System.Globalization;
using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Merges lifted part JSON trees when multiple mesh islands or segments contribute to the same part id.
/// Used after per-island lift and when composing delegated factory output.
/// </summary>
internal static class GeometryLiftJsonMerge
{
    /// <summary>
    /// Later mesh islands replace earlier root parts with the same id for pose/cuboids, but nested child ids
    /// absent from the newer island are retained from the earlier lift.
    /// </summary>
    public static void MergeRootChildLastWinsByPartId(JsonArray rootChildren, JsonNode? partNode)
    {
        if (partNode is not JsonObject co)
        {
            return;
        }

        var id = (string?)co["id"];
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        for (var i = rootChildren.Count - 1; i >= 0; i--)
        {
            if (rootChildren[i] is JsonObject ex && string.Equals((string?)ex["id"], id, StringComparison.Ordinal))
            {
                rootChildren[i] = MergeLiftedPartNodes(ex, co);
                return;
            }
        }

        rootChildren.Add(JsonNode.Parse(co.ToJsonString())!);
    }

    /// <summary>
    /// Deep-merges two lifted part nodes with the same id: incoming pose/shallow fields win;
    /// cuboids are unioned or replaced per overlay and piglin-head rules; children are merged by id.
    /// </summary>
    public static JsonObject MergeLiftedPartNodes(JsonObject earlier, JsonObject incoming)
    {
        var merged = JsonNode.Parse(incoming.ToJsonString())!.AsObject();
        var earlierCuboids = earlier["cuboids"] as JsonArray ?? new JsonArray();
        var incomingCuboids = merged["cuboids"] as JsonArray ?? new JsonArray();
        var preferIncomingCuboids = ShouldPreferIncomingCuboidsOverUnion(merged, incomingCuboids);
        var preferEarlierHumanoidArmCuboids = ShouldPreferEarlierHumanoidArmCuboids(
            (string?)incoming["id"], earlierCuboids, incomingCuboids);
        var preferIncomingThinHumanoidLimbCuboids = ShouldPreferIncomingThinHumanoidLimbCuboids(
            (string?)incoming["id"], incomingCuboids);
        var dropHumanoidHatForPiglinEars = incoming["children"] is JsonArray incomingKidsForHat &&
            (ContainsChildPartId(incomingKidsForHat, "left_ear") ||
             ContainsChildPartId(incomingKidsForHat, "right_ear") ||
             ContainsChildPartId(incomingKidsForHat, "left_ear_r1") ||
             ContainsChildPartId(incomingKidsForHat, "right_ear_r1"));
        var overlayPart = GeometryLiftForestMerge.IsMeshTransformerOverlayPartId((string?)incoming["id"]) ||
            GeometryLiftForestMerge.IsMeshTransformerOverlayPartId((string?)earlier["id"]);
        if (overlayPart && incomingCuboids.Count > 0)
        {
            merged["cuboids"] = JsonNode.Parse(incomingCuboids.ToJsonString())!.AsArray();
        }
        else
        {
            if (incomingCuboids.Count == 0)
            {
                merged["cuboids"] = JsonNode.Parse(earlierCuboids.ToJsonString())!.AsArray();
            }
            else if (earlierCuboids.Count == 0 || preferIncomingCuboids)
            {
                merged["cuboids"] = JsonNode.Parse(incomingCuboids.ToJsonString())!.AsArray();
            }
            else if (preferIncomingThinHumanoidLimbCuboids)
            {
                merged["cuboids"] = JsonNode.Parse(incomingCuboids.ToJsonString())!.AsArray();
            }
            else if (preferEarlierHumanoidArmCuboids)
            {
                merged["cuboids"] = JsonNode.Parse(earlierCuboids.ToJsonString())!.AsArray();
            }
            else if (incomingCuboids.Count > earlierCuboids.Count)
            {
                merged["cuboids"] = ShouldReplaceEarlierCuboidsWithIncoming(
                    (string?)incoming["id"], earlierCuboids, incomingCuboids)
                    ? JsonNode.Parse(incomingCuboids.ToJsonString())!.AsArray()
                    : MergeCuboidArraysUnionByFingerprint(earlierCuboids, incomingCuboids);
            }
            else if (incomingCuboids.Count < earlierCuboids.Count)
            {
                merged["cuboids"] = JsonNode.Parse(earlierCuboids.ToJsonString())!.AsArray();
            }
            else
            {
                merged["cuboids"] = JsonNode.Parse(incomingCuboids.ToJsonString())!.AsArray();
            }
        }

        var earlierKids = earlier["children"] as JsonArray ?? new JsonArray();
        var incomingKids = merged["children"] as JsonArray ?? new JsonArray();
        merged["children"] = MergeNestedChildrenPreferIncoming(
            earlierKids, incomingKids, dropHumanoidHatForPiglinEars, (string?)incoming["id"]);
        return merged;
    }

    /// <summary>
    /// Delegated factories lift the same part id from multiple islands; union cuboids by geometry fingerprint
    /// instead of last-wins replacement.
    /// </summary>
    public static JsonArray MergeCuboidArraysUnionByFingerprint(JsonArray earlier, JsonArray incoming)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var merged = new JsonArray();
        foreach (var source in new[] { earlier, incoming })
        {
            foreach (var cNode in source)
            {
                if (cNode is not JsonObject c)
                {
                    continue;
                }

                var fp = LiftedCuboidFingerprint(c);
                if (!seen.Add(fp))
                {
                    continue;
                }

                merged.Add(JsonNode.Parse(c.ToJsonString())!);
            }
        }

        return merged;
    }

    /// <summary>
    /// Stable fingerprint for deduplicating cuboids during union merge (shape, inflate, face mask).
    /// </summary>
    public static string LiftedCuboidFingerprint(JsonObject cuboid)
    {
        static string F(JsonObject? o, string key) =>
            o?[key]?.GetValue<double>().ToString("R", CultureInfo.InvariantCulture) ?? "0";

        static string Vec3(JsonNode? node)
        {
            if (node is JsonArray a && a.Count >= 3)
            {
                return string.Create(CultureInfo.InvariantCulture,
                    $"{a[0]!.GetValue<double>().ToString("R", CultureInfo.InvariantCulture)},{a[1]!.GetValue<double>().ToString("R", CultureInfo.InvariantCulture)},{a[2]!.GetValue<double>().ToString("R", CultureInfo.InvariantCulture)}");
            }

            if (node is JsonObject o)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{F(o, "x")},{F(o, "y")},{F(o, "z")}");
            }

            return "0,0,0";
        }

        static string FaceMask(JsonNode? node)
        {
            if (node is not JsonArray a || a.Count == 0)
            {
                return "";
            }

            var faces = new List<string>(a.Count);
            foreach (var f in a)
            {
                if (f is not null)
                {
                    faces.Add(f.GetValue<string>());
                }
            }

            faces.Sort(StringComparer.Ordinal);
            return string.Join(",", faces);
        }

        var inflate = cuboid["inflate"]?.GetValue<double>().ToString("R", CultureInfo.InvariantCulture) ?? "0";
        return string.Create(CultureInfo.InvariantCulture,
            $"{Vec3(cuboid["from"])}|{Vec3(cuboid["to"])}|{inflate}|{FaceMask(cuboid["faceMask"])}");
    }

    /// <summary>
    /// Incoming children win ordering and shallow fields per id; subtree ids only on the earlier island are appended after.
    /// When <paramref name="incomingKids"/> is empty, earlier nested parts are copied verbatim (wrapper islands).
    /// </summary>
    public static JsonArray MergeNestedChildrenPreferIncoming(JsonArray earlierKids, JsonArray incomingKids,
        bool dropHumanoidHatWhenIncomingHasPiglinEars = false, string? incomingParentPartId = null)
    {
        if (incomingKids.Count == 0)
        {
            return JsonNode.Parse(earlierKids.ToJsonString())!.AsArray();
        }

        var incomingById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var n in incomingKids)
        {
            if (n is not JsonObject io)
            {
                continue;
            }

            var kid = (string?)io["id"];
            if (!string.IsNullOrEmpty(kid))
            {
                incomingById[kid] = io;
            }
        }

        var merged = new JsonArray();
        foreach (var n in incomingKids)
        {
            if (n is not JsonObject inc)
            {
                continue;
            }

            var cid = (string?)inc["id"];
            if (string.IsNullOrEmpty(cid))
            {
                continue;
            }

            JsonObject mergedChild;
            if (FindChildPartById(earlierKids, cid) is { } prev)
            {
                mergedChild = MergeLiftedPartNodes(prev, inc);
                if (dropHumanoidHatWhenIncomingHasPiglinEars &&
                    string.Equals(cid, "hat", StringComparison.Ordinal))
                {
                    mergedChild["cuboids"] = new JsonArray();
                }
            }
            else
            {
                mergedChild = JsonNode.Parse(inc.ToJsonString())!.AsObject();
            }

            merged.Add(mergedChild);
        }

        foreach (var n in earlierKids)
        {
            if (n is not JsonObject eo)
            {
                continue;
            }

            var eid = (string?)eo["id"];
            if (string.IsNullOrEmpty(eid) || incomingById.ContainsKey(eid))
            {
                continue;
            }

            if (IsHumanoidLimbPartId(incomingParentPartId) && IsHumanoidLimbOverlayChildId(eid))
            {
                continue;
            }

            if (dropHumanoidHatWhenIncomingHasPiglinEars &&
                string.Equals(eid, "hat", StringComparison.Ordinal))
            {
                var emptyHat = JsonNode.Parse(eo.ToJsonString())!.AsObject();
                emptyHat["cuboids"] = new JsonArray();
                merged.Add(emptyHat);
                continue;
            }

            merged.Add(JsonNode.Parse(eo.ToJsonString())!);
        }

        return merged;
    }

    /// <summary>
    /// <c>createDefaultSkeletonMesh</c> and similar void helpers replace HumanoidModel limbs with thin shells;
    /// do not union the wider template cuboid from the delegated prelude island.
    /// </summary>
    private static bool ShouldPreferIncomingThinHumanoidLimbCuboids(string? partId, JsonArray incomingCuboids)
    {
        if (!IsHumanoidLimbPartId(partId) || incomingCuboids.Count != 1 ||
            incomingCuboids[0] is not JsonObject incoming ||
            incoming["from"] is not JsonArray from ||
            incoming["to"] is not JsonArray to ||
            from.Count < 1 ||
            to.Count < 1)
        {
            return false;
        }

        var spanX = Math.Abs(to[0]!.GetValue<double>() - from[0]!.GetValue<double>());
        return spanX <= 2.5;
    }

    /// <summary>
    /// Player/piglin multi-island lifts: a later overlay island can emit undersized arm cuboids (e.g. jacket UV 30,x)
    /// that must not replace the primary humanoid arm from an earlier island.
    /// </summary>
    private static bool ShouldPreferEarlierHumanoidArmCuboids(
        string? partId,
        JsonArray earlierCuboids,
        JsonArray incomingCuboids)
    {
        if (earlierCuboids.Count != 1 || incomingCuboids.Count != 1 ||
            !IsHumanoidArmPartId(partId))
        {
            return false;
        }

        return CuboidYSpan(earlierCuboids[0] as JsonObject) > CuboidYSpan(incomingCuboids[0] as JsonObject) + 1e-3;
    }

    private static bool IsHumanoidArmPartId(string? partId) => IsHumanoidLimbPartId(partId);

    private static bool IsHumanoidLimbPartId(string? partId) =>
        string.Equals(partId, "left_arm", StringComparison.Ordinal) ||
        string.Equals(partId, "right_arm", StringComparison.Ordinal) ||
        string.Equals(partId, "left_leg", StringComparison.Ordinal) ||
        string.Equals(partId, "right_leg", StringComparison.Ordinal);

    private static bool IsHumanoidLimbOverlayChildId(string? partId) =>
        string.Equals(partId, "left_sleeve", StringComparison.Ordinal) ||
        string.Equals(partId, "right_sleeve", StringComparison.Ordinal) ||
        string.Equals(partId, "left_pants", StringComparison.Ordinal) ||
        string.Equals(partId, "right_pants", StringComparison.Ordinal) ||
        string.Equals(partId, "left_foot", StringComparison.Ordinal) ||
        string.Equals(partId, "right_foot", StringComparison.Ordinal);

    private static double CuboidYSpan(JsonObject? cuboid)
    {
        if (cuboid is null ||
            cuboid["from"] is not JsonArray from ||
            cuboid["to"] is not JsonArray to ||
            from.Count < 2 ||
            to.Count < 2)
        {
            return 0;
        }

        return to[1]!.GetValue<double>() - from[1]!.GetValue<double>();
    }

    /// <summary>
    /// Host <c>createBodyLayer</c> <c>addOrReplaceChild</c> after <c>HumanoidModel.createMesh</c> (e.g. zombie villager head/body)
    /// replaces the delegated template — do not union villager robe/head cuboids with humanoid defaults.
    /// </summary>
    private static bool ShouldReplaceEarlierCuboidsWithIncoming(
        string? partId,
        JsonArray earlierCuboids,
        JsonArray incomingCuboids)
    {
        if (incomingCuboids.Count == 0 || earlierCuboids.Count == 0 || incomingCuboids.Count <= earlierCuboids.Count)
        {
            return false;
        }

        return string.Equals(partId, "head", StringComparison.Ordinal) ||
               string.Equals(partId, "body", StringComparison.Ordinal);
    }

    private static bool ShouldPreferIncomingCuboidsOverUnion(
        JsonObject incomingPart,
        JsonArray incomingCuboids)
    {
        if (incomingCuboids.Count < 3)
        {
            return false;
        }

        if (incomingPart["children"] is not JsonArray kids)
        {
            return false;
        }

        return ContainsChildPartId(kids, "left_ear") || ContainsChildPartId(kids, "right_ear");
    }

    private static bool ContainsChildPartId(JsonArray parts, string id)
    {
        foreach (var n in parts)
        {
            if (n is JsonObject j && string.Equals((string?)j["id"], id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonObject? FindChildPartById(JsonArray parts, string id)
    {
        foreach (var n in parts)
        {
            if (n is JsonObject o && string.Equals((string?)o["id"], id, StringComparison.Ordinal))
            {
                return o;
            }
        }

        return null;
    }
}
