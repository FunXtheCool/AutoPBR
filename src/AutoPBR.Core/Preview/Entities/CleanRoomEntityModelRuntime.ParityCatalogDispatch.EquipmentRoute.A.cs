using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderEquipmentRouteA(

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
            case "EquipmentNautilusArmor":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "EquipmentNautilusSaddle":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "EquipmentCamelSaddle":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "EquipmentSaddle":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "EquipmentHorseArmor":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "EquipmentLlamaBody":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "EquipmentWolfBody":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "EquipmentHumanoid":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "EquipmentHumanoidBaby":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "EquipmentHumanoidLeggings":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "Skull":



                merged = BuildSkull(texRef, profile, isBaby, headPitch: idlePhase01 * 0.2f + wave * 0.1f);



                return true;

            case "Bell":



                merged = BuildBell(texRef, profile, isBaby, swing: idlePhase01 * 0.5f + wave * 0.15f);



                return true;

            case "Minecart":



                merged = BuildMinecart(texRef, profile, isBaby);



                return true;

            case "Boat":



                merged = BuildBoat(



                    texRef,



                    profile,



                    isBaby,



                    isChestBoat: normalizedAssetPath.Contains("/textures/entity/chest_boat/", StringComparison.OrdinalIgnoreCase));



                return true;

            case "ChestBoat":



                merged = BuildBoat(texRef, profile, isBaby, isChestBoat: true);



                return true;

            case "LeashKnot":



                merged = BuildLeashKnot(texRef, profile, isBaby);



                return true;

            case "ArmorStand":



                merged = BuildArmorStand(texRef, profile, isBaby);



                return true;

            case "Ravager":



                {



                    var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);



                    merged = BuildRavager(



                        texRef,



                        profile,



                        isBaby,



                        xRotDegrees: idlePhase01 * 10f + wave * 6f,



                        yRotDegrees: animationTimeSeconds * 32f,



                        walkAnimationPos: walkPos,



                        walkAnimationSpeed: walkSpeed,



                        attackTicksRemaining: 0f,



                        stunnedTicksRemaining: 0f,



                        roarAnimation: Math.Clamp(idlePhase01 * 0.35f + wave * 0.25f, 0f, 1f));



                    return true;



                }

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
