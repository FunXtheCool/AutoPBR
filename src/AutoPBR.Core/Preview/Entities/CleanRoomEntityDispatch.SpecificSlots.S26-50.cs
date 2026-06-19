using System.Numerics;
// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Entity-specific GPU bone slot dispatch ladder (<see cref="TryBuildSpecific"/>).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{

/// <summary>GPU specific-model dispatch slots fragment.</summary>
    private static bool TryBuildSpecificDispatchSlots26Through50(
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
            if (stem.Contains("shulker", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildShulker(
                    texRef,
                    profile,
                    isBaby,
                    peekAmount: Math.Clamp((wave + 1f) * 0.5f, 0f, 1f),
                    ageInTicks: animationTimeSeconds * 20f,
                    xRotDegrees: 0f,
                    yHeadRotDegrees: 180f,
                    yBodyRotDegrees: 0f);
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
            if (stem.Contains("snow_golem", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("snowman", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildSnowGolem(texRef, profile, isBaby, yRotDegrees: animationTimeSeconds * 40f, xRotDegrees: 0f);
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
            if (stem.Contains("iron_golem", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("irongolem", StringComparison.OrdinalIgnoreCase))
            {
                var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
                merged = BuildIronGolem(
                    texRef,
                    profile,
                    isBaby,
                    attackTicksRemaining: 0f,
                    offerFlowerTick: 0,
                    walkAnimationPos: walkPos,
                    walkAnimationSpeed: walkSpeed,
                    yRotDegrees: animationTimeSeconds * 28f,
                    xRotDegrees: 0f);
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
            if (stem.Contains("end_crystal", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/end_crystal/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEndCrystal(texRef, profile, isBaby, spin: idlePhase01 * 180f + animationTimeSeconds * 30f);
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
            if (stem.Contains("evoker_fangs", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/illager/evoker_fangs", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEvokerFangs(texRef, profile, isBaby, bitePhase: idlePhase01);
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
            if (stem.Contains("spit", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildLlamaSpit(texRef, profile, isBaby);
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
            if (stem.Contains("arrow", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildArrow(texRef, profile, isBaby, wobble: wave);
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
            if (stem.Contains("wind_charge", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/projectiles/wind_charge", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildWindCharge(texRef, profile, isBaby, spin: animationTimeSeconds);
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
            if (stem.Contains("trident", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/trident", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildTrident(texRef, profile, isBaby);
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
            if (stem.Contains("shield", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/shield", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.EndsWith("/textures/entity/shield_base.png", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.EndsWith("/textures/entity/shield_base_nopattern.png", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildShield(texRef, profile, isBaby);
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
            if (stem.Contains("banner", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/banner/", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.EndsWith("/textures/entity/banner_base.png", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBannerFlag(texRef, profile, isBaby, isWall: normalizedAssetPath.Contains("/textures/entity/banner_base", StringComparison.OrdinalIgnoreCase));
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
            if (stem.Contains("bed", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/bed/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBed(texRef, profile, isBaby);
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
            if (normalizedAssetPath.Contains("/textures/entity/equipment/", StringComparison.OrdinalIgnoreCase) &&
                !normalizedAssetPath.Contains("/textures/entity/equipment/happy_ghast_body/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);
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
            if (stem.Contains("skull", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/skull/", StringComparison.OrdinalIgnoreCase))
            {
                merged = stem.Contains("piglin", StringComparison.OrdinalIgnoreCase)
                ? BuildPiglinSkull(texRef, profile, isBaby, headPitch: idlePhase01 * 0.2f + wave * 0.1f)
                : BuildSkull(texRef, profile, isBaby, headPitch: idlePhase01 * 0.2f + wave * 0.1f);

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
            if (stem.Contains("bell", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/bell/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBell(texRef, profile, isBaby, swing: idlePhase01 * 0.5f + wave * 0.15f);
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
            if (stem.Contains("minecart", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildMinecart(texRef, profile, isBaby);
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
            if (normalizedAssetPath.Contains("/textures/entity/boat/", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/chest_boat/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildBoatFamily(
                    texRef,
                    profile,
                    isBaby,
                    isChestBoat: normalizedAssetPath.Contains("/textures/entity/chest_boat/", StringComparison.OrdinalIgnoreCase),
                    normalizedAssetPath);
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
            if (stem.Contains("leash_knot", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("lead_knot", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/leash_knot", StringComparison.OrdinalIgnoreCase) ||
                normalizedAssetPath.Contains("/textures/entity/lead_knot", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildLeashKnot(texRef, profile, isBaby);
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
            if (normalizedAssetPath.Contains("/textures/entity/armorstand/", StringComparison.OrdinalIgnoreCase))
            {
                merged = BuildArmorStand(texRef, profile, isBaby);
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
            if (stem.Contains("ravager", StringComparison.OrdinalIgnoreCase))
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
            if (stem.Contains("armadillo", StringComparison.OrdinalIgnoreCase))
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
            if (normalizedAssetPath.Contains("/textures/entity/breeze/", StringComparison.OrdinalIgnoreCase) ||
                stem.Contains("breeze", StringComparison.OrdinalIgnoreCase))
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
            if (stem.Contains("llama", StringComparison.OrdinalIgnoreCase))
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


        if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))
        {
            if (stem.Contains("camel", StringComparison.OrdinalIgnoreCase))
            {
                var babyCamelHeadZ = 0f;
                if (isBaby && DefinitionAnimationPreviewSampling.TrySampleCamelBabyWalkHeadPosition(profile, animationTimeSeconds, out var camelBabyHeadPos))
                {
                    babyCamelHeadZ = camelBabyHeadPos.Z;
                }

                var camelRootRollRad = 0f;
                if (!isBaby && DefinitionAnimationPreviewSampling.TrySampleCamelWalkRootRotationDegrees(profile, animationTimeSeconds, out var camelRootDeg))
                {
                    camelRootRollRad = camelRootDeg.Z * (MathF.PI / 180f);
                }

                merged = BuildCamel(
                    texRef,
                    profile,
                    isBaby,
                    neckBend: idlePhase01 * 0.25f + wave * 0.12f,
                    animationTimeSeconds,
                    idlePhase01,
                    babyWalkHeadTranslateZ: babyCamelHeadZ,
                    adultWalkRootRollRad: camelRootRollRad);
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
            if (stem.Contains("panda", StringComparison.OrdinalIgnoreCase))
            {
                var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave);
                merged = BuildPanda(
                texRef,
                profile,
                isBaby,
                bodyRoll: idlePhase01 * 0.20f + wave * 0.10f,
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





        return false;

    }
}
