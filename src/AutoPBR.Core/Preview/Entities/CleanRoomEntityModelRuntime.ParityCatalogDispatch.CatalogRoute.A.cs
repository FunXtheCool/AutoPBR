using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderCatalogRouteA(

        string builderMethod,
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        float wave,
        out MergedJavaBlockModel merged
    )
    {
        merged = null!;
        switch (builderMethod)
        {
            case "NautilusMob":



                merged = BuildNautilusMob(texRef, profile, isBaby, animationTimeSeconds);



                return true;

            case "Horse":



                // Catalog neckBend is preview-only idle motion for adults (not a vanilla javap channel). Baby equine



                // idle head_parts xRot/Y/Z already come from ported AbstractEquineModel + BabyHorse/BabyDonkey paths;



                // there is no separate vanilla term to retarget, and ~0.25 rad stacks on top and breaks parity.



                merged = BuildHorse(texRef, profile, isBaby, neckBend: isBaby ? 0f : (0.25f + (wave * 0.2f)));



                return true;

            case "DonkeyMuleHorse":



                merged = BuildHorseDonkeyMule(texRef, profile, isBaby, neckBend: isBaby ? 0f : (0.25f + (wave * 0.2f)));



                return true;

            case "HumanoidZombie":



                merged = BuildZombieHumanoid(texRef, profile, isBaby, armLift: 1.2f + idlePhase01 * 0.6f + wave * 0.2f);



                return true;

            case "HumanoidVillager":



                merged = BuildVillager(texRef, profile, isBaby, headPitch: wave * 0.06f, armFold: 0.18f + wave * 0.03f);



                return true;

            case "WanderingTrader":



                merged = BuildVillager(texRef, profile, isBaby, headPitch: wave * 0.06f, armFold: 0.2f + wave * 0.04f);



                return true;

            case "Enderman":



                merged = BuildEnderman(texRef, profile, isBaby, armLift: 0.16f + wave * 0.05f);



                return true;

            case "Witch":



                {



                    var (wWalkPos, wWalkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);



                    var witchEntityId = stem.GetHashCode(StringComparison.Ordinal);



                    merged = BuildWitch(



                        texRef,



                        profile,



                        isBaby,



                        yRotDegrees: wave * 10f,



                        xRotDegrees: idlePhase01 * 12f + wave * 6f,



                        walkAnimationPos: wWalkPos,



                        walkAnimationSpeed: wWalkSpeed,



                        entityId: witchEntityId,



                        ageInTicks: animationTimeSeconds * 20f,



                        isHoldingItem: true);



                    return true;



                }

            case "Evoker":



                merged = BuildEvoker(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);



                return true;

            case "Vindicator":



                merged = BuildVindicator(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);



                return true;

            case "Illager":



                merged = BuildIllager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, IllagerPreviewArmPoseKind.Crossed);



                return true;

            case "Pillager":



                merged = BuildIllager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, IllagerPreviewArmPoseKind.CrossbowHold);



                return true;

            case "Cow":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Cow");



                    var headPitch = idlePhase01 * 0.35f + wave * 0.15f;



                    if (normalizedAssetPath.Contains("/textures/entity/cow/cow_cold", StringComparison.OrdinalIgnoreCase))



                    {



                        merged = BuildColdCow(



                            texRef,



                            profile,



                            isBaby,



                            headPitch,



                            hasHorns: true,



                            rightHindLegPitchRad: rh,



                            leftHindLegPitchRad: lh,



                            rightFrontLegPitchRad: rf,



                            leftFrontLegPitchRad: lf);



                    }



                    else if (normalizedAssetPath.Contains("/textures/entity/cow/cow_warm", StringComparison.OrdinalIgnoreCase))



                    {



                        merged = BuildWarmCow(



                            texRef,



                            profile,



                            isBaby,



                            headPitch,



                            hasHorns: true,



                            rightHindLegPitchRad: rh,



                            leftHindLegPitchRad: lh,



                            rightFrontLegPitchRad: rf,



                            leftFrontLegPitchRad: lf);



                    }



                    else



                    {



                        merged = BuildCow(



                            texRef,



                            profile,



                            isBaby,



                            headPitch,



                            hasHorns: true,



                            rightHindLegPitchRad: rh,



                            leftHindLegPitchRad: lh,



                            rightFrontLegPitchRad: rf,



                            leftFrontLegPitchRad: lf);



                    }







                    return true;



                }

            case "Wolf":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Wolf");



                    merged = BuildWolf(



                        texRef,



                        profile,



                        isBaby,



                        headPitch: idlePhase01 * 0.45f + wave * 0.20f,



                        rightHindLegPitchRad: rh,



                        leftHindLegPitchRad: lh,



                        rightFrontLegPitchRad: rf,



                        leftFrontLegPitchRad: lf);



                    return true;



                }

            case "Fox":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Fox");



                    if (isBaby && DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkRightHindLegRotationDegrees(



                            profile, animationTimeSeconds, out var foxBabyRhDeg))



                    {



                        rh += foxBabyRhDeg.X * (MathF.PI / 180f);



                    }







                    merged = BuildFox(



                        texRef,



                        profile,



                        isBaby,



                        tailLift: 0f,



                        rightHindLegPitchRad: rh,



                        leftHindLegPitchRad: lh,



                        rightFrontLegPitchRad: rf,



                        leftFrontLegPitchRad: lf);



                    return true;



                }

            case "Goat":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Goat");



                    merged = BuildGoat(



                        texRef,



                        profile,



                        isBaby,



                        headPitch: idlePhase01 * 0.30f + wave * 0.15f,



                        rightHindLegPitchRad: rh,



                        leftHindLegPitchRad: lh,



                        rightFrontLegPitchRad: rf,



                        leftFrontLegPitchRad: lf);



                    return true;



                }

            case "Hoglin":



                {



                    float rh, lh, rf, lf, headPitch;



                    if (isBaby)



                    {



                        (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Hoglin");



                        headPitch = idlePhase01 * 0.35f + wave * 0.15f;



                    }



                    else



                    {



                        rh = lh = rf = lf = 0f;



                        headPitch = 0f;



                    }







                    merged = BuildHoglin(texRef, profile, isBaby, headPitch, rh, lh, rf, lf);



                    return true;



                }

            case "Sniffer":



                {



                    var snifferHead = idlePhase01 * 0.12f + wave * 0.08f;



                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferLongSniffHeadRotationDegrees(profile, animationTimeSeconds, out var sniffHeadDeg))



                    {



                        snifferHead += sniffHeadDeg.X * (MathF.PI / 180f);



                    }







                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkHeadRotationDegrees(profile, animationTimeSeconds, out var walkHeadDeg))



                    {



                        snifferHead += walkHeadDeg.X * (MathF.PI / 180f);



                    }







                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkBodyRotationDegrees(profile, animationTimeSeconds, out var walkBodyDeg))



                    {



                        snifferHead += walkBodyDeg.X * (MathF.PI / 180f) * 0.15f;



                    }







                    var sniffWalkRf = 0f;



                    var sniffWalkLf = 0f;



                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkRightFrontLegRotationDegrees(profile, animationTimeSeconds, out var sniffRfDeg))



                    {



                        sniffWalkRf = sniffRfDeg.X * (MathF.PI / 180f);



                    }







                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftFrontLegRotationDegrees(profile, animationTimeSeconds, out var sniffLfDeg))



                    {



                        sniffWalkLf = sniffLfDeg.X * (MathF.PI / 180f);



                    }







                    const float sniffDegToRad = MathF.PI / 180f;



                    var sniffWalkLm = Vector3.Zero;



                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftMidLegRotationDegrees(profile, animationTimeSeconds, out var sniffLmDeg))



                    {



                        sniffWalkLm = new Vector3(



                            sniffLmDeg.X * sniffDegToRad,



                            sniffLmDeg.Y * sniffDegToRad,



                            sniffLmDeg.Z * sniffDegToRad);



                    }







                    merged = BuildSniffer(



                        texRef,



                        profile,



                        isBaby,



                        headPitch: snifferHead,



                        walkRightFrontLegPitchRad: sniffWalkRf,



                        walkLeftFrontLegPitchRad: sniffWalkLf,



                        walkLeftMidLegEulerRad: sniffWalkLm);



                    return true;



                }

            case "Wither":



                // Manifest currently routes wither_skeleton through "Wither"; keep that texture on skeleton rig parity.



                merged = normalizedAssetPath.Contains("/textures/entity/skeleton/wither_skeleton", StringComparison.OrdinalIgnoreCase)



                    ? BuildSkeletonHumanoid(texRef, profile, isBaby, armLift: idlePhase01 * 0.25f + wave * 0.10f)



                    : BuildWither(texRef, profile, isBaby, wave: idlePhase01 * 0.35f + wave * 0.12f);



                return true;

            case "Warden":



                {



                    var wardenSway = idlePhase01 * 0.30f + wave * 0.10f;



                    if (DefinitionAnimationPreviewSampling.TrySampleWardenSniffBodyRotationDegrees(profile, animationTimeSeconds, out var wardenBodyDeg))



                    {



                        wardenSway += wardenBodyDeg.Z * (MathF.PI / 180f) * 0.15f;



                    }







                    merged = BuildWarden(texRef, profile, isBaby, sway: wardenSway);



                    return true;



                }

            case "MagmaCube":



                merged = BuildMagmaCube(texRef, profile, isBaby, squish: MathF.Max(0f, wave));



                return true;
        }

        return false;
    }
}
