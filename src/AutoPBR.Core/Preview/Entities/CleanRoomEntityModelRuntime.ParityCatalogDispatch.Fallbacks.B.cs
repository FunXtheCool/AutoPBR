using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderFallbacksB(

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
            case "Axolotl":







                {







                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Axolotl");







                    merged = BuildAxolotl(







                        texRef,







                        profile,







                        isBaby,







                        idleBob: idlePhase01 * 0.12f + wave * 0.06f,







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
