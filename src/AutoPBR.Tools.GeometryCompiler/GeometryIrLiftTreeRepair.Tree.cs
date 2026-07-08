using System.Text.Json.Nodes;

using AutoPBR.Preview;

namespace AutoPBR.Tools.GeometryCompiler;
internal static partial class GeometryIrLiftTreeRepair
{
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

    /// <summary>
    /// Deep mesh concat lifts <c>createInnerBodyLayer</c> after <c>createOuterBodyLayer</c> and last-wins the shared
    /// <c>cube</c> id. Re-insert the outer 8×8×8 shell when inner eyes are present.
    /// </summary>
    private static void EnsureSlimeOuterBodyLayer(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "right_eye", out _))
        {
            return;
        }

        if (TryFindPartById(rootChildren, "cube", out var cubePart) &&
            cubePart is not null &&
            PartCuboidMatchesBounds(cubePart, -4, 16, -4, 4, 24, 4))
        {
            return;
        }

        if (TryFindPartById(rootChildren, "outer_cube", out var existingOuter) && existingOuter is not null)
        {
            if (existingOuter["cuboids"] is JsonArray existingCuboids && existingCuboids.Count > 0)
            {
                return;
            }

            existingOuter["cuboids"] = CreateSlimeOuterShellCuboids();
            return;
        }

        rootChildren.Insert(0, CreateSlimeOuterShellPart());
    }

    private static JsonObject CreateSlimeOuterShellPart() =>
        new()
        {
            ["id"] = "outer_cube",
            ["pose"] = ZeroPose(),
            ["cuboids"] = CreateSlimeOuterShellCuboids(),
            ["children"] = new JsonArray()
        };

    private static JsonArray CreateSlimeOuterShellCuboids() =>
        new()
        {
            new JsonObject
            {
                ["from"] = new JsonArray { -4, 16, -4 },
                ["to"] = new JsonArray { 4, 24, 4 },
                ["uvOrigin"] = new JsonArray { 0, 0 },
                ["textureKey"] = "#skin",
                ["liftKind"] = "exact",
                ["previewDepthLayer"] = "translucentOverlay",
                ["provenance"] = "javap lift repair SlimeModel.createOuterBodyLayer"
            }
        };

    private static JsonObject ZeroPose() =>
        new()
        {
            ["translation"] = new JsonArray { 0, 0, 0 },
            ["rotationEulerRad"] = new JsonArray { 0, 0, 0 },
            ["eulerOrder"] = "XYZ"
        };

    private static bool PartCuboidMatchesBounds(JsonObject part, float fx, float fy, float fz, float tx, float ty, float tz)
    {
        if (part["cuboids"] is not JsonArray cuboids || cuboids.Count == 0)
        {
            return false;
        }

        if (cuboids[0] is not JsonObject c)
        {
            return false;
        }

        return TryReadVec3(c["from"], out var x0, out var y0, out var z0) &&
               TryReadVec3(c["to"], out var x1, out var y1, out var z1) &&
               Math.Abs(x0 - fx) < 0.01 && Math.Abs(y0 - fy) < 0.01 && Math.Abs(z0 - fz) < 0.01 &&
               Math.Abs(x1 - tx) < 0.01 && Math.Abs(y1 - ty) < 0.01 && Math.Abs(z1 - tz) < 0.01;
    }

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
                .ThenBy(occurrences.IndexOf)
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
