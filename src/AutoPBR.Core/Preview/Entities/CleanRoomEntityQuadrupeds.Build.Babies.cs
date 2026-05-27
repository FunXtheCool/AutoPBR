using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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
}
