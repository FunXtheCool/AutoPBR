using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>Repairs known flat IR trees before parity emit (lift ordering gaps).</summary>
internal static partial class GeometryIrPartTreeRepair
{
    private static void CollapseInnerBodyUnderBody(JsonArray rootChildren)

    {

        foreach (var n in rootChildren)

        {

            if (n is JsonObject part)

            {

                CollapseInnerBodyRecursive(part);

            }

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

        TrimInnerBodyFleeceCuboidFromBody(bodyPart);

        if (bodyPart["children"] is not JsonArray bodyKids)

        {

            return;

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
