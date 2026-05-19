using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Quadruped family rigs (cow, pig, sheep, wolf, llama, camel, panda, polar bear, rabbit, feline, fox).

    private static MergedJavaBlockModel BuildBabyCow(
        string texRef,
        float headPitch,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 13.569f, -5.1667f)),
            EntityParityTemplate.Rx(headPitch));
        new EntityCuboid(-3f, -4.569f, -4.8333f, 3f, 1.431f, 0.1667f, 0, 18, UvSizeW: 6, UvSizeH: 6, UvSizeD: 5).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(3f, -5.569f, -3.8333f, 4f, -3.569f, -2.8333f, 8, 29, UvSizeW: 1, UvSizeH: 2, UvSizeD: 1).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-4f, -5.569f, -3.8333f, -3f, -3.569f, -2.8333f, 4, 29, UvSizeW: 1, UvSizeH: 2, UvSizeD: 1, MirrorUv: true).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2f, -1.569f, -5.8333f, 2f, 1.431f, -4.8333f, 12, 29, UvSizeW: 4, UvSizeH: 3, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(3f, 19f, -5f));
        new EntityCuboid(-7f, -7f, -1f, 1f, -1f, 11f, 0, 0, UvSizeW: 8, UvSizeH: 6, UvSizeD: 12).Emit(b, bodyPose, p.BodyScale);

        var rfPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2.5f, 18f, -3.5f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 6f, 1.5f, 22, 18, UvSizeW: 3, UvSizeH: 6, UvSizeD: 3).Emit(b, rfPose, p.LegScale);

        var lfPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2.5f, 18f, -3.5f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 6f, 1.5f, 34, 18, UvSizeW: 3, UvSizeH: 6, UvSizeD: 3, MirrorUv: true).Emit(b, lfPose, p.LegScale);

        var rhPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2.5f, 18f, 3.5f)),
            EntityParityTemplate.Rx(rightHindLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 6f, 1.5f, 22, 27, UvSizeW: 3, UvSizeH: 6, UvSizeD: 3).Emit(b, rhPose, p.LegScale);

        var lhPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2.5f, 18f, 3.5f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 6f, 1.5f, 34, 27, UvSizeW: 3, UvSizeH: 6, UvSizeD: 3, MirrorUv: true).Emit(b, lhPose, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }

    private static MergedJavaBlockModel BuildBabySheep(
        string texRef,
        float grazeDip,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 17f, 0.5f));
        new EntityCuboid(-3f, -2f, -4.5f, 3f, 2f, 4.5f, 0, 10, UvSizeW: 6, UvSizeH: 4, UvSizeD: 9).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 15.5f, -2.5f)),
            EntityParityTemplate.Rx(-grazeDip));
        new EntityCuboid(-2.5f, -4.5f, -3.5f, 2.5f, 0.5f, 1.5f, 0, 0, UvSizeW: 5, UvSizeH: 5, UvSizeD: 5).Emit(b, headPose, p.HeadScale);

        var rhPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 19f, 3f)),
            EntityParityTemplate.Rx(rightHindLegPitchRad));
        new EntityCuboid(-1f, 0f, -1f, 1f, 5f, 1f, 0, 23).Emit(b, rhPose, p.LegScale);

        var lhPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2f, 19f, 3f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(-1f, 0f, -1f, 1f, 5f, 1f, 24, 12, MirrorUv: true).Emit(b, lhPose, p.LegScale);

        var rfPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 19f, -2f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(-1f, 0f, -1f, 1f, 5f, 1f, 8, 23).Emit(b, rfPose, p.LegScale);

        var lfPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2f, 19f, -2f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(-1f, 0f, -1f, 1f, 5f, 1f, 24, 5, MirrorUv: true).Emit(b, lfPose, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }

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

    private static MergedJavaBlockModel BuildBabyPig(
        string texRef,
        float snoutBob,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 19f, 0.5f));
        new EntityCuboid(-3f, -4.5f, 7f, 3f, 4.5f, 7f, 0, 0, UvSizeW: 6, UvSizeH: 9, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 19f, -2f)),
            EntityParityTemplate.Rx(snoutBob));
        new EntityCuboid(-5f, -5f, 7f, 1f, 1f, 7.025f, 0, 15, UvSizeW: 6, UvSizeH: 6, UvSizeD: 1).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-1.975f, -6f, 3f, 0.025f, -5f, 3.015f, 6, 27, UvSizeW: 2, UvSizeH: 1, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        var lfPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2.5f, 22f, -3f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(0f, -1f, 2f, 2f, 1f, 2f, 0, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1, MirrorUv: true).Emit(b, lfPose, p.LegScale);

        var rfPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2.5f, 22f, -3f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(0f, -1f, 2f, 2f, 1f, 2f, 23, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, rfPose, p.LegScale);

        var lhPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2.5f, 22f, 4f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(0f, -1f, 2f, 2f, 1f, 2f, 0, 4, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1, MirrorUv: true).Emit(b, lhPose, p.LegScale);

        var rhPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2.5f, 22f, 4f)),
            EntityParityTemplate.Rx(rightHindLegPitchRad));
        new EntityCuboid(0f, -1f, 2f, 2f, 1f, 2f, 23, 4, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, rhPose, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }

    private static void AppendPigQuadrupedMesh(
        RigBuilder b,
        BabyProfile p,
        float snoutBob,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var headScale = p.HeadScale;
        var bodyScale = p.BodyScale;
        var legScale = p.LegScale;

        // Body: texOffs(28,8), (-5,-10,-7)+ (10,16,8), PartPose.offsetAndRotation(0,11,2, pi/2,0,0).
        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 11f, 2f), EntityParityTemplate.Rx(MathF.PI / 2f));
        new EntityCuboid(-5f, -10f, -7f, 5f, 6f, 1f, 28, 8).Emit(b, bodyPose, bodyScale);

        // Head + snout (head PartPose (0,12,-6)); idle preview pitch on the head stack only.
        var headRoot = new Vector3(0f, 12f, -6f);
        var headPose = EntityParityTemplate.Mul(EntityParityTemplate.T(headRoot.X, headRoot.Y, headRoot.Z), EntityParityTemplate.Rx(snoutBob));
        new EntityCuboid(-4f, -4f, -8f, 4f, 4f, 0f, 0, 0).Emit(b, headPose, headScale);
        new EntityCuboid(-2f, 0f, -9f, 2f, 3f, -8f, 16, 16).Emit(b, headPose, headScale);

        // Legs: texOffs(0,16), (-2,0,-2)+(4,6,4); right pair unmirror, left pair mirror (vanilla pig UV).
        var rh = new Vector3(-3f, 18f, 7f);
        var lh = new Vector3(3f, 18f, 7f);
        var rf = new Vector3(-3f, 18f, -5f);
        var lf = new Vector3(3f, 18f, -5f);
        AppendQuadrupedLegSetStandardLocalCuboidTex016(
            b,
            legScale,
            legFootY: 6f,
            rh,
            lh,
            rf,
            lf,
            mirrorLeftHind: true,
            mirrorLeftFront: true,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);
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

    private static MergedJavaBlockModel BuildBabyWolf(
        string texRef,
        float headPitch,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 32);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 19f, 0f));
        new EntityCuboid(-3f, -2f, -4f, 3f, 2f, 4f, 0, 0, UvSizeW: 6, UvSizeH: 4, UvSizeD: 8).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 18.25f, -4f)),
            EntityParityTemplate.Rx(headPitch));
        new EntityCuboid(-3.25f, -3f, 6f, 1.75f, 2f, 6.025f, 0, 12, UvSizeW: 5, UvSizeH: 5, UvSizeD: 1).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-0.24f, -5f, 3f, 1.76f, -3f, 3.025f, 17, 12, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        var rightEarPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(-2f, -4.25f, -0.5f));
        new EntityCuboid(-1f, -1f, -0.5f, 1f, 1f, 0.5f, 0, 5, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, rightEarPose, p.HeadScale);

        var leftEarPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(2f, -4.25f, -0.5f));
        new EntityCuboid(-1f, -1f, -0.5f, 1f, 1f, 0.5f, 20, 5, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, leftEarPose, p.HeadScale);

        var rightHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1.5f, 21f, 3f)),
            EntityParityTemplate.Rx(rightHindLegPitchRad));
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 0, 22, UvSizeW: 2, UvSizeH: 3, UvSizeD: 2).Emit(b, rightHindPose, p.LegScale);

        var leftHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1.5f, 21f, 3f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 8, 22, UvSizeW: 2, UvSizeH: 3, UvSizeD: 2).Emit(b, leftHindPose, p.LegScale);

        var rightFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1.5f, 21f, -3f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 0, 0, UvSizeW: 2, UvSizeH: 3, UvSizeD: 2).Emit(b, rightFrontPose, p.LegScale);

        var leftFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1.5f, 21f, -3f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 20, 0, UvSizeW: 2, UvSizeH: 3, UvSizeD: 2, MirrorUv: true).Emit(b, leftFrontPose, p.LegScale);

        var tailPose = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -0.6f, 0.2f), EntityParityTemplate.Rx(-3.1f)));
        new EntityCuboid(-5.7f, -1f, 2f, 0.3f, 1f, 2.025f, 22, 16, UvSizeW: 6, UvSizeH: 2, UvSizeD: 1).Emit(b, tailPose, p.BodyScale);

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

    private static MergedJavaBlockModel BuildBabyGoat(
        string texRef,
        float headPitch,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var lhPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1.5f, 19.5f, 3f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(-1f, -0.5f, -1f, 1f, 4.5f, 1f, 29, 12, UvSizeW: 2, UvSizeH: 5, UvSizeD: 2, MirrorUv: true).Emit(b, lhPose, p.LegScale);

        var rhPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1.5f, 19.5f, 3f)),
            EntityParityTemplate.Rx(rightHindLegPitchRad));
        new EntityCuboid(-1f, -0.5f, -1f, 1f, 4.5f, 1f, 21, 12, UvSizeW: 2, UvSizeH: 5, UvSizeD: 2).Emit(b, rhPose, p.LegScale);

        var rfPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1.5f, 19.5f, -2f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(-1f, -0.5f, -1f, 1f, 4.5f, 1f, 21, 5, UvSizeW: 2, UvSizeH: 5, UvSizeD: 2).Emit(b, rfPose, p.LegScale);

        var lfPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1.5f, 19.5f, -2f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(-1f, -0.5f, -1f, 1f, 4.5f, 1f, 29, 5, UvSizeW: 2, UvSizeH: 5, UvSizeD: 2, MirrorUv: true).Emit(b, lfPose, p.LegScale);

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 17.8f, 0f));
        new EntityCuboid(-3f, -2.3f, -4.5f, 3f, 2.7f, 4.5f, 0, 10, UvSizeW: 6, UvSizeH: 5, UvSizeD: 9).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-2.5f, -2.2f, -4f, 2.5f, 1.8f, 4f, 0, 24, UvSizeW: 5, UvSizeH: 4, UvSizeD: 8).Emit(b, bodyPose, p.BodyScale);

        var headAnim = EntityParityTemplate.Mul(EntityParityTemplate.Rx(0.4363f), EntityParityTemplate.Rx(headPitch));
        var headPose = EntityParityTemplate.Mul(EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 15.5f, -3f)), headAnim);
        new EntityCuboid(-2f, -3.8126f, -5.1548f, 2f, 0.1874f, 0.8452f, 0, 0, UvSizeW: 4, UvSizeH: 4, UvSizeD: 6).Emit(b, headPose, p.HeadScale);

        var hornBase = EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-1.5f, -1.5f, -1f), EntityParityTemplate.Rx(-0.3926991f)));
        new EntityCuboid(0f, -4.5f, 0f, 1f, -2.5f, 1f, 24, 0, UvSizeW: 1, UvSizeH: 2, UvSizeD: 1).Emit(b, hornBase, p.HeadScale);
        new EntityCuboid(2f, -4.5f, 0f, 3f, -2.5f, 1f, 24, 0, UvSizeW: 1, UvSizeH: 2, UvSizeD: 1, MirrorUv: true).Emit(b, hornBase, p.HeadScale);

        var rEarPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-1.7f, -2.3126f, 0.1452f), EntityParityTemplate.Ry(-0.5236f)));
        new EntityCuboid(-2f, -0.5f, -0.5f, 0f, 0.5f, 0.5f, 0, 12, UvSizeW: 2, UvSizeH: 1, UvSizeD: 1).Emit(b, rEarPose, p.HeadScale);

        var lEarPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(1.7f, -2.3126f, 0.1452f), EntityParityTemplate.Ry(0.5236f)));
        new EntityCuboid(0f, -0.5f, -0.5f, 2f, 0.5f, 0.5f, 0, 12, UvSizeW: 2, UvSizeH: 1, UvSizeD: 1, MirrorUv: true).Emit(b, lEarPose, p.HeadScale);

        var headMainPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(0f, -1.3126f, -1.1548f));
        new EntityCuboid(-2f, -2.5f, -4f, 2f, 1.5f, 2f, 0, 0, UvSizeW: 4, UvSizeH: 4, UvSizeD: 6).Emit(b, headMainPose, p.HeadScale);

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

    private static MergedJavaBlockModel BuildBabyLlama(string texRef, float neckBend)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(128, 64);
        var root = Matrix4x4.Identity;

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 12f, -4f)),
            EntityParityTemplate.Rx(neckBend));
        new EntityCuboid(-3f, -9f, -4f, 3f, 2f, 0f, 0, 0, UvSizeW: 6, UvSizeH: 11, UvSizeD: 4).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-1.5f, -7f, -7f, 1.5f, -4f, -4f, 0, 15, UvSizeW: 3, UvSizeH: 3, UvSizeD: 3).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(0.5f, -11f, -3f, 2.5f, -9f, -1f, 20, 4, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2.5f, -11f, -3f, -0.5f, -9f, -1f, 20, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2, MirrorUv: true).Emit(b, headPose, p.HeadScale);

        var rhPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2.5f, 16.5f, 4.5f));
        new EntityCuboid(-1.4f, -0.5f, -1.5f, 1.6f, 7.5f, 1.5f, 0, 45, UvSizeW: 3, UvSizeH: 8, UvSizeD: 3).Emit(b, rhPose, p.LegScale);

        var lhPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(2.5f, 16.5f, 4.5f));
        new EntityCuboid(-1.6f, -0.5f, -1.5f, 1.4f, 7.5f, 1.5f, 12, 45, UvSizeW: 3, UvSizeH: 8, UvSizeD: 3, MirrorUv: true).Emit(b, lhPose, p.LegScale);

        var rfPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2.5f, 16.5f, -3.5f));
        new EntityCuboid(-1.4f, -0.5f, -1.5f, 1.6f, 7.5f, 1.5f, 0, 34, UvSizeW: 3, UvSizeH: 8, UvSizeD: 3).Emit(b, rfPose, p.LegScale);

        var lfPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(2.5f, 16.5f, -3.5f));
        new EntityCuboid(-1.6f, -0.5f, -1.5f, 1.4f, 7.5f, 1.5f, 12, 34, UvSizeW: 3, UvSizeH: 8, UvSizeD: 3, MirrorUv: true).Emit(b, lfPose, p.LegScale);

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 14f, 2.5f));
        new EntityCuboid(-4f, -3f, -8.5f, 4f, 3f, 4.5f, 0, 15, UvSizeW: 8, UvSizeH: 6, UvSizeD: 13).Emit(b, bodyPose, p.BodyScale);

        var chestRy = EntityParityTemplate.Ry(MathF.PI / 2f);
        var chestRight = EntityParityTemplate.Mul(EntityParityTemplate.Mul(root, EntityParityTemplate.T(-8.5f, 4f, 3f)), chestRy);
        new EntityCuboid(-3f, 0f, 0f, 5f, 8f, 3f, 45, 28, UvSizeW: 8, UvSizeH: 8, UvSizeD: 3).Emit(b, chestRight, p.BodyScale);

        var chestLeft = EntityParityTemplate.Mul(EntityParityTemplate.Mul(root, EntityParityTemplate.T(5.5f, 4f, 3f)), chestRy);
        new EntityCuboid(-3f, 0f, 0f, 5f, 8f, 3f, 45, 41, UvSizeW: 8, UvSizeH: 8, UvSizeD: 3).Emit(b, chestLeft, p.BodyScale);

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

    private static MergedJavaBlockModel BuildBabyCamel(
        string texRef,
        float neckBend,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad,
        float headWalkTranslateZ = 0f)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(128, 128);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 7f, 0f));
        new EntityCuboid(-4.5f, -4f, -8f, 4.5f, 4f, 8f, 0, 14, UvSizeW: 9, UvSizeH: 8, UvSizeD: 16).Emit(b, bodyPose, p.BodyScale);

        var tailPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, -1.5f, 8.05f));
        new EntityCuboid(-1.5f, -0.5f, -0.025f, 1.5f, 8.5f, 0.025f, 50, 38, UvSizeW: 3, UvSizeH: 9, UvSizeD: 1).Emit(b, tailPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 1f, -7.5f + headWalkTranslateZ)),
            EntityParityTemplate.Rx(neckBend));
        new EntityCuboid(-2.5f, -3f, -7.5f, 2.5f, 2f, -0.5f, 20, 0, UvSizeW: 5, UvSizeH: 5, UvSizeD: 7).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2.5f, -12f, -7.5f, 2.5f, -3f, -2.5f, 0, 0, UvSizeW: 5, UvSizeH: 9, UvSizeD: 5).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2.5f, -12f, -10.5f, 2.5f, -8f, -7.5f, 0, 14, UvSizeW: 5, UvSizeH: 4, UvSizeD: 3).Emit(b, headPose, p.HeadScale);

        var rightEarPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2.5f, -11f, -4f));
        new EntityCuboid(-3f, -0.5f, -1f, 0f, 0.5f, 1f, 37, 0, UvSizeW: 3, UvSizeH: 1, UvSizeD: 2).Emit(b, rightEarPose, p.HeadScale);

        var leftEarPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(2.5f, -11f, -4f));
        new EntityCuboid(0f, -0.5f, -1f, 3f, 0.5f, 1f, 47, 0, UvSizeW: 3, UvSizeH: 1, UvSizeD: 2).Emit(b, leftEarPose, p.HeadScale);

        var rightFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-3f, 11.5f, -5.5f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(-1.5f, -0.5f, -1.5f, 1.5f, 12.5f, 1.5f, 36, 14, UvSizeW: 3, UvSizeH: 13, UvSizeD: 3).Emit(b, rightFrontPose, p.LegScale);

        var leftFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(3f, 11.5f, -5.5f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(-1.5f, -0.5f, -1.5f, 1.5f, 12.5f, 1.5f, 48, 14, UvSizeW: 3, UvSizeH: 13, UvSizeD: 3).Emit(b, leftFrontPose, p.LegScale);

        var leftHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(3f, 11.5f, 5.5f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(-1.5f, -0.5f, -1.5f, 1.5f, 12.5f, 1.5f, 12, 38, UvSizeW: 3, UvSizeH: 13, UvSizeD: 3).Emit(b, leftHindPose, p.LegScale);

        var rightHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-3f, 11.5f, 5.5f)),
            EntityParityTemplate.Rx(rightHindLegPitchRad));
        new EntityCuboid(-1.5f, -0.5f, -1.5f, 1.5f, 12.5f, 1.5f, 0, 38, UvSizeW: 3, UvSizeH: 13, UvSizeD: 3).Emit(b, rightHindPose, p.LegScale);

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

    private static MergedJavaBlockModel BuildBabyPanda(
        string texRef,
        float bodyRoll,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 18.5f, 2.5f)),
            EntityParityTemplate.Ry(bodyRoll));
        new EntityCuboid(-4.5f, -3.5f, -5.5f, 4.5f, 3.5f, 5.5f, 0, 11).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 19f, -3f));
        new EntityCuboid(-3.5f, -3f, -5f, 3.5f, 3f, 0f, 0, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2f, 1f, -6f, 2f, 3f, -5f, 24, 6).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-4.5f, -4f, -3.5f, -1.5f, -1f, -2.5f, 24, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(1.5f, -4f, -3.5f, 4.5f, -1f, -2.5f, 33, 0).Emit(b, headPose, p.HeadScale);

        var rightHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-3f, 22f, 6.5f)),
            EntityParityTemplate.Rx(rightHindLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 2f, 1.5f, 0, 34).Emit(b, rightHindPose, p.LegScale);

        var leftHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(3f, 22f, 6.5f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 2f, 1.5f, 12, 34).Emit(b, leftHindPose, p.LegScale);

        var rightFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-3f, 22f, -1.5f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 2f, 1.5f, 0, 29).Emit(b, rightFrontPose, p.LegScale);

        var leftFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(3f, 22f, -1.5f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 2f, 1.5f, 12, 29).Emit(b, leftFrontPose, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }

    private static MergedJavaBlockModel BuildBabyPolarBear(
        string texRef,
        float headLift,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(128, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 17.5f, 0f));
        new EntityCuboid(-4f, -3.5f, -6f, 4f, 3.5f, 6f, 0, 9).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 18.625f, -5.75f)),
            EntityParityTemplate.Rx(headLift));
        new EntityCuboid(-3f, -2.625f, -4.25f, 3f, 2.375f, -0.25f, 0, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2f, 0.375f, -6.25f, 2f, 2.375f, -4.25f, 20, 3).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-4f, -3.625f, -2.75f, -2f, -1.625f, -1.75f, 20, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(2f, -3.625f, -2.75f, 4f, -1.625f, -1.75f, 26, 0).Emit(b, headPose, p.HeadScale);

        var rightHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2.5f, 21.5f, 4.5f)),
            EntityParityTemplate.Rx(rightHindLegPitchRad));
        new EntityCuboid(-1.5f, -0.5f, -1.5f, 1.5f, 2.5f, 1.5f, 0, 34).Emit(b, rightHindPose, p.LegScale);

        var leftHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2.5f, 21.5f, 4.5f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(-1.5f, -0.5f, -1.5f, 1.5f, 2.5f, 1.5f, 12, 34).Emit(b, leftHindPose, p.LegScale);

        var rightFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2.5f, 21.5f, -4.5f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(-1.5f, -0.5f, -1.5f, 1.5f, 2.5f, 1.5f, 0, 28).Emit(b, rightFrontPose, p.LegScale);

        var leftFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2.5f, 21.5f, -4.5f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(-1.5f, -0.5f, -1.5f, 1.5f, 2.5f, 1.5f, 12, 28).Emit(b, leftFrontPose, p.LegScale);

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

    private static void GetCowModelBodyAndHeadPoses(float headPitch, out Matrix4x4 bodyPose, out Matrix4x4 headPose)
    {
        bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 5f, 2f), EntityParityTemplate.Rx(MathF.PI / 2f));
        headPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 4f, -8f), EntityParityTemplate.Rx(headPitch));
    }

    private static void AppendQuadrupedFamilyCowModelBodyHeadLegs(
        RigBuilder b,
        BabyProfile p,
        float headPitch,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        GetCowModelBodyAndHeadPoses(headPitch, out var bodyPose, out var headPose);
        new EntityCuboid(-6f, -10f, -7f, 6f, 8f, 3f, 18, 4).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-4f, -4f, -6f, 4f, 4f, 0f, 0, 0).Emit(b, headPose, p.HeadScale);
        AppendCowModelTex016LegsJar2612(
            b,
            p,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);
    }

    private static void AppendCowMooshroomUdderOnly(RigBuilder b, BabyProfile p, float headPitch)
    {
        GetCowModelBodyAndHeadPoses(headPitch, out var bodyPose, out _);
        new EntityCuboid(-2f, 2f, -8f, 2f, 8f, -7f, 52, 0).Emit(b, bodyPose, p.BodyScale);
    }

    private static void AppendColdCowHornsNamedJar2612(RigBuilder b, BabyProfile p, Matrix4x4 headPose)
    {
        var rightHornPose = EntityParityTemplate.Mul(
            headPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(-4.5f, -2.5f, -3.5f), EntityParityTemplate.Rx(1.5708f)));
        new EntityCuboid(-1.5f, -4.5f, -0.5f, 0.5f, 1.5f, 1.5f, 0, 40).Emit(b, rightHornPose, p.HeadScale);

        var leftHornPose = EntityParityTemplate.Mul(
            headPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(5.5f, -2.5f, -5f), EntityParityTemplate.Rx(1.5708f)));
        new EntityCuboid(-1.5f, -3f, -0.5f, 0.5f, 3f, 1.5f, 0, 32).Emit(b, leftHornPose, p.HeadScale);
    }

    private static void AppendCowMooshroomUdderAndOptionalHorns(RigBuilder b, BabyProfile p, float headPitch, bool hasHorns)
    {
        AppendCowMooshroomUdderOnly(b, p, headPitch);
        if (!hasHorns)
        {
            return;
        }

        GetCowModelBodyAndHeadPoses(headPitch, out _, out var headPose);
        new EntityCuboid(-5f, -5f, -4f, -4f, -2f, -3f, 22, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(4f, -5f, -4f, 5f, -2f, -3f, 22, 0).Emit(b, headPose, p.HeadScale);
    }

    private static void AppendQuadrupedLegSetStandardLocalCuboidTex016(
        RigBuilder b,
        float legScale,
        float legFootY,
        Vector3 rightHindRoot,
        Vector3 leftHindRoot,
        Vector3 rightFrontRoot,
        Vector3 leftFrontRoot,
        bool mirrorLeftHind,
        bool mirrorLeftFront,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        void EmitLeg(Vector3 root, float pitchRad, bool mirrorUv)
        {
            var pose = EntityParityTemplate.Mul(
                EntityParityTemplate.T(root.X, root.Y, root.Z),
                EntityParityTemplate.Rx(pitchRad));
            QuadrupedLegCuboidTex016(legFootY, mirrorUv).Emit(b, pose, legScale);
        }

        EmitLeg(rightHindRoot, rightHindLegPitchRad, mirrorUv: false);
        EmitLeg(leftHindRoot, leftHindLegPitchRad, mirrorLeftHind);
        EmitLeg(rightFrontRoot, rightFrontLegPitchRad, mirrorUv: false);
        EmitLeg(leftFrontRoot, leftFrontLegPitchRad, mirrorLeftFront);
    }

    private static void AppendCowModelTex016LegsJar2612(
        RigBuilder b,
        BabyProfile p,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad) =>
        AppendQuadrupedLegSetStandardLocalCuboidTex016(
            b,
            p.LegScale,
            legFootY: 12f,
            new Vector3(-4f, 12f, 7f),
            new Vector3(4f, 12f, 7f),
            new Vector3(-4f, 12f, -5f),
            new Vector3(4f, 12f, -5f),
            mirrorLeftHind: true,
            mirrorLeftFront: true,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);

    /// <summary>
    /// Sheep (and other <c>QuadrupedModel.createBodyMesh(12,…)</c> callers): <c>texOffs(0,16)</c> legs at <c>(±4,12,7)</c> / <c>(±4,12,-5)</c> front Z matches <c>createLegs</c> <c>-5</c> float literal.
    /// </summary>
    private static void AppendQuadrupedSetupAnimLegSetTex016(
        RigBuilder b,
        BabyProfile p,
        float legFootY,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        AppendQuadrupedLegSetStandardLocalCuboidTex016(
            b,
            p.LegScale,
            legFootY,
            new Vector3(-4f, 12f, 7f),
            new Vector3(4f, 12f, 7f),
            new Vector3(-4f, 12f, -5f),
            new Vector3(4f, 12f, -5f),
            mirrorLeftHind: false,
            mirrorLeftFront: false,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);
    }

    private static void AppendWolfModelLegSetTex018(
        RigBuilder b,
        BabyProfile p,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        void Leg(Vector3 root, float pitch)
        {
            new EntityCuboid(0f, 0f, -1f, 2f, 8f, 1f, 0, 18).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(root.X, root.Y, root.Z), EntityParityTemplate.Rx(pitch)), p.LegScale);
        }

        Leg(new Vector3(5.5f, 16f, 15f), rightHindLegPitchRad);
        Leg(new Vector3(9.5f, 16f, 15f), leftHindLegPitchRad);
        Leg(new Vector3(5.5f, 16f, 8f), rightFrontLegPitchRad);
        Leg(new Vector3(9.5f, 16f, 8f), leftFrontLegPitchRad);
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

    private static (float Rh, float Lh, float Rf, float Lf) ComputePreviewStandardQuadrupedLegPitches(
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        string? builderMethod = null,
        string? setupAnimModelJvm = null)
    {
        var state = PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave);
        var model = SetupAnimParityResolver.ResolveModelJvm(builderMethod, setupAnimModelJvm);
        const string quadruped = "net.minecraft.client.model.QuadrupedModel";
        if (VanillaSetupAnimRuntime.TryGetLegXRots(model, state, out var rh, out var lh, out var rf, out var lf) &&
            VanillaSetupAnimRuntime.LegPitchesVaryWithWalk(model, idlePhase01, wave))
        {
            return (rh, lh, rf, lf);
        }

        if (!string.Equals(model, quadruped, StringComparison.Ordinal) &&
            VanillaSetupAnimRuntime.TryGetLegXRots(quadruped, state, out rh, out lh, out rf, out lf))
        {
            return (rh, lh, rf, lf);
        }

        return (0f, 0f, 0f, 0f);
    }

    private static MergedJavaBlockModel BuildBabyRabbit(string texRef, float hopCompress)
    {
        var hop = Math.Clamp(hopCompress, -0.75f, 0.75f);
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var bodyR1 = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, -2f, -1.6f)),
            EntityParityTemplate.Er(-0.5236f - hop * 0.08f, 0f, 0f));
        new EntityCuboid(-2f, -2f, -3f, 2f, 1f, 3f, 0, 8, UvSizeW: 4, UvSizeH: 3, UvSizeD: 6).Emit(b, bodyR1, p.BodyScale);

        var tailR1 = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-0.1f, 0f, 0f)),
            EntityParityTemplate.Er(-0.5236f - hop * 0.06f, 0f, 0f));
        new EntityCuboid(-1.4f, -2.0268f, -1.0177f, 1.6f, 0.9732f, 1.9823f, 0, 21, UvSizeW: 3, UvSizeH: 3, UvSizeD: 3).Emit(b, tailR1, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, -5f, -2.6f)),
            EntityParityTemplate.Rx(-hop * 0.12f));
        new EntityCuboid(-2.5f, -3f, -3f, 2.5f, 1f, 1f, 0, 0, UvSizeW: 5, UvSizeH: 4, UvSizeD: 4).Emit(b, headPose, p.HeadScale);

        var rightEarPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1.5f, -3.5f, -0.5f));
        new EntityCuboid(-1f, -3.5f, -0.5f, 1f, 0.5f, 0.5f, 18, 0, UvSizeW: 2, UvSizeH: 4, UvSizeD: 1).Emit(b, rightEarPose, p.HeadScale);

        var leftEarPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(1.5f, -3.5f, -0.5f));
        new EntityCuboid(-1f, -3.5f, -0.5f, 1f, 0.5f, 0.5f, 24, 0, UvSizeW: 2, UvSizeH: 4, UvSizeD: 1).Emit(b, leftEarPose, p.HeadScale);

        var leftFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 1f, 0f)),
            EntityParityTemplate.Er(-0.3927f - hop * 0.1f, 0f, 0f));
        new EntityCuboid(-0.5f, -1.5f, -0.5f, 0.5f, 1.5f, 0.5f, 18, 8, UvSizeW: 1, UvSizeH: 3, UvSizeD: 1).Emit(b, leftFrontPose, p.LegScale);

        var rightFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 1f, 0f)),
            EntityParityTemplate.Er(-0.3927f - hop * 0.1f, 0f, 0f));
        new EntityCuboid(-0.5f, -1.5f, -0.5f, 0.5f, 1.5f, 0.5f, 14, 8, UvSizeW: 1, UvSizeH: 3, UvSizeD: 1).Emit(b, rightFrontPose, p.LegScale);

        var leftHaunchPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 0f, 0.5f)),
            EntityParityTemplate.Er(0f, -0.7854f, 0f));
        new EntityCuboid(-2f, -0.5f, 0f, 0f, 0.5f, 3f, 10, 17, UvSizeW: 2, UvSizeH: 1, UvSizeD: 3).Emit(b, leftHaunchPose, p.LegScale);

        var rightHaunchPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0.5f, 0f, -0.9f)),
            EntityParityTemplate.Er(0f, 0.7854f, 0f));
        new EntityCuboid(-2f, -0.5f, 0f, 0f, 0.5f, 3f, 0, 17, UvSizeW: 2, UvSizeH: 1, UvSizeD: 3).Emit(b, rightHaunchPose, p.LegScale);

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

    private static void AppendFelineModelQuadrupedMesh(
        RigBuilder b,
        BabyProfile p,
        float headTilt,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var bodyPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(0f, 12f, -10f),
            EntityParityTemplate.Rx(MathF.PI / 2f));
        new EntityCuboid(-2f, 3f, -8f, 2f, 19f, -2f, 20, 0, UvSizeW: 4, UvSizeH: 16, UvSizeD: 6).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 15f, -9f), EntityParityTemplate.Rx(headTilt));
        new EntityCuboid(-2.5f, -2f, -3f, 2.5f, 2f, 2f, 0, 0, UvSizeW: 5, UvSizeH: 4, UvSizeD: 5).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-1.5f, -0.001f, -4f, 1.5f, 1.999f, -2f, 0, 24, UvSizeW: 3, UvSizeH: 2, UvSizeD: 2).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2f, -3f, 0f, -1f, -2f, 2f, 0, 10, UvSizeW: 1, UvSizeH: 1, UvSizeD: 2).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(1f, -3f, 0f, 2f, -2f, 2f, 6, 10, UvSizeW: 1, UvSizeH: 1, UvSizeD: 2).Emit(b, headPose, p.HeadScale);

        var tail1Pose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(0f, 15f, 8f),
            EntityParityTemplate.Rx(0.9f));
        new EntityCuboid(-0.5f, 0f, 0f, 0.5f, 8f, 1f, 0, 15, UvSizeW: 1, UvSizeH: 8, UvSizeD: 1).Emit(b, tail1Pose, p.BodyScale);

        var tail2Pose = EntityParityTemplate.T(0f, 20f, 14f);
        new EntityCuboid(-0.5f, 0f, 0f, 0.5f, 8f, 1f, 4, 15, UvSizeW: 1, UvSizeH: 8, UvSizeD: 1).Emit(b, tail2Pose, p.BodyScale);

        new EntityCuboid(-1f, 0f, 1f, 1f, 6f, 3f, 8, 13, UvSizeW: 2, UvSizeH: 6, UvSizeD: 2).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(1.1f, 18f, 5f), EntityParityTemplate.Rx(rightHindLegPitchRad)), p.LegScale);
        new EntityCuboid(-1f, 0f, 1f, 1f, 6f, 3f, 8, 13, UvSizeW: 2, UvSizeH: 6, UvSizeD: 2).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(-1.1f, 18f, 5f), EntityParityTemplate.Rx(leftHindLegPitchRad)), p.LegScale);
        new EntityCuboid(-1f, 0f, 0f, 1f, 10f, 2f, 40, 0, UvSizeW: 2, UvSizeH: 10, UvSizeD: 2).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(1.2f, 14.1f, -5f), EntityParityTemplate.Rx(rightFrontLegPitchRad)), p.LegScale);
        new EntityCuboid(-1f, 0f, 0f, 1f, 10f, 2f, 40, 0, UvSizeW: 2, UvSizeH: 10, UvSizeD: 2).Emit(b, EntityParityTemplate.Mul(EntityParityTemplate.T(-1.2f, 14.1f, -5f), EntityParityTemplate.Rx(leftFrontLegPitchRad)), p.LegScale);
    }

    private static MergedJavaBlockModel BuildBabyCat(
        string texRef,
        float headTilt,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        AppendFelineModelQuadrupedMesh(b, p, headTilt, rightHindLegPitchRad, leftHindLegPitchRad, rightFrontLegPitchRad, leftFrontLegPitchRad);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain: true);
    }

    internal static MergedJavaBlockModel BuildBabyFeline(
        string texRef,
        float headPitchRad,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20f, -3.125f)),
            EntityParityTemplate.Rx(headPitchRad));
        new EntityCuboid(-2.5f, -3f, -2.875f, 2.5f, 1f, 1.125f, 0, 0, UvSizeW: 5, UvSizeH: 4, UvSizeD: 4).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2f, -4f, -0.875f, -1f, -3f, 1.125f, 18, 0, UvSizeW: 1, UvSizeH: 1, UvSizeD: 2).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(1f, -4f, -0.875f, 2f, -3f, 1.125f, 24, 0, UvSizeW: 1, UvSizeH: 1, UvSizeD: 2).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-1.5f, -1f, -3.875f, 1.5f, 1f, -2.875f, 18, 3, UvSizeW: 3, UvSizeH: 2, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        var leftFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 22f, -1.5f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(-0.5f, 0f, -1f, 0.5f, 2f, 1f, 18, 18, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2).Emit(b, leftFrontPose, p.LegScale);

        var rightFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1f, 22f, -1.5f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(-0.5f, 0f, -1f, 0.5f, 2f, 1f, 12, 18, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2, MirrorUv: true).Emit(b, rightFrontPose, p.LegScale);

        var leftHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 22f, 2.5f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(-0.5f, 0f, -1f, 0.5f, 2f, 1f, 18, 22, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2).Emit(b, leftHindPose, p.LegScale);

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20.5f, 0.5f));
        new EntityCuboid(-2f, -1.5f, -3.5f, 2f, 1.5f, 3.5f, 0, 8, UvSizeW: 4, UvSizeH: 3, UvSizeD: 7).Emit(b, bodyPose, p.BodyScale);

        var rightHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1f, 22f, 2.5f)),
            EntityParityTemplate.Rx(rightHindLegPitchRad));
        new EntityCuboid(-0.5f, 0f, -1f, 0.5f, 2f, 1f, 12, 22, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2, MirrorUv: true).Emit(b, rightHindPose, p.LegScale);

        var tail1Pose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 19.107f, 3.9151f)),
            EntityParityTemplate.Rx(-0.567232f));
        new EntityCuboid(-0.5f, -0.107f, 0.0849f, 0.5f, 0.893f, 5.0849f, 0, 18, UvSizeW: 1, UvSizeH: 1, UvSizeD: 5).Emit(b, tail1Pose, p.BodyScale);

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

    private static MergedJavaBlockModel BuildBabyFox(
        string texRef,
        float tailLift,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(48, 32);
        var root = Matrix4x4.Identity;

        var headPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 18.125f, 0.125f));
        new EntityCuboid(-2.125f, -5.125f, 6f, 2.875f, -0.125f, 6.025f, 0, 0, UvSizeW: 5, UvSizeH: 5, UvSizeD: 1).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(0.875f, -7.125f, 2f, 2.875f, -5.125f, 2.025f, 18, 20, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-4.125f, -4.125f, 2f, -2.125f, -3.125f, 2.025f, 22, 8, UvSizeW: 2, UvSizeH: 1, UvSizeD: 1).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-4.125f, -4.125f, 2f, -2.125f, -3.125f, 2.025f, 22, 11, UvSizeW: 2, UvSizeH: 1, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        void BabyFoxLeg(Vector3 legRoot, int texU, int texV, float pitchRad)
        {
            var pose = EntityParityTemplate.Mul(
                EntityParityTemplate.Mul(root, EntityParityTemplate.T(legRoot.X, legRoot.Y, legRoot.Z)),
                EntityParityTemplate.Rx(pitchRad));
            new EntityCuboid(0f, -1f, 2f, 2f, 1f, 2.025f, texU, texV, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, pose, p.LegScale);
        }

        BabyFoxLeg(new Vector3(-1.5f, 22f, 4f), 22, 4, rightHindLegPitchRad);
        BabyFoxLeg(new Vector3(1.5f, 22f, 4f), 22, 0, leftHindLegPitchRad);
        BabyFoxLeg(new Vector3(-1.5f, 22f, 0f), 22, 4, rightFrontLegPitchRad);
        BabyFoxLeg(new Vector3(1.5f, 22f, 0f), 22, 0, leftFrontLegPitchRad);

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20f, 2f));
        new EntityCuboid(-2f, -3f, 5f, 2f, 3f, 5.025f, 0, 10, UvSizeW: 4, UvSizeH: 6, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        var tailPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, -0.5f, 3f)),
            EntityParityTemplate.Rx(ComputeFoxTailBaselinePitchRad(tailLift)));
        new EntityCuboid(-1.48f, -1f, 3f, 1.52f, 5f, 3.025f, 0, 20, UvSizeW: 3, UvSizeH: 6, UvSizeD: 1).Emit(b, tailPose, p.BodyScale);

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


    /// <summary>
    /// Preview hop offset for <see cref="BuildRabbit"/>: vanilla <c>RabbitModel.setupAnim</c> is render-state driven;
    /// this adds a bounded deterministic sine on <paramref name="animationTimeSeconds"/> so rabbit previews move over time.
    /// </summary>
    internal static float ComputePreviewRabbitHopSinTerm(float animationTimeSeconds) =>
        MathF.Sin(animationTimeSeconds * (MathF.PI * 2f * 2.15f)) * 0.18f;


    /// <summary>
    /// Fox tail baseline from <c>FoxModel.createBodyLayer</c>: part pose xRot starts at <c>-0.05235988f</c> (−3°)
    /// before any runtime setup/preview adjustments.
    /// </summary>
    internal static float ComputeFoxTailBaselinePitchRad(float tailLift) =>
        -0.05235988f + tailLift;
}
