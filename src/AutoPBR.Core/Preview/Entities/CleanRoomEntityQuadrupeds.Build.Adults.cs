using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    internal static MergedJavaBlockModel BuildQuadruped(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyCow(
                texRef,
                headPitch,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        AppendQuadrupedFamilyCowModelBodyHeadLegs(
            b,
            p,
            headPitch,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildPig(
        string texRef,
        MinecraftNativeProfile _,
        bool isBaby,
        float snoutBob,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        if (isBaby)
        {
            return BuildBabyPig(
                texRef,
                snoutBob,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        AppendPigQuadrupedMesh(b, p, snoutBob, rightHindLegPitchRad, leftHindLegPitchRad, rightFrontLegPitchRad, leftFrontLegPitchRad);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildColdPig(
        string texRef,
        MinecraftNativeProfile _,
        bool isBaby,
        float snoutBob,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        if (isBaby)
        {
            return BuildBabyPig(
                texRef,
                snoutBob,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        AppendPigQuadrupedMesh(b, p, snoutBob, rightHindLegPitchRad, leftHindLegPitchRad, rightFrontLegPitchRad, leftFrontLegPitchRad);

        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 11f, 2f), EntityParityTemplate.Rx(MathF.PI / 2f));
        new EntityCuboid(-10f, -7f, 10f, 6f, 1f, 10.5f, 28, 32, UvSizeW: 16, UvSizeH: 8, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildWolf(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyWolf(
                texRef,
                headPitch,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 32);
        // WolfModel (get): two trunk segments (6x9x6 + 8x6x7), head with ears/nose, four 2x8x2 legs, and 2x8x2 tail.
        // Head look uses <c>xRot</c> (pitch) on the head part — same <c>PartPose.offsetAndRotation</c> stack as other quadrupeds (<see cref="EntityParityTemplate.Rx"/>), not yRot.
        // Leg roots: <c>QuadrupedModel.setupAnim</c> via lifted IR (<see cref="VanillaSetupAnimRuntime"/>).
        var headRoot = EntityParityTemplate.Mul(EntityParityTemplate.T(7f, 13.5f, 1f), EntityParityTemplate.Rx(headPitch));
        new EntityCuboid(-2f, -3f, -2f, 4f, 3f, 2f, 0, 0).Emit(b, headRoot, p.HeadScale); // real_head 6x6x4
        new EntityCuboid(-2f, -5f, 0f, 0f, -3f, 1f, 16, 14).Emit(b, EntityParityTemplate.Mul(headRoot, EntityParityTemplate.Rx(-0.2617994f)), p.HeadScale); // right ear (±15° relative pitch vs skull)
        new EntityCuboid(2f, -5f, 0f, 4f, -3f, 1f, 16, 14).Emit(b, EntityParityTemplate.Mul(headRoot, EntityParityTemplate.Rx(0.2617994f)), p.HeadScale); // left ear
        new EntityCuboid(-0.5f, -0.001f, -5f, 2.5f, 2.999f, -1f, 0, 10).Emit(b, headRoot, p.HeadScale); // nose

        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(8f, 14f, 10f), EntityParityTemplate.Rx(MathF.PI / 2f));
        new EntityCuboid(-3f, -2f, -3f, 3f, 7f, 3f, 18, 14).Emit(b, bodyPose, p.BodyScale); // body 6x9x6
        var upperBodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(7f, 14f, 5f), EntityParityTemplate.Rx(MathF.PI / 2f));
        new EntityCuboid(-3f, -3f, -3f, 5f, 3f, 4f, 21, 0).Emit(b, upperBodyPose, p.BodyScale); // upper_body 8x6x7

        AppendWolfModelLegSetTex018(b, p, rightHindLegPitchRad, leftHindLegPitchRad, rightFrontLegPitchRad, leftFrontLegPitchRad);

        var tailPose = EntityParityTemplate.Mul(EntityParityTemplate.T(8f, 20f, 13f), EntityParityTemplate.Rx(-0.3490659f - headPitch * 0.25f));
        new EntityCuboid(-1.5f, -1.5f, 0f, 1.5f, 6.5f, 2f, 9, 18).Emit(b, tailPose, p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildGoat(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyGoat(
                texRef,
                headPitch,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        // GoatModel.createBodyLayer (~1.21.4): fleece double-layer body PartPose (0,24,0); head group (1,14,0)+pitch;
        // horns/nose/ears in vanilla space; nose root pose offsetAndRotation(0,-8,-8, ~0.96rad, 0, 0); legs 3x6x3 / 3x10x3.
        var bodyPose = EntityParityTemplate.T(0f, 24f, 0f);
        new EntityCuboid(-4f, -17f, -7f, 5f, -6f, 9f, 1, 1).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-5f, -18f, -8f, 6f, -4f, 3f, 0, 28).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(EntityParityTemplate.T(1f, 14f, 0f), EntityParityTemplate.Rx(headPitch));
        new EntityCuboid(-6f, -11f, -10f, -3f, -9f, -9f, 2, 61).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(2f, -11f, -10f, 5f, -9f, -9f, 2, 61).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-0.01f, -16f, -10f, 1.99f, -9f, -8f, 12, 55).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2.99f, -16f, -10f, -0.99f, -9f, -8f, 12, 55).Emit(b, headPose, p.HeadScale);

        var nosePose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -8f, -8f), EntityParityTemplate.Rx(0.959931f));
        new EntityCuboid(-3f, -4f, -8f, 2f, 3f, 2f, 34, 46).Emit(b, nosePose, p.HeadScale);

        new EntityCuboid(0f, 4f, 0f, 3f, 10f, 3f, 36, 29).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(1f, 14f, 4f), EntityParityTemplate.Rx(rightHindLegPitchRad)), p.LegScale);
        new EntityCuboid(0f, 4f, 0f, 3f, 10f, 3f, 49, 29).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(-3f, 14f, 4f), EntityParityTemplate.Rx(leftHindLegPitchRad)), p.LegScale);
        new EntityCuboid(0f, 0f, 0f, 3f, 10f, 3f, 49, 2).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(1f, 14f, -6f), EntityParityTemplate.Rx(rightFrontLegPitchRad)), p.LegScale);
        new EntityCuboid(0f, 0f, 0f, 3f, 10f, 3f, 35, 2).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(-3f, 14f, -6f), EntityParityTemplate.Rx(leftFrontLegPitchRad)), p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildLlama(string texRef, MinecraftNativeProfile profile, bool isBaby, float neckBend)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyLlama(texRef, neckBend);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(128, 64);
        // LlamaModel.createBodyLayer (~1.21.4): cow-class rotated body; head stack PartPose (0,7,-6)+bend; chests Ry(pi/2); legs 4x14x4.
        var bodyPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 5f, 2f), Matrix4x4.CreateRotationX(MathF.PI / 2f));
        new EntityCuboid(-6f, -10f, -7f, 6f, 8f, 3f, 29, 0).Emit(b, bodyPose, p.BodyScale);

        var headPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 7f, -6f), Matrix4x4.CreateRotationX(neckBend));
        new EntityCuboid(-2f, -14f, -10f, 2f, -10f, -1f, 0, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-4f, -16f, -6f, 4f, 2f, 0f, 0, 14).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-4f, -19f, -4f, -1f, -16f, -2f, 17, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(1f, -19f, -4f, 4f, -16f, -2f, 17, 0).Emit(b, headPose, p.HeadScale);

        var chestRy = Matrix4x4.CreateRotationY(MathF.PI / 2f);
        var chestRight = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-8.5f, 3f, 3f), chestRy);
        var chestLeft = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(5.5f, 3f, 3f), chestRy);
        new EntityCuboid(-3f, 0f, 0f, 5f, 8f, 3f, 45, 28).Emit(b, chestRight, p.BodyScale);
        new EntityCuboid(-3f, 0f, 0f, 5f, 8f, 3f, 45, 41).Emit(b, chestLeft, p.BodyScale);

        new EntityCuboid(-2f, 0f, -2f, 2f, 14f, 2f, 29, 29).Emit(b, Matrix4x4.CreateTranslation(-3.5f, 10f, 6f), p.LegScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 14f, 2f, 29, 29).Emit(b, Matrix4x4.CreateTranslation(3.5f, 10f, 6f), p.LegScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 14f, 2f, 29, 29).Emit(b, Matrix4x4.CreateTranslation(-3.5f, 10f, -5f), p.LegScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 14f, 2f, 29, 29).Emit(b, Matrix4x4.CreateTranslation(3.5f, 10f, -5f), p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildCamel(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float neckBend,
        float animationTimeSeconds = 0f,
        float idlePhase01 = 0f,
        float babyWalkHeadTranslateZ = 0f,
        float adultWalkRootRollRad = 0f)
    {
        _ = profile;
        if (isBaby)
        {
            var w = Wave(animationTimeSeconds, 0.8f);
            var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, w);
            return BuildBabyCamel(texRef, neckBend, rh, lh, rf, lf, headWalkTranslateZ: babyWalkHeadTranslateZ);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(128, 128);
        var bodyPose = Math.Abs(adultWalkRootRollRad) > 1e-5f
            ? EntityParityTemplate.Rz(adultWalkRootRollRad)
            : Matrix4x4.Identity;
        // CamelEntityModel: elongated body 14x12x28, hump 12x10x14, neck 8x18x10, snout 6x6x10, legs 4x17x4.
        new EntityCuboid(1, 10, -6, 15, 22, 22, 0, 25, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, bodyPose, p.BodyScale); // torso
        new EntityCuboid(3.5f, 19.5f, 3.5f, 15.5f, 29.5f, 17.5f, 74, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale); // hump
        new EntityCuboid(12.5f, 16, 8.5f, 20.5f, 34, 18.5f, 60, 24, OffsetX: 0, OffsetY: neckBend, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // neck
        new EntityCuboid(19.5f, 26.5f, 10f, 25.5f, 32.5f, 20f, 45, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // snout
        new EntityCuboid(3.5f, 0, 7.5f, 7.5f, 17, 11.5f, 21, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(8.5f, 0, 7.5f, 12.5f, 17, 11.5f, 58, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(3.5f, 0, 11.5f, 7.5f, 17, 15.5f, 21, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(8.5f, 0, 11.5f, 12.5f, 17, 15.5f, 58, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildPanda(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float bodyRoll,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildBabyPanda(
                texRef,
                bodyRoll,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        // Legacy baby only (pre–26.1): scaled adult mesh — not used once <see cref="UsesPostBabyModelUpdate"/> is true.
        var p = !UsesPostBabyModelUpdate(profile) && isBaby
            ? new BabyProfile(0.78f, 1.10f, 0.80f)
            : BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        // PandaModel (gcz): head group at (0,11.5,-17), body at (0,10,0) with Rx(pi/2), legs 6x9x6.
        // Leg roots: <c>QuadrupedModel.setupAnim</c> via lifted IR (<see cref="VanillaSetupAnimRuntime"/>).
        var bodyPose = Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(0f, 10f, 0f),
            Matrix4x4.Multiply(Matrix4x4.CreateRotationX(MathF.PI / 2f), Matrix4x4.CreateRotationY(bodyRoll)));
        new EntityCuboid(-9.5f, -13f, -6.5f, 9.5f, 13f, 6.5f, 0, 25).Emit(b, bodyPose, p.BodyScale);

        var headPose = Matrix4x4.CreateTranslation(0f, 11.5f, -17f);
        new EntityCuboid(-6.5f, -5f, -4f, 6.5f, 5f, 5f, 0, 6).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-3.5f, 0f, -6f, 3.5f, 5f, -4f, 45, 16).Emit(b, headPose, p.HeadScale); // nose
        new EntityCuboid(3.5f, -8f, -1f, 8.5f, -4f, 0f, 52, 25).Emit(b, headPose, p.HeadScale); // left ear
        new EntityCuboid(-8.5f, -8f, -1f, -3.5f, -4f, 0f, 52, 25).Emit(b, headPose, p.HeadScale); // right ear

        new EntityCuboid(-3f, 0f, -3f, 3f, 9f, 3f, 40, 0).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(-5.5f, 15f, 9f), EntityParityTemplate.Rx(rightHindLegPitchRad)), p.LegScale);
        new EntityCuboid(-3f, 0f, -3f, 3f, 9f, 3f, 40, 0).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(5.5f, 15f, 9f), EntityParityTemplate.Rx(leftHindLegPitchRad)), p.LegScale);
        new EntityCuboid(-3f, 0f, -3f, 3f, 9f, 3f, 40, 0).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(-5.5f, 15f, -9f), EntityParityTemplate.Rx(rightFrontLegPitchRad)), p.LegScale);
        new EntityCuboid(-3f, 0f, -3f, 3f, 9f, 3f, 40, 0).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(5.5f, 15f, -9f), EntityParityTemplate.Rx(leftFrontLegPitchRad)), p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildPolarBear(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headLift,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        if (isBaby && UsesPostBabyModelUpdate(profile))
        {
            return BuildBabyPolarBear(
                texRef,
                headLift,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = isBaby ? new BabyProfile(0.80f, 1.08f, 0.82f) : BabyProfile.Adult;
        var b = new RigBuilder(128, 64);
        // PolarBearModel (gdi): head (7 cube) + snout + ears, body stack at Rx(pi/2), front/hind leg size split.
        // Leg roots: same <c>QuadrupedModel.setupAnim</c> <c>xRot</c> via lifted IR.
        var headPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 10f, -16f), EntityParityTemplate.Rx(headLift));
        new EntityCuboid(-3.5f, -3f, -3f, 3.5f, 4f, 4f, 0, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2.5f, 1f, -6f, 2.5f, 4f, -3f, 0, 44).Emit(b, headPose, p.HeadScale); // mouth
        new EntityCuboid(-4.5f, -4f, -1f, -2.5f, -2f, 0f, 26, 0).Emit(b, headPose, p.HeadScale); // right ear
        new EntityCuboid(2.5f, -4f, -1f, 4.5f, -2f, 0f, 26, 0).Emit(b, headPose, p.HeadScale); // left ear

        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(-2f, 9f, 12f), EntityParityTemplate.Rx(MathF.PI / 2f));
        new EntityCuboid(-5f, -13f, -7f, 9f, 1f, 4f, 0, 19).Emit(b, bodyPose, p.BodyScale); // lower body
        new EntityCuboid(-4f, -25f, -7f, 8f, -13f, 3f, 39, 0).Emit(b, bodyPose, p.BodyScale); // upper body/hump

        new EntityCuboid(-2f, 0f, -2f, 2f, 10f, 6f, 50, 22).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(-4.5f, 14f, 6f), EntityParityTemplate.Rx(rightHindLegPitchRad)), p.LegScale); // right hind
        new EntityCuboid(-2f, 0f, -2f, 2f, 10f, 6f, 50, 22).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(4.5f, 14f, 6f), EntityParityTemplate.Rx(leftHindLegPitchRad)), p.LegScale); // left hind
        new EntityCuboid(-2f, 0f, -2f, 2f, 10f, 4f, 50, 40).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(-3.5f, 14f, -8f), EntityParityTemplate.Rx(rightFrontLegPitchRad)), p.LegScale); // right front
        new EntityCuboid(-2f, 0f, -2f, 2f, 10f, 4f, 50, 40).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(3.5f, 14f, -8f), EntityParityTemplate.Rx(leftFrontLegPitchRad)), p.LegScale); // left front
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    internal static MergedJavaBlockModel BuildCow(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        bool hasHorns,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyCow(
                texRef,
                headPitch,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        AppendQuadrupedFamilyCowModelBodyHeadLegs(
            b,
            p,
            headPitch,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);
        AppendCowMooshroomUdderAndOptionalHorns(b, p, headPitch, hasHorns);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildColdCow(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        bool hasHorns,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyCow(
                texRef,
                headPitch,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        GetCowModelBodyAndHeadPoses(headPitch, out var bodyPose, out var headPose);

        // ColdCowModel: one body part with fleece + torso + udder (same PartPose), then head + horns; legs stay CowModel roots (added last here).
        new EntityCuboid(-6.5f, -10.5f, -7.5f, 6.5f, 8.5f, 3.5f, 20, 32, UvSizeW: 12, UvSizeH: 18, UvSizeD: 10).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-6f, -10f, -7f, 6f, 8f, 3f, 18, 4).Emit(b, bodyPose, p.BodyScale);
        AppendCowMooshroomUdderOnly(b, p, headPitch);

        new EntityCuboid(-4f, -4f, -6f, 4f, 4f, 0f, 0, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-3f, 1f, -7f, 3f, 4f, -6f, 9, 33, UvSizeW: 6, UvSizeH: 3, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        if (hasHorns)
        {
            AppendColdCowHornsNamedJar2612(b, p, headPose);
        }

        AppendCowModelTex016LegsJar2612(
            b,
            p,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildWarmCow(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        bool hasHorns,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyCow(
                texRef,
                headPitch,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        GetCowModelBodyAndHeadPoses(headPitch, out var bodyPose, out var headPose);
        new EntityCuboid(-6f, -10f, -7f, 6f, 8f, 3f, 18, 4).Emit(b, bodyPose, p.BodyScale);

        new EntityCuboid(-4f, -4f, -6f, 4f, 4f, 0f, 0, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-3f, 1f, -7f, 3f, 4f, -6f, 1, 33, UvSizeW: 6, UvSizeH: 3, UvSizeD: 1).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-8f, -3f, -5f, -4f, -1f, -3f, 27, 0, UvSizeW: 4, UvSizeH: 2, UvSizeD: 2).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-8f, -5f, -5f, -6f, -3f, -3f, 39, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(4f, -3f, -5f, 8f, -1f, -3f, 27, 0, UvSizeW: 4, UvSizeH: 2, UvSizeD: 2).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(6f, -5f, -5f, 8f, -3f, -3f, 39, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2).Emit(b, headPose, p.HeadScale);

        AppendCowModelTex016LegsJar2612(
            b,
            p,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);

        new EntityCuboid(-2f, 2f, -8f, 2f, 8f, -7f, 52, 0).Emit(b, bodyPose, p.BodyScale);
        if (hasHorns)
        {
            new EntityCuboid(-5f, -5f, -4f, -4f, -2f, -3f, 22, 0).Emit(b, headPose, p.HeadScale);
            new EntityCuboid(4f, -5f, -4f, 5f, -2f, -3f, 22, 0).Emit(b, headPose, p.HeadScale);
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildSheep(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float grazeDip,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabySheep(
                texRef,
                grazeDip,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        // SheepModel.createBodyLayer (1.21.4): wool body texOffs(28,8), cuboid (-4,-10,-7)-(4,6,-1), PartPose.offsetAndRotation(0,5,2, pi/2,0,0).
        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 5f, 2f), EntityParityTemplate.Rx(MathF.PI / 2f));
        new EntityCuboid(-4f, -10f, -7f, 4f, 6f, -1f, 28, 8).Emit(b, bodyPose, p.BodyScale);
        // Head group (0,6,-8); grazing drives <c>head.xRot</c> in <c>SheepModel.setupAnim</c> — preview uses <see cref="EntityParityTemplate.Rx"/> like cow/pig.
        var headPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 6f, -8f), EntityParityTemplate.Rx(-grazeDip));
        new EntityCuboid(-3f, -4f, -6f, 3f, 2f, 2f, 0, 0).Emit(b, headPose, p.HeadScale);
        AppendQuadrupedSetupAnimLegSetTex016(
            b,
            p,
            legFootY: 6f,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildRabbit(string texRef, MinecraftNativeProfile profile, bool isBaby, float hopCompress)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyRabbit(texRef, hopCompress);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        // RabbitModel (gdn): body 6x5x10, head 5x4x5, haunches 2x4x5 + hind feet 2x1x7, front legs 2x7x2.
        var hop = Math.Clamp(hopCompress, -0.75f, 0.75f);

        var leftHaunch = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(11f, 17.5f, 11.7f), Matrix4x4.CreateRotationX(-0.36651915f + hop * 0.15f));
        var rightHaunch = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(5f, 17.5f, 11.7f), Matrix4x4.CreateRotationX(-0.36651915f + hop * 0.15f));
        new EntityCuboid(-1f, 0f, 0f, 1f, 4f, 5f, 30, 15).Emit(b, leftHaunch, p.LegScale);
        new EntityCuboid(-1f, 0f, 0f, 1f, 4f, 5f, 16, 15).Emit(b, rightHaunch, p.LegScale);
        new EntityCuboid(-1f, 5.5f, -3.7f, 1f, 6.5f, 3.3f, 26, 24).Emit(b, leftHaunch, p.LegScale);
        new EntityCuboid(-1f, 5.5f, -3.7f, 1f, 6.5f, 3.3f, 8, 24).Emit(b, rightHaunch, p.LegScale);

        var bodyPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 19f, 16f), Matrix4x4.CreateRotationX(-0.34906584f - hop * 0.08f));
        new EntityCuboid(-3f, -2f, -10f, 3f, 3f, 0f, 0, 0).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-1f, 0f, -1f, 1f, 7f, 1f, 8, 15).Emit(b, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(11f, 17f, 7f), Matrix4x4.CreateRotationX(-0.19198622f - hop * 0.1f)), p.LegScale); // left front leg
        new EntityCuboid(-1f, 0f, -1f, 1f, 7f, 1f, 0, 15).Emit(b, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(5f, 17f, 7f), Matrix4x4.CreateRotationX(-0.19198622f - hop * 0.1f)), p.LegScale); // right front leg

        var headPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 16f, 7f), Matrix4x4.CreateRotationX(-hop * 0.12f));
        new EntityCuboid(-2.5f, -4f, -5f, 2.5f, 0f, 0f, 32, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2.5f, -9f, -1f, -0.5f, -4f, 0f, 52, 0, XRot: 0f, YRot: -0.2617994f, ZRot: 0f) { RotationPivot = Vector3.Zero }.Emit(b, headPose, p.HeadScale); // right ear
        new EntityCuboid(0.5f, -9f, -1f, 2.5f, -4f, 0f, 58, 0, XRot: 0f, YRot: 0.2617994f, ZRot: 0f) { RotationPivot = Vector3.Zero }.Emit(b, headPose, p.HeadScale); // left ear
        new EntityCuboid(-0.5f, -2.5f, -5.5f, 0.5f, -1.5f, -4.5f, 32, 9).Emit(b, headPose, p.HeadScale); // nose
        new EntityCuboid(-1.5f, 0f, 0f, 1.5f, 3f, 2f, 52, 6).Emit(b, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 20f, 15f), Matrix4x4.CreateRotationX(-0.3490659f + hop * 0.12f)), p.BodyScale); // tail
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildCat(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headTilt,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        if (isBaby && UsesPostBabyModelUpdate(profile))
        {
            return BuildBabyCat(
                texRef,
                headTilt,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = UsesPostBabyModelUpdate(profile)
            ? BabyProfile.Adult
            : (isBaby ? new BabyProfile(0.82f, 1.08f, 0.84f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 64);
        AppendFelineModelQuadrupedMesh(b, p, headTilt, rightHindLegPitchRad, leftHindLegPitchRad, rightFrontLegPitchRad, leftFrontLegPitchRad);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
    private static MergedJavaBlockModel BuildFox(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float tailLift,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyFox(
                texRef,
                tailLift,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(48, 32);

        // Head root PartPose.offset(-1, 16.5, -3); cuboids in head-local space (javap).
        var headRoot = EntityParityTemplate.T(-1f, 16.5f, -3f);
        new EntityCuboid(-3f, -2f, -5f, 5f, 4f, 1f, 1, 5).Emit(b, headRoot, p.HeadScale);
        new EntityCuboid(-3f, -4f, -4f, -1f, -2f, -3f, 8, 1).Emit(b, headRoot, p.HeadScale);
        new EntityCuboid(3f, -4f, -4f, 5f, -2f, -3f, 15, 1).Emit(b, headRoot, p.HeadScale);
        new EntityCuboid(-1f, 2.01f, -8f, 3f, 4.01f, -5f, 6, 18).Emit(b, headRoot, p.HeadScale);

        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 16f, -6f), EntityParityTemplate.Rx(MathF.PI / 2f));
        new EntityCuboid(-3f, 3.999f, -3.5f, 3f, 14.999f, 2.5f, 24, 15).Emit(b, bodyPose, p.BodyScale);

        // Tail: child of body; PartPose.offsetAndRotation(-4,15,-1, ~-3°, 0, 0); texOffs(30,0) (2,0,-1)+(4,9,5).
        var tailBasePitch = -0.05235988f + tailLift;
        var tailPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-4f, 15f, -1f), EntityParityTemplate.Er(tailBasePitch, 0f, 0f)));
        new EntityCuboid(2f, 0f, -1f, 6f, 9f, 4f, 30, 0).Emit(b, tailPose, p.BodyScale);

        // Legs: texOffs (13,24) right vs (4,24) left mirror; addBox(2,0.5,-1)+(2,6,2); CubeDeformation(0.001) omitted.
        var rh = new Vector3(-5f, 17.5f, 7f);
        var lh = new Vector3(-1f, 17.5f, 7f);
        var rf = new Vector3(-5f, 17.5f, 0f);
        var lf = new Vector3(-1f, 17.5f, 0f);
        new EntityCuboid(2f, 0.5f, -1f, 4f, 6.5f, 1f, 13, 24).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(rh.X, rh.Y, rh.Z), EntityParityTemplate.Rx(rightHindLegPitchRad)), p.LegScale);
        new EntityCuboid(2f, 0.5f, -1f, 4f, 6.5f, 1f, 4, 24, MirrorUv: true).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(lh.X, lh.Y, lh.Z), EntityParityTemplate.Rx(leftHindLegPitchRad)), p.LegScale);
        new EntityCuboid(2f, 0.5f, -1f, 4f, 6.5f, 1f, 13, 24).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(rf.X, rf.Y, rf.Z), EntityParityTemplate.Rx(rightFrontLegPitchRad)), p.LegScale);
        new EntityCuboid(2f, 0.5f, -1f, 4f, 6.5f, 1f, 4, 24, MirrorUv: true).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(lf.X, lf.Y, lf.Z), EntityParityTemplate.Rx(leftFrontLegPitchRad)), p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }
}
