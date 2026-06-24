using System.Text.Json;
using System.Text.Json.Nodes;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Repairs known flat IR trees before parity emit (lift ordering gaps).</summary>
internal static partial class GeometryIrPartTreeRepair
{
    private static readonly (string ChildId, string ParentId)[] GlobalReparentRules =

    [

        ("beak", "head"),

        ("red_thing", "head"),

        ("hat", "head"),

        ("nose", "head"),

        ("left_ear", "head"),

        ("right_ear", "head"),

        ("head_r1", "head"),

        ("neck_r1", "head_parts"),

        ("head", "head_parts"),

        ("mole", "head"),

        ("top_gills", "head"),

        ("left_gills", "head"),

        ("right_gills", "head"),

        ("right_leg_r1", "right_hind_leg"),

        ("left_leg_r1", "left_hind_leg"),

        ("rods", "body"),

        ("tail2", "tail1"),

        ("tail_r1", "tail"),

        ("wind_bottom", "wind_body"),

        ("wind_mid", "wind_bottom"),

        ("wind_top", "wind_mid"),

        ("right_sleeve", "right_arm"),

        ("left_sleeve", "left_arm"),

        ("jacket", "body"),

        ("left_pants", "left_leg"),

        ("right_pants", "right_leg"),

    ];



    /// <summary>

    /// Quadruped leg binds under <c>body</c> — valid only when the lifter missed hierarchy but Java used

    /// <c>addOrReplaceChild</c>. Flat 26.1.2 bakes (e.g. creeper) keep legs as root siblings with root-absolute

    /// <c>PartPose.offset</c>; reparenting without pose rebase stacks body Y onto legs and breaks preview.

    /// </summary>

    private static readonly (string ChildId, string ParentId)[] QuadrupedLegReparentRules =

    [

        ("left_front_leg", "body"),

        ("right_front_leg", "body"),

        ("left_hind_leg", "body"),

        ("right_hind_leg", "body"),

    ];



    private static readonly string[] QuadrupedLegPartIds =

    [

        "left_front_leg",

        "right_front_leg",

        "left_hind_leg",

        "right_hind_leg",

    ];



    public static JsonElement ApplyForParityCatalog(string? officialJvmName, JsonElement geometryRoot)

