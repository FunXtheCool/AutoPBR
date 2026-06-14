using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>Repairs known flat IR trees before parity emit (lift ordering gaps).</summary>
internal static partial class GeometryIrPartTreeRepair
{
    /// <summary>
    /// Feline <c>createBodyMesh</c> binds <c>tail1</c> and <c>tail2</c> on <c>getRoot()</c> with entity-space
    /// <c>PartPose.offset</c> (reference_java flat siblings). Reparenting without pose rebase doubles tail2 world offset.
    /// </summary>
    private static bool ShouldSkipFelineFlatTailReparent(
        string childId,
        string parentId,
        JsonArray rootChildren,
        string? officialJvmName)
    {
        if (!string.Equals(childId, "tail2", StringComparison.Ordinal) ||
            !string.Equals(parentId, "tail1", StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(officialJvmName) &&
            officialJvmName.Contains(".animal.feline.", StringComparison.Ordinal))
        {
            return true;
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

    /// <summary>
    /// HappyGhastModel lift keeps a nested <c>inner_body</c> under <c>body</c> and a duplicate fleece cuboid at
    /// <c>uvOrigin[1]=32</c>. Java reference bakes omit both — trim only when that exact pattern is present.
    /// </summary>
    private static void CollapseInnerBodyUnderBody(JsonArray rootChildren)
    {
        if (!BodyContainsInnerBodyChild(rootChildren))
        {
            return;
        }

        foreach (var n in rootChildren)
        {
            if (n is JsonObject part)
            {
                CollapseInnerBodyRecursive(part);
            }
        }
    }

    private static bool BodyContainsInnerBodyChild(JsonArray siblings)
    {
        foreach (var n in siblings)
        {
            if (n is not JsonObject o)
            {
                continue;
            }

            if (string.Equals((string?)o["id"], "body", StringComparison.Ordinal) &&
                o["children"] is JsonArray kids &&
                kids.Any(ch => ch is JsonObject co &&
                               string.Equals((string?)co["id"], "inner_body", StringComparison.Ordinal)))
            {
                return true;
            }

            if (o["children"] is JsonArray nested && BodyContainsInnerBodyChild(nested))
            {
                return true;
            }
        }

        return false;
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



        var removedInnerBodyChild = false;
        for (var i = bodyKids.Count - 1; i >= 0; i--)

        {

            if (bodyKids[i] is JsonObject ch &&

                string.Equals((string?)ch["id"], "inner_body", StringComparison.Ordinal))

            {

                bodyKids.RemoveAt(i);
                removedInnerBodyChild = true;

                break;

            }

        }

        // Only trim fleece duplicate when an inner_body child was present.
        // This avoids stripping legitimate body-shell cuboids on flat quadrupeds.
        if (removedInnerBodyChild)
        {
            TrimInnerBodyFleeceCuboidFromBody(bodyPart);
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
        if (!TryFindDirectChildSlotRecursive(rootChildren, childId, out var childArray, out var childIdx, out var childNode) ||
            childNode is null ||
            childArray is null ||
            childIdx < 0)
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
            if (!ReferenceEquals(childArray, parentKids))
            {
                childArray.RemoveAt(childIdx);
            }

            return;
        }

        parentKids.Add(childNode.DeepClone());
        childArray.RemoveAt(childIdx);
    }

    private static bool TryFindDirectChildSlotRecursive(
        JsonArray siblings,
        string id,
        out JsonArray? parentArray,
        out int index,
        out JsonObject? node)
    {
        for (var i = 0; i < siblings.Count; i++)
        {
            if (siblings[i] is JsonObject o && string.Equals((string?)o["id"], id, StringComparison.Ordinal))
            {
                parentArray = siblings;
                index = i;
                node = o;
                return true;
            }
        }

        foreach (var n in siblings)
        {
            if (n is JsonObject o &&
                o["children"] is JsonArray kids &&
                TryFindDirectChildSlotRecursive(kids, id, out parentArray, out index, out node))
            {
                return true;
            }
        }

        parentArray = null;
        index = -1;
        node = null;
        return false;
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

    /// <summary>
    /// <c>DonkeyModel.createBodyLayer</c> lifts with <c>root</c> at <c>T(0,24,0)</c> (layer-definition anchor only).
    /// Child <c>PartPose</c> values already match <c>AbstractEquineModel.createBodyMesh</c> entity space — drop the root offset.
    /// </summary>
    private static bool ShouldZeroEquineCreateBodyLayerRootOffset(
        string? officialJvmName,
        JsonObject shardDoc,
        JsonObject rootPart)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName) ||
            !officialJvmName.Contains(".animal.equine.", StringComparison.Ordinal) ||
            officialJvmName.Contains("AbstractEquine", StringComparison.Ordinal))
        {
            return false;
        }

        var factory = shardDoc["factoryMethod"]?.GetValue<string>();
        if (!string.Equals(factory, "createBodyLayer", StringComparison.Ordinal) &&
            !string.Equals(factory, "createBabyLayer", StringComparison.Ordinal))
        {
            return false;
        }

        if (rootPart["pose"] is not JsonObject pose ||
            pose["translation"] is not JsonArray tr ||
            tr.Count < 2)
        {
            return false;
        }

        var dy = tr[1]?.GetValue<double>() ?? 0;
        return Math.Abs(dy) > 0.5;
    }

    private static void ZeroRootTranslation(JsonObject rootPart)
    {
        if (rootPart["pose"] is not JsonObject pose ||
            pose["translation"] is not JsonArray tr ||
            tr.Count < 3)
        {
            return;
        }

        tr[0] = 0;
        tr[1] = 0;
        tr[2] = 0;
    }

    /// <summary>
    /// Java <c>createBodyLayer</c> for camel and armadillo keeps legs on <c>getRoot()</c> with entity-space
    /// <c>PartPose.offset</c> (leg pose Y matches body) while head/tail nest under <c>body</c>.
    /// </summary>
    private static bool IsEntitySpaceFlatRootLegMeshFamily(string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        return officialJvmName.Contains(".animal.camel.", StringComparison.Ordinal) ||
               officialJvmName.Contains(".animal.armadillo.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Quadruped factories that bind legs under <c>body</c> in Java (nested hierarchy pilots). When the lifter or
    /// compiler hoist leaves them as flat root siblings, preview repair must renest — not skip like creeper flat bake.
    /// </summary>
    private static bool IsNestedBodyLegQuadrupedMeshFamily(string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        return officialJvmName.Contains(".animal.axolotl.", StringComparison.Ordinal) ||
               officialJvmName.Contains(".animal.rabbit.", StringComparison.Ordinal) ||
               officialJvmName.Contains(".animal.sniffer.", StringComparison.Ordinal) ||
               officialJvmName.Contains(".animal.wolf.", StringComparison.Ordinal) ||
               officialJvmName.Contains(".animal.llama.", StringComparison.Ordinal) ||
               officialJvmName.Contains(".animal.equine.BabyDonkey", StringComparison.Ordinal) ||
               officialJvmName.Contains(".monster.dragon.EnderDragon", StringComparison.Ordinal);
    }

    /// <summary>
    /// When the head stack lives under <c>body</c> but legs are still root siblings, the lifter emitted
    /// body-relative leg offsets at root (see <c>BabyDonkeyModel</c>). Reparent legs under body; do not treat as flat quadruped bake.
    /// Documented: runtime-ir-preview-plan § Baby JVM family; vanilla-preview-parity § Baby equine pass 3.
    /// </summary>
    private static bool HeadStackNestedUnderBody(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "body", out var bodyNode) || bodyNode is null ||
            bodyNode["children"] is not JsonArray bodyKids)
        {
            return false;
        }

        foreach (var headId in new[] { "head_parts", "head", "neck", "neck_r1" })
        {
            foreach (var n in bodyKids)
            {
                if (n is JsonObject o && string.Equals((string?)o["id"], headId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsFlatRootSibling(JsonArray rootChildren, string partId)
    {
        foreach (var n in rootChildren)
        {
            if (n is JsonObject o && string.Equals((string?)o["id"], partId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

}
