using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    /// <summary>Invokes the rig named by <paramref name="builderMethod"/> (26.1.2 parity manifest <c>builder_method</c>).</summary>
    private static bool TryInvokeParityCatalogBuilder(
        string builderMethod,
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        out MergedJavaBlockModel merged)
    {
        merged = null!;
        var wave = Wave(animationTimeSeconds, 0.8f);
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
            case "Cow":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Cow");
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

                    return true;
                }
            case "Wolf":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Wolf");
                    merged = BuildWolf(
                        texRef,
                        profile,
                        isBaby,
                        headPitch: idlePhase01 * 0.45f + wave * 0.20f,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                    return true;
                }
            case "Fox":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Fox");
                    if (isBaby && DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkRightHindLegRotationDegrees(
                            profile, animationTimeSeconds, out var foxBabyRhDeg))
                    {
                        rh += foxBabyRhDeg.X * (MathF.PI / 180f);
                    }

                    merged = BuildFox(
                        texRef,
                        profile,
                        isBaby,
                        tailLift: 0f,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                    return true;
                }
            case "Goat":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Goat");
                    merged = BuildGoat(
                        texRef,
                        profile,
                        isBaby,
                        headPitch: idlePhase01 * 0.30f + wave * 0.15f,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                    return true;
                }
            case "Hoglin":
                {
                    float rh, lh, rf, lf, headPitch;
                    if (isBaby)
                    {
                        (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Hoglin");
                        headPitch = idlePhase01 * 0.35f + wave * 0.15f;
                    }
                    else
                    {
                        rh = lh = rf = lf = 0f;
                        headPitch = 0f;
                    }

                    merged = BuildHoglin(texRef, profile, isBaby, headPitch, rh, lh, rf, lf);
                    return true;
                }
            case "Sniffer":
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
                    return true;
                }
            case "Wither":
                // Manifest currently routes wither_skeleton through "Wither"; keep that texture on skeleton rig parity.
                merged = normalizedAssetPath.Contains("/textures/entity/skeleton/wither_skeleton", StringComparison.OrdinalIgnoreCase)
                    ? BuildSkeletonHumanoid(texRef, profile, isBaby, armLift: idlePhase01 * 0.25f + wave * 0.10f)
                    : BuildWither(texRef, profile, isBaby, wave: idlePhase01 * 0.35f + wave * 0.12f);
                return true;
            case "Warden":
                {
                    var wardenSway = idlePhase01 * 0.30f + wave * 0.10f;
                    if (DefinitionAnimationPreviewSampling.TrySampleWardenSniffBodyRotationDegrees(profile, animationTimeSeconds, out var wardenBodyDeg))
                    {
                        wardenSway += wardenBodyDeg.Z * (MathF.PI / 180f) * 0.15f;
                    }

                    merged = BuildWarden(texRef, profile, isBaby, sway: wardenSway);
                    return true;
                }
            case "MagmaCube":
                merged = BuildMagmaCube(texRef, profile, isBaby, squish: MathF.Max(0f, wave));
                return true;
            case "Slime":
                merged = BuildSlime(texRef, profile, isBaby);
                return true;
            case "Silverfish":
                merged = BuildSilverfish(texRef, profile, isBaby, ageInTicks: wave);
                return true;
            case "Endermite":
                merged = BuildEndermite(texRef, profile, isBaby, ageInTicks: wave);
                return true;
            case "ShulkerBullet":
                merged = BuildShulkerBullet(texRef, profile, isBaby, yRotDegrees: animationTimeSeconds * 45f, xRotDegrees: wave * 25f);
                return true;
            case "Shulker":
                merged = BuildShulker(
                    texRef,
                    profile,
                    isBaby,
                    peekAmount: Math.Clamp((wave + 1f) * 0.5f, 0f, 1f),
                    ageInTicks: animationTimeSeconds * 20f,
                    xRotDegrees: 0f,
                    yHeadRotDegrees: 180f,
                    yBodyRotDegrees: 0f);
                return true;
            case "SnowGolem":
                merged = BuildSnowGolem(texRef, profile, isBaby, yRotDegrees: animationTimeSeconds * 40f, xRotDegrees: 0f);
                return true;
            case "IronGolem":
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
                    return true;
                }
            case "EndCrystal":
                merged = BuildEndCrystal(texRef, profile, isBaby, spin: idlePhase01 * 180f + animationTimeSeconds * 30f);
                return true;
            case "EvokerFangs":
                merged = BuildEvokerFangs(texRef, profile, isBaby, bitePhase: idlePhase01);
                return true;
            case "LlamaSpit":
                merged = BuildLlamaSpit(texRef, profile, isBaby);
                return true;
            case "Arrow":
                merged = BuildArrow(texRef, profile, isBaby, wobble: wave);
                return true;
            case "ArrowSpectral":
                merged = BuildArrow(texRef, profile, isBaby, wobble: wave);
                return true;
            case "ArrowTipped":
                merged = BuildArrow(texRef, profile, isBaby, wobble: wave);
                return true;
            case "WindCharge":
                merged = BuildWindCharge(texRef, profile, isBaby, spin: animationTimeSeconds);
                return true;
            case "Trident":
                merged = BuildTrident(texRef, profile, isBaby);
                return true;
            case "Shield":
                merged = BuildShield(texRef, profile, isBaby);
                return true;
            case "BannerFlagStanding":
                merged = BuildBannerFlag(texRef, profile, isBaby, isWall: false);
                return true;
            case "BannerFlagWall":
                merged = BuildBannerFlag(texRef, profile, isBaby, isWall: true);
                return true;
            case "Bed":
                merged = BuildBed(texRef, profile, isBaby);
                return true;
            case "EquipmentLayer":
                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);
                return true;
            case "EquipmentWings":
                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);
                return true;
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
            case "Camel":
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
                    return true;
                }
            case "Panda":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Panda");
                    merged = BuildPanda(
                        texRef,
                        profile,
                        isBaby,
                        bodyRoll: idlePhase01 * 0.20f + wave * 0.10f,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                    return true;
                }
            case "PolarBear":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "PolarBear");
                    merged = BuildPolarBear(
                        texRef,
                        profile,
                        isBaby,
                        headLift: idlePhase01 * 0.22f + wave * 0.10f,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                    return true;
                }
            case "Piglin":
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
                    return true;
                }
            case "ZombifiedPiglin":
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
                    return true;
                }
            case "Pig":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Pig");
                    merged = BuildPig(texRef, profile, isBaby, snoutBob: 0f, rh, lh, rf, lf);
                    return true;
                }
            case "ColdPig":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "ColdPig");
                    merged = BuildColdPig(texRef, profile, isBaby, snoutBob: 0f, rh, lh, rf, lf);
                    return true;
                }
            case "Sheep":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Sheep");
                    merged = BuildSheep(texRef, profile, isBaby, grazeDip: 0.35f + idlePhase01 * 0.25f + wave * 0.25f, rh, lh, rf, lf);
                    return true;
                }
            case "Rabbit":
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
                    return true;
                }
            case "Dolphin":
                merged = BuildDolphin(
                    texRef,
                    profile,
                    isBaby,
                    swimSway: idlePhase01 * 0.6f + wave * 0.25f + ComputePreviewDolphinSwimOscillation(animationTimeSeconds));
                return true;
            case "Cat":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Cat");
                    merged = BuildCat(
                        texRef,
                        profile,
                        isBaby,
                        headTilt: idlePhase01 * 0.2f + wave * 0.1f,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                    return true;
                }
            case "BabyFeline":
                {
                    if (!isBaby)
                    {
                        return false;
                    }

                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "BabyFeline");
                    merged = BuildBabyFeline(
                        texRef,
                        headPitchRad: idlePhase01 * 0.2f + wave * 0.1f,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                    return true;
                }
            case "Chicken":
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
                        ? BuildColdChicken(texRef, profile, headPitchRad, headYawRad, wingZ, rLeg, lLeg)
                        : BuildChicken(
                            texRef,
                            profile,
                            isBaby,
                            headPitchRad: headPitchRad,
                            headYawRad: headYawRad,
                            wingZRadians: wingZ,
                            rightLegPitchRad: rLeg,
                            leftLegPitchRad: lLeg);
                    return true;
                }
            case "Creeper":
                merged = BuildCreeper(texRef, profile, isBaby, bodyBob: idlePhase01 * 0.2f + wave * 0.1f);
                return true;
            case "Spider":
                merged = BuildSpider(texRef, profile, isBaby, legSpread: 0.45f + idlePhase01 * 0.25f + wave * 0.2f);
                return true;
            case "DragonFireball":
                merged = BuildDragonFireball(texRef, profile, isBaby, framePick01: idlePhase01);
                return true;
            case "EnderDragon":
                merged = BuildEnderDragon(texRef, profile, isBaby, wingSweep: 0.4f + idlePhase01 * 0.35f + wave * 0.15f);
                return true;
            case "Bat":
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
                    return true;
                }
            case "Blaze":
                merged = BuildBlaze(texRef, profile, isBaby, rodSpin: idlePhase01 * 0.65f + wave * 0.25f);
                return true;
            case "BeeStinger":
                merged = BuildBeeStinger(texRef, profile, isBaby);
                return true;
            case "Bee":
                merged = BuildBee(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.65f + wave * 0.25f);
                return true;
            case "Allay":
                merged = BuildAllay(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.70f + wave * 0.22f);
                return true;
            case "Vex":
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
                return true;
            case "Phantom":
                merged = BuildPhantom(normalizedAssetPath, texRef, profile, isBaby, flapTime: animationTimeSeconds);
                return true;
            case "Parrot":
                merged = BuildParrot(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.55f + wave * 0.22f);
                return true;
            case "HappyGhastHarness":
                merged = BuildHappyGhastHarness(texRef, profile, isBaby, gogglesEquippedBlend: idlePhase01);
                return true;
            case "HappyGhast":
                merged = BuildHappyGhast(texRef, profile, isBaby, tentacleSway: idlePhase01 * 0.5f + wave * 0.25f);
                return true;
            case "Ghast":
                merged = BuildGhast(texRef, profile, isBaby, tentacleSway: idlePhase01 * 0.5f + wave * 0.25f);
                return true;
            case "GuardianElder":
                merged = BuildGuardian(texRef, profile, isBaby, spinePulse: idlePhase01 * 0.4f + wave * 0.2f, geometryScale: 2.35f);
                return true;
            case "Guardian":
                merged = BuildGuardian(texRef, profile, isBaby, spinePulse: idlePhase01 * 0.4f + wave * 0.2f, geometryScale: 1f);
                return true;
            case "GuardianBeam":
                merged = BuildBeamColumn(texRef, profile, isBaby, twist: idlePhase01 * MathF.PI * 2f);
                return true;
            case "Pufferfish":
                merged = BuildPufferfish(texRef, profile, isBaby, puff: 0.4f + idlePhase01 * 0.25f + wave * 0.12f);
                return true;
            case "Turtle":
                merged = BuildTurtle(texRef, profile, isBaby, swimLift: idlePhase01 * 0.20f + wave * 0.08f);
                return true;
            case "Squid":
                merged = BuildSquid(texRef, profile, isBaby, tentacleWave: idlePhase01 * 0.45f + wave * 0.25f);
                return true;
            case "Salmon":
                merged = BuildSalmon(texRef, profile, isBaby, tailSway: idlePhase01 * 0.7f + wave * 0.22f);
                return true;
            case "Cod":
                merged = BuildCod(texRef, profile, isBaby, tailSway: idlePhase01 * 0.8f + wave * 0.25f);
                return true;
            case "TropicalFishB":
                merged = BuildTropicalFishB(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);
                return true;
            case "TropicalFishA":
                merged = BuildTropicalFishA(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);
                return true;
            case "Strider":
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
                    return true;
                }
            case "Tadpole":
                merged = BuildTadpole(texRef, profile, isBaby, tailSway: idlePhase01 * 0.45f + wave * 0.2f);
                return true;
            case "Axolotl":
                {
                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Axolotl");
                    merged = BuildAxolotl(
                        texRef,
                        profile,
                        isBaby,
                        idleBob: idlePhase01 * 0.12f + wave * 0.06f,
                        rightHindLegPitchRad: rh,
                        leftHindLegPitchRad: lh,
                        rightFrontLegPitchRad: rf,
                        leftFrontLegPitchRad: lf);
                    return true;
                }
            case "Frog":
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
                    return true;
                }
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
            case "PlayerHumanoid":
                merged = BuildHumanoid(texRef, profile, isBaby, armLift: 0.18f + idlePhase01 * 0.25f + wave * 0.08f);
                return true;
            case "PlayerSlim":
                merged = BuildPlayerSlim(texRef, profile, isBaby, armLift: 0.18f + idlePhase01 * 0.25f + wave * 0.08f);
                return true;
            case "PlayerWide":
                merged = BuildPlayerWide(texRef, profile, isBaby, armLift: 0.18f + idlePhase01 * 0.25f + wave * 0.08f);
                return true;
            case "HumanoidGeneric":
                merged = BuildHumanoid(texRef, profile, isBaby, armLift: idlePhase01 * 0.4f + wave * 0.1f);
                return true;
            default:
                return false;
        }
    }
}