    {

        if (EntityPreviewDebugSettings.SkipAllPartTreeRepair)
        {
            return geometryRoot;
        }

        var node = JsonNode.Parse(geometryRoot.GetRawText());

        if (node is not JsonObject doc || doc["roots"] is not JsonArray roots)

        {

            return geometryRoot;

        }



        foreach (var root in roots)

        {

            if (root is not JsonObject ro || ro["children"] is not JsonArray rootKids)

            {

                continue;

            }



            var skipQuadrupedLegReparent = UsesVanillaFlatQuadrupedLegBake(rootKids, doc, officialJvmName);
            if (EntityPreviewDebugSettings.RepairForceLegReparentOnFlatBake)
            {
                skipQuadrupedLegReparent = false;
            }

            if (!EntityPreviewDebugSettings.RepairHeadStackLegReparent &&
                HeadStackNestedUnderBody(rootKids))
            {
                skipQuadrupedLegReparent = true;
            }



            if (EntityPreviewDebugSettings.RepairGlobalReparentRules)
            {
                HoistFelineFlatTail2ToRoot(rootKids);

                foreach (var (childId, parentId) in GlobalReparentRules)
                {
                    if (ShouldSkipGlobalReparentRule(childId, parentId, rootKids, officialJvmName))
                    {
                        continue;
                    }

                    ReparentFlatPart(rootKids, childId, parentId);
                }
            }



            if (!skipQuadrupedLegReparent && EntityPreviewDebugSettings.RepairQuadrupedLegReparent)
            {
                foreach (var (childId, parentId) in QuadrupedLegReparentRules)
                {
                    // Rabbit/axolotl bind legs under frontlegs/backlegs/body in Java; only renest mis-hoisted flat root siblings.
                    if (!IsFlatRootSibling(rootKids, childId))
                    {
                        continue;
                    }

                    ReparentFlatPart(rootKids, childId, parentId);
                }
            }



            if (EntityPreviewDebugSettings.RepairRemoveDuplicateRootSiblings)
            {
                RemoveRootSiblingWhenNested(rootKids);
            }

            if (EntityPreviewDebugSettings.RepairCollapseInnerBody)
            {
                CollapseInnerBodyUnderBody(rootKids);
            }

            if (EntityPreviewDebugSettings.RepairDeduplicateNestedPartIds)
            {
                DeduplicateNestedPartIds(ro);
            }

            if (ShouldApplyPlayerWideMeshParityRepair(officialJvmName, rootKids))
            {
                RemovePlayerMeshInternalParts(rootKids);
                GeometryIrPlayerMeshParityRepair.Apply(rootKids);
            }
            else
            {
                GeometryIrPlayerMeshParityRepair.ApplyHumanoidHatHeadCanonicalBounds(rootKids);
            }

            RemoveDuplicateInflatedBodyOverlayCuboids(rootKids);
            RemoveCopperGolemSetupAnimRotationHelperParts(rootKids, officialJvmName);
            RemoveArmorStandArmorWideOverlayParts(rootKids, officialJvmName);
            EnsureSnifferLegsUnderBone(rootKids, officialJvmName);

            if (EntityPreviewDebugSettings.RepairZeroEquineRootOffset &&
                ShouldZeroEquineCreateBodyLayerRootOffset(officialJvmName, doc, ro))
            {
                ZeroRootTranslation(ro);
            }

            if (ShouldApplyCollapsedNautilusBabyInnerRootOffset(officialJvmName, doc, ro))
            {
                ApplyCollapsedInnerRootFromBytecodeProbe(doc, ro);
            }

        }



        RepairObjectChestModelCuboidOrigins(doc);

        if (ShouldApplyPlayerWideMeshParityRepair(officialJvmName, roots) ||
            ShouldApplyReferenceCuboidPreviewRepair(officialJvmName))
        {
            var repaired = JsonDocument.Parse(doc.ToJsonString()).RootElement;
            if (LoadReferenceRootForTopologyAlign(officialJvmName!) is { } referenceRoot)
            {
                return ShouldApplyReferenceCuboidPreviewRepair(officialJvmName)
                    ? GeometryIrReferencePoseSync.ApplyForParityPreview(referenceRoot, repaired)
                    : GeometryIrReferencePoseSync.ApplyForComparisons(
                        referenceRoot,
                        GeometryIrReferenceTopologyAlign.ApplyForWorldPoseCompare(referenceRoot, repaired));
            }

            return repaired;
        }

        if (ShouldApplyReferenceCuboidOnlyShardRepair(officialJvmName) &&
            LoadReferenceRootForTopologyAlign(officialJvmName!) is { } cuboidRefRoot)
        {
            GeometryIrReferencePoseSync.SyncCuboidsIntoShard(cuboidRefRoot, doc);
        }

        return JsonDocument.Parse(doc.ToJsonString()).RootElement;

    }

    /// <summary>
    /// <c>CopperGolemModel.createBodyLayer</c> keeps setupAnim <c>*_r1</c> rotation nodes and <c>rightItem</c> that Java
    /// reference bakes omit from the static mesh (cuboids live on parent limbs at bind pose).
    /// </summary>
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

    /// <summary>
    /// <c>SnifferModel</c> binds six legs as siblings of <c>body</c> under <c>bone</c>; mis-hoisted flat leg repair must not leave them at factory root.
    /// </summary>
    private static void EnsureSnifferLegsUnderBone(JsonArray rootChildren, string? officialJvmName)
    {
        if (officialJvmName is null ||
            !officialJvmName.Contains(".animal.sniffer.SnifferModel", StringComparison.Ordinal) ||
            !TryFindPartById(rootChildren, "bone", out var boneNode) || boneNode is not JsonObject bone)
        {
            return;
        }

        bone["children"] ??= new JsonArray();
        if (bone["children"] is not JsonArray boneKids)
        {
            return;
        }

        foreach (var legId in QuadrupedLegPartIds)
        {
            if (!TryDetachPartFromForest(rootChildren, legId, out var leg) || leg is null)
            {
                continue;
            }

            if (!IsDirectChild(boneKids, legId))
            {
                boneKids.Add(leg);
            }
        }
    }

