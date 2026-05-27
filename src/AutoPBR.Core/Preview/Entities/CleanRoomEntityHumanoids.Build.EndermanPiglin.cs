using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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

}
