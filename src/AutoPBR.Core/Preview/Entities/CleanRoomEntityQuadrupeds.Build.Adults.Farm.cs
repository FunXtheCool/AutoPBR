using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), GeometryIrLerBasisKind.StandardWorldRoot);
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

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), GeometryIrLerBasisKind.StandardWorldRoot);
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

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), GeometryIrLerBasisKind.StandardWorldRoot);
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
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), GeometryIrLerBasisKind.StandardWorldRoot);
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
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), GeometryIrLerBasisKind.StandardWorldRoot);
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

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), GeometryIrLerBasisKind.StandardWorldRoot);
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

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), GeometryIrLerBasisKind.StandardWorldRoot);
    }
}
