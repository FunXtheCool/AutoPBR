using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderFallbacksC(

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
            case "Frog":







                {







                    var frogCroak = idlePhase01 * 0.08f + wave * 0.05f;







                    if (DefinitionAnimationPreviewSampling.TrySampleFrogCroakCroakingBodyPosition(profile, animationTimeSeconds, out var croakBodyPos))







                    {







                        frogCroak = Math.Clamp(croakBodyPos.Y, 0f, 1f);







                    }















                    var frogLeftLegPitch = 0f;







                    var frogRightLegPitch = 0f;







                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftLegRotationDegrees(profile, animationTimeSeconds, out var frogLLegDeg))







                    {







                        frogLeftLegPitch = frogLLegDeg.X * (MathF.PI / 180f);







                    }















                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightLegRotationDegrees(profile, animationTimeSeconds, out var frogRLegDeg))







                    {







                        frogRightLegPitch = frogRLegDeg.X * (MathF.PI / 180f);







                    }















                    const float frogDegToRad = MathF.PI / 180f;







                    var frogLArmX = 0f;







                    var frogLArmY = 0f;







                    var frogLArmZ = 0f;







                    var frogRArmX = 0f;







                    var frogRArmY = 0f;







                    var frogRArmZ = 0f;







                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftArmRotationDegrees(profile, animationTimeSeconds, out var frogLArmDeg))







                    {







                        frogLArmX = frogLArmDeg.X * frogDegToRad;







                        frogLArmY = frogLArmDeg.Y * frogDegToRad;







                        frogLArmZ = frogLArmDeg.Z * frogDegToRad;







                    }















                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightArmRotationDegrees(profile, animationTimeSeconds, out var frogRArmDeg))







                    {







                        frogRArmX = frogRArmDeg.X * frogDegToRad;







                        frogRArmY = frogRArmDeg.Y * frogDegToRad;







                        frogRArmZ = frogRArmDeg.Z * frogDegToRad;







                    }















                    var frogLArmPos = Vector3.Zero;







                    var frogRArmPos = Vector3.Zero;







                    var frogLLegPos = Vector3.Zero;







                    var frogRLegPos = Vector3.Zero;







                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftArmPosition(profile, animationTimeSeconds, out var pLa))







                    {







                        frogLArmPos = pLa;







                    }















                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightArmPosition(profile, animationTimeSeconds, out var pRa))







                    {







                        frogRArmPos = pRa;







                    }















                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftLegPosition(profile, animationTimeSeconds, out var pLl))







                    {







                        frogLLegPos = pLl;







                    }















                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightLegPosition(profile, animationTimeSeconds, out var pRl))







                    {







                        frogRLegPos = pRl;







                    }















                    merged = BuildFrog(







                        texRef,







                        profile,







                        isBaby,







                        croakInflate: frogCroak,







                        walkLeftLegPitchRad: frogLeftLegPitch,







                        walkRightLegPitchRad: frogRightLegPitch,







                        walkLeftArmXRad: frogLArmX,







                        walkLeftArmYRad: frogLArmY,







                        walkLeftArmZRad: frogLArmZ,







                        walkRightArmXRad: frogRArmX,







                        walkRightArmYRad: frogRArmY,







                        walkRightArmZRad: frogRArmZ,







                        walkLeftArmPos: frogLArmPos,







                        walkRightArmPos: frogRArmPos,







                        walkLeftLegPos: frogLLegPos,







                        walkRightLegPos: frogRLegPos);







                    return true;







                }

        }

        return false;
    }
}
