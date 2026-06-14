using System.Numerics;
// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Entity-specific GPU bone slot dispatch ladder (<see cref="TryBuildSpecific"/>).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{

/// <summary>GPU specific-model dispatch slots fragment.</summary>
    private static bool TryBuildSpecificDispatchSlots1Through25(
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
            if (normalizedAssetPath.Contains("/textures/entity/nautilus/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildNautilusMob(texRef, profile, isBaby, animationTimeSeconds);
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
            if (stem.Contains("horse_zombie", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("horse_skeleton", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildHorse(texRef, profile, isBaby, neckBend: 0f);
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
            if (stem.Contains("zombie", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildZombieHumanoid(texRef, profile, isBaby, armLift: 1.2f + idlePhase01 * 0.6f + wave * 0.2f);
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
            if (stem.Contains("villager", StringComparison.OrdinalIgnoreCase))
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


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("wandering_trader", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildVillager(texRef, profile, isBaby, headPitch: wave * 0.06f, armFold: 0.2f + wave * 0.04f);
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
            if (stem.Contains("enderman", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEnderman(texRef, profile, isBaby, armLift: 0.16f + wave * 0.05f);
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
            if (stem.Contains("witch", StringComparison.OrdinalIgnoreCase))
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
            if (stem.Contains("evoker", StringComparison.OrdinalIgnoreCase) &&
                !stem.Contains("fang", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEvoker(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);
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
            if (stem.Contains("vindicator", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildVindicator(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);
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
            if (stem.Contains("illusioner", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildIllager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, EntityIllagerPreviewArmPose.Crossed);
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
            if (stem.Contains("pillager", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("illager", StringComparison.OrdinalIgnoreCase))
            {
                var illagerPose = stem.Contains("pillager", StringComparison.OrdinalIgnoreCase)
                    ? EntityIllagerPreviewArmPose.CrossbowHold
                    : EntityIllagerPreviewArmPose.Crossed;
                merged = BuildIllager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, illagerPose);
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
            if (stem.Contains("cow", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                var headPitch = idlePhase01 * 0.35f + wave * 0.15f;
                if (normalizedAssetPath.Contains("/textures/entity/cow/cow_cold", StringComparison.OrdinalIgnoreCase))
                {
                    merged = BuildColdCow(
                        texRef,
                        profile,
                        isBaby,
                        headPitch,
                        hasHorns: true,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                }
                else if (normalizedAssetPath.Contains("/textures/entity/cow/cow_warm", StringComparison.OrdinalIgnoreCase))
                {
                    merged = BuildWarmCow(
                        texRef,
                        profile,
                        isBaby,
                        headPitch,
                        hasHorns: true,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                }
                else
                {
                    merged = BuildCow(
                        texRef,
                        profile,
                        isBaby,
                        headPitch,
                        hasHorns: true,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                }

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
            if (stem.Contains("mooshroom", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildCow(
                texRef,
                profile,
                isBaby,
                headPitch: idlePhase01 * 0.30f + wave * 0.12f,
                hasHorns: true,
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
            if (stem.Contains("wolf", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildWolf(
                texRef,
                profile,
                isBaby,
                headPitch: idlePhase01 * 0.45f + wave * 0.20f,
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
            if (stem.Contains("goat", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildGoat(
                texRef,
                profile,
                isBaby,
                headPitch: idlePhase01 * 0.30f + wave * 0.15f,
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
            if (stem.Contains("zoglin", StringComparison.OrdinalIgnoreCase))
            {
                float rh, lh, rf, lf, headPitch;
                if (isBaby)
                {
                    (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                    headPitch = idlePhase01 * 0.35f + wave * 0.15f;
                }
                else
                {
                    rh = lh = rf = lf = 0f;
                    headPitch = 0f;
                }

                merged = BuildZoglin(texRef, profile, isBaby, headPitch, rh, lh, rf, lf);
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
            if (stem.Contains("hoglin", StringComparison.OrdinalIgnoreCase))
            {
                float rh, lh, rf, lf, headPitch;
                if (isBaby)
                {
                    (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                    headPitch = idlePhase01 * 0.35f + wave * 0.15f;
                }
                else
                {
                    rh = lh = rf = lf = 0f;
                    headPitch = 0f;
                }

                merged = BuildHoglin(texRef, profile, isBaby, headPitch, rh, lh, rf, lf);
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
            if (stem.Contains("sniffer", StringComparison.OrdinalIgnoreCase))
            {
                var snifferHead = idlePhase01 * 0.12f + wave * 0.08f;
                if (DefinitionAnimationPreviewSampling.TrySampleSnifferLongSniffHeadRotationDegrees(profile, animationTimeSeconds, out var sniffHeadDeg))
                {
                    snifferHead += sniffHeadDeg.X * (MathF.PI / 180f);
                }

                if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkHeadRotationDegrees(profile, animationTimeSeconds, out var walkHeadDeg))
                {
                    snifferHead += walkHeadDeg.X * (MathF.PI / 180f);
                }

                if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkBodyRotationDegrees(profile, animationTimeSeconds, out var walkBodyDeg))
                {
                    snifferHead += walkBodyDeg.X * (MathF.PI / 180f) * 0.15f;
                }

                var sniffWalkRf = 0f;
                var sniffWalkLf = 0f;
                if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkRightFrontLegRotationDegrees(profile, animationTimeSeconds, out var sniffRfDeg))
                {
                    sniffWalkRf = sniffRfDeg.X * (MathF.PI / 180f);
                }

                if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftFrontLegRotationDegrees(profile, animationTimeSeconds, out var sniffLfDeg))
                {
                    sniffWalkLf = sniffLfDeg.X * (MathF.PI / 180f);
                }

                const float sniffDegToRad = MathF.PI / 180f;
                var sniffWalkLm = Vector3.Zero;
                if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftMidLegRotationDegrees(profile, animationTimeSeconds, out var sniffLmDeg))
                {
                    sniffWalkLm = new Vector3(
                        sniffLmDeg.X * sniffDegToRad,
                        sniffLmDeg.Y * sniffDegToRad,
                        sniffLmDeg.Z * sniffDegToRad);
                }

                merged = BuildSniffer(
                    texRef,
                    profile,
                    isBaby,
                    headPitch: snifferHead,
                    walkRightFrontLegPitchRad: sniffWalkRf,
                    walkLeftFrontLegPitchRad: sniffWalkLf,
                    walkLeftMidLegEulerRad: sniffWalkLm);
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
            if (stem.Contains("wither", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildWither(texRef, profile, isBaby, wave: idlePhase01 * 0.35f + wave * 0.12f);
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
            if (stem.Contains("warden", StringComparison.OrdinalIgnoreCase))
            {
                var wardenSway = idlePhase01 * 0.30f + wave * 0.10f;
                if (DefinitionAnimationPreviewSampling.TrySampleWardenSniffBodyRotationDegrees(profile, animationTimeSeconds, out var wardenBodyDeg))
                {
                    wardenSway += wardenBodyDeg.Z * (MathF.PI / 180f) * 0.15f;
                }

                merged = BuildWarden(texRef, profile, isBaby, sway: wardenSway);
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
            if (stem.Contains("magma_cube", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("magmacube", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildMagmaCube(texRef, profile, isBaby, squish: MathF.Max(0f, wave));
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
            if (stem.Contains("slime", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSlime(texRef, profile, isBaby);
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
            if (stem.Contains("silverfish", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSilverfish(texRef, profile, isBaby, ageInTicks: wave);
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
            if (stem.Contains("endermite", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEndermite(texRef, profile, isBaby, ageInTicks: wave);
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
            if (stem.Contains("shulker_bullet", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/shulker/spark", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildShulkerBullet(texRef, profile, isBaby, yRotDegrees: animationTimeSeconds * 45f, xRotDegrees: wave * 25f);
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
