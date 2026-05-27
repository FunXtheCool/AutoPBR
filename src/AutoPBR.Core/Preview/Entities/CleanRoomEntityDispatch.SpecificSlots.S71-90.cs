using System.Numerics;
// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Entity-specific GPU bone slot dispatch ladder (<see cref="TryBuildSpecific"/>).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{

/// <summary>GPU specific-model dispatch slots 71-90.</summary>
    private static bool TryBuildSpecificDispatchSlots71Through90(
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
            if (string.Equals(stem, "bee_stinger", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/bee/bee_stinger", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBeeStinger(texRef, profile, isBaby);
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
            if (stem.Contains("bee", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBee(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.65f + wave * 0.25f);
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
            if (stem.Contains("allay", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildAllay(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.70f + wave * 0.22f);
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
            if (stem.Contains("vex", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildVex(
                    texRef,
                    profile,
                    isBaby,
                    yRotDegrees: animationTimeSeconds * 36f,
                    xRotDegrees: idlePhase01 * 8f + wave * 5f,
                    ageInTicks: animationTimeSeconds * 20f,
                    isCharging: false,
                    rightHandHoldingItem: false,
                    leftHandHoldingItem: false);
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
            if (stem.Contains("phantom", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildPhantom(normalizedAssetPath, texRef, profile, isBaby, flapTime: animationTimeSeconds);
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
            if (stem.Contains("parrot", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildParrot(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.55f + wave * 0.22f);
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
            if (stem.Contains("happy_ghast_ropes", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/ghast/happy_ghast_ropes", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/equipment/happy_ghast_body/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildHappyGhastHarness(texRef, profile, isBaby, gogglesEquippedBlend: idlePhase01);
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
            if (stem.Contains("happy_ghast", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/happy_ghast", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/ghast/happy_ghast", StringComparison.OrdinalIgnoreCase))
            {
                // Stem still matches substring "ghast" â€” branch before generic ghast so textures route correctly.
                merged = BuildHappyGhast(texRef, profile, isBaby, tentacleSway: idlePhase01 * 0.5f + wave * 0.25f);
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
            if (stem.Contains("ghast", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildGhast(texRef, profile, isBaby, tentacleSway: idlePhase01 * 0.5f + wave * 0.25f);
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
            if (stem.Contains("guardian_elder", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("elder_guardian", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/guardian_elder", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildGuardian(texRef, profile, isBaby, spinePulse: idlePhase01 * 0.4f + wave * 0.2f, geometryScale: 2.35f);
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
            if (stem.Contains("guardian_beam", StringComparison.OrdinalIgnoreCase))
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
            if (stem.Contains("guardian", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildGuardian(texRef, profile, isBaby, spinePulse: idlePhase01 * 0.4f + wave * 0.2f, geometryScale: 1f);
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
            if (stem.Contains("pufferfish", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildPufferfish(texRef, profile, isBaby, puff: 0.4f + idlePhase01 * 0.25f + wave * 0.12f);
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
            if (stem.Contains("turtle", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTurtle(texRef, profile, isBaby, swimLift: idlePhase01 * 0.20f + wave * 0.08f);
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
            if (stem.Contains("glow_squid", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("squid", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSquid(texRef, profile, isBaby, tentacleWave: idlePhase01 * 0.45f + wave * 0.25f);
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
            if (stem.Contains("salmon", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSalmon(texRef, profile, isBaby, tailSway: idlePhase01 * 0.7f + wave * 0.22f);
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
            if (stem.Contains("cod", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildCod(texRef, profile, isBaby, tailSway: idlePhase01 * 0.8f + wave * 0.25f);
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
            if (stem.Contains("tropical_fish_b", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/fish/tropical_fish_b", StringComparison.OrdinalIgnoreCase))
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
            if (stem.Contains("tropical_fish_a", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/fish/tropical_fish_a", StringComparison.OrdinalIgnoreCase))
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
            if (stem.Contains("tropical_fish", StringComparison.OrdinalIgnoreCase))
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






        return false;
    }
}
