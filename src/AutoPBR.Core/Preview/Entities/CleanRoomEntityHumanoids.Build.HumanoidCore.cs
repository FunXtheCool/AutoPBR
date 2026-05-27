using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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

}
