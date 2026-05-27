using System.Numerics;
// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Entity-specific GPU bone slot dispatch ladder (<see cref="TryBuildSpecific"/>).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{

/// <summary>GPU specific-model dispatch slots 51-70.</summary>
    private static bool TryBuildSpecificDispatchSlots51Through70(
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
            if (stem.Contains("polar_bear", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("polarbear", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildPolarBear(
                texRef,
                profile,
                isBaby,
                headLift: idlePhase01 * 0.22f + wave * 0.10f,
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
            if (stem.Contains("zombified_piglin", StringComparison.OrdinalIgnoreCase))
            {
                var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
                merged = BuildZombifiedPiglin(
                    texRef,
                    profile,
                    isBaby,
                    headPitch: idlePhase01 * 0.24f + wave * 0.10f,
                    armLift: idlePhase01 * 0.28f + wave * 0.10f,
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


        // Stem "piglin" contains substring "pig"; route before the pig mob check.
        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("piglin", StringComparison.OrdinalIgnoreCase))
            {
                var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
                merged = BuildPiglin(
                    texRef,
                    profile,
                    isBaby,
                    headPitch: idlePhase01 * 0.28f + wave * 0.11f,
                    armLift: idlePhase01 * 0.35f + wave * 0.12f,
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
            if (stem.Contains("pig_cold", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/pig/pig_cold", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildColdPig(texRef, profile, isBaby, snoutBob: 0f, rh, lh, rf, lf);
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
            if (stem.Contains("pig", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildPig(texRef, profile, isBaby, snoutBob: 0f, rh, lh, rf, lf);
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
            if (stem.Contains("sheep", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildSheep(texRef, profile, isBaby, grazeDip: 0.35f + idlePhase01 * 0.25f + wave * 0.25f, rh, lh, rf, lf);
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
            if ((stem.Contains("horse", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("mule", StringComparison.OrdinalIgnoreCase)) &&
                (stem.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("mule", StringComparison.OrdinalIgnoreCase)))
            {
                merged = BuildHorseDonkeyMule(
                    texRef,
                    profile,
                    isBaby,
                    neckBend: isBaby ? 0f : (0.25f + (wave * 0.2f)));
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
            if ((stem.Contains("horse", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("mule", StringComparison.OrdinalIgnoreCase)) &&
                !(stem.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("mule", StringComparison.OrdinalIgnoreCase)))
            {
                merged = BuildHorse(texRef, profile, isBaby, neckBend: isBaby ? 0f : (0.25f + (wave * 0.2f)));
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
            if (stem.Contains("rabbit", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/rabbit/", StringComparison.OrdinalIgnoreCase))
            {
                var hop = Math.Clamp(0.25f + wave * 0.25f + ComputePreviewRabbitHopSinTerm(animationTimeSeconds), -0.75f, 0.75f);
                var tiltOk = isBaby
                    ? DefinitionAnimationPreviewSampling.TrySampleBabyRabbitIdleHeadTiltBodyPosition(profile, animationTimeSeconds, out var tiltBody)
                    : DefinitionAnimationPreviewSampling.TrySampleRabbitIdleHeadTiltBodyPosition(profile, animationTimeSeconds, out tiltBody);
                if (tiltOk)
                {
                    hop = Math.Clamp(hop + tiltBody.Y * 0.18f, -0.75f, 0.75f);
                }

                if (!isBaby && DefinitionAnimationPreviewSampling.TrySampleRabbitHopFrontLegsPosition(profile, animationTimeSeconds, out var hopFrontLegs))
                {
                    hop = Math.Clamp(hop + hopFrontLegs.Y * 0.12f + hopFrontLegs.Z * 0.06f, -0.75f, 0.75f);
                }

                merged = BuildRabbit(texRef, profile, isBaby, hopCompress: hop);
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
            if (stem.Contains("dolphin", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildDolphin(
                texRef,
                profile,
                isBaby,
                swimSway: idlePhase01 * 0.6f + wave * 0.25f + ComputePreviewDolphinSwimOscillation(animationTimeSeconds));
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
            if (stem.Contains("axolotl", StringComparison.OrdinalIgnoreCase))
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


        // Vanilla OcelotModel matches CatModel cuboids; stem is "ocelot" (substring "cat" is false).
        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("cat", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("ocelot", StringComparison.OrdinalIgnoreCase))
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
            if (stem.Contains("fox", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildFox(
                texRef,
                profile,
                isBaby,
                tailLift: 0f,
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
            if (stem.Contains("chicken", StringComparison.OrdinalIgnoreCase))
            {
                ComputeChickenParityPreviewDrivers(
                    animationTimeSeconds,
                    idlePhase01,
                    wave,
                    out var headPitchRad,
                    out var headYawRad,
                    out var wingZ,
                    out var rLeg,
                    out var lLeg);
                merged = IsAdultColdChickenStem(stem) && !isBaby
                    ? BuildColdChicken(
                        texRef,
                        profile,
                        headPitchRad: headPitchRad,
                        headYawRad: headYawRad,
                        wingZRadians: wingZ,
                        rightLegPitchRad: rLeg,
                        leftLegPitchRad: lLeg)
                    : BuildChicken(
                        texRef,
                        profile,
                        isBaby,
                        headPitchRad: headPitchRad,
                        headYawRad: headYawRad,
                        wingZRadians: wingZ,
                        rightLegPitchRad: rLeg,
                        leftLegPitchRad: lLeg);
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
            if (stem.Contains("creeper", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildCreeper(texRef, profile, isBaby, bodyBob: idlePhase01 * 0.2f + wave * 0.1f);
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
            if (stem.Contains("spider", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSpider(texRef, profile, isBaby, legSpread: 0.45f + idlePhase01 * 0.25f + wave * 0.2f);
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
            if (stem.Contains("dragon_fireball", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/enderdragon/dragon_fireball", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildDragonFireball(texRef, profile, isBaby, framePick01: idlePhase01);
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
            if (stem.Contains("enderdragon", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("dragon", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEnderDragon(texRef, profile, isBaby, wingSweep: 0.4f + idlePhase01 * 0.35f + wave * 0.15f);
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
            if (stem.Contains("bat", StringComparison.OrdinalIgnoreCase))
            {
                var batWingFold = idlePhase01 * 0.35f + wave * 0.15f;
                var batRightRy = batWingFold * 0.5f;
                var batLeftRy = batWingFold * 0.5f;
                if (DefinitionAnimationPreviewSampling.TrySampleBatFlyingRightWingRotationDegrees(profile, animationTimeSeconds, out var batRWingDeg) &&
                    DefinitionAnimationPreviewSampling.TrySampleBatFlyingLeftWingRotationDegrees(profile, animationTimeSeconds, out var batLWingDeg))
                {
                    batRightRy = batRWingDeg.Y * (MathF.PI / 180f);
                    batLeftRy = batLWingDeg.Y * (MathF.PI / 180f);
                }

                if (DefinitionAnimationPreviewSampling.TrySampleBatRestingRightWingRotationDegrees(profile, animationTimeSeconds, out var batRestRWingDeg) &&
                    DefinitionAnimationPreviewSampling.TrySampleBatRestingLeftWingRotationDegrees(profile, animationTimeSeconds, out var batRestLWingDeg))
                {
                    const float batRestingWingBlend = 0.22f;
                    var restRight = batRestRWingDeg.Y * (MathF.PI / 180f);
                    var restLeft = batRestLWingDeg.Y * (MathF.PI / 180f);
                    batRightRy = batRightRy + (restRight - batRightRy) * batRestingWingBlend;
                    batLeftRy = batLeftRy + (restLeft - batLeftRy) * batRestingWingBlend;
                }

                var batRightWingZ = 0f;
                var batLeftWingZ = 0f;
                if (DefinitionAnimationPreviewSampling.TrySampleBatRestingRightWingPosition(profile, animationTimeSeconds, out var batRestRPos) &&
                    DefinitionAnimationPreviewSampling.TrySampleBatRestingLeftWingPosition(profile, animationTimeSeconds, out var batRestLPos))
                {
                    const float batRestingPosBlend = 0.22f;
                    batRightWingZ = batRestRPos.Z * batRestingPosBlend;
                    batLeftWingZ = batRestLPos.Z * batRestingPosBlend;
                }

                merged = BuildBat(
                    texRef,
                    profile,
                    isBaby,
                    rightWingYawRad: batRightRy,
                    leftWingYawRad: batLeftRy,
                    restingWingPivotZRight: batRightWingZ,
                    restingWingPivotZLeft: batLeftWingZ);
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
            if (stem.Contains("blaze", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBlaze(texRef, profile, isBaby, rodSpin: idlePhase01 * 0.65f + wave * 0.25f);
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
