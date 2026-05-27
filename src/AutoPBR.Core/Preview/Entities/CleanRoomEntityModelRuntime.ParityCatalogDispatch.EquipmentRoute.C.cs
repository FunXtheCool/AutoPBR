using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderEquipmentRouteC(

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

        }

        return false;
    }
}
