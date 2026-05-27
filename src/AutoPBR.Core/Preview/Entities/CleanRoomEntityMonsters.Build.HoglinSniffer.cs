using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    /// <summary>
    /// <c>BabyHoglinModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.hoglin.BabyHoglinModel.json</c> (<c>128×64</c>).
    /// Part order matches IR DFS. Ears are parented to the head stack so preview <paramref name="headPitch"/> matches vanilla parenting.
    /// Leg <c>xRot</c> uses the same <see cref="ComputePreviewStandardQuadrupedLegPitches"/> drivers as other quadruped previews.
    /// </summary>
    private static MergedJavaBlockModel BuildBabyHoglin(
        string texRef,
        float headPitch,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(128, 64);
        const float zThin = 0.02f;

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(0f, 13f, -7f),
            EntityParityTemplate.Rx(0.87266463f + headPitch));
        new EntityCuboid(-5f, -2.2605f, -10.547f, 5f, 1.7395f, 1.453f, 0, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-7f, -4.0981f, -8.4879f, -5f, 0.9019f, -6.4879f, 44, 29).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(5f, -4.0981f, -8.4879f, 7f, 0.9019f, -6.4879f, 52, 29).Emit(b, headPose, p.HeadScale);

        var bodyPose = EntityParityTemplate.T(0f, 24f, 0f);
        new EntityCuboid(-14f, -7f, 8f - zThin * 0.5f, -6f, 7f, 8f + zThin * 0.5f, 0, 16, UvSizeW: 8, UvSizeH: 14, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-18f, -8f, -zThin * 0.5f, -12f, 3f, zThin * 0.5f, 24, 39, UvSizeW: 6, UvSizeH: 11, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        var rightEarPose = EntityParityTemplate.Mul(
            headPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(-5f, -1f, -1.5f), EntityParityTemplate.Rz(-0.87266463f)));
        new EntityCuboid(-5.1f, -0.5f, -2f, 0.9f, 0.5f, 2f, 32, 5).Emit(b, rightEarPose, p.HeadScale);

        var leftEarPose = EntityParityTemplate.Mul(
            headPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(5f, -1f, -1.5f), EntityParityTemplate.Rz(0.87266463f)));
        new EntityCuboid(-0.9f, -0.5f, -2f, 5.1f, 0.5f, 2f, 32, 0).Emit(b, leftEarPose, p.HeadScale);

        var rightHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(-2.5f, 18f, 4.5f),
            EntityParityTemplate.Rx(rightHindLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 6f, 1.5f, 0, 47).Emit(b, rightHindPose, p.LegScale);

        var leftHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(2.5f, 18f, 4.5f),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 6f, 1.5f, 12, 47).Emit(b, leftHindPose, p.LegScale);

        var rightFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(-2.5f, 18f, -4.5f),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 6f, 1.5f, 0, 38).Emit(b, rightFrontPose, p.LegScale);

        var leftFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(2.5f, 18f, -4.5f),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 6f, 1.5f, 12, 38).Emit(b, leftFrontPose, p.LegScale);

        EntityParityTemplate.AssertFinitePose(headPose, "babyHoglin.headPose");
        EntityParityTemplate.AssertFinitePose(bodyPose, "babyHoglin.bodyPose");
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>HoglinModel</c> root mesh — Java 1.21.11 <c>hen.f</c> (javap): body/maned strip, head + ears/horns, legs.
    /// </summary>
    private static MergedJavaBlockModel BuildHoglin(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        float rightHindLegPitchRad = 0f,
        float leftHindLegPitchRad = 0f,
        float rightFrontLegPitchRad = 0f,
        float leftFrontLegPitchRad = 0f)
        => BuildHoglinFamily(
            texRef,
            profile,
            isBaby,
            headPitch,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);

    /// <summary>
    /// <c>ZoglinModel</c> uses the same body-layer topology as <c>HoglinModel</c>; keep a dedicated builder entry
    /// so route integrity can be asserted separately from hoglin stems.
    /// </summary>
    private static MergedJavaBlockModel BuildZoglin(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        float rightHindLegPitchRad = 0f,
        float leftHindLegPitchRad = 0f,
        float rightFrontLegPitchRad = 0f,
        float leftFrontLegPitchRad = 0f)
        => BuildHoglinFamily(
            texRef,
            profile,
            isBaby,
            headPitch,
            rightHindLegPitchRad,
            leftHindLegPitchRad,
            rightFrontLegPitchRad,
            leftFrontLegPitchRad);

    /// <summary>
    /// Reusable hoglin-family template: geometry block, rig/setup block, setup formula block, global transform audit.
    /// </summary>
    private static MergedJavaBlockModel BuildHoglinFamily(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        float rightHindLegPitchRad = 0f,
        float leftHindLegPitchRad = 0f,
        float rightFrontLegPitchRad = 0f,
        float leftFrontLegPitchRad = 0f)
    {
        if (isBaby)
        {
            _ = profile;
            return BuildBabyHoglin(
                texRef,
                headPitch,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(128, 64);

        const float thin = 0.08f;
        var headScale = p.HeadScale;
        var bodyScale = p.BodyScale;
        var legScale = p.LegScale;

        // Geometry + rig/setup block from javap body-layer constants.
        // Body: texOffs(1,1), (-8,-7,-13)+(16,14,26), PartPose (0,7,0).
        var bodyRootTr = new Vector3(0f, 7f, 0f);
        var bodyPose = EntityParityTemplate.T(bodyRootTr.X, bodyRootTr.Y, bodyRootTr.Z);
        new EntityCuboid(-8f, -7f, -13f, 8f, 7f, 13f, 1, 1).Emit(b, bodyPose, bodyScale);

        // Mane: adult f() uses local z -7; baby e() rewrites mane local z to -3 before transformer.
        const float maneLocalZ = -7f;
        var manePose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(0f, -14f, maneLocalZ));
        new EntityCuboid(-thin, 0f, -9f, thin, 10f, 10f, 90, 33, UvSizeW: 1, UvSizeH: 10, UvSizeD: 19).Emit(b, manePose, bodyScale);

        // Setup math block: head baseline pitch formula from setupAnim.
        // Head: texOffs(61,1), (-7,-3,-19)+(14,6,19); PartPose.offsetAndRotation(0,2,-12, headPitch + 50°, 0, 0).
        var headRoot = new Vector3(0f, 2f, -12f);
        var headXRot = ComputeHoglinFamilyHeadPitchRad(headPitch, transformerScale: 1f);
        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(headRoot.X, headRoot.Y, headRoot.Z),
            EntityParityTemplate.Rx(headXRot));
        new EntityCuboid(-7f, -3f, -19f, 7f, 3f, 0f, 61, 1).Emit(b, headPose, headScale);

        // Ears: texOffs(1,1)/(1,6), each 6×1×4 at local (-6,-1,-2) and (0,-1,-2); PartPose + Rz(±40°) at (±6,-2,-3).
        var rightEarPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-6f, -2f, -3f), EntityParityTemplate.Rz(-0.6981317f)));
        var leftEarPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(6f, -2f, -3f), EntityParityTemplate.Rz(0.6981317f)));
        new EntityCuboid(-6f, -1f, -2f, 0f, 0f, 2f, 1, 1).Emit(b, rightEarPose, headScale);
        new EntityCuboid(0f, -1f, -2f, 6f, 0f, 2f, 1, 6).Emit(b, leftEarPose, headScale);

        // Horns: texOffs(10,13)/(1,13), 2×11×2 at (-1,-11,-1); PartPose (±7,2,-12) from head.
        new EntityCuboid(-1f, -11f, -1f, 1f, 0f, 1f, 10, 13).Emit(b, EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(-7f, 2f, -12f)), headScale);
        new EntityCuboid(-1f, -11f, -1f, 1f, 0f, 1f, 1, 13).Emit(b, EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(7f, 2f, -12f)), headScale);

        var rfl = new Vector3(-4f, 10f, -8.5f);
        var lfl = new Vector3(4f, 10f, -8.5f);
        var rhl = new Vector3(-5f, 13f, 10f);
        var lhl = new Vector3(5f, 13f, 10f);
        new EntityCuboid(-3f, 0f, -3f, 3f, 14f, 3f, 66, 42).Emit(b, EntityParityTemplate.T(rfl.X, rfl.Y, rfl.Z), legScale);
        new EntityCuboid(-3f, 0f, -3f, 3f, 14f, 3f, 41, 42).Emit(b, EntityParityTemplate.T(lfl.X, lfl.Y, lfl.Z), legScale);
        new EntityCuboid(-2.5f, 0f, -2.5f, 2.5f, 11f, 2.5f, 21, 45).Emit(b, EntityParityTemplate.T(rhl.X, rhl.Y, rhl.Z), legScale);
        new EntityCuboid(-2.5f, 0f, -2.5f, 2.5f, 11f, 2.5f, 0, 45).Emit(b, EntityParityTemplate.T(lhl.X, lhl.Y, lhl.Z), legScale);

        // Global transform audit block: guard against NaN/Inf matrices before bake.
        EntityParityTemplate.AssertFinitePose(bodyPose, "hoglin.bodyPose");
        EntityParityTemplate.AssertFinitePose(headPose, "hoglin.headPose");
        EntityParityTemplate.AssertFinitePose(manePose, "hoglin.manePose");
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// HoglinModel baby transformer (<c>hen.b</c> = <c>gzl(true, 8, 6, 1.9, 2, 24, Set.of("head"))</c>).
    /// </summary>
    private static class BabyHoglinMeshTransformer
    {
        public const float HeadGeometryScale = 1.5f / 1.9f;

        public const float BodyGeometryScale = 0.5f;

        public static Vector3 TransformHeadRootTranslation(float x, float y, float z) =>
            new((x + 0f) * HeadGeometryScale, (y + 8f) * HeadGeometryScale, (z + 6f) * HeadGeometryScale);

        public static Vector3 TransformBodyRootTranslation(float x, float y, float z) =>
            new((x + 0f) * BodyGeometryScale, (y + 24f) * BodyGeometryScale, (z + 0f) * BodyGeometryScale);
    }

    /// <summary>
    /// <c>SnifferModel.createBodyLayer</c> (26.1.2 javap): <c>bone</c> at <c>T(0,5,0)</c>; <c>body</c> + six legs are <b>siblings</b> under <c>bone</c> with leg offsets in bone space;
    /// <c>head</c> is a child of <c>body</c> at <c>T(0,6.5,-19.48)</c> with main slab, zero-height rim, ears, nose, lower beak. Adult / legacy baby shell; 26.1+ baby uses <see cref="BuildSnifflet"/>.
    /// <c>SNIFFER_WALK</c> <c>left_mid_leg</c> rotation from IR (when present) drives the left mid leg via <see cref="EntityParityTemplate.Er"/>; right mid has no rotation channel in the shipped walk IR.
    /// </summary>
    private static MergedJavaBlockModel BuildSniffer(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitch,
        float walkRightFrontLegPitchRad = 0f,
        float walkLeftFrontLegPitchRad = 0f,
        Vector3 walkLeftMidLegEulerRad = default)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildSnifflet(
                texRef,
                headPitch,
                walkRightFrontLegPitchRad,
                walkLeftFrontLegPitchRad,
                walkLeftMidLegEulerRad);
        }

        var p = !UsesPostBabyModelUpdate(profile) && isBaby
            ? new BabyProfile(0.74f, 1.0f, 0.76f)
            : BabyProfile.Adult;
        var b = new RigBuilder(192, 192);
        // SnifferModel.createBodyLayer (~1.21.4): giant shell body + six legs + articulated head/beak set.
        var rootPose = Matrix4x4.CreateTranslation(0f, 5f, 0f); // "bone" root
        var bodyPose = Matrix4x4.Multiply(rootPose, Matrix4x4.Identity);
        new EntityCuboid(-12.5f, -14f, -20f, 12.5f, 15f, 20f, 62, 68).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-12.5f, -14f, -20f, 12.5f, 10f, 20f, 62, 0).Emit(b, bodyPose, p.BodyScale); // inner shell
        // SnifferModel: texOffs(87,68).addBox(-12.5,12,-20, 25,0,40) — zero height; UV footprint still 25×1×40 texels.
        new EntityCuboid(-12.5f, 12f, -20f, 12.5f, 12f, 20f, 87, 68, UvSizeW: 25, UvSizeH: 1, UvSizeD: 40).Emit(b, bodyPose, p.BodyScale);

        static Matrix4x4 LegOnBone(Matrix4x4 bone, float tx, float ty, float tz, float pitchRad) =>
            Matrix4x4.Multiply(bone, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(tx, ty, tz), Matrix4x4.CreateRotationX(pitchRad)));

        static Matrix4x4 LegMidOnBone(Matrix4x4 bone, float tx, float ty, float tz, Vector3 eulerRad) =>
            eulerRad.X == 0f && eulerRad.Y == 0f && eulerRad.Z == 0f
                ? LegOnBone(bone, tx, ty, tz, 0f)
                : EntityParityTemplate.Mul(
                    bone,
                    EntityParityTemplate.Mul(EntityParityTemplate.T(tx, ty, tz), EntityParityTemplate.Er(eulerRad.X, eulerRad.Y, eulerRad.Z)));

        var sniffRfLegPose = LegOnBone(rootPose, -7.5f, 10f, -15f, walkRightFrontLegPitchRad);
        new EntityCuboid(-3.5f, -1f, -4f, 3.5f, 9f, 4f, 32, 87).Emit(b, sniffRfLegPose, p.LegScale); // right front
        new EntityCuboid(-3.5f, -1f, -4f, 3.5f, 9f, 4f, 32, 105).Emit(b, LegOnBone(rootPose, -7.5f, 10f, 0f, 0f), p.LegScale); // right mid (no ROTATION channel on SNIFFER_WALK in 26.1.2 IR)
        new EntityCuboid(-3.5f, -1f, -4f, 3.5f, 9f, 4f, 32, 123).Emit(b, LegOnBone(rootPose, -7.5f, 10f, 15f, 0f), p.LegScale); // right hind
        var sniffLfLegPose = LegOnBone(rootPose, 7.5f, 10f, -15f, walkLeftFrontLegPitchRad);
        new EntityCuboid(-3.5f, -1f, -4f, 3.5f, 9f, 4f, 0, 87).Emit(b, sniffLfLegPose, p.LegScale); // left front
        new EntityCuboid(-3.5f, -1f, -4f, 3.5f, 9f, 4f, 0, 105).Emit(b, LegMidOnBone(rootPose, 7.5f, 10f, 0f, walkLeftMidLegEulerRad), p.LegScale); // left mid
        new EntityCuboid(-3.5f, -1f, -4f, 3.5f, 9f, 4f, 0, 123).Emit(b, LegOnBone(rootPose, 7.5f, 10f, 15f, 0f), p.LegScale); // left hind

        var headPose = Matrix4x4.Multiply(bodyPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 6.5f, -19.48f), Matrix4x4.CreateRotationX(headPitch)));
        new EntityCuboid(-6.5f, -7.5f, -11.5f, 6.5f, 10.5f, -0.5f, 8, 15).Emit(b, headPose, p.HeadScale);
        // texOffs(8,4).addBox(-6.5,7.5,-11.5, 13,0,11)
        new EntityCuboid(-6.5f, 7.5f, -11.5f, 6.5f, 7.5f, -0.5f, 8, 4, UvSizeW: 13, UvSizeH: 1, UvSizeD: 11).Emit(b, headPose, p.HeadScale);

        new EntityCuboid(0f, 0f, -3f, 1f, 19f, 4f, 2, 0).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.CreateTranslation(6.51f, -7.5f, -4.51f)), p.HeadScale); // left ear
        new EntityCuboid(-1f, 0f, -3f, 0f, 19f, 4f, 48, 0).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.CreateTranslation(-6.51f, -7.5f, -4.51f)), p.HeadScale); // right ear
        new EntityCuboid(-6.5f, -2f, -9f, 6.5f, 0f, 0f, 10, 45).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.CreateTranslation(0f, -4.5f, -11.5f)), p.HeadScale); // nose
        new EntityCuboid(-6.5f, -7f, -8f, 6.5f, 5f, 1f, 10, 57).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.CreateTranslation(0f, 2.5f, -12.5f)), p.HeadScale); // lower beak
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>SniffletModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.sniffer.SniffletModel.json</c> (<c>192×192</c>):
    /// baby sniffer / <c>snifflet.png</c> on 26.1+ (<see cref="UsesPostBabyModelUpdate"/>). Part order matches IR DFS under <c>root</c>.
    /// <paramref name="headPitch"/> reuses the sniffer parity driver (idle + <c>SNIFFER_LONGSNIFF</c> + scaled <c>SNIFFER_WALK</c> body pitch),
    /// applied as <c>xRot</c> after each head-group part pose (<c>head</c>, ears, nose, lower beak).
    /// <paramref name="walkRightFrontLegPitchRad"/> / <paramref name="walkLeftFrontLegPitchRad"/> apply <c>SNIFFER_WALK</c> front-leg <c>xRot</c> from IR when sampled.
    /// <paramref name="walkLeftMidLegEulerRad"/> applies <c>SNIFFER_WALK</c> <c>left_mid_leg</c> <c>ROTATION</c> (vanilla <c>Er</c> order) from IR when sampled; right mid has no walk rotation in shipped IR.
    /// </summary>
    private static MergedJavaBlockModel BuildSnifflet(
        string texRef,
        float headPitch,
        float walkRightFrontLegPitchRad = 0f,
        float walkLeftFrontLegPitchRad = 0f,
        Vector3 walkLeftMidLegEulerRad = default)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(192, 192);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(6f, -3f, -9.5f));
        new EntityCuboid(-14f, -0.5f, 14f, 0f, 19.5f, 14.25f, 0, 35, UvSizeW: 14, UvSizeH: 20, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-13f, -14f, -0.5f, 1f, 1f, 19.5f, 0, 0).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-13f, 0f, -0.5f, 1f, 0f, 19.5f, 68, 0, UvSizeW: 14, UvSizeH: 1, UvSizeD: 20).Emit(b, bodyPose, p.BodyScale);

        static Matrix4x4 HeadGroupPart(Matrix4x4 rootPose, float tx, float ty, float tz, float xRot)
        {
            return EntityParityTemplate.Mul(
                EntityParityTemplate.Mul(rootPose, EntityParityTemplate.T(tx, ty, tz)),
                EntityParityTemplate.Rx(xRot));
        }

        var headPose = HeadGroupPart(root, -6f, -4.75f, 0f, headPitch);
        new EntityCuboid(-5f, -4.25f, -7.5f, 5f, 4.75f, 1.5f, 68, 20).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-5f, 3.75f, -7.5f, 5f, 3.75f, 1.5f, 88, 20, UvSizeW: 10, UvSizeH: 1, UvSizeD: 9).Emit(b, headPose, p.HeadScale);

        var leftEarPose = HeadGroupPart(root, 5f, -4.25f, -1.5f, headPitch);
        new EntityCuboid(0f, 0f, -2f, 1f, 11f, 1f, 104, 38).Emit(b, leftEarPose, p.HeadScale);

        var rightEarPose = HeadGroupPart(root, -5f, -4.25f, -1.5f, headPitch);
        new EntityCuboid(-1f, 0f, -2f, 0f, 11f, 1f, 96, 38).Emit(b, rightEarPose, p.HeadScale);

        var nosePose = HeadGroupPart(root, 0f, -1.25f, -9.5f, headPitch);
        new EntityCuboid(-5f, -3f, -2f, 5f, 0f, 2f, 68, 47).Emit(b, nosePose, p.HeadScale);

        var lowerBeakPose = HeadGroupPart(root, 0f, 1.25f, -9.5f, headPitch);
        new EntityCuboid(-5f, -2.5f, -2f, 5f, 2.5f, 2f, 68, 38).Emit(b, lowerBeakPose, p.HeadScale);

        var rfLeg = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-4f, -4f, -7f)),
            EntityParityTemplate.Rx(walkRightFrontLegPitchRad));
        new EntityCuboid(-2f, -1f, -2f, 2f, 4f, 2f, 0, 69).Emit(b, rfLeg, p.LegScale);
        var rmLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-4f, -4f, 0f));
        new EntityCuboid(-2f, -1f, -2f, 2f, 4f, 2f, 0, 78).Emit(b, rmLeg, p.LegScale);
        var rhLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-4f, -4f, 7f));
        new EntityCuboid(-2f, -1f, -2f, 2f, 4f, 2f, 0, 87).Emit(b, rhLeg, p.LegScale);
        var lfLeg = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(4f, -4f, -7f)),
            EntityParityTemplate.Rx(walkLeftFrontLegPitchRad));
        new EntityCuboid(-2f, -1f, -2f, 2f, 4f, 2f, 16, 69).Emit(b, lfLeg, p.LegScale);
        var lmLeg = walkLeftMidLegEulerRad.X == 0f && walkLeftMidLegEulerRad.Y == 0f && walkLeftMidLegEulerRad.Z == 0f
            ? EntityParityTemplate.Mul(root, EntityParityTemplate.T(4f, -4f, 0f))
            : EntityParityTemplate.Mul(
                EntityParityTemplate.Mul(root, EntityParityTemplate.T(4f, -4f, 0f)),
                EntityParityTemplate.Er(walkLeftMidLegEulerRad.X, walkLeftMidLegEulerRad.Y, walkLeftMidLegEulerRad.Z));
        new EntityCuboid(-2f, -1f, -2f, 2f, 4f, 2f, 16, 78).Emit(b, lmLeg, p.LegScale);
        var lhLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(4f, -4f, 7f));
        new EntityCuboid(-2f, -1f, -2f, 2f, 4f, 2f, 16, 87).Emit(b, lhLeg, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    /// <summary>
    /// <c>BabyArmadilloModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.armadillo.BabyArmadilloModel.json</c> (<c>64×64</c>).
    /// Body shell, legs, and roll plate use IR root poses. The IR lists <c>head_cube</c> / <c>right_ear</c> / <c>left_ear</c> at the model root;
    /// preview parents those under <c>body</c> with the same neck anchor as <see cref="BuildArmadillo"/> (<c>T(0,−2,−11)</c>) so the head stays on the torso (javap flattening would otherwise park the head at the origin).
    /// <paramref name="tailWalkPitchRad"/> is ignored — baby class has no tail cuboid.

}
