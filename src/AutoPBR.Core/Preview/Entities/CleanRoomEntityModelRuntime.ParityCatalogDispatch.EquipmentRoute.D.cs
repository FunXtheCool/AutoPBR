using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderEquipmentRouteD(

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

        }

        return false;
    }
}
