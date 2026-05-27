using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderEquipmentRouteE(

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

        }

        return false;
    }
}
