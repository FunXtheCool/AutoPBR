using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderCatalogRouteC(

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

        }

        return false;
    }
}
