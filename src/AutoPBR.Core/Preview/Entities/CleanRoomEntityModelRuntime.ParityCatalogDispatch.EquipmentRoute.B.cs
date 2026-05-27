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
            case "Armadillo":







                {







                    var armadilloTailWalkRad = 0f;







                    if (isBaby)







                    {







                        if (DefinitionAnimationPreviewSampling.TrySampleBabyArmadilloWalkTailRotationDegrees(profile, animationTimeSeconds, out var babyTailDeg))







                        {







                            armadilloTailWalkRad = babyTailDeg.X * (MathF.PI / 180f);







                        }







                    }







                    else if (DefinitionAnimationPreviewSampling.TrySampleArmadilloWalkTailRotationDegrees(profile, animationTimeSeconds, out var adultTailDeg))







                    {







                        armadilloTailWalkRad = adultTailDeg.X * (MathF.PI / 180f);







                    }















                    merged = BuildArmadillo(







                        texRef,







                        profile,







                        isBaby,







                        headPitch: idlePhase01 * 0.14f + wave * 0.08f,







                        tailWalkPitchRad: armadilloTailWalkRad);







                    return true;







                }

            case "Breeze":







                {







                    var shootHeadPitchRad = 0f;







                    if (DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadRotationDegrees(profile, animationTimeSeconds, out var shootHeadDeg))







                    {







                        shootHeadPitchRad = shootHeadDeg.X * (MathF.PI / 180f);







                    }















                    var shootHeadPos = Vector3.Zero;







                    if (DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadPosition(profile, animationTimeSeconds, out var shootHeadTranslation))







                    {







                        shootHeadPos = shootHeadTranslation;







                    }















                    merged = BuildBreeze(







                        normalizedAssetPath,







                        texRef,







                        profile,







                        isBaby,







                        swirl: idlePhase01 * 0.6f + wave * 0.2f,







                        windAnimTimeSeconds: animationTimeSeconds,







                        shootHeadAdditivePitchRad: shootHeadPitchRad,







                        shootHeadAdditiveTranslate: shootHeadPos);







                    return true;







                }

            case "Llama":







                merged = BuildLlama(texRef, profile, isBaby, neckBend: idlePhase01 * 0.30f + wave * 0.10f);







                return true;

        }

        return false;
    }
}
