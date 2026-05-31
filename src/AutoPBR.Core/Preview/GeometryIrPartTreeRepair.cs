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



            var skipQuadrupedLegReparent = UsesVanillaFlatQuadrupedLegBake(rootKids, doc);
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
                foreach (var (childId, parentId) in GlobalReparentRules)
                {
                    ReparentFlatPart(rootKids, childId, parentId);
                }
            }



            if (!skipQuadrupedLegReparent && EntityPreviewDebugSettings.RepairQuadrupedLegReparent)
            {
                foreach (var (childId, parentId) in QuadrupedLegReparentRules)
                {
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

            if (EntityPreviewDebugSettings.RepairZeroEquineRootOffset &&
                ShouldZeroEquineCreateBodyLayerRootOffset(officialJvmName, doc, ro))
            {
                ZeroRootTranslation(ro);
            }

        }



        return JsonDocument.Parse(doc.ToJsonString()).RootElement;

    }

    /// <summary>
    /// True when Java factory keeps <c>body</c> and leg parts as flat <c>root</c> siblings (no addChild hierarchy).
    /// Matches <see cref="GeometryIrLiftQualityReport"/> flat-nested / legs-at-root detection for pilot quadrupeds.
    /// When true, skip <see cref="QuadrupedLegReparentRules"/> — runtime-ir-preview-plan § Quadruped body placement regression
    /// and § Baby JVM family (fox, cow, chicken, <c>BabyHorseModel</c>, …). Exception: <see cref="HeadStackNestedUnderBody"/>.
    /// </summary>

    internal static bool UsesVanillaFlatQuadrupedLegBake(JsonArray rootChildren, JsonObject? shardDoc = null)

    {

        _ = shardDoc;



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
        if (HeadStackNestedUnderBody(rootChildren))

        {

            return false;

        }



        return true;

    }
}
