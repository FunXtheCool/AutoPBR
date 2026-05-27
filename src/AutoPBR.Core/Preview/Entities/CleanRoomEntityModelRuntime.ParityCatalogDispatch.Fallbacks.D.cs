using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderFallbacksD(

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
            case "HangingSignEntity":







                merged = BuildHangingSignEntity(texRef, profile, isBaby);







                return true;

            case "StandingSignEntity":







                merged = BuildStandingSignEntity(texRef, profile, isBaby);







                return true;

            case "DecoratedPotEntity":







                merged = BuildDecoratedPotEntity(texRef, profile, isBaby);







                return true;

            case "ConduitEntity":







                merged = BuildConduitEntity(texRef, profile, isBaby, spin: idlePhase01 * MathF.PI * 2f);







                return true;

            case "Creaking":







                {







                    var creakLean = idlePhase01 * 0.12f + wave * 0.06f;







                    if (DefinitionAnimationPreviewSampling.TrySampleCreakingWalkUpperBodyRotationDegrees(profile, animationTimeSeconds, out var upperBodyDeg))







                    {







                        creakLean += upperBodyDeg.Z * (MathF.PI / 180f);







                    }















                    const float creakingAttackLoopSeconds = 0.708333f;







                    var attackT = animationTimeSeconds % creakingAttackLoopSeconds;







                    if (attackT < 0f)







                    {







                        attackT += creakingAttackLoopSeconds;







                    }















                    if (DefinitionAnimationPreviewSampling.TrySampleCreakingAttackUpperBodyRotationDegrees(profile, attackT, out var attackUpperDeg))







                    {







                        creakLean += attackUpperDeg.Y * (MathF.PI / 180f) * 0.02f;







                    }















                    merged = BuildCreaking(texRef, profile, isBaby, lean: creakLean);







                    return true;







                }

            case "ExperienceOrb":







                merged = BuildExperienceOrb(







                    texRef,







                    profile,







                    isBaby,







                    bob: idlePhase01 * 0.25f + wave * 0.1f,







                    spritePick01: idlePhase01);







                return true;

            case "FishingHook":







                merged = BuildFishingHook(texRef, profile, isBaby, sway: wave * 0.15f);







                return true;

            case "BeaconBeam":







                merged = BuildBeaconBeam(texRef, profile, isBaby, scroll: idlePhase01);







                return true;

            case "HumanoidZombieVillager":







                merged = BuildZombieVillager(texRef, profile, isBaby, armLift: 1.15f + idlePhase01 * 0.55f + wave * 0.18f);







                return true;

            case "BeamColumn":







                merged = BuildBeamColumn(texRef, profile, isBaby, twist: idlePhase01 * MathF.PI * 2f);







                return true;

            case "HumanoidSkeleton":







                merged = BuildSkeletonHumanoid(texRef, profile, isBaby, armLift: idlePhase01 * 0.35f + wave * 0.12f);







                return true;

            case "Skeleton":







                merged = BuildSkeletonHumanoid(texRef, profile, isBaby, armLift: idlePhase01 * 0.35f + wave * 0.12f);







                return true;

            case "Zombie":







                merged = BuildZombieHumanoid(texRef, profile, isBaby, armLift: 1.2f + idlePhase01 * 0.6f + wave * 0.2f);







                return true;

            case "EndPortalSurface":







                merged = BuildEndPortalSurface(texRef, profile, isBaby);







                return true;

            case "EnchantingTableBook":







                merged = BuildEnchantingTableBook(texRef, profile, isBaby, flap: idlePhase01 * 0.4f + wave * 0.15f);







                return true;

            case "CopperGolem":







                {







                    var golemSwing = idlePhase01 * 0.5f + wave * 0.2f;







                    if (DefinitionAnimationPreviewSampling.TrySampleCopperGolemWalkBodyRotationDegrees(profile, animationTimeSeconds, out var golemBodyDeg))







                    {







                        golemSwing += golemBodyDeg.X * (MathF.PI / 180f) * 0.25f;







                    }















                    merged = BuildCopperGolem(texRef, profile, isBaby, armSwing: golemSwing);







                    return true;







                }

            case "ChestEntity":







                merged = BuildChestEntity(texRef, profile, isBaby);







                return true;

        }

        return false;
    }
}
