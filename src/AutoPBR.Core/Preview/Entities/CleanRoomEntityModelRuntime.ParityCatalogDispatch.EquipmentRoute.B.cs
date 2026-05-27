using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderEquipmentRouteB(

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
            case "Camel":



                {



                    var babyCamelHeadZ = 0f;



                    if (isBaby && DefinitionAnimationPreviewSampling.TrySampleCamelBabyWalkHeadPosition(profile, animationTimeSeconds, out var camelBabyHeadPos))



                    {



                        babyCamelHeadZ = camelBabyHeadPos.Z;



                    }







                    var camelRootRollRad = 0f;



                    if (!isBaby && DefinitionAnimationPreviewSampling.TrySampleCamelWalkRootRotationDegrees(profile, animationTimeSeconds, out var camelRootDeg))



                    {



                        camelRootRollRad = camelRootDeg.Z * (MathF.PI / 180f);



                    }







                    merged = BuildCamel(



                        texRef,



                        profile,



                        isBaby,



                        neckBend: idlePhase01 * 0.25f + wave * 0.12f,



                        animationTimeSeconds,



                        idlePhase01,



                        babyWalkHeadTranslateZ: babyCamelHeadZ,



                        adultWalkRootRollRad: camelRootRollRad);



                    return true;



                }

            case "Panda":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Panda");



                    merged = BuildPanda(



                        texRef,



                        profile,



                        isBaby,



                        bodyRoll: idlePhase01 * 0.20f + wave * 0.10f,



                        rightHindLegPitchRad: rh,



                        leftHindLegPitchRad: lh,



                        rightFrontLegPitchRad: rf,



                        leftFrontLegPitchRad: lf);



                    return true;



                }

            case "PolarBear":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "PolarBear");



                    merged = BuildPolarBear(



                        texRef,



                        profile,



                        isBaby,



                        headLift: idlePhase01 * 0.22f + wave * 0.10f,



                        rightHindLegPitchRad: rh,



                        leftHindLegPitchRad: lh,



                        rightFrontLegPitchRad: rf,



                        leftFrontLegPitchRad: lf);



                    return true;



                }

            case "Piglin":



                {



                    var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);



                    merged = BuildPiglin(



                        texRef,



                        profile,



                        isBaby,



                        headPitch: idlePhase01 * 0.28f + wave * 0.11f,



                        armLift: idlePhase01 * 0.35f + wave * 0.12f,



                        walkAnimationPos: walkPos,



                        walkAnimationSpeed: walkSpeed,



                        ageInTicks: animationTimeSeconds * 20f);



                    return true;



                }

            case "ZombifiedPiglin":



                {



                    var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);



                    merged = BuildZombifiedPiglin(



                        texRef,



                        profile,



                        isBaby,



                        headPitch: idlePhase01 * 0.24f + wave * 0.10f,



                        armLift: idlePhase01 * 0.28f + wave * 0.10f,



                        walkAnimationPos: walkPos,



                        walkAnimationSpeed: walkSpeed,



                        ageInTicks: animationTimeSeconds * 20f);



                    return true;



                }

            case "Pig":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Pig");



                    merged = BuildPig(texRef, profile, isBaby, snoutBob: 0f, rh, lh, rf, lf);



                    return true;



                }

            case "ColdPig":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "ColdPig");



                    merged = BuildColdPig(texRef, profile, isBaby, snoutBob: 0f, rh, lh, rf, lf);



                    return true;



                }

            case "Sheep":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Sheep");



                    merged = BuildSheep(texRef, profile, isBaby, grazeDip: 0.35f + idlePhase01 * 0.25f + wave * 0.25f, rh, lh, rf, lf);



                    return true;



                }

            case "Rabbit":



                {



                    var hop = Math.Clamp(0.25f + wave * 0.25f + ComputePreviewRabbitHopSinTerm(animationTimeSeconds), -0.75f, 0.75f);



                    var tiltOk = isBaby



                        ? DefinitionAnimationPreviewSampling.TrySampleBabyRabbitIdleHeadTiltBodyPosition(profile, animationTimeSeconds, out var tiltBody)



                        : DefinitionAnimationPreviewSampling.TrySampleRabbitIdleHeadTiltBodyPosition(profile, animationTimeSeconds, out tiltBody);



                    if (tiltOk)



                    {



                        hop = Math.Clamp(hop + tiltBody.Y * 0.18f, -0.75f, 0.75f);



                    }







                    if (!isBaby && DefinitionAnimationPreviewSampling.TrySampleRabbitHopFrontLegsPosition(profile, animationTimeSeconds, out var hopFrontLegs))



                    {



                        hop = Math.Clamp(hop + hopFrontLegs.Y * 0.12f + hopFrontLegs.Z * 0.06f, -0.75f, 0.75f);



                    }







                    merged = BuildRabbit(texRef, profile, isBaby, hopCompress: hop);



                    return true;



                }

            case "Dolphin":



                merged = BuildDolphin(



                    texRef,



                    profile,



                    isBaby,



                    swimSway: idlePhase01 * 0.6f + wave * 0.25f + ComputePreviewDolphinSwimOscillation(animationTimeSeconds));



                return true;

            case "Cat":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Cat");



                    merged = BuildCat(



                        texRef,



                        profile,



                        isBaby,



                        headTilt: idlePhase01 * 0.2f + wave * 0.1f,



                        rightHindLegPitchRad: rh,



                        leftHindLegPitchRad: lh,



                        rightFrontLegPitchRad: rf,



                        leftFrontLegPitchRad: lf);



                    return true;



                }

            case "BabyFeline":



                {



                    if (!isBaby)



                    {



                        return false;



                    }







                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "BabyFeline");



                    merged = BuildBabyFeline(



                        texRef,



                        headPitchRad: idlePhase01 * 0.2f + wave * 0.1f,



                        rightHindLegPitchRad: rh,



                        leftHindLegPitchRad: lh,



                        rightFrontLegPitchRad: rf,



                        leftFrontLegPitchRad: lf);



                    return true;



                }

            case "Chicken":



                {



                    ComputeChickenParityPreviewDrivers(



                        animationTimeSeconds,



                        idlePhase01,



                        wave,



                        out var headPitchRad,



                        out var headYawRad,



                        out var wingZ,



                        out var rLeg,



                        out var lLeg);



                    merged = IsAdultColdChickenStem(stem) && !isBaby



                        ? BuildColdChicken(texRef, profile, headPitchRad, headYawRad, wingZ, rLeg, lLeg)



                        : BuildChicken(



                            texRef,



                            profile,



                            isBaby,



                            headPitchRad: headPitchRad,



                            headYawRad: headYawRad,



                            wingZRadians: wingZ,



                            rightLegPitchRad: rLeg,



                            leftLegPitchRad: lLeg);



                    return true;



                }

            case "Creeper":



                merged = BuildCreeper(texRef, profile, isBaby, bodyBob: idlePhase01 * 0.2f + wave * 0.1f);



                return true;

            case "Spider":



                merged = BuildSpider(texRef, profile, isBaby, legSpread: 0.45f + idlePhase01 * 0.25f + wave * 0.2f);



                return true;

            case "DragonFireball":



                merged = BuildDragonFireball(texRef, profile, isBaby, framePick01: idlePhase01);



                return true;

            case "EnderDragon":



                merged = BuildEnderDragon(texRef, profile, isBaby, wingSweep: 0.4f + idlePhase01 * 0.35f + wave * 0.15f);



                return true;

            case "Bat":



                {



                    var batWingFold = idlePhase01 * 0.35f + wave * 0.15f;



                    var batRightRy = batWingFold * 0.5f;



                    var batLeftRy = batWingFold * 0.5f;



                    if (DefinitionAnimationPreviewSampling.TrySampleBatFlyingRightWingRotationDegrees(profile, animationTimeSeconds, out var batRWingDeg) &&



                        DefinitionAnimationPreviewSampling.TrySampleBatFlyingLeftWingRotationDegrees(profile, animationTimeSeconds, out var batLWingDeg))



                    {



                        batRightRy = batRWingDeg.Y * (MathF.PI / 180f);



                        batLeftRy = batLWingDeg.Y * (MathF.PI / 180f);



                    }







                    if (DefinitionAnimationPreviewSampling.TrySampleBatRestingRightWingRotationDegrees(profile, animationTimeSeconds, out var batRestRWingDeg) &&



                        DefinitionAnimationPreviewSampling.TrySampleBatRestingLeftWingRotationDegrees(profile, animationTimeSeconds, out var batRestLWingDeg))



                    {



                        const float batRestingWingBlend = 0.22f;



                        var restRight = batRestRWingDeg.Y * (MathF.PI / 180f);



                        var restLeft = batRestLWingDeg.Y * (MathF.PI / 180f);



                        batRightRy = batRightRy + (restRight - batRightRy) * batRestingWingBlend;



                        batLeftRy = batLeftRy + (restLeft - batLeftRy) * batRestingWingBlend;



                    }







                    var batRightWingZ = 0f;



                    var batLeftWingZ = 0f;



                    if (DefinitionAnimationPreviewSampling.TrySampleBatRestingRightWingPosition(profile, animationTimeSeconds, out var batRestRPos) &&



                        DefinitionAnimationPreviewSampling.TrySampleBatRestingLeftWingPosition(profile, animationTimeSeconds, out var batRestLPos))



                    {



                        const float batRestingPosBlend = 0.22f;



                        batRightWingZ = batRestRPos.Z * batRestingPosBlend;



                        batLeftWingZ = batRestLPos.Z * batRestingPosBlend;



                    }







                    merged = BuildBat(



                        texRef,



                        profile,



                        isBaby,



                        rightWingYawRad: batRightRy,



                        leftWingYawRad: batLeftRy,



                        restingWingPivotZRight: batRightWingZ,



                        restingWingPivotZLeft: batLeftWingZ);



                    return true;



                }

            case "Blaze":



                merged = BuildBlaze(texRef, profile, isBaby, rodSpin: idlePhase01 * 0.65f + wave * 0.25f);



                return true;

            case "BeeStinger":



                merged = BuildBeeStinger(texRef, profile, isBaby);



                return true;

            case "Bee":



                merged = BuildBee(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.65f + wave * 0.25f);



                return true;
        }

        return false;
    }
}
