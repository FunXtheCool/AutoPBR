using System.Numerics;
// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Entity-specific GPU bone slot dispatch ladder (<see cref="TryBuildSpecific"/>).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{

/// <summary>GPU specific-model dispatch slots 105-117.</summary>
    private static bool TryBuildSpecificDispatchSlots105ThroughEnd(
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

        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out var gpuBoneDispatchSlot))
        {
            if (stem.Contains("giant", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildHumanoid(texRef, profile, isBaby, armLift: 0.35f + idlePhase01 * 0.25f + wave * 0.1f);
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
            if (normalizedAssetPath.Contains("/textures/entity/fish/", StringComparison.OrdinalIgnoreCase) &&
                stem.Contains("tropical_b", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTropicalFishB(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);
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
            if (normalizedAssetPath.Contains("/textures/entity/fish/", StringComparison.OrdinalIgnoreCase) &&
                (stem.Contains("tropical_a", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("tropical_fish", StringComparison.OrdinalIgnoreCase)))
            {
                merged = BuildTropicalFishA(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);
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
            if (stem.Contains("end_gateway_beam", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/end_gateway_beam", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBeamColumn(texRef, profile, isBaby, twist: idlePhase01 * MathF.PI * 2f);
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
            if (normalizedAssetPath.Contains("/textures/entity/skeleton/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSkeletonHumanoid(texRef, profile, isBaby, armLift: idlePhase01 * 0.35f + wave * 0.12f);
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
            if (stem.Contains("end_portal", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/end_portal", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEndPortalSurface(texRef, profile, isBaby);
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
            if (normalizedAssetPath.Contains("/textures/entity/cat/", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildCat(
                texRef,
                profile,
                isBaby,
                headTilt: idlePhase01 * 0.2f + wave * 0.1f,
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
            if (stem.Contains("enchanting_table_book", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/enchanting_table_book", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEnchantingTableBook(texRef, profile, isBaby, flap: idlePhase01 * 0.4f + wave * 0.15f);
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
            if (normalizedAssetPath.Contains("/textures/entity/copper_golem/", StringComparison.OrdinalIgnoreCase))
            {
                var golemSwing = idlePhase01 * 0.5f + wave * 0.2f;
                if (DefinitionAnimationPreviewSampling.TrySampleCopperGolemWalkBodyRotationDegrees(profile, animationTimeSeconds, out var golemBodyDeg))
                {
                    golemSwing += golemBodyDeg.X * (MathF.PI / 180f) * 0.25f;
                }

                merged = BuildCopperGolem(texRef, profile, isBaby, armSwing: golemSwing);
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
            if (normalizedAssetPath.Contains("/textures/entity/chest/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildChestEntity(texRef, profile, isBaby);
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
            if (normalizedAssetPath.Contains("/textures/entity/player/", StringComparison.OrdinalIgnoreCase) &&
                normalizedAssetPath.Contains("/textures/entity/player/slim/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildPlayerSlim(texRef, profile, isBaby, armLift: 0.18f + idlePhase01 * 0.25f + wave * 0.08f);
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
            if (normalizedAssetPath.Contains("/textures/entity/player/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildPlayerWide(texRef, profile, isBaby, armLift: 0.18f + idlePhase01 * 0.25f + wave * 0.08f);
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
            if (normalizedAssetPath.Contains("/textures/entity/llama/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildLlama(texRef, profile, isBaby, neckBend: idlePhase01 * 0.30f + wave * 0.10f);
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
