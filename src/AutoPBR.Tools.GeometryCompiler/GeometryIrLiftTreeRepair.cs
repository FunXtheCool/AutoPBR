using System.Text.Json.Nodes;

using AutoPBR.Core.Preview;

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
        ("right_sleeve", "right_arm"),
        ("left_sleeve", "left_arm"),
        ("jacket", "body"),
        ("left_pants", "left_leg"),
        ("right_pants", "right_leg"),
    ];

    /// <param name="roots">Lifted mesh part tree roots to repair in place.</param>
    /// <param name="officialJvmName">
    /// When set (geometry compiler batch / pipeline), hoists standard quadruped legs from under <c>body</c> for flat
    /// vanilla bakes (cow, creeper, pig, …). Omitted for direct <see cref="JavapFloatGeometryMeshLift.TryLift"/> probes
    /// so nested pilots (feline, wolf, axolotl, …) keep legs under <c>body</c>.
    /// </param>
    public static JsonArray Apply(JsonArray roots, string? officialJvmName = null)
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
                GeometryIrPlayerMeshParityRepair.Apply(rootKids);
                TrimHumanoidDelegateOverlayCuboids(rootKids);
                TrimHumanoidHatHeadCanonicalBounds(rootKids);
                RemoveDuplicateInflatedBodyOverlayCuboids(rootKids);
                RemoveCopperGolemSetupAnimRotationHelperParts(rootKids, officialJvmName);
                HoistFelineFlatTail2ToRoot(rootKids);
                if (ShouldHoistStandardQuadrupedLegsToRoot(rootKids, officialJvmName))
                {
                    HoistStandardQuadrupedLegsFromBody(rootKids);
                }

                RemoveDuplicatePartIdsPreferCuboids(rootKids);
                EnsureSlimeOuterBodyLayer(rootKids);
                GeometryIrPartTreeRepair.RepairTexCropUvSpanDuplicateAnchors(rootKids);
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
    /// (piglin, player, drowned, …). Delegated <c>HumanoidModel.createMesh</c> hosts can also retain a full player
    /// overlay kit from deep concat pollution — drop it unless arms are player-scale (4px wide).
    /// Frog feet are real parts and are kept (no jacket/pants overlay kit).
    /// </summary>
    private static void RemovePlayerMeshInternalParts(JsonArray rootChildren)
    {
        var hasJacket = TryFindPartById(rootChildren, "jacket", out _);
        var hasWaist = TryFindPartById(rootChildren, "waist", out _);
        var hasInnerBody = TryFindPartById(rootChildren, "inner_body", out _);
        if (!hasJacket && !hasWaist && !hasInnerBody)
        {
            RemoveLimbOverlayChildren(rootChildren);
            return;
        }

        if (hasJacket && UsesPlayerScaleHumanoidArms(rootChildren))
        {
            RemovePartIdsFromForest(rootChildren, ["waist", "inner_body", "left_foot", "right_foot"]);
            return;
        }

        RemovePartIdsFromForest(rootChildren,
        [
            "jacket",
            "waist",
            "inner_body",
            "left_pants",
            "right_pants",
            "left_sleeve",
            "right_sleeve",
            "left_foot",
            "right_foot",
        ]);
        RemoveLimbOverlayChildren(rootChildren);
    }

    private static bool UsesPlayerScaleHumanoidArms(JsonArray rootChildren)
    {
        foreach (var armId in new[] { "left_arm", "right_arm" })
        {
            if (!TryFindPartById(rootChildren, armId, out var arm) || arm is null ||
                arm["cuboids"] is not JsonArray cuboids ||
                cuboids.Count == 0 ||
                cuboids[0] is not JsonObject cuboid ||
                cuboid["from"] is not JsonArray from ||
                cuboid["to"] is not JsonArray to ||
                from.Count < 1 ||
                to.Count < 1)
            {
                continue;
            }

            var spanX = Math.Abs(to[0]!.GetValue<double>() - from[0]!.GetValue<double>());
            if (spanX >= 3.5)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveLimbOverlayChildren(JsonArray parts)
    {
        foreach (var n in parts)
        {
            if (n is not JsonObject part)
            {
                continue;
            }

            if (part["children"] is JsonArray kids)
            {
                for (var i = kids.Count - 1; i >= 0; i--)
                {
                    if (kids[i] is JsonObject child &&
                        child["id"]?.GetValue<string>() is { } childId &&
                        IsHumanoidLimbOverlayChildId(childId))
                    {
                        kids.RemoveAt(i);
                    }
                }

                RemoveLimbOverlayChildren(kids);
            }
        }
    }

    private static bool IsHumanoidLimbOverlayChildId(string id) =>
        id is "left_sleeve" or "right_sleeve" or "left_pants" or "right_pants" or "left_foot" or "right_foot";

    /// <summary>
    /// Thin-limb humanoid delegates (skeleton/stray/bogged) keep one head/body cuboid in reference_java; shallow
    /// <c>HumanoidModel.createMesh</c> can still union hat/jacket overlay boxes onto those parts.
    /// </summary>
    private static void TrimHumanoidDelegateOverlayCuboids(JsonArray rootChildren)
    {
        if (!UsesThinHumanoidLimbs(rootChildren))
        {
            return;
        }

        TrimDelegateHeadOverlayCuboids(rootChildren);
        TrimDelegateBodyOverlayCuboids(rootChildren);
        TrimDelegateLimbOverlayCuboids(rootChildren);
    }

    private static bool UsesThinHumanoidLimbs(JsonArray rootChildren)
    {
        foreach (var armId in new[] { "left_arm", "right_arm" })
        {
            if (!TryFindPartById(rootChildren, armId, out var arm) || arm is null ||
                arm["cuboids"] is not JsonArray cuboids ||
                cuboids.Count == 0 ||
                cuboids[0] is not JsonObject cuboid ||
                cuboid["from"] is not JsonArray from ||
                cuboid["to"] is not JsonArray to ||
                from.Count < 1 ||
                to.Count < 1)
            {
                continue;
            }

            var spanX = Math.Abs(to[0]!.GetValue<double>() - from[0]!.GetValue<double>());
            if (spanX <= 2.5)
            {
                return true;
            }
        }

        return false;
    }

    private static void TrimHumanoidHatHeadCanonicalBounds(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "head", out var head) || head is null ||
            head["children"] is not JsonArray headKids ||
            !TryFindPartById(headKids, "hat", out var hat) || hat is null ||
            hat["cuboids"] is not JsonArray hatCuboids || hatCuboids.Count == 0 ||
            hatCuboids[0] is not JsonObject hatCuboid ||
            head["cuboids"] is not JsonArray headCuboids || headCuboids.Count == 0 ||
            headCuboids[0] is not JsonObject headCuboid ||
            !IsCanonicalHumanoidHeadCuboid(hatCuboid) ||
            IsCanonicalHumanoidHeadCuboid(headCuboid))
        {
            return;
        }

        CopyCuboidBounds(hatCuboid, headCuboid, 0, 0);
        headCuboid.Remove("inflate");
    }

    private static bool IsCanonicalHumanoidHeadCuboid(JsonObject cuboid)
    {
        if (cuboid["from"] is not JsonArray from || cuboid["to"] is not JsonArray to ||
            from.Count < 3 || to.Count < 3)
        {
            return false;
        }

        return Math.Abs(from[0]!.GetValue<double>() + 4) < 0.05 &&
               Math.Abs(from[1]!.GetValue<double>() + 8) < 0.05 &&
               Math.Abs(from[2]!.GetValue<double>() + 4) < 0.05 &&
               Math.Abs(to[0]!.GetValue<double>() - 4) < 0.05 &&
               Math.Abs(to[1]!.GetValue<double>()) < 0.05 &&
               Math.Abs(to[2]!.GetValue<double>() - 4) < 0.05;
    }

    private static void CopyCuboidBounds(JsonObject source, JsonObject target, int uvU, int uvV)
    {
        target["from"] = source["from"]?.DeepClone();
        target["to"] = source["to"]?.DeepClone();
        target["uvOrigin"] = new JsonArray(uvU, uvV);
    }

    private static void RemoveDuplicateInflatedBodyOverlayCuboids(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "body", out var body) || body is null ||
            body["cuboids"] is not JsonArray cuboids)
        {
            return;
        }

        for (var i = cuboids.Count - 1; i >= 0; i--)
        {
            if (cuboids[i] is not JsonObject inflated || inflated["inflate"] is null)
            {
                continue;
            }

            foreach (var other in cuboids)
            {
                if (other is not JsonObject plain || plain["inflate"] is not null ||
                    !CuboidBoundsEqual(plain, inflated))
                {
                    continue;
                }

                inflated.Remove("inflate");
                break;
            }
        }
    }

    private static bool CuboidBoundsEqual(JsonObject left, JsonObject right)
    {
        if (left["from"] is not JsonArray lf || right["from"] is not JsonArray rf ||
            left["to"] is not JsonArray lt || right["to"] is not JsonArray rt ||
            lf.Count < 3 || rf.Count < 3 || lt.Count < 3 || rt.Count < 3)
        {
            return false;
        }

        for (var i = 0; i < 3; i++)
        {
            if (Math.Abs(lf[i]!.GetValue<double>() - rf[i]!.GetValue<double>()) > 0.01 ||
                Math.Abs(lt[i]!.GetValue<double>() - rt[i]!.GetValue<double>()) > 0.01)
            {
                return false;
            }
        }

        return true;
    }

    private static void TrimDelegateHeadOverlayCuboids(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "head", out var head) || head is null ||
            head["cuboids"] is not JsonArray headCuboids ||
            headCuboids.Count <= 1)
        {
            return;
        }

        var hatHasCuboids = HeadHasHatWithCuboids(head);
        if (!hatHasCuboids)
        {
            return;
        }

        for (var i = headCuboids.Count - 1; i >= 0; i--)
        {
            if (headCuboids[i] is not JsonObject cub)
            {
                continue;
            }

            if (cub["inflate"] is not null ||
                cub["uvOrigin"] is JsonArray { Count: >= 2 } uv &&
                Math.Abs(uv[1]!.GetValue<double>() - 32) < 0.01)
            {
                headCuboids.RemoveAt(i);
            }
        }
    }

    private static bool HeadHasHatWithCuboids(JsonObject head)
    {
        if (head["children"] is not JsonArray headKids)
        {
            return false;
        }

        foreach (var kid in headKids)
        {
            if (kid is JsonObject hat &&
                hat["id"]?.GetValue<string>() is "hat" &&
                hat["cuboids"] is JsonArray { Count: > 0 })
            {
                return true;
            }
        }

        return false;
    }

    private static void TrimDelegateBodyOverlayCuboids(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "body", out var body) || body is null ||
            body["cuboids"] is not JsonArray bodyCuboids ||
            bodyCuboids.Count <= 1)
        {
            return;
        }

        JsonObject? keep = null;
        var keepSpan = 0.0;
        foreach (var cubNode in bodyCuboids)
        {
            if (cubNode is not JsonObject cub || cub["inflate"] is not null)
            {
                continue;
            }

            var span = CuboidAxisSpan(cub, 1);
            if (span > keepSpan)
            {
                keepSpan = span;
                keep = cub;
            }
        }

        if (keep is null)
        {
            return;
        }

        body["cuboids"] = new JsonArray { JsonNode.Parse(keep.ToJsonString())! };
    }

    private static void TrimDelegateLimbOverlayCuboids(JsonArray rootChildren)
    {
        foreach (var limbId in new[] { "left_arm", "right_arm", "left_leg", "right_leg" })
        {
            if (!TryFindPartById(rootChildren, limbId, out var limb) || limb is null ||
                limb["cuboids"] is not JsonArray cuboids ||
                cuboids.Count <= 1)
            {
                continue;
            }

            JsonObject? keep = null;
            foreach (var cubNode in cuboids)
            {
                if (cubNode is not JsonObject cub || cub["inflate"] is not null)
                {
                    continue;
                }

                if (CuboidAxisSpan(cub, 0) <= 2.5)
                {
                    keep = cub;
                    break;
                }
            }

            keep ??= cuboids[0] as JsonObject;
            if (keep is not null)
            {
                limb["cuboids"] = new JsonArray { JsonNode.Parse(keep.ToJsonString())! };
            }
        }
    }

    private static double CuboidAxisSpan(JsonObject cuboid, int axis)
    {
        if (cuboid["from"] is not JsonArray from ||
            cuboid["to"] is not JsonArray to ||
            from.Count <= axis ||
            to.Count <= axis)
        {
            return 0;
        }

        return Math.Abs(to[axis]!.GetValue<double>() - from[axis]!.GetValue<double>());
    }

    private static void RemoveCopperGolemSetupAnimRotationHelperParts(JsonArray rootChildren, string? officialJvmName)
    {
        if (officialJvmName is null ||
            !officialJvmName.Contains(".golem.CopperGolemModel", StringComparison.Ordinal))
        {
            return;
        }

        RemovePartIdsFromForest(rootChildren,
        [
            "right_arm_r1",
            "left_arm_r1",
            "body_r1",
            "right_leg_r1",
            "left_leg_r1",
            "rightItem",
        ]);
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
    /// HappyGhastModel lift keeps nested <c>inner_body</c> under <c>body</c> and a duplicate fleece cuboid at
    /// <c>uvOrigin[1]=32</c>. Java reference bakes omit both — never hoist inner_body cuboids into visible output.
    /// </summary>
    private static void CollapseInnerBodyUnderBody(JsonArray rootChildren)
    {
        if (!BodyContainsInnerBodyChild(rootChildren))
        {
            return;
        }

        foreach (var n in rootChildren)
        {
            if (n is not JsonObject part)
            {
                continue;
            }

            CollapseInnerBodyRecursive(part);
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

            if (cub["uvOrigin"] is JsonArray { Count: >= 2 } uv &&
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

    private static bool ShouldHoistStandardQuadrupedLegsToRoot(JsonArray rootChildren, string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        if (IsNestedBodyLegQuadrupedMeshFamily(officialJvmName))
        {
            return false;
        }

        if (officialJvmName.Contains(".animal.feline.", StringComparison.Ordinal) ||
            officialJvmName.Contains(".animal.wolf.", StringComparison.Ordinal) ||
            officialJvmName.Contains(".animal.llama.", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryFindPartById(rootChildren, "body", out var body) || body is null ||
            body["children"] is not JsonArray bodyKids)
        {
            return false;
        }

        foreach (var kid in bodyKids)
        {
            if (kid is JsonObject child &&
                child["id"]?.GetValue<string>() is { } id &&
                IsStandardQuadrupedLegPartId(id))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNestedBodyLegQuadrupedMeshFamily(string officialJvmName) =>
        officialJvmName.Contains(".animal.axolotl.", StringComparison.Ordinal) ||
        officialJvmName.Contains(".animal.rabbit.", StringComparison.Ordinal) ||
        officialJvmName.Contains(".animal.sniffer.", StringComparison.Ordinal) ||
        officialJvmName.Contains(".animal.equine.BabyDonkey", StringComparison.Ordinal) ||
        officialJvmName.Contains(".monster.dragon.EnderDragon", StringComparison.Ordinal);

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
