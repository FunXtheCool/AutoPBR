using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderCatalogRouteA(

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
            case "NautilusMob":







                merged = BuildNautilusMob(texRef, profile, isBaby, animationTimeSeconds);







                return true;

            case "Horse":







                // Catalog neckBend is preview-only idle motion for adults (not a vanilla javap channel). Baby equine







                // idle head_parts xRot/Y/Z already come from ported AbstractEquineModel + BabyHorse/BabyDonkey paths;







                // there is no separate vanilla term to retarget, and ~0.25 rad stacks on top and breaks parity.







                merged = BuildHorse(texRef, profile, isBaby, neckBend: isBaby ? 0f : (0.25f + (wave * 0.2f)));







                return true;

            case "DonkeyMuleHorse":







                merged = BuildHorseDonkeyMule(texRef, profile, isBaby, neckBend: isBaby ? 0f : (0.25f + (wave * 0.2f)));







                return true;

            case "HumanoidZombie":







                merged = BuildZombieHumanoid(texRef, profile, isBaby, armLift: 1.2f + idlePhase01 * 0.6f + wave * 0.2f);







                return true;

            case "HumanoidVillager":







                merged = BuildVillager(texRef, profile, isBaby, headPitch: wave * 0.06f, armFold: 0.18f + wave * 0.03f);







                return true;

            case "WanderingTrader":







                merged = BuildVillager(texRef, profile, isBaby, headPitch: wave * 0.06f, armFold: 0.2f + wave * 0.04f);







                return true;

            case "Enderman":







                merged = BuildEnderman(texRef, profile, isBaby, armLift: 0.16f + wave * 0.05f);







                return true;

            case "Witch":







                {







                    var (wWalkPos, wWalkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);







                    var witchEntityId = stem.GetHashCode(StringComparison.Ordinal);







                    merged = BuildWitch(







                        texRef,







                        profile,







                        isBaby,







                        yRotDegrees: wave * 10f,







                        xRotDegrees: idlePhase01 * 12f + wave * 6f,







                        walkAnimationPos: wWalkPos,







                        walkAnimationSpeed: wWalkSpeed,







                        entityId: witchEntityId,







                        ageInTicks: animationTimeSeconds * 20f,







                        isHoldingItem: true);







                    return true;







                }

            case "Evoker":







                merged = BuildEvoker(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);







                return true;

            case "Vindicator":







                merged = BuildVindicator(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);







                return true;

            case "Illager":







                merged = BuildIllager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, IllagerPreviewArmPoseKind.Crossed);







                return true;

            case "Pillager":







                merged = BuildIllager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, IllagerPreviewArmPoseKind.CrossbowHold);







                return true;

        }

        return false;
    }
}
