using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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
        // Bind uses ModelPart.translateAndRotate (T then R block), not PartPose Er×T storage.
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
}
