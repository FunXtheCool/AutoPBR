using System.Globalization;
using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Semantic checks on lifted part trees before marking shards <c>ok</c>.
/// </summary>
internal static class GeometryIrLiftTreeValidator
{
    private static readonly HashSet<(string ParentId, string ChildId)> KnownNestedPairs = new()
    {
        ("head", "beak"),
        ("head", "red_thing"),
        ("head", "hat"),
        ("head", "nose"),
        ("head", "mole"),
        ("head", "top_gills"),
        ("head", "left_gills"),
        ("head", "right_gills"),
        ("head", "nose"),
        ("body", "rods"),
        ("tail1", "tail2"),
    };

    private const double BboxEpsilon = 1e-4;

    public static GeometryIrStructuralValidator.Result ValidateRoots(
        JsonArray? roots,
        string? contextJvmName)
    {
        var issues = new List<GeometryIrStructuralValidator.Issue>();
        var ctx = contextJvmName ?? "<lift>";
        if (roots is null || roots.Count == 0)
        {
            issues.Add(new GeometryIrStructuralValidator.Issue(ctx, "empty_roots", "lift produced no root parts"));
            return new GeometryIrStructuralValidator.Result(false, issues);
        }

        var partIds = new HashSet<string>(StringComparer.Ordinal);
        var crossPartCuboidKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        var bboxOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var root in roots)
        {
            if (root is JsonObject ro)
            {
                WalkPart(ro, $"{ctx}/roots", partIds, crossPartCuboidKeys, bboxOwners, issues);
            }
        }

        if (roots[0] is JsonObject firstRoot && firstRoot["children"] is JsonArray rootKids)
        {
            ValidateFlatNestedAtRoot(rootKids, ctx, issues);
        }

