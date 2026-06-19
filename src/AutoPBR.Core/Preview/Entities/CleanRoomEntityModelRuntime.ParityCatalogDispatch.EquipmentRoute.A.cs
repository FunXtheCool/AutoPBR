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







                merged = BuildBoatFamily(
                    texRef,
                    profile,
                    isBaby,
                    isChestBoat: normalizedAssetPath.Contains("/textures/entity/chest_boat/", StringComparison.OrdinalIgnoreCase),
                    normalizedAssetPath);







                return true;

            case "ChestBoat":







                merged = BuildBoatFamily(texRef, profile, isBaby, isChestBoat: true, normalizedAssetPath);







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

        }

        return false;
    }
}