    private static bool TryDetachPartFromForest(JsonArray searchRoots, string partId, out JsonObject? detached)
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

            if (part["children"] is JsonArray kids && TryDetachPartFromForest(kids, partId, out detached))
            {
                return true;
            }
        }

        return false;
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

    private static bool ShouldApplyReferenceCuboidOnlyShardRepair(string? officialJvmName) =>
        officialJvmName is not null &&
        (officialJvmName.Contains(".animal.feline.BabyCatModel", StringComparison.Ordinal) ||
         officialJvmName.Contains(".animal.feline.BabyOcelotModel", StringComparison.Ordinal) ||
         officialJvmName.Contains(".golem.CopperGolemModel", StringComparison.Ordinal) ||
         officialJvmName.Contains(".object.armorstand.ArmorStandArmorModel", StringComparison.Ordinal));

    /// <summary>
    /// <c>ArmorStandArmorModel</c> bytecode lifts player-style overlay shells; Java reference bakes head/hat only.
    /// </summary>
    private static void RemoveArmorStandArmorWideOverlayParts(JsonArray rootChildren, string? officialJvmName)
    {
        if (officialJvmName is null ||
            !officialJvmName.Contains(".object.armorstand.ArmorStandArmorModel", StringComparison.Ordinal))
        {
            return;
        }

        RemovePartIdsFromForest(rootChildren,
        [
            "jacket",
            "left_pants",
            "right_pants",
            "left_sleeve",
            "right_sleeve",
        ]);
    }

    /// <summary>
    /// <c>ModelLayers.ZOMBIE</c> / <c>HUSK</c> bake <c>HumanoidModel.createMesh</c> on wrapper classes; bytecode lift keeps
    /// stacked PartPose islands while Java reference bakes flatten to root-sibling offsets (same as drowned-style preview).
    /// </summary>
    private static bool ShouldApplyHumanoidLayerMeshReferenceRepair(string? officialJvmName) =>
        GeometryIrHumanoidLayerMeshPreviewPolicy.IsHumanoidLayerMeshJvm(officialJvmName);

    private static bool ShouldApplyReferenceCuboidPreviewRepair(string? officialJvmName) =>
        ShouldApplyHumanoidLayerMeshReferenceRepair(officialJvmName) ||
        string.Equals(officialJvmName, "net.minecraft.client.model.HumanoidModel", StringComparison.Ordinal);

    private static bool IsPlayerMeshFamily(string? officialJvmName) =>
        string.Equals(officialJvmName, "net.minecraft.client.model.player.PlayerModel", StringComparison.Ordinal) ||
        string.Equals(officialJvmName, "net.minecraft.client.model.HumanoidModel", StringComparison.Ordinal);

    /// <summary>
    /// <c>PlayerModel.createMesh</c> wide overlay kit (jacket/pants/sleeves) is reused by piglin and other humanoid delegates.
    /// </summary>
    private static bool ShouldApplyPlayerWideMeshParityRepair(string? officialJvmName, JsonArray roots)
    {
        if (IsPlayerMeshFamily(officialJvmName))
        {
            return true;
        }

        foreach (var root in roots)
        {
            if (root is JsonObject ro &&
                ro["children"] is JsonArray rootKids &&
                UsesPlayerWideMeshOverlayKit(rootKids))
            {
                return true;
            }
        }

        return false;
    }

    private static bool UsesPlayerWideMeshOverlayKit(JsonArray rootChildren) =>
        TryFindPartById(rootChildren, "jacket", out _) &&
        TryFindPartById(rootChildren, "left_pants", out _);

    private static JsonElement? LoadReferenceRootForTopologyAlign(string officialJvmName) =>
        GeometryIrReferenceBakePaths.TryLoadReferenceRoot(officialJvmName, out var root)
            ? root
            : null;

    /// <summary>
    /// <c>PlayerModel.createMesh</c> bytecode keeps waist/feet/inner_body parts that Java reference bakes omit.
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
    /// True when Java factory keeps <c>body</c> and leg parts as flat <c>root</c> siblings (no addChild hierarchy).
    /// Matches <see cref="GeometryIrLiftQualityReport"/> flat-nested / legs-at-root detection for pilot quadrupeds.
    /// When true, skip <see cref="QuadrupedLegReparentRules"/> — runtime-ir-preview-plan § Quadruped body placement regression
    /// and § Baby JVM family (fox, cow, chicken, <c>BabyHorseModel</c>, …). Exception: <see cref="HeadStackNestedUnderBody"/>
    /// (baby equine mis-lift). Camel also nests head under body but keeps entity-space flat root legs.
    /// </summary>

    internal static bool UsesVanillaFlatQuadrupedLegBake(
        JsonArray rootChildren,
        JsonObject? shardDoc = null,
        string? officialJvmName = null)

    {

        officialJvmName ??= shardDoc?["officialJvmName"]?.GetValue<string>();

        if (TryFindPartById(rootChildren, "bone", out var boneNode) && boneNode is JsonObject boneObj &&
            boneObj["children"] is JsonArray boneKids &&
            QuadrupedLegPartIds.Any(legId => TryFindPartById(boneKids, legId, out _)))
        {
            return true;
        }

        var rootIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var n in rootChildren)

        {

            if (n is JsonObject o && o["id"]?.GetValue<string>() is { } id)

            {

                rootIds.Add(id);

            }

        }



        if (!rootIds.Contains("body"))

        {

            return false;

        }



        if (!QuadrupedLegPartIds.Any(rootIds.Contains))

        {

            return false;

        }



        // Nested head stack (baby donkey class): flat leg siblings are body-relative offsets mis-lifted to root.
        // Camel/armadillo bind legs on getRoot() with entity-space offsets while head/tail nest on body.
        if (HeadStackNestedUnderBody(rootChildren))

        {

            return IsEntitySpaceFlatRootLegMeshFamily(officialJvmName);

        }

        // Java nests standard legs on body (axolotl, rabbit, sniffer, …). Compiler hoist can leave
        // body-relative poses as flat root siblings — renest under body; do not treat as creeper-class flat bake.
        if (IsNestedBodyLegQuadrupedMeshFamily(officialJvmName))

        {

            return false;

        }



        return true;

    }

    /// <summary>
    /// Baby wolf bind tail xRot (-π/6) is overwritten every frame by <c>WolfModel.setupAnim</c> with
    /// <see cref="CleanRoomEntityModelRuntime.WolfDefaultTailAngleRad"/>; Explore bind preview must match that idle pose.
    /// </summary>
    public static JsonElement ApplyWolfIdleTailPreviewPose(string? officialJvmName, JsonElement geometryRoot)
    {
        if (!string.Equals(
                officialJvmName,
                "net.minecraft.client.model.animal.wolf.BabyWolfModel",
                StringComparison.Ordinal))
        {
            return geometryRoot;
        }

        var node = JsonNode.Parse(geometryRoot.GetRawText());
        if (node is not JsonObject doc || !TrySetPartRotationEulerX(doc, "tail", CleanRoomEntityModelRuntime.WolfDefaultTailAngleRad))
        {
            return geometryRoot;
        }

        return JsonDocument.Parse(doc.ToJsonString()).RootElement;
    }

    private static bool TrySetPartRotationEulerX(JsonObject doc, string partId, float xRad)
    {
        if (doc["roots"] is not JsonArray roots)
        {
            return false;
        }

        foreach (var root in roots)
        {
            if (root is JsonObject rootObj && TrySetPartRotationEulerXRecursive(rootObj, partId, xRad))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySetPartRotationEulerXRecursive(JsonObject part, string partId, float xRad)
    {
        if (string.Equals((string?)part["id"], partId, StringComparison.Ordinal) &&
            part["pose"]?["rotationEulerRad"] is JsonArray euler &&
            euler.Count >= 1)
        {
            euler[0] = xRad;
            return true;
        }

        if (part["children"] is not JsonArray children)
        {
            return false;
        }

        foreach (var child in children)
        {
            if (child is JsonObject childObj && TrySetPartRotationEulerXRecursive(childObj, partId, xRad))
            {
                return true;
            }
        }

        return false;
    }
}
