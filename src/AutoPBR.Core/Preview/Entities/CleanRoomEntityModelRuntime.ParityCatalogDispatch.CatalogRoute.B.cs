using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderCatalogRouteB(

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

        }

        return false;
    }
}
