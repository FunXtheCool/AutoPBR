using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderEquipmentRouteF(

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
