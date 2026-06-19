using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>T0 helpers for Phase 1A binding / hierarchy lift probes (jar-gated).</summary>
internal static class MeshLiftHierarchyTestSupport
{
    internal static int MaxTreeDepth(JsonArray roots)
    {
        var max = 0;
        foreach (var node in roots)
        {
            if (node is JsonObject p)
            {
                max = Math.Max(max, PartDepth(p, 1));
            }
        }

        return max;
    }

    private static int PartDepth(JsonObject part, int depth)
    {
        if (part["children"] is not JsonArray kids || kids.Count == 0)
        {
            return depth;
        }

        var max = depth;
        foreach (var ch in kids)
        {
            if (ch is JsonObject co)
            {
                max = Math.Max(max, PartDepth(co, depth + 1));
            }
        }

        return max;
    }

    internal static bool LegsAreRootSiblings(JsonArray roots)
    {
        var legIds = new[] { "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg" };
        return legIds.All(id => roots.OfType<JsonObject>().Any(p => string.Equals((string?)p["id"], id, StringComparison.Ordinal)));
    }

    internal static bool LegsNestedUnderBody(JsonArray roots)
    {
        var body = FindPartById(roots, "body");
        if (body is null || body["children"] is not JsonArray kids)
        {
            return false;
        }

        var legIds = new[] { "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg" };
        return legIds.Any(id => kids.OfType<JsonObject>().Any(p => string.Equals((string?)p["id"], id, StringComparison.Ordinal)));
    }

    /// <summary>Phase 1A nested pilots: legs off mesh root (under body, bone, or deeper).</summary>
    internal static bool StandardQuadrupedLegsLiftedOffRoot(JsonArray roots)
    {
        if (LegsAreRootSiblings(roots))
        {
            return false;
        }

        var legIds = new[] { "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg" };
        return legIds.All(id => FindPartById(roots, id) is not null);
    }

    internal static JsonObject? FindPartById(JsonArray roots, string id)
    {
        foreach (var r in roots)
        {
            if (r is JsonObject ro && TryFindPartById(ro, id, out var found))
            {
                return found;
            }
        }

        return null;
    }

    private static bool TryFindPartById(JsonObject part, string id, out JsonObject? found)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            found = part;
            return true;
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co && TryFindPartById(co, id, out found))
                {
                    return true;
                }
            }
        }

        found = null;
        return false;
    }
}
