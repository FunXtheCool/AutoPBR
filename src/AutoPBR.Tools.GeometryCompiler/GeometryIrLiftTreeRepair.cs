using System.Text.Json.Nodes;

using AutoPBR.Preview;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Repairs known flat IR trees after lift, before strict validation (mirrors Core parity repair).
/// </summary>
internal static partial class GeometryIrLiftTreeRepair
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
}
