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



        if (ShouldRepairObjectBoatFamily(officialJvmName))
        {
            RepairObjectBoatFamilyHullPartPose(doc);
        }

        RepairObjectChestModelCuboidOrigins(doc);

        if (ShouldApplyPlayerWideMeshParityRepair(officialJvmName, roots) ||
            ShouldApplyHumanoidLayerMeshReferenceRepair(officialJvmName))
        {
            var repaired = JsonDocument.Parse(doc.ToJsonString()).RootElement;
            if (LoadReferenceRootForTopologyAlign(officialJvmName!) is { } referenceRoot)
            {
                return ShouldApplyHumanoidLayerMeshReferenceRepair(officialJvmName)
                    ? GeometryIrReferencePoseSync.ApplyForParityPreview(referenceRoot, repaired)
                    : GeometryIrReferencePoseSync.ApplyForComparisons(
                        referenceRoot,
                        GeometryIrReferenceTopologyAlign.ApplyForWorldPoseCompare(referenceRoot, repaired));
            }

            return repaired;
        }

        return JsonDocument.Parse(doc.ToJsonString()).RootElement;

    }

    /// <summary>
    /// <c>ModelLayers.ZOMBIE</c> / <c>HUSK</c> bake <c>HumanoidModel.createMesh</c> on wrapper classes; bytecode lift keeps
    /// stacked PartPose islands while Java reference bakes flatten to root-sibling offsets (same as drowned-style preview).
    /// </summary>
    private static bool ShouldApplyHumanoidLayerMeshReferenceRepair(string? officialJvmName) =>
        GeometryIrHumanoidLayerMeshPreviewPolicy.IsHumanoidLayerMeshJvm(officialJvmName);

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
}