        return new GeometryIrStructuralValidator.Result(issues.Count == 0, issues);
    }

    private static void WalkPart(
        JsonObject part,
        string path,
        HashSet<string> partIds,
        Dictionary<string, string> crossPartCuboidKeys,
        Dictionary<string, string> bboxOwners,
        List<GeometryIrStructuralValidator.Issue> issues)
    {
        var partId = (string?)part["id"] ?? "<no-id>";
        var partPath = $"{path}/{partId}";
        if (!partIds.Add(partId))
        {
            issues.Add(new GeometryIrStructuralValidator.Issue(partPath, "duplicate_part_id",
                $"duplicate part id '{partId}' in tree"));
        }

        if (part["cuboids"] is JsonArray cuboids)
        {
            foreach (var c in cuboids)
            {
                if (c is not JsonObject cuboid)
                {
                    continue;
                }

                var crossKey = CuboidGeometryKey(cuboid);
                if (crossPartCuboidKeys.TryGetValue(crossKey, out var priorPart) &&
                    !string.Equals(priorPart, partId, StringComparison.Ordinal) &&
                    ShouldReportCrossPartCuboidDuplicate(priorPart, partId, cuboid))
                {
                    issues.Add(new GeometryIrStructuralValidator.Issue(partPath, "duplicate_cuboid_across_parts",
                        $"cuboid matches '{priorPart}' ({crossKey})"));
                }
                else if (!string.IsNullOrEmpty(crossKey))
                {
                    crossPartCuboidKeys[crossKey] = partId;
                }

                var bboxKey = BboxKey(cuboid);
                if (bboxKey.Length > 0)
                {
                    if (bboxOwners.TryGetValue(bboxKey, out var owner) &&
                        !string.Equals(owner, partId, StringComparison.Ordinal) &&
                        ShouldReportBboxCollision(owner, partId, cuboid))
                    {
                        issues.Add(new GeometryIrStructuralValidator.Issue(partPath, "cuboid_bbox_collision",
                            $"bbox matches part '{owner}'"));
                    }
                    else
                    {
                        bboxOwners[bboxKey] = partId;
                    }
                }
            }
        }

        if (part["children"] is JsonArray children)
        {
            foreach (var ch in children)
            {
                if (ch is JsonObject child)
                {
                    WalkPart(child, partPath, partIds, crossPartCuboidKeys, bboxOwners, issues);
                }
            }
        }
    }

    private static void ValidateFlatNestedAtRoot(
        JsonArray rootChildren,
        string ctx,
        List<GeometryIrStructuralValidator.Issue> issues)
    {
        var rootIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ch in rootChildren)
        {
            if (ch is JsonObject o && o["id"]?.GetValue<string>() is { } id)
            {
                rootIds.Add(id);
            }
        }

        var flatCount = 0;
        foreach (var (parent, child) in KnownNestedPairs)
        {
            if (rootIds.Contains(parent) && rootIds.Contains(child))
            {
                flatCount++;
            }
        }

        if (flatCount > 0)
        {
            issues.Add(new GeometryIrStructuralValidator.Issue(ctx, "flat_nested_part_at_root",
                $"{flatCount} known child part(s) are root siblings of their parent"));
        }
    }

    /// <summary>
    /// Shared limb templates (same addBox dims on front/hind) are expected; stolen torso boxes are not.
    /// </summary>
    private static bool ShouldReportCrossPartCuboidDuplicate(string priorPartId, string partId, JsonObject cuboid)
    {
        _ = cuboid;
        if (IsSharedLimbTemplatePair(priorPartId, partId))
        {
            return false;
        }

        if (IsPlayerOverlayPart(priorPartId) || IsPlayerOverlayPart(partId))
        {
            return false;
        }

        return string.Equals(priorPartId, "body", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(partId, "body", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Bbox reuse across limbs is expected; reuse on a leg from torso/head is the stolen-cuboid failure mode.
    /// </summary>
    private static bool ShouldReportBboxCollision(string priorPartId, string partId, JsonObject cuboid)
    {
        _ = cuboid;
        if (IsSharedLimbTemplatePair(priorPartId, partId))
        {
            return false;
        }

        if (IsPlayerOverlayPart(priorPartId) || IsPlayerOverlayPart(partId))
        {
            return false;
        }

        if (IsSaddleHarnessPart(partId) && string.Equals(priorPartId, "body", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(priorPartId, "body", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(partId, "body", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSaddleHarnessPart(string partId) =>
        string.Equals(partId, "saddle", StringComparison.Ordinal) ||
        string.Equals(partId, "reins", StringComparison.Ordinal) ||
        string.Equals(partId, "bridle", StringComparison.Ordinal) ||
        string.Equals(partId, "harness", StringComparison.Ordinal) ||
        partId.Contains("saddle", StringComparison.OrdinalIgnoreCase);

    private static bool IsPlayerOverlayPart(string partId) =>
        partId.Contains("jacket", StringComparison.OrdinalIgnoreCase) ||
        partId.Contains("sleeve", StringComparison.OrdinalIgnoreCase) ||
        partId.Contains("pants", StringComparison.OrdinalIgnoreCase) ||
        partId.Contains("hat", StringComparison.OrdinalIgnoreCase) ||
        partId.Contains("cloak", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(partId, "head", StringComparison.OrdinalIgnoreCase);

    private static bool IsSharedLimbTemplatePair(string a, string b)
    {
        if (!IsLimbPartId(a) || !IsLimbPartId(b))
        {
            return false;
        }

        return !string.Equals(a, b, StringComparison.Ordinal);
    }

    private static bool IsLimbPartId(string partId) =>
        partId.Contains("leg", StringComparison.OrdinalIgnoreCase) ||
        partId.Contains("arm", StringComparison.OrdinalIgnoreCase) ||
        partId.Contains("wing", StringComparison.OrdinalIgnoreCase);

    private static bool IsTorsoOrHeadPart(string partId) =>
        string.Equals(partId, "body", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(partId, "head", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(partId, "torso", StringComparison.OrdinalIgnoreCase) ||
        partId.Contains("body", StringComparison.OrdinalIgnoreCase);

    private static bool IsThinSheetCuboid(JsonObject cuboid)
    {
        if (!TryReadVec3(cuboid["from"], out var fx, out var fy, out var fz) ||
            !TryReadVec3(cuboid["to"], out var tx, out var ty, out var tz))
        {
            return false;
        }

        var dx = Math.Abs(tx - fx);
        var dy = Math.Abs(ty - fy);
        var dz = Math.Abs(tz - fz);
        return dx < BboxEpsilon || dy < BboxEpsilon || dz < BboxEpsilon;
    }

    private static string CuboidGeometryKey(JsonObject cuboid)
    {
        var from = FormatVec(cuboid["from"]);
        var to = FormatVec(cuboid["to"]);
        var uv = FormatVec(cuboid["uvOrigin"], 2);
        var mirror = cuboid["mirrorU"]?.GetValue<bool>() == true ? "m1" : "m0";
        return $"{from}|{to}|{uv}|{mirror}";
    }

    private static string BboxKey(JsonObject cuboid)
    {
        if (!TryReadVec3(cuboid["from"], out var fx, out var fy, out var fz) ||
            !TryReadVec3(cuboid["to"], out var tx, out var ty, out var tz))
        {
            return "";
        }

        return string.Create(CultureInfo.InvariantCulture,
            $"{Round(fx)},{Round(fy)},{Round(fz)}|{Round(tx)},{Round(ty)},{Round(tz)}");
    }

    private static string FormatVec(JsonNode? node, int expected = 3)
    {
        if (node is not JsonArray arr)
        {
            return "?";
        }

        var parts = new List<string>(expected);
        for (var i = 0; i < expected && i < arr.Count; i++)
        {
            parts.Add(TryReadJsonNumber(arr[i], out var d)
                ? Round(d).ToString(CultureInfo.InvariantCulture)
                : "?");
        }

        return string.Join(",", parts);
    }

    private static bool TryReadVec3(JsonNode? node, out double x, out double y, out double z)
    {
        x = y = z = 0;
        if (node is not JsonArray arr || arr.Count < 3)
        {
            return false;
        }

        return TryReadJsonNumber(arr[0], out x) &&
               TryReadJsonNumber(arr[1], out y) &&
               TryReadJsonNumber(arr[2], out z);
    }

    private static bool TryReadJsonNumber(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue jv)
        {
            return false;
        }

        if (jv.TryGetValue<double>(out value))
        {
            return true;
        }

        if (jv.TryGetValue<float>(out var f))
        {
            value = f;
            return true;
        }

        return false;
    }

    private static double Round(double v) => Math.Round(v, 4);
}
