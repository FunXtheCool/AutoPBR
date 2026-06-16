using System.Numerics;
// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Entity-specific GPU bone slot dispatch ladder (<see cref="TryBuildSpecific"/>).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{

/// <summary>GPU specific-model dispatch slots 91-104.</summary>
    private static bool TryBuildSpecificDispatchSlots91Through104(
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        int? fastPathOnlySlot,
        ref int slot,
        float wave,
        out MergedJavaBlockModel merged,
        out int specificRouteSlotHit)
    {
        merged = null!;
        specificRouteSlotHit = 0;

        var gpuBoneDispatchSlot = 0;

        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/strider/", StringComparison.OrdinalIgnoreCase))
            {
                var (walkPos, rawWalkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
                var walkSpeed = MathF.Min(0.25f, rawWalkSpeed);
                merged = BuildStrider(
                    texRef,
                    profile,
                    isBaby,
                    walkAnimationPos: walkPos,
                    walkAnimationSpeed: walkSpeed,
                    ageInTicks: animationTimeSeconds * 20f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/tadpole/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTadpole(texRef, profile, isBaby, tailSway: idlePhase01 * 0.45f + wave * 0.2f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        // Path-based: stems like "frogspawn" contain "frog" but live under textures/block or unrelated dirs.
        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/axolotl/", StringComparison.OrdinalIgnoreCase))
            {
                if (TryBuildAxolotlMeshPreferGeometryIr(
                        normalizedAssetPath,
                        stem,
                        texRef,
                        profile,
                        isBaby,
                        idlePhase01,
                        animationTimeSeconds,
                        out merged))
                {
                    specificRouteSlotHit = gpuBoneDispatchSlot;
                    return true;
                }

                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildAxolotl(
                texRef,
                profile,
                isBaby,
                idleBob: idlePhase01 * 0.12f + wave * 0.06f,
                rightHindLegPitchRad: rh,
                leftHindLegPitchRad: lh,
                rightFrontLegPitchRad: rf,
                leftFrontLegPitchRad: lf);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/frog/", StringComparison.OrdinalIgnoreCase))
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
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/signs/hanging/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildHangingSignEntity(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/signs/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildStandingSignEntity(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/decorated_pot/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildDecoratedPotEntity(texRef, profile, isBaby);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/conduit/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildConduitEntity(texRef, profile, isBaby, spin: idlePhase01 * MathF.PI * 2f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/creaking/", StringComparison.OrdinalIgnoreCase))
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
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("experience_orb", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/experience_orb", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildExperienceOrb(
                texRef,
                profile,
                isBaby,
                bob: idlePhase01 * 0.25f + wave * 0.1f,
                spritePick01: idlePhase01);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("fishing_hook", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/fishing_hook", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildFishingHook(texRef, profile, isBaby, sway: wave * 0.15f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("beacon_beam", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/beacon_beam", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBeaconBeam(texRef, profile, isBaby, scroll: idlePhase01);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/zombie_villager", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildZombieVillager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (normalizedAssetPath.Contains("/textures/entity/villager", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildVillager(texRef, profile, isBaby, headPitch: wave * 0.06f, armFold: 0.18f + wave * 0.03f);
                specificRouteSlotHit = gpuBoneDispatchSlot;
                return true;
            }

            if (fastPathOnlySlot.HasValue)
            {
                return false;
            }
        }

        return false;
    }
}
