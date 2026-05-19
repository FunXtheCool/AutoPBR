using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Humanoid, piglin, and illager rigs.

    /// <summary>Preview stand-in for vanilla <c>AbstractIllager.IllagerArmPose</c> (<c>IllagerModel.setupAnim</c>, 26.1.2 <c>client.jar</c>).</summary>
    private enum IllagerPreviewArmPoseKind
    {
        Crossed,
        AttackingEmptyHands,
        AttackingWeapon,
        Spellcasting,
        BowAndArrow,
        CrossbowHold,
        CrossbowCharge,
        Celebrating,
    }


    private static MergedJavaBlockModel BuildHumanoid(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float armLift)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.74f, 1.22f, 0.75f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.78f, 1.12f, 0.82f) : BabyProfile.Adult);

        var b = new RigBuilder(64, 64);
        // Canonical biped baseline: body 8x12x4, head 8x8x8, arms/legs 4x12x4.
        new EntityCuboid(4, 12, 6, 12, 24, 10, 16, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale); // torso
        new EntityCuboid(4, 24, 4, 12, 32, 12, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // head
        new EntityCuboid(0, 12, 6, 4, 24, 10, 40, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(2f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // arm l
        new EntityCuboid(12, 12, 6, 16, 24, 10, 40, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(14f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // arm r
        new EntityCuboid(4, 0, 6, 8, 12, 10, 0, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // leg l
        new EntityCuboid(8, 0, 6, 12, 12, 10, 0, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // leg r
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BabyZombieModel.createBodyLayer</c> — geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.zombie.BabyZombieModel.json</c> (<c>64×64</c>):
    /// <c>body</c> @ <c>T(0,17.5,0)</c>; <c>head</c> @ <c>T(0,15.25,0)</c> (two stacked cuboids); arms @ <c>T(∓3,15.5,0)</c>; legs @ <c>T(∓1,20,0)</c>.
    /// DFS order matches IR. Arm <c>xRot</c> uses the same preview driver as adult zombie routes (<paramref name="armLiftRad"/>).
    /// </summary>
    private static MergedJavaBlockModel BuildBabyZombie(string texRef, float armLiftRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 17.5f, 0f));
        new EntityCuboid(-2f, -2.5f, -1f, 2f, 2.5f, 1f, 16, 16, UvSizeW: 4, UvSizeH: 5, UvSizeD: 2).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 15.25f, 0f));
        new EntityCuboid(-6.25f, -3f, 6f, -0.25f, 3f, 6f, 3, 3, UvSizeW: 6, UvSizeH: 6, UvSizeD: 1).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-6.15f, -3f, 6f, -0.15f, 3f, 6.25f, 35, 3, UvSizeW: 6, UvSizeH: 6, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        var rightArmPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-3f, 15.5f, 0f)),
            EntityParityTemplate.Rx(armLiftRad));
        new EntityCuboid(-1f, -0.5f, -1f, 1f, 4.5f, 1f, 36, 16, UvSizeW: 2, UvSizeH: 5, UvSizeD: 2).Emit(b, rightArmPose, p.BodyScale);

        var leftArmPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(3f, 15.5f, 0f)),
            EntityParityTemplate.Rx(armLiftRad));
        new EntityCuboid(-1f, -0.5f, -1f, 1f, 4.5f, 1f, 28, 16, UvSizeW: 2, UvSizeH: 5, UvSizeD: 2).Emit(b, leftArmPose, p.BodyScale);

        var rightLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1f, 20f, 0f));
        new EntityCuboid(-1f, 0f, -1f, 1f, 4f, 1f, 8, 16, UvSizeW: 2, UvSizeH: 4, UvSizeD: 2).Emit(b, rightLegPose, p.LegScale);

        var leftLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 20f, 0f));
        new EntityCuboid(-1f, 0f, -1f, 1f, 4f, 1f, 0, 16, UvSizeW: 2, UvSizeH: 4, UvSizeD: 2).Emit(b, leftLegPose, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// Catalogued baby drowned skins use the same mesh as <see cref="BuildBabyZombie"/>; geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.zombie.BabyDrownedModel.json</c> matches
    /// <c>BabyZombieModel.json</c> because vanilla delegates <c>createBodyLayer</c>.
    /// </summary>
    /// <remarks>
    /// <para>26.1.2 <c>tools/minecraft-parity/26.1.2/client.jar</c> — <c>javap -c -p net.minecraft.client.model.monster.zombie.BabyDrownedModel</c>:
    /// <c>createBodyLayer(CubeDeformation)</c> is <c>aload_0</c> then
    /// <c>invokestatic net/minecraft/client/model/monster/zombie/BabyZombieModel.createBodyLayer:(Lnet/minecraft/client/model/geom/builders/CubeDeformation;)Lnet/minecraft/client/model/geom/builders/LayerDefinition;</c>
    /// (<c>areturn</c>). No distinct <c>CubeListBuilder</c> literals on <c>BabyDrownedModel</c>.</para>
    /// </remarks>
    private static MergedJavaBlockModel BuildBabyDrowned(string texRef, float armLiftRad) => BuildBabyZombie(texRef, armLiftRad);

    /// <summary>
    /// <c>BabyZombieVillagerModel.createBodyLayer</c> — geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.zombie.BabyZombieVillagerModel.json</c> (<c>64×64</c>).
    /// </summary>
    private static MergedJavaBlockModel BuildBabyZombieVillager(string texRef, float armLiftRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 18.75f, 0f));
        new EntityCuboid(-2f, -2.75f, -1.5f, 2f, 2.25f, 1.5f, 0, 15, UvSizeW: 4, UvSizeH: 5, UvSizeD: 3).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-2.75f, -1.5f, 4f, 3.25f, 1.5f, 4.1f, 16, 22, UvSizeW: 6, UvSizeH: 3, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        var headRoot = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 16f, 0f));
        new EntityCuboid(-4f, -8f, -3.5f, 4f, 0f, 3.5f, 0, 0, UvSizeW: 8, UvSizeH: 8, UvSizeD: 7).Emit(b, headRoot, p.HeadScale);

        var hatPose = EntityParityTemplate.Mul(headRoot, EntityParityTemplate.T(0f, -4f, 0f));
        new EntityCuboid(-4f, -3.5f, 8f, 4f, 3.5f, 8.3f, 0, 31, UvSizeW: 8, UvSizeH: 7, UvSizeD: 1).Emit(b, hatPose, p.HeadScale);

        var hatRimPose = EntityParityTemplate.Mul(headRoot, EntityParityTemplate.T(0f, -4.5f, 0f));
        new EntityCuboid(-7f, -0.5f, -6f, 7f, 0.5f, 6f, 0, 46, UvSizeW: 14, UvSizeH: 1, UvSizeD: 12).Emit(b, hatRimPose, p.HeadScale);

        var nosePose = EntityParityTemplate.Mul(headRoot, EntityParityTemplate.T(0f, -1f, -4f));
        new EntityCuboid(-1f, -1f, -0.5f, 1f, 1f, 0.5f, 23, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, nosePose, p.HeadScale);

        var rightArmPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-3f, 15.5f, 0f)),
            EntityParityTemplate.Rx(armLiftRad));
        new EntityCuboid(-0.5f, -1f, 2f, 4.5f, 1f, 2f, 24, 15, UvSizeW: 5, UvSizeH: 2, UvSizeD: 1).Emit(b, rightArmPose, p.BodyScale);

        var leftArmPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(3f, 15.5f, 0f)),
            EntityParityTemplate.Rx(armLiftRad));
        new EntityCuboid(-0.5f, -1f, 2f, 4.5f, 1f, 2f, 16, 15, UvSizeW: 5, UvSizeH: 2, UvSizeD: 1).Emit(b, leftArmPose, p.BodyScale);

        var rightLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1f, 21.5f, 0f));
        new EntityCuboid(-1f, -0.5f, -1f, 1f, 2.5f, 1f, 8, 23, UvSizeW: 2, UvSizeH: 3, UvSizeD: 2).Emit(b, rightLegPose, p.LegScale);

        var leftLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 21.5f, 0f));
        new EntityCuboid(-1f, -0.5f, -1f, 1f, 2.5f, 1f, 0, 23, UvSizeW: 2, UvSizeH: 3, UvSizeD: 2).Emit(b, leftLegPose, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BabyVillagerModel.createBodyLayer</c> — geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.npc.BabyVillagerModel.json</c> (<c>64×64</c>):
    /// root DFS order <c>right_hand</c>, <c>middlearm_r1</c>, legs, then head stack (<c>head</c>/<c>hat</c>/<c>hat_rim</c>/<c>nose</c>), <c>body</c>, <c>bb_main</c>.
    /// Head look uses <paramref name="headPitchRad"/> on the villager head pivot (same preview channel as <see cref="BuildVillager"/>).
    /// </summary>
    private static MergedJavaBlockModel BuildBabyVillager(string texRef, float headPitchRad, float armFoldRad)
    {
        _ = armFoldRad;
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        const float foldedArmX = -1.0472f;
        var rightHandPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-3f, 1.4025f, -0.9599f)),
            EntityParityTemplate.Rx(foldedArmX));
        new EntityCuboid(-1f, -2.4925f, -1.8401f, 1f, 1.5075f, 0.1599f, 36, 15, UvSizeW: 2, UvSizeH: 4, UvSizeD: 2).Emit(b, rightHandPose, p.BodyScale);
        new EntityCuboid(5f, -2.4925f, -1.8401f, 7f, 1.5075f, 0.1599f, 16, 15, UvSizeW: 2, UvSizeH: 4, UvSizeD: 2).Emit(b, rightHandPose, p.BodyScale);

        var middleArmPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 0.9024f, -1.8175f)),
            EntityParityTemplate.Rx(foldedArmX));
        new EntityCuboid(-2f, -0.9924f, -0.9825f, 2f, 1.0076f, 1.0175f, 24, 17, UvSizeW: 4, UvSizeH: 2, UvSizeD: 2).Emit(b, middleArmPose, p.BodyScale);

        var rightLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1f, 21.5f, 0f));
        new EntityCuboid(-1f, -0.5f, -1f, 1f, 2.5f, 1f, 8, 23, UvSizeW: 2, UvSizeH: 3, UvSizeD: 2).Emit(b, rightLegPose, p.LegScale);

        var leftLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 21.5f, 0f));
        new EntityCuboid(-1f, -0.5f, -1f, 1f, 2.5f, 1f, 0, 23, UvSizeW: 2, UvSizeH: 3, UvSizeD: 2).Emit(b, leftLegPose, p.LegScale);

        var headLook = EntityParityTemplate.Mul(
            root,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 16f, 0f), EntityParityTemplate.Rx(headPitchRad)));
        new EntityCuboid(-4f, -8f, -3.5f, 4f, 0f, 3.5f, 0, 0, UvSizeW: 8, UvSizeH: 8, UvSizeD: 7).Emit(b, headLook, p.HeadScale);

        var hatPose = EntityParityTemplate.Mul(headLook, EntityParityTemplate.T(0f, -4f, 0f));
        new EntityCuboid(-4f, -3.5f, 8f, 4f, 3.5f, 8.3f, 0, 30, UvSizeW: 8, UvSizeH: 7, UvSizeD: 1).Emit(b, hatPose, p.HeadScale);

        var hatRimPose = EntityParityTemplate.Mul(headLook, EntityParityTemplate.T(0f, -4.5f, 0f));
        new EntityCuboid(-7f, -0.5f, -6f, 7f, 0.5f, 6f, 0, 45, UvSizeW: 14, UvSizeH: 1, UvSizeD: 12).Emit(b, hatRimPose, p.HeadScale);

        var nosePose = EntityParityTemplate.Mul(headLook, EntityParityTemplate.T(0f, -2f, -4f));
        new EntityCuboid(-1f, 0f, -0.5f, 1f, 2f, 0.5f, 23, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, nosePose, p.HeadScale);

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 18.75f, 0f));
        new EntityCuboid(-2f, -2.75f, -1.5f, 2f, 2.25f, 1.5f, 0, 15, UvSizeW: 4, UvSizeH: 5, UvSizeD: 3).Emit(b, bodyPose, p.BodyScale);

        var bbMainPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0.5f, 24f, 0f));
        new EntityCuboid(-8f, -1.5f, 4f, -2f, 1.5f, 4.2f, 16, 21, UvSizeW: 6, UvSizeH: 3, UvSizeD: 1).Emit(b, bbMainPose, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildZombieHumanoid(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float armLift)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            // BabyZombieModel mesh; BabyDrownedModel.createBodyLayer forwards to it (javap). Catalogued zombie + drowned + husk baby skins.
            return BuildBabyDrowned(texRef, armLift);
        }

        // Zombie-family keeps canonical biped proportions but with the attack-forward arm channel.
        return BuildHumanoid(texRef, profile, isBaby, armLift);
    }

    /// <summary>
    /// Vanilla baby skeleton/stray/bogged/wither skeleton reuse <c>SkeletonModel</c> (<see cref="UsesPostBabyModelUpdate"/> profiles) with uniform
    /// <c>LivingEntity.getAgeScale()</c> — same mesh as adult, IR <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.skeleton.SkeletonModel.json</c>.
    /// </summary>
    private static MergedJavaBlockModel BuildSkeletonHumanoid(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float armLift)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? BabyProfile.VanillaUniformBaby : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.10f, 0.82f) : BabyProfile.Adult);

        var b = new RigBuilder(64, 32);
        // Skeleton/stray/bogged proportions: torso 8x12x4, head 8x8x8, limbs 2x12x2.
        new EntityCuboid(4f, 12f, 6f, 12f, 24f, 10f, 16, 16).Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(4f, 24f, 4f, 12f, 32f, 12f, 0, 0).Emit(b, Matrix4x4.Identity, p.HeadScale);
        new EntityCuboid(2f, 12f, 7f, 4f, 24f, 9f, 40, 16, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(3f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // arm l
        new EntityCuboid(12f, 12f, 7f, 14f, 24f, 9f, 40, 16, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(13f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // arm r
        new EntityCuboid(6f, 0f, 7f, 8f, 12f, 9f, 0, 16).Emit(b, Matrix4x4.Identity, p.LegScale); // leg l
        new EntityCuboid(8f, 0f, 7f, 10f, 12f, 9f, 0, 16).Emit(b, Matrix4x4.Identity, p.LegScale); // leg r
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// Wide <c>PlayerModel</c> (Steve): baby avatars reuse the adult baked layer with uniform <c>getAgeScale()</c> (<c>AvatarRenderer</c> passes shadow radius <c>0.5F</c>;
    /// age scale is still <c>LivingEntity.DEFAULT_BABY_SCALE</c>). Geometry IR: <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.player.PlayerModel.json</c> when indexed.
    /// </summary>
    private static MergedJavaBlockModel BuildPlayerWide(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float armLift)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? BabyProfile.VanillaUniformBaby : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.78f, 1.12f, 0.82f) : BabyProfile.Adult);

        var b = new RigBuilder(64, 64);
        // Steve geometry: canonical 4px arms, but player UV channels differ from generic humanoid (left arm/left leg use 2nd sheet columns).
        new EntityCuboid(4, 12, 6, 12, 24, 10, 16, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale); // torso
        new EntityCuboid(4, 24, 4, 12, 32, 12, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // head
        new EntityCuboid(0, 12, 6, 4, 24, 10, 32, 48, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(2f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // left arm
        new EntityCuboid(12, 12, 6, 16, 24, 10, 40, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(14f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // right arm
        new EntityCuboid(4, 0, 6, 8, 12, 10, 16, 48, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // left leg
        new EntityCuboid(8, 0, 6, 12, 12, 10, 0, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // right leg
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>Alex (slim) player — same uniform baby scale convention as <see cref="BuildPlayerWide"/>.</summary>
    private static MergedJavaBlockModel BuildPlayerSlim(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float armLift)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? BabyProfile.VanillaUniformBaby : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.78f, 1.12f, 0.82f) : BabyProfile.Adult);

        var b = new RigBuilder(64, 64);
        // Alex geometry: torso/head/legs match humanoid; arms are 3x12x4 instead of 4x12x4.
        new EntityCuboid(4, 12, 6, 12, 24, 10, 16, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale); // torso
        new EntityCuboid(4, 24, 4, 12, 32, 12, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // head
        new EntityCuboid(1, 12, 6, 4, 24, 10, 32, 48, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(2.5f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // arm l slim
        new EntityCuboid(12, 12, 6, 15, 24, 10, 40, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(13.5f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // arm r slim
        new EntityCuboid(4, 0, 6, 8, 12, 10, 16, 48, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // leg l
        new EntityCuboid(8, 0, 6, 12, 12, 10, 0, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // leg r
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildVillager(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        float armFold)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildBabyVillager(texRef, headPitch, armFold);
        }

        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.72f, 1.24f, 0.74f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.78f, 1.14f, 0.80f) : BabyProfile.Adult);

        var b = new RigBuilder(64, 64);
        // Villager family baseline: head 8x10x8 + nose 2x4x2, robe body 8x12x6, two 4x12x4 legs.
        new EntityCuboid(4f, 24f, 4f, 12f, 34f, 12f, 0, 0, XRot: headPitch, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(8f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.HeadScale); // head + hat region
        new EntityCuboid(7f, 28f, 12f, 9f, 32f, 14f, 24, 0, XRot: headPitch, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(8f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.HeadScale); // nose
        new EntityCuboid(4f, 12f, 5f, 12f, 24f, 11f, 16, 20).Emit(b, Matrix4x4.Identity, p.BodyScale); // robe body
        new EntityCuboid(3f, 18f, 5f, 13f, 22f, 11f, 44, 22, XRot: armFold, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(8f, 20f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // folded arms
        new EntityCuboid(4f, 0f, 6f, 8f, 12f, 10f, 0, 22).Emit(b, Matrix4x4.Identity, p.LegScale); // leg l
        new EntityCuboid(8f, 0f, 6f, 12f, 12f, 10f, 0, 22).Emit(b, Matrix4x4.Identity, p.LegScale); // leg r
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildZombieVillager(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float armLift)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildBabyZombieVillager(texRef, armLift);
        }

        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.74f, 1.22f, 0.75f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.78f, 1.12f, 0.82f) : BabyProfile.Adult);

        var b = new RigBuilder(64, 64);
        // Zombie villager: zombie biped limbs/body plus villager-style nose.
        new EntityCuboid(4f, 12f, 6f, 12f, 24f, 10f, 16, 16).Emit(b, Matrix4x4.Identity, p.BodyScale); // torso
        new EntityCuboid(4f, 24f, 4f, 12f, 32f, 12f, 0, 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // head
        new EntityCuboid(7f, 28f, 12f, 9f, 32f, 14f, 24, 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // nose
        new EntityCuboid(0f, 12f, 6f, 4f, 24f, 10f, 40, 16, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(2f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // arm l
        new EntityCuboid(12f, 12f, 6f, 16f, 24f, 10f, 40, 16, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(14f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // arm r
        new EntityCuboid(4f, 0f, 6f, 8f, 12f, 10f, 0, 16).Emit(b, Matrix4x4.Identity, p.LegScale); // leg l
        new EntityCuboid(8f, 0f, 6f, 12f, 12f, 10f, 0, 16).Emit(b, Matrix4x4.Identity, p.LegScale); // leg r
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildEnderman(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float armLift)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.68f, 1.26f, 0.70f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.74f, 1.18f, 0.76f) : BabyProfile.Adult);

        var b = new RigBuilder(64, 32);
        // Enderman: slender long limbs (2x30x2), body 8x12x4, head 8x8x8.
        new EntityCuboid(4f, 30f, 6f, 12f, 42f, 10f, 32, 16).Emit(b, Matrix4x4.Identity, p.BodyScale); // torso
        new EntityCuboid(4f, 42f, 4f, 12f, 50f, 12f, 0, 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // head
        new EntityCuboid(2f, 30f, 7f, 4f, 60f, 9f, 56, 0, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(3f, 60f, 8f) }.Emit(b, Matrix4x4.Identity, p.LegScale); // arm l
        new EntityCuboid(12f, 30f, 7f, 14f, 60f, 9f, 56, 0, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(13f, 60f, 8f) }.Emit(b, Matrix4x4.Identity, p.LegScale); // arm r
        new EntityCuboid(6f, 0f, 7f, 8f, 30f, 9f, 56, 0).Emit(b, Matrix4x4.Identity, p.LegScale); // leg l
        new EntityCuboid(8f, 0f, 7f, 10f, 30f, 9f, 56, 0).Emit(b, Matrix4x4.Identity, p.LegScale); // leg r
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BabyPiglinModel.createBodyLayer</c> (26.1.2 <c>client.jar</c> javap) + <c>AbstractPiglinModel.setupAnim</c> ear motion
    /// (<c>getDefaultEarAngleInDegrees</c> = <c>5</c> for babies). Ears: <c>head</c> → <c>left_ear</c>/<c>right_ear</c> @ <c>(±4.2,-4,0)</c>
    /// → <c>*_r1</c> with <c>PartPose.offsetAndRotation(±1,1.75,0,0,0,∓0.6109)</c> (flattened IR lists ears as root siblings — hierarchy follows Java).
    /// <c>BabyZombifiedPiglinModel.createBodyLayer</c> forwards to this method per javap (see <see cref="BuildBabyZombifiedPiglin"/> remarks).
    /// </summary>
    private static MergedJavaBlockModel BuildBabyPiglin(
        string texRef,
        float headPitch,
        float armLift,
        float walkAnimationPos,
        float walkAnimationSpeed,
        float ageInTicks)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 18f, -0.5f));
        new EntityCuboid(-3f, -3f, -1f, 3f, 2f, 2f, 0, 13).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 15f, 0f)),
            EntityParityTemplate.Rx(headPitch));
        new EntityCuboid(-1.5f, -3f, -4.5f, 1.5f, 0f, -3.5f, 21, 30).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-4.5f, -6f, -3.5f, 4.5f, 0f, 3.5f, 0, 0).Emit(b, headPose, p.HeadScale);

        const float babyEarBaseDeg = 5f;
        var earBaseRad = babyEarBaseDeg * (MathF.PI / 180f);
        var earPhase = ageInTicks * 0.1f + walkAnimationPos * 0.5f;
        var earAmp = 0.08f + walkAnimationSpeed * 0.4f;
        var leftEarOuterZ = -earBaseRad - MathF.Cos(earPhase * 1.2f) * earAmp;
        var rightEarOuterZ = earBaseRad + MathF.Cos(earPhase) * earAmp;

        const float earR1ZLeft = -0.6109f;
        const float earR1ZRight = 0.6109f;
        var leftEarOuterPose = EntityParityTemplate.Mul(
            headPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(4.2f, -4f, 0f), EntityParityTemplate.Rz(leftEarOuterZ)));
        var leftEarR1Pose = EntityParityTemplate.Mul(
            leftEarOuterPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(1f, 1.75f, 0f), EntityParityTemplate.Er(0f, 0f, earR1ZLeft)));
        new EntityCuboid(-0.5f, -3f, -2f, 0.5f, 3f, 2f, 0, 21).Emit(b, leftEarR1Pose, p.HeadScale);

        var rightEarOuterPose = EntityParityTemplate.Mul(
            headPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(-4.2f, -4f, 0f), EntityParityTemplate.Rz(rightEarOuterZ)));
        var rightEarR1Pose = EntityParityTemplate.Mul(
            rightEarOuterPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(-1f, 1.75f, 0f), EntityParityTemplate.Er(0f, 0f, earR1ZRight)));
        new EntityCuboid(-0.5f, -3f, -2f, 0.5f, 3f, 2f, 18, 13).Emit(b, rightEarR1Pose, p.HeadScale);

        var leftArmPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(4f, 15f, 0f)),
            EntityParityTemplate.Rx(armLift));
        new EntityCuboid(-1f, 0f, -1.5f, 1f, 5f, 1.5f, 28, 13).Emit(b, leftArmPose, p.BodyScale);

        var rightArmPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-4f, 15f, 0f)),
            EntityParityTemplate.Rx(armLift));
        new EntityCuboid(-1f, 0f, -1.5f, 1f, 5f, 1.5f, 10, 30).Emit(b, rightArmPose, p.BodyScale);

        var rightLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1.5f, 20f, 0f));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 4f, 1.5f, 22, 23).Emit(b, rightLegPose, p.LegScale);

        var leftLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(1.5f, 20f, 0f));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 4f, 1.5f, 10, 23).Emit(b, leftLegPose, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// Baby zombified piglin preview mesh — identical to <see cref="BuildBabyPiglin"/> because vanilla delegates
    /// <c>createBodyLayer</c> to <c>BabyPiglinModel</c>.
    /// </summary>
    /// <remarks>
    /// <para>26.1.2 <c>tools/minecraft-parity/26.1.2/client.jar</c> — <c>javap -c -p net.minecraft.client.model.monster.piglin.BabyZombifiedPiglinModel</c>:
    /// <c>public static LayerDefinition createBodyLayer()</c> is only
    /// <c>invokestatic net/minecraft/client/model/monster/piglin/BabyPiglinModel.createBodyLayer:()Lnet/minecraft/client/model/geom/builders/LayerDefinition;</c>
    /// then <c>areturn</c>. Class still overrides <c>getDefaultEarAngleInDegrees</c> (<c>5.0f</c>), same as <c>BabyPiglinModel</c>.</para>
    /// </remarks>
    private static MergedJavaBlockModel BuildBabyZombifiedPiglin(
        string texRef,
        float headPitch,
        float armLift,
        float walkAnimationPos,
        float walkAnimationSpeed,
        float ageInTicks) =>
        BuildBabyPiglin(texRef, headPitch, armLift, walkAnimationPos, walkAnimationSpeed, ageInTicks);

    /// <summary>
    /// <c>AbstractPiglinModel.setupAnim</c> ear <c>zRot</c> (26.1.2 <c>client.jar</c>): outer ear parts oscillate on top of
    /// <see cref="GetDefaultAbstractPiglinEarBaseRollRad"/> from <paramref name="defaultEarAngleDegrees"/>.
    /// </summary>
    private static void ComputeAbstractPiglinEarOuterZRotRad(
        float defaultEarAngleDegrees,
        float walkAnimationPos,
        float walkAnimationSpeed,
        float ageInTicks,
        out float leftEarZRotRad,
        out float rightEarZRotRad)
    {
        var earBaseRad = GetDefaultAbstractPiglinEarBaseRollRad(defaultEarAngleDegrees);
        var earPhase = ageInTicks * 0.1f + walkAnimationPos * 0.5f;
        var earAmp = 0.08f + walkAnimationSpeed * 0.4f;
        leftEarZRotRad = -earBaseRad - MathF.Cos(earPhase * 1.2f) * earAmp;
        rightEarZRotRad = earBaseRad + MathF.Cos(earPhase) * earAmp;
    }


    private static float GetDefaultAbstractPiglinEarBaseRollRad(float defaultEarAngleDegrees) =>
        defaultEarAngleDegrees * (MathF.PI / 180f);

    /// <summary>
    /// Piglin / zombified piglin mob: <c>AdultPiglinModel.createBodyLayer</c> (26.1.2 javap): <c>PlayerModel.createMesh(NONE,false)</c>
    /// wide arms/legs + sleeve/pants overlays (<c>CubeDeformation.extend(0.25f)</c> → ±0.25 unit skin-space shell), piglin torso replacing
    /// <c>HumanoidModel</c> body, <c>AbstractPiglinModel.addHead</c> (hat cleared at runtime). Preview uses canonical biped skin layout
    /// (<see cref="BuildHumanoid"/>). Ear <c>zRot</c> follows <see cref="ComputeAbstractPiglinEarOuterZRotRad"/> (adult default 30°).
    /// Full lifted part tree: <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.piglin.AdultPiglinModel.json</c>.
    /// </summary>
    private static MergedJavaBlockModel BuildPiglin(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        float armLift,
        float walkAnimationPos = 0f,
        float walkAnimationSpeed = 0f,
        float ageInTicks = 0f)
    {
        if (isBaby)
        {
            _ = profile;
            return BuildBabyPiglin(texRef, headPitch, armLift, walkAnimationPos, walkAnimationSpeed, ageInTicks);
        }

        var p = BabyProfile.Adult;
        const float sleevePantsInflate = 0.25f;

        var b = new RigBuilder(64, 64);
        new EntityCuboid(4, 12, 6, 12, 24, 10, 16, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale); // AdultPiglinModel replaces Humanoid body — same 8×12×4 UV island as Humanoid outer torso
        new EntityCuboid(0, 12, 6, 4, 24, 10, 40, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(2f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // PlayerModel wide left_arm
        new EntityCuboid(12, 12, 6, 16, 24, 10, 40, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(14f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale); // Humanoid right_arm (unchanged by wide path)
        new EntityCuboid(4, 0, 6, 8, 12, 10, 0, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // leg l
        new EntityCuboid(8, 0, 6, 12, 12, 10, 0, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // leg r

        new EntityCuboid(0f - sleevePantsInflate, 12f - sleevePantsInflate, 6f - sleevePantsInflate, 4f + sleevePantsInflate, 24f + sleevePantsInflate, 10f + sleevePantsInflate, 48, 48, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(2f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(12f - sleevePantsInflate, 12f - sleevePantsInflate, 6f - sleevePantsInflate, 16f + sleevePantsInflate, 24f + sleevePantsInflate, 10f + sleevePantsInflate, 40, 32, XRot: armLift, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(14f, 24f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(4f - sleevePantsInflate, 0f - sleevePantsInflate, 6f - sleevePantsInflate, 8f + sleevePantsInflate, 12f + sleevePantsInflate, 10f + sleevePantsInflate, 0, 48).Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(8f - sleevePantsInflate, 0f - sleevePantsInflate, 6f - sleevePantsInflate, 12f + sleevePantsInflate, 12f + sleevePantsInflate, 10f + sleevePantsInflate, 0, 32).Emit(b, Matrix4x4.Identity, p.LegScale);

        ComputeAbstractPiglinEarOuterZRotRad(30f, walkAnimationPos, walkAnimationSpeed, ageInTicks, out var leftEarZ, out var rightEarZ);
        var headRoot = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 28f, 8f), Matrix4x4.CreateRotationX(headPitch));
        AppendAbstractPiglinHeadBoxes(b, headRoot, p.HeadScale, leftEarZ, rightEarZ);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildZombifiedPiglin(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        float armLift,
        float walkAnimationPos = 0f,
        float walkAnimationSpeed = 0f,
        float ageInTicks = 0f)
    {
        if (isBaby)
        {
            _ = profile;
            return BuildBabyZombifiedPiglin(texRef, headPitch, armLift, walkAnimationPos, walkAnimationSpeed, ageInTicks);
        }

        // Adult: shares piglin geometry, with its own animation channel values from routing.
        return BuildPiglin(texRef, profile, isBaby: false, headPitch, armLift, walkAnimationPos, walkAnimationSpeed, ageInTicks);
    }

    /// <summary>
    /// <c>AbstractPiglinModel.addHead</c> piglin head stack (26.1.2 <c>client.jar</c>). <paramref name="leftEarZRotRad"/> /
    /// <paramref name="rightEarZRotRad"/> are total <c>zRot</c> on each ear hinge (base ±30° for adults from <c>PartPose</c> plus
    /// <c>AbstractPiglinModel.setupAnim</c> when provided from <see cref="ComputeAbstractPiglinEarOuterZRotRad"/>; use
    /// <c>∓GetDefaultAbstractPiglinEarBaseRollRad(30)</c> for skull / static preview).
    /// </summary>
    private static void AppendAbstractPiglinHeadBoxes(
        RigBuilder b,
        Matrix4x4 headPose,
        float headScale,
        float leftEarZRotRad,
        float rightEarZRotRad)
    {
        new EntityCuboid(-5f, -8f, -4f, 5f, 0f, 4f, 0, 0).Emit(b, headPose, headScale);
        new EntityCuboid(-2f, -4f, -5f, 2f, 0f, -4f, 31, 1).Emit(b, headPose, headScale);
        new EntityCuboid(2f, -2f, -5f, 3f, 0f, -4f, 2, 4).Emit(b, headPose, headScale);
        new EntityCuboid(-3f, -2f, -5f, -2f, 0f, -4f, 2, 0).Emit(b, headPose, headScale);

        var leftEarPose = Matrix4x4.Multiply(headPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(4.5f, -6f, 0f), Matrix4x4.CreateRotationZ(leftEarZRotRad)));
        new EntityCuboid(0f, 0f, -2f, 1f, 5f, 2f, 51, 6).Emit(b, leftEarPose, headScale);

        var rightEarPose = Matrix4x4.Multiply(headPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-4.5f, -6f, 0f), Matrix4x4.CreateRotationZ(rightEarZRotRad)));
        new EntityCuboid(-1f, 0f, -2f, 0f, 5f, 2f, 39, 6).Emit(b, rightEarPose, headScale);
    }

    /// <summary>Vanilla <c>AnimationUtils.bobModelPart</c> (26.1.2 <c>client.jar</c>).</summary>
    private static void IllagerBobModelPart(ref float xRot, ref float zRot, float ageInTicks, float mult)
    {
        zRot += mult * ((MathF.Cos(ageInTicks * 0.09f) * 0.05f) + 0.05f);
        xRot += mult * (MathF.Sin(ageInTicks * 0.067f) * 0.05f);
    }

    /// <summary>Vanilla <c>AnimationUtils.bobArms</c> (26.1.2 <c>client.jar</c>): right mult <c>+1</c>, left mult <c>-1</c>.</summary>
    private static void IllagerBobArms(ref float rightArmX, ref float rightArmZ, ref float leftArmX, ref float leftArmZ, float ageInTicks)
    {
        IllagerBobModelPart(ref rightArmX, ref rightArmZ, ageInTicks, 1f);
        IllagerBobModelPart(ref leftArmX, ref leftArmZ, ageInTicks, -1f);
    }

    /// <summary>Vanilla <c>AnimationUtils.swingWeaponDown</c> (26.1.2 <c>client.jar</c>).</summary>
    private static void IllagerSwingWeaponDown(
        ref float rightArmX,
        ref float rightArmY,
        ref float rightArmZ,
        ref float leftArmX,
        ref float leftArmY,
        ref float leftArmZ,
        bool mainHandIsRight,
        float attackAnim,
        float ageInTicks)
    {
        var f5 = MathF.Sin(attackAnim * MathF.PI);
        var inner = 1f - (1f - attackAnim) * (1f - attackAnim);
        var f6 = MathF.Sin(inner * MathF.PI);
        rightArmZ = 0f;
        leftArmZ = 0f;
        rightArmY = 0.15707964f;
        leftArmY = -0.15707964f;
        if (mainHandIsRight)
        {
            rightArmX = -1.8849558f + (MathF.Cos(ageInTicks * 0.09f) * 0.15f);
            leftArmX = 0f + (MathF.Cos(ageInTicks * 0.19f) * 0.5f);
            rightArmX += f5 * 2.2f - f6 * 0.4f;
            leftArmX += f5 * 1.2f - f6 * 0.4f;
        }
        else
        {
            rightArmX = 0f + (MathF.Cos(ageInTicks * 0.19f) * 0.5f);
            leftArmX = -1.8849558f + (MathF.Cos(ageInTicks * 0.09f) * 0.15f);
            rightArmX += f5 * 1.2f - f6 * 0.4f;
            leftArmX += f5 * 2.2f - f6 * 0.4f;
        }

        IllagerBobArms(ref rightArmX, ref rightArmZ, ref leftArmX, ref leftArmZ, ageInTicks);
    }

    /// <summary>Vanilla <c>AnimationUtils.animateZombieArms</c> for illager empty-hand attacks (26.1.2 <c>client.jar</c>).</summary>
    private static void IllagerAnimateZombieArms(
        ref float leftArmX,
        ref float leftArmY,
        ref float leftArmZ,
        ref float rightArmX,
        ref float rightArmY,
        ref float rightArmZ,
        bool useFifteenDivisor,
        bool swingIsStab,
        float attackTime,
        float ageInTicks)
    {
        if (swingIsStab)
        {
            IllagerBobArms(ref rightArmX, ref rightArmZ, ref leftArmX, ref leftArmZ, ageInTicks);
            return;
        }

        var div = useFifteenDivisor ? 1.5f : 2.25f;
        var f6 = -MathF.PI / div;
        var f7 = MathF.Sin(attackTime * MathF.PI);
        var inner = 1f - (1f - attackTime) * (1f - attackTime);
        var f8 = MathF.Sin(inner * MathF.PI);
        rightArmZ = 0f;
        rightArmY = -0.1f + 0.6f * f7;
        rightArmX = f6;
        rightArmX += f7 * 1.2f - f8 * 0.4f;
        leftArmZ = 0f;
        leftArmY = -0.1f + 0.6f * f7;
        leftArmX = f6;
        leftArmX += f7 * 1.2f - f8 * 0.4f;
        IllagerBobArms(ref rightArmX, ref rightArmZ, ref leftArmX, ref leftArmZ, ageInTicks);
    }

    /// <summary>Vanilla <c>AnimationUtils.animateCrossbowHold</c> (26.1.2 <c>client.jar</c>).</summary>
    private static void IllagerAnimateCrossbowHold(
        ref float rightArmX,
        ref float rightArmY,
        ref float rightArmZ,
        ref float leftArmX,
        ref float leftArmY,
        ref float leftArmZ,
        float headYawRad,
        float headPitchRad,
        bool rightHanded)
    {
        if (rightHanded)
        {
            rightArmY += -0.3f + headYawRad;
            leftArmY += 0.6f + headYawRad;
            rightArmX = -1.5707964f + headPitchRad + 0.1f;
            leftArmX = -1.5f + headPitchRad;
        }
        else
        {
            leftArmY += -0.3f + headYawRad;
            rightArmY += 0.6f + headYawRad;
            leftArmX = -1.5707964f + headPitchRad + 0.1f;
            rightArmX = -1.5f + headPitchRad;
        }
    }

    /// <summary>Vanilla <c>AnimationUtils.animateCrossbowCharge</c> (26.1.2 <c>client.jar</c>).</summary>
    private static void IllagerAnimateCrossbowCharge(
        ref float rightArmX,
        ref float rightArmY,
        ref float rightArmZ,
        ref float leftArmX,
        ref float leftArmY,
        ref float leftArmZ,
        float maxCrossbowChargeDuration,
        float ticksUsingItem,
        bool rightHanded)
    {
        var denom = Math.Max(1f, maxCrossbowChargeDuration);
        var t = Math.Clamp(ticksUsingItem, 0f, denom) / denom;
        var sign = rightHanded ? 1f : -1f;
        if (rightHanded)
        {
            rightArmY = -0.8f;
            rightArmX = -0.97079635f;
            leftArmX = rightArmX;
            leftArmY = (0.4f + (0.85f - 0.4f) * t) * sign;
            leftArmX = leftArmX + (-1.5707964f - leftArmX) * t;
        }
        else
        {
            leftArmY = 0.8f;
            leftArmX = -0.97079635f;
            rightArmX = leftArmX;
            rightArmY = (0.4f + (0.85f - 0.4f) * t) * -1f;
            rightArmX = rightArmX + (-1.5707964f - rightArmX) * t;
        }
    }

    /// <summary>
    /// Vanilla <c>IllagerModel.setupAnim</c> (26.1.2 <c>client.jar</c>): head look, riding vs walk limbs, <c>IllagerArmPose</c> arms,
    /// folded <c>arms</c> vs separate arms visibility. Hat cuboid omitted — Java ctor forces <c>hat.visible = false</c>.
    /// Baby illagers reuse the adult <c>IllagerModel</c> with uniform <c>LivingEntity.getAgeScale()</c> (see <c>IllagerRenderer</c> → <c>MobRenderer</c>);
    /// IR: <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.illager.IllagerModel.json</c>.
    /// </summary>
    private static MergedJavaBlockModel BuildIllager(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        float wave,
        IllagerPreviewArmPoseKind armPose)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? BabyProfile.VanillaUniformBaby : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.12f, 0.84f) : BabyProfile.Adult);

        const float k6662 = 0.6662f;
        const float degToRad = 0.017453292f;
        var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
        var headYawRad = (wave * 8f + idlePhase01 * 6f) * degToRad;
        var headPitchRad = (idlePhase01 * 10f + wave * 4f) * degToRad;
        var ageInTicks = animationTimeSeconds * 20f;
        var isRiding = false;
        var showFoldedArms = armPose == IllagerPreviewArmPoseKind.Crossed;
        var showSeparateArms = !showFoldedArms;
        var attachVindicatorAxe = armPose == IllagerPreviewArmPoseKind.AttackingWeapon;

        float rlX;
        float rlY;
        float rlZ;
        float llX;
        float llY;
        float llZ;
        float raX;
        float raY;
        float raZ;
        float laX;
        float laY;
        float laZ;
        if (isRiding)
        {
            raX = laX = -0.62831855f;
            raY = laY = raZ = laZ = 0f;
            rlX = llX = -1.4137167f;
            rlY = 0.31415927f;
            llY = -0.31415927f;
            rlZ = 0.07853982f;
            llZ = -0.07853982f;
        }
        else
        {
            rlX = MathF.Cos(walkPos * k6662) * 1.4f * walkSpeed * 0.5f;
            llX = MathF.Cos(walkPos * k6662 + MathF.PI) * 1.4f * walkSpeed * 0.5f;
            rlY = rlZ = llY = llZ = 0f;
            if (showFoldedArms)
            {
                raX = raY = raZ = laX = laY = laZ = 0f;
            }
            else
            {
                raX = MathF.Cos(walkPos * k6662 + MathF.PI) * 2f * walkSpeed * 0.5f;
                laX = MathF.Cos(walkPos * k6662) * 2f * walkSpeed * 0.5f;
                raY = raZ = laY = laZ = 0f;
            }
        }

        switch (armPose)
        {
            case IllagerPreviewArmPoseKind.Crossed:
                break;
            case IllagerPreviewArmPoseKind.AttackingEmptyHands:
                {
                    var attackT = Math.Clamp(0.35f + idlePhase01 * 0.45f + wave * 0.2f, 0f, 1f);
                    IllagerAnimateZombieArms(
                        ref laX,
                        ref laY,
                        ref laZ,
                        ref raX,
                        ref raY,
                        ref raZ,
                        useFifteenDivisor: true,
                        swingIsStab: false,
                        attackTime: attackT,
                        ageInTicks: ageInTicks);
                    break;
                }
            case IllagerPreviewArmPoseKind.AttackingWeapon:
                {
                    raX = raY = raZ = laX = laY = laZ = 0f;
                    var attackAnim = Math.Clamp(0.2f + idlePhase01 * 0.55f + wave * 0.25f, 0f, 1f);
                    IllagerSwingWeaponDown(ref raX, ref raY, ref raZ, ref laX, ref laY, ref laZ, mainHandIsRight: true, attackAnim, ageInTicks);
                    break;
                }
            case IllagerPreviewArmPoseKind.Spellcasting:
                {
                    var sc = MathF.Cos(ageInTicks * k6662);
                    raX = sc * 0.25f;
                    laX = sc * 0.25f;
                    raZ = 2.3561945f;
                    laZ = -2.3561945f;
                    raY = laY = 0f;
                    break;
                }
            case IllagerPreviewArmPoseKind.BowAndArrow:
                {
                    raY = -0.1f + headYawRad;
                    raX = -1.5707964f + headPitchRad;
                    laX = -0.9424779f + headPitchRad;
                    laY = headYawRad - 0.4f;
                    laZ = 1.5707964f;
                    raZ = 0f;
                    break;
                }
            case IllagerPreviewArmPoseKind.CrossbowHold:
                IllagerAnimateCrossbowHold(ref raX, ref raY, ref raZ, ref laX, ref laY, ref laZ, headYawRad, headPitchRad, rightHanded: true);
                break;
            case IllagerPreviewArmPoseKind.CrossbowCharge:
                IllagerAnimateCrossbowCharge(
                    ref raX,
                    ref raY,
                    ref raZ,
                    ref laX,
                    ref laY,
                    ref laZ,
                    maxCrossbowChargeDuration: 25f,
                    ticksUsingItem: (animationTimeSeconds * 20f) % 26f,
                    rightHanded: true);
                break;
            case IllagerPreviewArmPoseKind.Celebrating:
                {
                    var cc = MathF.Cos(ageInTicks * k6662);
                    raX = cc * 0.05f;
                    laX = cc * 0.05f;
                    raZ = 2.670354f;
                    laZ = -2.3561945f;
                    raY = laY = 0f;
                    break;
                }
            default:
                break;
        }

        var b = new RigBuilder(64, 64);
        var headWorld = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 32f, 8f), EntityParityTemplate.Er(headPitchRad, headYawRad, 0f));
        new EntityCuboid(-4f, -10f, -4f, 4f, 0f, 4f, 0, 0).Emit(b, headWorld, p.HeadScale);
        new EntityCuboid(-1f, -1f, -6f, 1f, 3f, -4f, 24, 0).Emit(b, Matrix4x4.Multiply(headWorld, Matrix4x4.CreateTranslation(0f, -2f, 0f)), p.HeadScale);

        var bodyPose = Matrix4x4.CreateTranslation(8f, 12f, 8f);
        new EntityCuboid(-4f, 0f, -3f, 4f, 12f, 3f, 16, 20).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-4f, 0f, -3f, 4f, 20f, 3f, 0, 38).Emit(b, bodyPose, p.BodyScale);

        if (showFoldedArms)
        {
            var foldedArmsPose = Matrix4x4.Multiply(
                Matrix4x4.CreateTranslation(8f, 15f, 7f),
                Matrix4x4.CreateRotationX(-0.75f));
            new EntityCuboid(-8f, -2f, -2f, -4f, 6f, 2f, 44, 22).Emit(b, foldedArmsPose, p.BodyScale);
            new EntityCuboid(-4f, 2f, -2f, 4f, 6f, 2f, 40, 38).Emit(b, foldedArmsPose, p.BodyScale);
            new EntityCuboid(4f, -2f, -2f, 8f, 6f, 2f, 44, 22).Emit(b, foldedArmsPose, p.BodyScale);
        }

        if (showSeparateArms)
        {
            var rightArmPose = Matrix4x4.CreateTranslation(3f, 14f, 8f);
            var leftArmPose = Matrix4x4.CreateTranslation(13f, 14f, 8f);
            new EntityCuboid(-3f, -2f, -2f, 1f, 10f, 2f, 40, 46, XRot: raX, YRot: raY, ZRot: raZ) { RotationPivot = Vector3.Zero }.Emit(b, rightArmPose, p.BodyScale);
            new EntityCuboid(-1f, -2f, -2f, 3f, 10f, 2f, 40, 46, XRot: laX, YRot: laY, ZRot: laZ) { RotationPivot = Vector3.Zero }.Emit(b, leftArmPose, p.BodyScale);
            if (attachVindicatorAxe)
            {
                new EntityCuboid(-3.6f, 8.2f, -0.9f, -1.0f, 11.4f, 0.9f, 56, 0, XRot: raX, YRot: raY, ZRot: raZ) { RotationPivot = Vector3.Zero }.Emit(b, rightArmPose, p.LegScale);
            }
        }

        new EntityCuboid(-2f, 0f, -2f, 2f, 12f, 2f, 0, 22, XRot: rlX, YRot: rlY, ZRot: rlZ) { RotationPivot = Vector3.Zero }.Emit(b, Matrix4x4.CreateTranslation(6f, 0f, 8f), p.LegScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 12f, 2f, 0, 22, XRot: llX, YRot: llY, ZRot: llZ) { RotationPivot = Vector3.Zero }.Emit(b, Matrix4x4.CreateTranslation(10f, 0f, 8f), p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>Vanilla <c>WitchModel.setupAnim</c> (26.1.2 <c>client.jar</c>).</summary>
    private static MergedJavaBlockModel BuildWitch(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float yRotDegrees,
        float xRotDegrees,
        float walkAnimationPos,
        float walkAnimationSpeed,
        int entityId,
        float ageInTicks,
        bool isHoldingItem)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.74f, 1.22f, 0.76f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.14f, 0.84f) : BabyProfile.Adult);

        const float k6662 = 0.6662f;
        const float degToRad = 0.017453292f;
        var headYawRad = yRotDegrees * degToRad;
        var headPitchRad = xRotDegrees * degToRad;
        var rlX = MathF.Cos(walkAnimationPos * k6662) * 1.4f * walkAnimationSpeed * 0.5f;
        var llX = MathF.Cos(walkAnimationPos * k6662 + MathF.PI) * 1.4f * walkAnimationSpeed * 0.5f;
        var idRem = ((entityId % 10) + 10) % 10;
        var wobble = 0.01f * idRem;
        var noseX = MathF.Sin(ageInTicks * wobble) * 4.5f * degToRad;
        var noseZ = MathF.Cos(ageInTicks * wobble) * 2.5f * degToRad;
        if (isHoldingItem)
        {
            noseX = -0.9f;
        }

        var b = new RigBuilder(64, 128);
        var headWorld = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 32f, 8f), EntityParityTemplate.Er(headPitchRad, headYawRad, 0f));
        new EntityCuboid(-4f, -10f, -4f, 4f, 0f, 4f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, headWorld, p.HeadScale);
        var noseLocal = isHoldingItem
            ? Matrix4x4.Multiply(headWorld, Matrix4x4.CreateTranslation(0f, 1f, -1.5f))
            : Matrix4x4.Multiply(headWorld, Matrix4x4.CreateTranslation(0f, -2f, 0f));
        new EntityCuboid(-1f, -1f, -6f, 1f, 3f, -4f, 24, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: noseX, YRot: 0f, ZRot: noseZ) { RotationPivot = Vector3.Zero }.Emit(b, noseLocal, p.HeadScale);
        new EntityCuboid(0f, 1f, -6.75f, 1f, 2f, -5.75f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Multiply(headWorld, Matrix4x4.CreateTranslation(0f, -4f, 0f)), p.HeadScale);

        var hatBase = Matrix4x4.Multiply(headWorld, Matrix4x4.CreateTranslation(-5f, -10.03125f, -5f));
        new EntityCuboid(0f, 0f, 0f, 10f, 2f, 10f, 0, 64, OffsetX: 0, OffsetY: 0, OffsetZ: 0) { RotationPivot = Vector3.Zero }.Emit(b, hatBase, p.HeadScale);
        var hat2 = Matrix4x4.Multiply(hatBase, Matrix4x4.CreateTranslation(1.75f, -4f, 2f));
        new EntityCuboid(0f, 0f, 0f, 7f, 4f, 7f, 0, 76, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: -0.05235988f, YRot: 0f, ZRot: 0.02617994f) { RotationPivot = Vector3.Zero }.Emit(b, hat2, p.HeadScale);
        var hat3 = Matrix4x4.Multiply(hat2, Matrix4x4.CreateTranslation(1.75f, -4f, 2f));
        new EntityCuboid(0f, 0f, 0f, 4f, 4f, 4f, 0, 87, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: -0.10471976f, YRot: 0f, ZRot: 0.05235988f) { RotationPivot = Vector3.Zero }.Emit(b, hat3, p.HeadScale);
        var hatTip = Matrix4x4.Multiply(hat3, Matrix4x4.CreateTranslation(1.75f, -2f, 2f));
        new EntityCuboid(0f, 0f, 0f, 1.5f, 2.5f, 1.5f, 0, 95, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: -0.20943952f, YRot: 0f, ZRot: 0.10471976f) { RotationPivot = Vector3.Zero }.Emit(b, hatTip, p.HeadScale);

        var bodyPose = Matrix4x4.CreateTranslation(8f, 12f, 8f);
        new EntityCuboid(-4f, 0f, -3f, 4f, 12f, 3f, 16, 20, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-4f, 0f, -3f, 4f, 20f, 3f, 0, 38, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, bodyPose, p.BodyScale);
        var foldedArmsPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 15f, 7f), Matrix4x4.CreateRotationX(-0.75f));
        new EntityCuboid(-8f, -2f, -2f, -4f, 6f, 2f, 44, 22, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, foldedArmsPose, p.BodyScale);
        new EntityCuboid(-4f, 2f, -2f, 4f, 6f, 2f, 40, 38, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, foldedArmsPose, p.BodyScale);
        new EntityCuboid(4f, -2f, -2f, 8f, 6f, 2f, 44, 22, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, foldedArmsPose, p.BodyScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 12f, 2f, 0, 22, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: rlX, YRot: 0f, ZRot: 0f) { RotationPivot = Vector3.Zero }.Emit(b, Matrix4x4.CreateTranslation(6f, 0f, 8f), p.LegScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 12f, 2f, 0, 22, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: llX, YRot: 0f, ZRot: 0f) { RotationPivot = Vector3.Zero }.Emit(b, Matrix4x4.CreateTranslation(10f, 0f, 8f), p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildEvoker(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        float wave) =>
        BuildIllager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, IllagerPreviewArmPoseKind.Spellcasting);


    private static MergedJavaBlockModel BuildVindicator(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        float wave) =>
        BuildIllager(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, IllagerPreviewArmPoseKind.AttackingWeapon);
}
