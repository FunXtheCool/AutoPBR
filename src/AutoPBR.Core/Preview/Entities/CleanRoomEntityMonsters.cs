using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Hostile/nether mobs, golems, strider, dragon, creaking.

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
    /// </summary>
    private static MergedJavaBlockModel BuildBabyArmadillo(string texRef, float headPitch, float tailWalkPitchRad)
    {
        _ = tailWalkPitchRad;
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20f, 0.5f));
        new EntityCuboid(-2f, -3.5f, 5f - 0.01f, 2f, 3.5f, 5.3f + 0.01f, 0, 0, UvSizeW: 4, UvSizeH: 7, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-2.5f, -2f, -3f, 2.5f, 2f, 3f, 0, 11, UvSizeW: 5, UvSizeH: 4, UvSizeD: 6).Emit(b, bodyPose, p.BodyScale);

        var rightEarCubePose = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 1.5f, 1f), EntityParityTemplate.Er(-1.0472f, 0f, 0f)));
        new EntityCuboid(-0.5f, -0.5f, -2f, 0.5f, 0.5f, 2f, 22, 11, UvSizeW: 1, UvSizeH: 1, UvSizeD: 4).Emit(b, rightEarCubePose, p.HeadScale);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(0f, -2f, -11f)),
            EntityParityTemplate.Mul(EntityParityTemplate.Er(0.7417649f, 0f, 0f), EntityParityTemplate.Rx(headPitch)));
        new EntityCuboid(-1f, -2f, -4f, 1f, 0f, 0f, 20, 17, UvSizeW: 2, UvSizeH: 2, UvSizeD: 4).Emit(b, headPose, p.HeadScale);

        const float earZ = 0.06f;
        var rightEarPose = EntityParityTemplate.Mul(
            headPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(-1f, -2f, -0.3f), EntityParityTemplate.Er(-0.4363f, -0.1134f, 0.0524f)));
        new EntityCuboid(-1.8f, -2f, -earZ, 0.2f, 1f, earZ, 28, 8, UvSizeW: 2, UvSizeH: 3, UvSizeD: 1).Emit(b, rightEarPose, p.HeadScale);

        var leftEarPose = EntityParityTemplate.Mul(
            headPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(1f, -2f, -0.3f), EntityParityTemplate.Er(-0.4363f, 0.1134f, -0.0524f)));
        new EntityCuboid(-0.2f, -2f, -earZ, 1.8f, 1f, earZ, 28, 8, UvSizeW: 2, UvSizeH: 3, UvSizeD: 1, MirrorUv: true).Emit(b, leftEarPose, p.HeadScale);

        var rhLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1.5f, 22f, 2.5f));
        new EntityCuboid(-1f, 0f, -1f, 1f, 2f, 1f, 20, 27, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2).Emit(b, rhLeg, p.LegScale);
        var lhLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(1.5f, 22f, 2.5f));
        new EntityCuboid(-1f, 0f, -1f, 1f, 2f, 1f, 20, 27, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2, MirrorUv: true).Emit(b, lhLeg, p.LegScale);
        var rfLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(1.5f, 22f, -1.5f));
        new EntityCuboid(-1f, 0f, -1f, 1f, 2f, 1f, 20, 23, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2).Emit(b, rfLeg, p.LegScale);
        var lfLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1.5f, 22f, -1.5f));
        new EntityCuboid(-1f, 0f, -1f, 1f, 2f, 1f, 24, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2, MirrorUv: true).Emit(b, lfLeg, p.LegScale);

        var rollPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20.7f, 0.5f));
        new EntityCuboid(-3f, -3f, 6f, 3f, 3f, 6.3f, 0, 25, UvSizeW: 6, UvSizeH: 6, UvSizeD: 1).Emit(b, rollPose, p.BodyScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildArmadillo(string texRef, MinecraftNativeProfile profile, bool isBaby, float headPitch, float tailWalkPitchRad = 0f)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildBabyArmadillo(texRef, headPitch, tailWalkPitchRad);
        }

        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.72f, 1.10f, 0.74f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.04f, 0.82f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 64);
        // ArmadilloModel.createBodyLayer (~1.21.4): body 8x8x12, tiny tail, compact head/ears, plus roll-up cube part.
        var bodyPose = Matrix4x4.CreateTranslation(0f, 21f, 4f);
        new EntityCuboid(-4f, -7f, -10f, 4f, 1f, 2f, 0, 20).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-4f, -7f, -10f, 4f, 1f, 2f, 0, 40).Emit(b, bodyPose, p.BodyScale);

        var tailPitch = 0.5061f + tailWalkPitchRad;
        new EntityCuboid(-0.5f, -0.0865f, 0.0933f, 0.5f, 5.9135f, 1.0933f, 44, 53).Emit(b, Matrix4x4.Multiply(bodyPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, -3f, 1f), Matrix4x4.CreateRotationX(tailPitch))), p.BodyScale); // tail

        var headPose = Matrix4x4.Multiply(
            Matrix4x4.Multiply(bodyPose, Matrix4x4.CreateTranslation(0f, -2f, -11f)),
            Matrix4x4.CreateRotationX(headPitch));
        new EntityCuboid(-1.5f, -1f, -1f, 1.5f, 4f, 1f, 43, 15).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.CreateRotationX(-0.3927f)), p.HeadScale); // head cube
        new EntityCuboid(-2f, -3f, 0f, 0f, 2f, 1f, 43, 10).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-1f, -1f, 0f), Matrix4x4.Multiply(Matrix4x4.CreateRotationX(0.1886f), Matrix4x4.CreateRotationY(-0.3864f)))), p.HeadScale);
        new EntityCuboid(0f, -3f, 0f, 2f, 2f, 1f, 47, 10).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(1f, -2f, 0f), Matrix4x4.Multiply(Matrix4x4.CreateRotationX(0.1886f), Matrix4x4.CreateRotationY(0.3864f)))), p.HeadScale);

        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 51, 31).Emit(b, Matrix4x4.CreateTranslation(-2f, 21f, 4f), p.LegScale);
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 42, 31).Emit(b, Matrix4x4.CreateTranslation(2f, 21f, 4f), p.LegScale);
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 51, 43).Emit(b, Matrix4x4.CreateTranslation(-2f, 21f, -4f), p.LegScale);
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 42, 43).Emit(b, Matrix4x4.CreateTranslation(2f, 21f, -4f), p.LegScale);

        // Roll-up cube used by animation state.
        new EntityCuboid(-5f, -10f, -6f, 5f, 0f, 4f, 0, 0).Emit(b, Matrix4x4.CreateTranslation(0f, 24f, 0f), p.BodyScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BreezeModel</c> (javap <c>gbm</c> 1.21.4; named <c>BreezeModel</c> 26.1.2): vanilla splits layers — <c>createBodyLayer</c> is <c>32×32</c> and retains only <c>head</c> + <c>rods</c>;
    /// <c>createWindLayer</c> is <c>128×128</c> and retains <c>wind_body</c> → <c>wind_bottom</c> → <c>wind_mid</c> → <c>wind_top</c> (<c>wind_top</c> child pose <c>T(0,−6,0)</c> under <c>wind_mid</c>, not −7).
    /// <c>createEyesLayer</c> is <c>32×32</c>. Preview merges body + <c>#wind</c> (sibling <c>breeze_wind</c>) + <c>#eyes</c>; <c>breeze_wind.png</c> / <c>breeze_eyes.png</c> paths build those layers alone.
    /// Wind tiers sample vanilla idle <c>BreezeAnimation.IDLE</c> <c>wind_mid</c>/<c>wind_top</c> POSITION keyframes from shipped IR
    /// (<see cref="DefinitionAnimationPreviewSampling"/>) from lifted <c>BreezeAnimation.IDLE</c> when present.
    /// Head pitch can add <c>BreezeAnimation.SHOOT</c> <c>head</c> ROTATION X from IR when present; <c>head</c> POSITION from the same clip adds on the head pivot.
    /// </summary>
    private static MergedJavaBlockModel BuildBreeze(
        string normalizedAssetPath,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float swirl,
        float windAnimTimeSeconds,
        float shootHeadAdditivePitchRad,
        Vector3 shootHeadAdditiveTranslate = default)
    {
        _ = isBaby;
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        var isEyesTexture = norm.Contains("breeze_eyes", StringComparison.OrdinalIgnoreCase);
        var isWindTexture = norm.Contains("breeze_wind", StringComparison.OrdinalIgnoreCase);
        var eyesRef = CompanionDiffuseTextureRefFromSiblingFileStem(norm, "breeze_eyes");
        var windRef = CompanionDiffuseTextureRefFromSiblingFileStem(norm, "breeze_wind");

        if (isEyesTexture)
        {
            var bEyes = new RigBuilder(32, 32);
            var eyesOnlyHeadPivot = EntityParityTemplate.Mul(
                EntityParityTemplate.T(0f, 4f, 0f),
                EntityParityTemplate.Mul(
                    EntityParityTemplate.T(shootHeadAdditiveTranslate.X, shootHeadAdditiveTranslate.Y, shootHeadAdditiveTranslate.Z),
                    EntityParityTemplate.Rx(shootHeadAdditivePitchRad)));
            new EntityCuboid(-5f, -5f, -4.2f, 5f, -2f, -0.2f, 4, 24).Emit(bEyes, eyesOnlyHeadPivot, 1f);
            new EntityCuboid(-4f, -8f, -4f, 4f, 0f, 4f, 0, 0).Emit(bEyes, eyesOnlyHeadPivot, 1f);
            return ApplyLivingEntityRendererPreviewBasis(bEyes.Build(texRef));
        }

        if (isWindTexture)
        {
            var windOnly = new RigBuilder(128, 128);
            AppendBreezeWindCuboids(windOnly, profile, windAnimTimeSeconds, "#skin");
            return ApplyLivingEntityRendererPreviewBasis(windOnly.Build(texRef));
        }

        var body = new RigBuilder(32, 32);
        var rodsRoot = EntityParityTemplate.T(0f, 8f, 0f);
        var rod1Pose = EntityParityTemplate.Mul(
            rodsRoot,
            EntityParityTemplate.Mul(
                EntityParityTemplate.T(2.5981f, -3f, 1.5f),
                EntityParityTemplate.Er(-2.7489f + swirl, -1.0472f, MathF.PI)));
        new EntityCuboid(-1f, 0f, -3f, 1f, 8f, -1f, 0, 17).Emit(body, rod1Pose, 1f);

        var rod2Pose = EntityParityTemplate.Mul(
            rodsRoot,
            EntityParityTemplate.Mul(
                EntityParityTemplate.T(-2.5981f, -3f, 1.5f),
                EntityParityTemplate.Er(-2.7489f - swirl, 1.0472f, MathF.PI)));
        new EntityCuboid(-1f, 0f, -3f, 1f, 8f, -1f, 0, 17).Emit(body, rod2Pose, 1f);

        var rod3Pose = EntityParityTemplate.Mul(
            rodsRoot,
            EntityParityTemplate.Mul(
                EntityParityTemplate.T(0f, -3f, -3f),
                EntityParityTemplate.Er(0.3927f, 0f, 0f)));
        new EntityCuboid(-1f, 0f, -3f, 1f, 8f, -1f, 0, 17).Emit(body, rod3Pose, 1f);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(0f, 4f, 0f),
            EntityParityTemplate.Mul(
                EntityParityTemplate.T(shootHeadAdditiveTranslate.X, shootHeadAdditiveTranslate.Y, shootHeadAdditiveTranslate.Z),
                EntityParityTemplate.Rx(shootHeadAdditivePitchRad)));
        new EntityCuboid(-5f, -5f, -4.2f, 5f, -2f, -0.2f, 4, 24).Emit(body, headPose, 1f);
        new EntityCuboid(-4f, -8f, -4f, 4f, 0f, 4f, 0, 0).Emit(body, headPose, 1f);
        new EntityCuboid(-5f, -5f, -4.2f, 5f, -2f, -0.2f, 4, 24).Emit(body, headPose, 1f, "#eyes");
        new EntityCuboid(-4f, -8f, -4f, 4f, 0f, 4f, 0, 0).Emit(body, headPose, 1f, "#eyes");

        var bodyModel = body.Build(texRef, new Dictionary<string, string>(StringComparer.Ordinal) { ["eyes"] = eyesRef });
        var windModel = BuildBreezeWindCompositeForMainDiffuse(texRef, windRef, profile, windAnimTimeSeconds);
        return ApplyLivingEntityRendererPreviewBasis(MergeEntityPreviewMeshes(bodyModel, windModel));
    }


    private static MergedJavaBlockModel MergeEntityPreviewMeshes(MergedJavaBlockModel a, MergedJavaBlockModel b)
    {
        var elements = new List<ModelElement>(a.Elements.Count + b.Elements.Count);
        elements.AddRange(a.Elements);
        elements.AddRange(b.Elements);
        var textures = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in a.Textures)
        {
            textures[kv.Key] = kv.Value;
        }

        foreach (var kv in b.Textures)
        {
            textures[kv.Key] = kv.Value;
        }

        return new MergedJavaBlockModel
        {
            Elements = elements,
            Textures = textures,
        };
    }


    private static MergedJavaBlockModel BuildBreezeWindCompositeForMainDiffuse(
        string breezeDiffuseTexRef,
        string windTextureRef,
        MinecraftNativeProfile profile,
        float windAnimTimeSeconds)
    {
        var b = new RigBuilder(128, 128);
        AppendBreezeWindCuboids(b, profile, windAnimTimeSeconds, "#wind");
        return b.Build(breezeDiffuseTexRef, new Dictionary<string, string>(StringComparer.Ordinal) { ["wind"] = windTextureRef });
    }


    private static void AppendBreezeWindCuboids(RigBuilder b, MinecraftNativeProfile profile, float windAnimTimeSeconds, string windTexKey)
    {
        var windBodyPose = Matrix4x4.Identity;
        var windBottom = EntityParityTemplate.Mul(windBodyPose, EntityParityTemplate.T(0f, 24f, 0f));
        new EntityCuboid(-2.5f, -7f, -2.5f, 2.5f, 0f, 2.5f, 1, 83).Emit(b, windBottom, 1f, windTexKey);
        Matrix4x4 windMid;
        Matrix4x4 windTop;
        if (DefinitionAnimationPreviewSampling.SamplePosition(
                profile,
                "net.minecraft.client.animation.definitions.BreezeAnimation",
                "IDLE",
                "wind_mid",
                windAnimTimeSeconds,
                out var trMid) &&
            DefinitionAnimationPreviewSampling.SamplePosition(
                profile,
                "net.minecraft.client.animation.definitions.BreezeAnimation",
                "IDLE",
                "wind_top",
                windAnimTimeSeconds,
                out var trTop))
        {
            windMid = EntityParityTemplate.Mul(
                windBottom,
                EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -7f, 0f), EntityParityTemplate.T(trMid.X, trMid.Y, trMid.Z)));
            new EntityCuboid(-6f, -6f, -6f, 6f, 0f, 6f, 74, 28).Emit(b, windMid, 1f, windTexKey);
            new EntityCuboid(-4f, -6f, -4f, 4f, 0f, 4f, 78, 32).Emit(b, windMid, 1f, windTexKey);
            new EntityCuboid(-2.5f, -6f, -2.5f, 2.5f, 0f, 2.5f, 49, 71).Emit(b, windMid, 1f, windTexKey);
            windTop = EntityParityTemplate.Mul(
                windMid,
                EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -6f, 0f), EntityParityTemplate.T(trTop.X, trTop.Y, trTop.Z)));
        }
        else
        {
            windMid = windBottom;
            windTop = windMid;
        }

        new EntityCuboid(-9f, -8f, -9f, 9f, 0f, 9f, 0, 0).Emit(b, windTop, 1f, windTexKey);
        new EntityCuboid(-6f, -8f, -6f, 6f, 0f, 6f, 6, 6).Emit(b, windTop, 1f, windTexKey);
        new EntityCuboid(-2.5f, -8f, -2.5f, 2.5f, 0f, 2.5f, 105, 57).Emit(b, windTop, 1f, windTexKey);
    }

    /// <summary>
    /// Vanilla <c>SlimeModel</c> (26.1.2 <c>client.jar</c>): <c>createOuterBodyLayer</c> + <c>createInnerBodyLayer</c> on one <c>64×32</c> sheet.
    /// No <c>setupAnim</c> on this class — squish / size live in renderer state, not these cuboids.
    /// </summary>
    private static MergedJavaBlockModel BuildSlime(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        // createOuterBodyLayer — child "cube"
        new EntityCuboid(-4f, 16f, -4f, 4f, 24f, 4f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        // createInnerBodyLayer — "cube", "right_eye", "left_eye", "mouth"
        new EntityCuboid(-3f, 17f, -3f, 3f, 23f, 3f, 0, 16).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-3.25f, 18f, -3.5f, -1.25f, 20f, -1.5f, 32, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(1.25f, 18f, -3.5f, 3.25f, 20f, -1.5f, 32, 4).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(0f, 21f, -3.5f, 1f, 22f, -2.5f, 32, 8).Emit(b, Matrix4x4.Identity, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// Vanilla <c>MagmaCubeModel</c> (26.1.2 <c>client.jar</c>): eight <c>addBox(-4,16+i,-4,8,1,8)</c> with UV from <c>createBodyLayer</c>;
    /// <c>setupAnim</c> sets each body cube part <c>y = (i - 4) * max(0,squish) * 1.7</c> (<c>SlimeRenderState.squish</c>).
    /// </summary>
    private static MergedJavaBlockModel BuildMagmaCube(string texRef, MinecraftNativeProfile profile, bool isBaby, float squish)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);
        // createBodyLayer texOffs / v stride — cumulative for i=1..3, then u=32 with same v recurrence for i>=4.
        ReadOnlySpan<(int U, int V)> segTex =
        [
            (0, 0), (0, 9), (0, 27), (0, 54), (32, 54), (32, 63), (32, 81), (32, 108)
        ];
        var s = MathF.Max(0f, squish);
        for (var i = 0; i < 8; i++)
        {
            var y0 = 16f + i + (i - 4) * s * 1.7f;
            var (u, v) = segTex[i];
            new EntityCuboid(-4f, y0, -4f, 4f, y0 + 1f, 4f, u, v).Emit(b, Matrix4x4.Identity, 1f);
        }

        new EntityCuboid(-2f, 18f, -2f, 2f, 22f, 2f, 24, 40).Emit(b, Matrix4x4.Identity, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// Vanilla <c>SilverfishModel.setupAnim</c> (26.1.2): same phase as endermite segments, but
    /// <c>yRot = cos(phase) * π * 0.05 * (1 + |i-2|)</c>, <c>x = sin(phase) * π * 0.2 * |i-2|</c>; wing layers copy <c>yRot</c>/<c>x</c> from body parts 2, 4, 1.
    /// </summary>
    private static void SilverfishSegmentAnim(int segmentIndex, float ageInTicks, out float yRot, out float xOff)
    {
        var phase = ageInTicks * 0.9f + segmentIndex * 0.15f * MathF.PI;
        yRot = MathF.Cos(phase) * MathF.PI * 0.05f * (1f + MathF.Abs(segmentIndex - 2));
        xOff = MathF.Sin(phase) * MathF.PI * 0.2f * MathF.Abs(segmentIndex - 2);
    }


    private static Matrix4x4 SilverfishSegmentPose(float height, float zTrack, float yRot, float xOff) =>
        Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(0f, 24f - height, zTrack),
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(xOff, 0f, 0f), Matrix4x4.CreateRotationY(yRot)));


    private static MergedJavaBlockModel BuildSilverfish(string texRef, MinecraftNativeProfile profile, bool isBaby, float ageInTicks)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        // SilverfishModel BODY_SIZES / BODY_TEXS — Java 26.1.2 client.jar static init + createBodyLayer.
        (float X, float Y, float Z)[] seg =
        [
            (3f, 2f, 2f), (4f, 3f, 2f), (6f, 4f, 3f), (3f, 3f, 3f), (2f, 2f, 3f), (2f, 1f, 2f), (1f, 1f, 2f)
        ];
        (int U, int V)[] segTex = [(0, 0), (0, 4), (0, 9), (0, 16), (0, 22), (11, 0), (13, 4)];
        var zTrack = new float[seg.Length];
        var z = -3.5f;
        for (var i = 0; i < seg.Length; i++)
        {
            zTrack[i] = z;
            var s = seg[i];
            SilverfishSegmentAnim(i, ageInTicks, out var yRot, out var xOff);
            var pose = SilverfishSegmentPose(s.Y, z, yRot, xOff);
            new EntityCuboid(-s.X * 0.5f, 0f, -s.Z * 0.5f, s.X * 0.5f, s.Y, s.Z * 0.5f, segTex[i].U, segTex[i].V).Emit(b, pose, 1f);
            if (i < seg.Length - 1)
            {
                z += (s.Z + seg[i + 1].Z) * 0.5f;
            }
        }

        SilverfishSegmentAnim(2, ageInTicks, out var yL0, out var xL0);
        var poseW0 = Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(0f, 16f, zTrack[2]),
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(xL0, 0f, 0f), Matrix4x4.CreateRotationY(yL0)));
        new EntityCuboid(-5f, 0f, -seg[2].Z * 0.5f, 5f, 8f, seg[2].Z * 0.5f, 20, 0).Emit(b, poseW0, 1f);

        SilverfishSegmentAnim(4, ageInTicks, out var yL1, out var xL1);
        var poseW1 = Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(0f, 20f, zTrack[4]),
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(xL1, 0f, 0f), Matrix4x4.CreateRotationY(yL1)));
        new EntityCuboid(-3f, 0f, -seg[4].Z * 0.5f, 3f, 4f, seg[4].Z * 0.5f, 20, 11).Emit(b, poseW1, 1f);

        SilverfishSegmentAnim(1, ageInTicks, out var yL2, out var xL2);
        var zW2Min = -seg[4].Z * 0.5f;
        var zW2Max = zW2Min + seg[1].Z;
        var poseW2 = Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(0f, 19f, zTrack[1]),
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(xL2, 0f, 0f), Matrix4x4.CreateRotationY(yL2)));
        new EntityCuboid(-3f, 0f, zW2Min, 3f, 5f, zW2Max, 20, 18).Emit(b, poseW2, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }



    /// <summary>
    /// Vanilla <c>EndermiteModel.setupAnim</c> (26.1.2): <c>yRot = cos(phase) * π * 0.01 * (1 + |i-2|)</c>, <c>x = sin(phase) * π * 0.1 * |i-2|</c> with <c>phase = ageInTicks*0.9 + i*0.15*π</c>.
    /// </summary>
    private static void EndermiteSegmentAnim(int segmentIndex, float ageInTicks, out float yRot, out float xOff)
    {
        var phase = ageInTicks * 0.9f + segmentIndex * 0.15f * MathF.PI;
        yRot = MathF.Cos(phase) * MathF.PI * 0.01f * (1f + MathF.Abs(segmentIndex - 2));
        xOff = MathF.Sin(phase) * MathF.PI * 0.1f * MathF.Abs(segmentIndex - 2);
    }


    private static Matrix4x4 EndermiteSegmentPose(float height, float zTrack, float yRot, float xOff) =>
        Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(0f, 24f - height, zTrack),
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(xOff, 0f, 0f), Matrix4x4.CreateRotationY(yRot)));


    private static MergedJavaBlockModel BuildEndermite(string texRef, MinecraftNativeProfile profile, bool isBaby, float ageInTicks)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        (float X, float Y, float Z)[] parts = [(4f, 3f, 2f), (6f, 4f, 5f), (3f, 3f, 1f), (1f, 2f, 1f)];
        (int U, int V)[] uv = [(0, 0), (0, 5), (0, 14), (0, 18)];
        var z = -3.5f;
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            EndermiteSegmentAnim(i, ageInTicks, out var yRot, out var xOff);
            var pose = EndermiteSegmentPose(p.Y, z, yRot, xOff);
            new EntityCuboid(-p.X * 0.5f, 0f, -p.Z * 0.5f, p.X * 0.5f, p.Y, p.Z * 0.5f, uv[i].U, uv[i].V).Emit(b, pose, 1f);
            if (i < parts.Length - 1)
            {
                z += (p.Z + parts[i + 1].Z) * 0.5f;
            }
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// Vanilla <c>ShulkerModel</c> (26.1.2 <c>client.jar</c>): <c>createShellMesh</c> + head from <c>createBodyLayer</c>;
    /// <c>setupAnim</c> matches <c>ShulkerRenderState.peekAmount</c>, <c>ageInTicks</c>, <c>xRot</c>/<c>yHeadRot</c>/<c>yBodyRot</c> (degrees in state for head).
    /// </summary>
    private static MergedJavaBlockModel BuildShulker(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float peekAmount,
        float ageInTicks,
        float xRotDegrees,
        float yHeadRotDegrees,
        float yBodyRotDegrees)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);
        new EntityCuboid(-8f, -8f, -8f, 8f, 0f, 8f, 0, 28).Emit(b, Matrix4x4.CreateTranslation(0f, 24f, 0f), 1f);
        var f2 = (0.5f + peekAmount) * MathF.PI;
        var f3 = -1f + MathF.Sin(f2);
        var f4 = f2 > MathF.PI ? MathF.Sin(ageInTicks * 0.1f) * 0.7f : 0f;
        var lidY = 24f + MathF.Sin(f2) * 8f + f4;
        var lidYRot = peekAmount > 0.3f ? MathF.Pow(f3, 4f) * MathF.PI * 0.125f : 0f;
        var lidPose = Matrix4x4.CreateTranslation(0f, lidY, 0f);
        new EntityCuboid(-8f, -16f, -8f, 8f, -4f, 8f, 0, 0, XRot: 0f, YRot: lidYRot, ZRot: 0f) { RotationPivot = new Vector3(0f, -4f, 0f) }.Emit(b, lidPose, 1f);
        var deg = MathF.PI / 180f;
        var headXRad = xRotDegrees * deg;
        var headYRad = (yHeadRotDegrees - 180f - yBodyRotDegrees) * deg;
        new EntityCuboid(-3f, 0f, -3f, 3f, 6f, 3f, 0, 52, XRot: headXRad, YRot: headYRad, ZRot: 0f).Emit(b, Matrix4x4.CreateTranslation(0f, 12f, 0f), 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// Vanilla <c>SnowGolemModel</c> (26.1.2): <c>CubeDeformation(-0.5)</c> baked into corners; <c>setupAnim</c> from <c>LivingEntityRenderState.yRot</c>/<c>xRot</c> (degrees) and arm follow sin/cos of <c>0.25*yRot</c>.
    /// </summary>
    private static MergedJavaBlockModel BuildSnowGolem(string texRef, MinecraftNativeProfile profile, bool isBaby, float yRotDegrees, float xRotDegrees)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.72f, 1.08f, 0.74f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.04f, 0.82f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 64);
        var deg = MathF.PI / 180f;
        var yaw = yRotDegrees * deg;
        var upper = yaw * 0.25f;
        var c = MathF.Cos(upper);
        var s = MathF.Sin(upper);
        var xHead = xRotDegrees * deg;
        new EntityCuboid(-3.5f, -7.5f, -3.5f, 3.5f, -0.5f, 3.5f, 0, 0, UvSizeW: 8, UvSizeH: 8, UvSizeD: 8, XRot: xHead, YRot: yaw, ZRot: 0f) { RotationPivot = new Vector3(0f, 0f, 0f) }.Emit(b, Matrix4x4.CreateTranslation(0f, 4f, 0f), p.HeadScale);
        new EntityCuboid(-4.5f, -9.5f, -4.5f, 4.5f, -0.5f, 4.5f, 0, 16, UvSizeW: 10, UvSizeH: 10, UvSizeD: 10, XRot: 0f, YRot: upper, ZRot: 0f) { RotationPivot = new Vector3(0f, 0f, 0f) }.Emit(b, Matrix4x4.CreateTranslation(0f, 13f, 0f), p.BodyScale);
        new EntityCuboid(-5.5f, -11.5f, -5.5f, 5.5f, -0.5f, 5.5f, 0, 36, UvSizeW: 12, UvSizeH: 12, UvSizeD: 12).Emit(b, Matrix4x4.CreateTranslation(0f, 24f, 0f), p.BodyScale);
        var leftArmPose = Matrix4x4.CreateTranslation(5f + c * 5f, 6f, 1f - s * 5f);
        new EntityCuboid(-0.5f, 0.5f, -0.5f, 10.5f, 1.5f, 0.5f, 32, 0, UvSizeW: 12, UvSizeH: 2, UvSizeD: 2, XRot: 0f, YRot: upper, ZRot: 1f) { RotationPivot = new Vector3(0f, 1f, 0f) }.Emit(b, leftArmPose, p.LegScale);
        var rightArmPose = Matrix4x4.CreateTranslation(-5f - c * 5f, 6f, -1f + s * 5f);
        new EntityCuboid(-0.5f, 0.5f, -0.5f, 10.5f, 1.5f, 0.5f, 32, 0, UvSizeW: 12, UvSizeH: 2, UvSizeD: 2, XRot: 0f, YRot: upper + MathF.PI, ZRot: -1f) { RotationPivot = new Vector3(0f, 1f, 0f) }.Emit(b, rightArmPose, p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// Vanilla <c>IronGolemModel.setupAnim</c> (26.1.2 <c>client.jar</c>): <c>IronGolemRenderState</c> arms use
    /// <c>Mth.triangleWave</c> on attack / offer-flower / walk; legs <c>±1.5f * triangleWave(walkPos,13) * walkSpeed</c>;
    /// head <c>xRot</c>/<c>yRot</c> from degrees × <c>π/180</c>.
    /// </summary>
    private static MergedJavaBlockModel BuildIronGolem(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float attackTicksRemaining,
        int offerFlowerTick,
        float walkAnimationPos,
        float walkAnimationSpeed,
        float yRotDegrees,
        float xRotDegrees)
    {
        // IronGolemModel (1.21.11 obf. hbr): lower "body" child uses CubeDeformation(0.5) on a 9×5×6 — baked as 10×6×7 with UV 9×5×6.
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.62f, 1.0f, 0.64f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.70f, 1.0f, 0.72f) : BabyProfile.Adult);
        var b = new RigBuilder(128, 128);
        const float deg = MathF.PI / 180f;

        float rightArmX;
        float leftArmX;
        if (attackTicksRemaining > 0f)
        {
            var tw = VanillaMthTriangleWave(attackTicksRemaining, 10f);
            rightArmX = -2f + (1.5f * tw);
            leftArmX = -2f + (1.5f * tw);
        }
        else if (offerFlowerTick > 0)
        {
            var tw = VanillaMthTriangleWave(offerFlowerTick, 70f);
            rightArmX = -0.8f + (0.025f * tw);
            leftArmX = 0f;
        }
        else
        {
            var tw = VanillaMthTriangleWave(walkAnimationPos, 13f);
            rightArmX = (-0.2f + (1.5f * tw)) * walkAnimationSpeed;
            leftArmX = (-0.2f - (1.5f * tw)) * walkAnimationSpeed;
        }

        var twLeg = VanillaMthTriangleWave(walkAnimationPos, 13f);
        var rightLegX = (-1.5f * twLeg) * walkAnimationSpeed;
        var leftLegX = (1.5f * twLeg) * walkAnimationSpeed;

        var headYaw = yRotDegrees * deg;
        var headPitch = xRotDegrees * deg;
        var bodyT = Matrix4x4.CreateTranslation(0f, -7f, 0f);
        var headParent = Matrix4x4.CreateTranslation(0f, -7f, -2f);
        var headPivot = new Vector3(0f, -7f, -1.5f);

        new EntityCuboid(-4f, -12f, -5.5f, 4f, -2f, 2.5f, 0, 0, XRot: headPitch, YRot: headYaw, ZRot: 0f) { RotationPivot = headPivot }.Emit(b, headParent, p.HeadScale);
        new EntityCuboid(-1f, -5f, -7.5f, 1f, -1f, -5.5f, 24, 0, XRot: headPitch, YRot: headYaw, ZRot: 0f) { RotationPivot = headPivot }.Emit(b, headParent, p.HeadScale); // nose
        new EntityCuboid(-9f, -2f, -6f, 9f, 10f, 5f, 0, 40).Emit(b, bodyT, p.BodyScale); // body
        new EntityCuboid(-5f, 9.5f, -3.5f, 5f, 15.5f, 3.5f, 0, 70, UvSizeW: 9, UvSizeH: 5, UvSizeD: 6).Emit(b, bodyT, p.BodyScale);
        new EntityCuboid(-13f, -2.5f, -3f, -9f, 27.5f, 3f, 60, 21, OffsetX: -2f, OffsetY: 0f, OffsetZ: 0f, XRot: rightArmX, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(-11f, -2.5f, 0f) }.Emit(b, bodyT, p.LegScale); // right arm
        new EntityCuboid(9f, -2.5f, -3f, 13f, 27.5f, 3f, 60, 58, OffsetX: -2f, OffsetY: 0f, OffsetZ: 0f, XRot: leftArmX, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(11f, -2.5f, 0f) }.Emit(b, bodyT, p.LegScale); // left arm
        new EntityCuboid(-3.5f, -3f, -3f, 2.5f, 13f, 2f, 37, 0, XRot: rightLegX, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(-4f, 11f, 0f) }.Emit(b, Matrix4x4.CreateTranslation(-4f, 11f, 0f), p.LegScale); // right leg
        new EntityCuboid(-3.5f, -3f, -3f, 2.5f, 13f, 2f, 60, 0, XRot: leftLegX, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(5f, 11f, 0f) }.Emit(b, Matrix4x4.CreateTranslation(5f, 11f, 0f), p.LegScale); // left leg
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>HoglinModel</c>/<c>ZoglinModel</c> head baseline from <c>PartPose.offsetAndRotation(..., 0.87266463f + headPitch, ...)</c>.
    /// Baby transformer scales the resulting angle.
    /// </summary>
    internal static float ComputeHoglinFamilyHeadPitchRad(float setupAnimHeadPitchRad, float transformerScale) =>
        (0.87266463f + setupAnimHeadPitchRad) * transformerScale;

    /// <summary>
    /// Matches <c>StriderModel.animateBristle</c> (26.1.2 <c>client.jar</c> javap): accumulates <c>zRot</c> deltas on three bristle parts.
    /// </summary>
    private static void AccumulateStriderBristleZRotFromAnimateBristle(
        float ageInTicks,
        float bristlePhaseFactor,
        ref float topZ,
        ref float midZ,
        ref float bottomZ)
    {
        topZ += bristlePhaseFactor * 0.6f + 0.1f * MathF.Sin(ageInTicks * 0.4f);
        midZ += bristlePhaseFactor * 1.2f + 0.1f * MathF.Sin(ageInTicks * 0.2f);
        bottomZ += bristlePhaseFactor * 1.3f + 0.05f * MathF.Sin(-ageInTicks * 0.4f);
    }

    /// <summary>
    /// <c>BabyStriderModel.createBodyLayer</c> + <c>StriderModel.setupAnim</c> / <c>BabyStriderModel.customAnimations</c>
    /// (26.1.2 <c>client.jar</c> javap). Cuboids follow <c>createBodyLayer</c> float literals (the checked-in
    /// <c>BabyStriderModel.json</c> IR does not match javap for several boxes). Motion matches <c>setupAnim</c> (non-ridden)
    /// then baby <c>customAnimations</c>. Atlas <c>64×128</c> matches strider entity skins.
    /// </summary>
    private static MergedJavaBlockModel BuildBabyStrider(
        string texRef,
        float walkAnimationPos,
        float walkAnimationSpeed,
        float ageInTicks)
    {
        var b = new RigBuilder(64, 128);
        const float k10Deg = 0.17453292f;
        var w = walkAnimationPos;
        var sp = walkAnimationSpeed;
        var bodyZ = 0.4f * MathF.Sin(w * 1.5f) * sp;
        var bodyY = 17.25f - MathF.Cos(w * 1.5f) * 2f * sp;
        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, bodyY, 0f), EntityParityTemplate.Rz(bodyZ));

        new EntityCuboid(-3.5f, -3.75f, -4f, 3.5f, 3.25f, 4f, 0, 0, UvSizeW: 7, UvSizeH: 7, UvSizeD: 8).Emit(b, bodyPose, 1f);

        var leftLegX = MathF.Sin(w * 0.75f) * 2f * sp;
        var rightLegX = MathF.Sin(w * 0.75f + MathF.PI) * 2f * sp;
        var leftLegZ = k10Deg * MathF.Cos(w * 0.75f) * sp;
        var rightLegZ = k10Deg * MathF.Cos(w * 0.75f + MathF.PI) * sp;
        var leftLegY = 20f + 2f * MathF.Sin(w * 0.75f + MathF.PI) * sp;
        var rightLegY = 20f + 2f * MathF.Sin(w * 0.75f) * sp;

        var rightLegPose = EntityParityTemplate.Mul(EntityParityTemplate.T(-1.5f, rightLegY, 0f), EntityParityTemplate.Er(rightLegX, 0f, rightLegZ));
        new EntityCuboid(-1f, 0f, -1f, 1f, 4f, 1f, 0, 24, UvSizeW: 2, UvSizeH: 4, UvSizeD: 2).Emit(b, rightLegPose, 1f);

        var leftLegPose = EntityParityTemplate.Mul(EntityParityTemplate.T(1.5f, leftLegY, 0f), EntityParityTemplate.Er(leftLegX, 0f, leftLegZ));
        new EntityCuboid(-1f, 0f, -1f, 1f, 4f, 1f, 8, 24, UvSizeW: 2, UvSizeH: 4, UvSizeD: 2).Emit(b, leftLegPose, 1f);

        var bristleF = MathF.Cos(w * 1.5f + MathF.PI) * sp;
        var z2 = 0f;
        var z1 = 0f;
        var z0 = 0f;
        AccumulateStriderBristleZRotFromAnimateBristle(ageInTicks, bristleF, ref z2, ref z1, ref z0);

        var bristle0Pose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -4.25f, 2f), EntityParityTemplate.Rz(z0)));
        new EntityCuboid(-3.5f, -2.5f, 0f, 3.5f, 0.5f, 0f, 0, 21, UvSizeW: 7, UvSizeH: 3, UvSizeD: 1).Emit(b, bristle0Pose, 1f);

        var bristle1Pose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -4.25f, 0f), EntityParityTemplate.Rz(z1)));
        new EntityCuboid(-3.5f, -2.5f, 0f, 3.5f, 0.5f, 0f, 0, 18, UvSizeW: 7, UvSizeH: 3, UvSizeD: 1).Emit(b, bristle1Pose, 1f);

        var bristle2Pose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -4.25f, -2f), EntityParityTemplate.Rz(z2)));
        new EntityCuboid(-3.5f, -2.5f, 0f, 3.5f, 0.5f, 0f, 0, 15, UvSizeW: 7, UvSizeH: 3, UvSizeD: 1).Emit(b, bristle2Pose, 1f);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>AdultStriderModel.createBodyLayer</c> + <c>StriderModel.setupAnim</c> + <c>AdultStriderModel.customAnimations</c>
    /// (26.1.2 <c>client.jar</c> javap). Geometry literals align with
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.strider.AdultStriderModel.json</c>.
    /// </summary>
    private static MergedJavaBlockModel BuildStrider(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float walkAnimationPos,
        float walkAnimationSpeed,
        float ageInTicks)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyStrider(texRef, walkAnimationPos, walkAnimationSpeed, ageInTicks);
        }

        var b = new RigBuilder(64, 128);
        const float k10Deg = 0.17453292f;
        var w = walkAnimationPos;
        var sp = walkAnimationSpeed;
        var bodyZ = 0.4f * MathF.Sin(w * 1.5f) * sp;
        var bodyExtraY = 1f - MathF.Cos(w * 1.5f) * 2f * sp;
        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 1f + bodyExtraY, 0f), EntityParityTemplate.Rz(bodyZ));

        var leftLegX = MathF.Sin(w * 0.75f) * 2f * sp;
        var rightLegX = MathF.Sin(w * 0.75f + MathF.PI) * 2f * sp;
        var leftLegZ = k10Deg * MathF.Cos(w * 0.75f) * sp;
        var rightLegZ = k10Deg * MathF.Cos(w * 0.75f + MathF.PI) * sp;
        var leftLegY = 8f + 2f * MathF.Sin(w * 0.75f + MathF.PI) * sp;
        var rightLegY = 8f + 2f * MathF.Sin(w * 0.75f) * sp;

        var rightLegPose = EntityParityTemplate.Mul(EntityParityTemplate.T(-4f, rightLegY, 0f), EntityParityTemplate.Er(rightLegX, 0f, rightLegZ));
        new EntityCuboid(-2f, 0f, -2f, 2f, 16f, 2f, 0, 32, UvSizeW: 4, UvSizeH: 16, UvSizeD: 4).Emit(b, rightLegPose, 1f);

        var leftLegPose = EntityParityTemplate.Mul(EntityParityTemplate.T(4f, leftLegY, 0f), EntityParityTemplate.Er(leftLegX, 0f, leftLegZ));
        new EntityCuboid(-2f, 0f, -2f, 2f, 16f, 2f, 0, 55, UvSizeW: 4, UvSizeH: 16, UvSizeD: 4).Emit(b, leftLegPose, 1f);

        new EntityCuboid(-8f, -6f, -8f, 8f, 8f, 8f, 0, 0, UvSizeW: 16, UvSizeH: 14, UvSizeD: 16).Emit(b, bodyPose, 1f);

        var bristleF = MathF.Cos(w * 1.5f + MathF.PI) * sp;
        var zRt = -0.87266463f;
        var zRm = -1.134464f;
        var zRb = -1.2217305f;
        AccumulateStriderBristleZRotFromAnimateBristle(ageInTicks, bristleF, ref zRt, ref zRm, ref zRb);
        var zLt = 0.87266463f;
        var zLm = 1.134464f;
        var zLb = 1.2217305f;
        AccumulateStriderBristleZRotFromAnimateBristle(ageInTicks, bristleF, ref zLt, ref zLm, ref zLb);

        var bristleBaseRightBottom = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-8f, 4f, -8f), EntityParityTemplate.Rz(zRb)));
        var bristleBaseRightMid = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-8f, -1f, -8f), EntityParityTemplate.Rz(zRm)));
        var bristleBaseRightTop = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-8f, -5f, -8f), EntityParityTemplate.Rz(zRt)));
        var bristleBaseLeftTop = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(8f, -6f, -8f), EntityParityTemplate.Rz(zLt)));
        var bristleBaseLeftMid = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(8f, -2f, -8f), EntityParityTemplate.Rz(zLm)));
        var bristleBaseLeftBottom = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(8f, 3f, -8f), EntityParityTemplate.Rz(zLb)));

        // Vanilla sheet quads are 0-thick on Y; use a 1-texel slab so parity tests see stable non-zero extent (see RigBuilder AddBox).
        new EntityCuboid(-12f, -0.5f, 0f, 0f, 0.5f, 16f, 16, 65, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseRightBottom, 1f);
        new EntityCuboid(-12f, -0.5f, 0f, 0f, 0.5f, 16f, 16, 49, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseRightMid, 1f);
        new EntityCuboid(-12f, -0.5f, 0f, 0f, 0.5f, 16f, 16, 33, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseRightTop, 1f);
        new EntityCuboid(0f, -0.5f, 0f, 12f, 0.5f, 16f, 16, 33, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseLeftTop, 1f);
        new EntityCuboid(0f, -0.5f, 0f, 12f, 0.5f, 16f, 16, 49, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseLeftMid, 1f);
        new EntityCuboid(0f, -0.5f, 0f, 12f, 0.5f, 16f, 16, 65, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseLeftBottom, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>CreeperModel.createBodyLayer</c> — Java 1.21.11 client <c>hcn.a()</c> (<c>64×32</c>): <c>head</c> <c>6×6×6</c> @ <c>(0,0)</c>
    /// + <c>CubeDeformation(0.6)</c> (preview omits inflate; UV footprint <c>6³</c>); <c>PartPose.offset(0,6,-8)</c>;
    /// <c>body</c> <c>8×16×6</c> @ <c>(28,8)</c> + <c>CubeDeformation(1.75)</c> + <c>PartPose.offsetAndRotation(0,5,2, π/2,0,0)</c>;
    /// shared leg <c>4×6×4</c> @ <c>(0,16)</c> + <c>CubeDeformation(0.5)</c>; leg roots <c>T(∓3,12,7)</c> / <c>T(∓3,12,-5)</c>.
    /// Preview adds <paramref name="bodyBob"/> to part Y pivots (idle bob).
    /// </summary>
    private static MergedJavaBlockModel BuildCreeper(string texRef, MinecraftNativeProfile profile, bool isBaby, float bodyBob)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.74f, 1.18f, 0.74f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.10f, 0.82f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 32);
        var y = bodyBob;

        var headPose = EntityParityTemplate.T(0f, 6f + y, -8f);
        new EntityCuboid(-3f, -4f, -4f, 3f, 2f, 2f, 0, 0, UvSizeW: 6, UvSizeH: 6, UvSizeD: 6).Emit(b, headPose, p.HeadScale);

        var bodyPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(0f, 5f + y, 2f),
            EntityParityTemplate.Rx(MathF.PI / 2f));
        new EntityCuboid(-4f, -10f, -7f, 4f, 6f, -1f, 28, 8, UvSizeW: 8, UvSizeH: 16, UvSizeD: 6).Emit(b, bodyPose, p.BodyScale);

        // Shared leg cuboid + <c>texOffs(0,16)</c> (single <c>hdl</c> reference in <c>hcn.a</c>).
        new EntityCuboid(-2f, 0f, -2f, 2f, 6f, 2f, 0, 16, UvSizeW: 4, UvSizeH: 6, UvSizeD: 4).Emit(b, EntityParityTemplate.T(-3f, 12f + y, 7f), p.LegScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 6f, 2f, 0, 16, UvSizeW: 4, UvSizeH: 6, UvSizeD: 4).Emit(b, EntityParityTemplate.T(3f, 12f + y, 7f), p.LegScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 6f, 2f, 0, 16, UvSizeW: 4, UvSizeH: 6, UvSizeD: 4).Emit(b, EntityParityTemplate.T(-3f, 12f + y, -5f), p.LegScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 6f, 2f, 0, 16, UvSizeW: 4, UvSizeH: 6, UvSizeD: 4).Emit(b, EntityParityTemplate.T(3f, 12f + y, -5f), p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildSpider(string texRef, MinecraftNativeProfile profile, bool isBaby, float legSpread)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.76f, 1.10f, 0.72f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.84f, 1.04f, 0.78f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 32);
        // SpiderModel.createSpiderBodyLayer (LayerDefinition) — cuboids align with
        // docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.spider.SpiderModel.json (cephalothorax / abdomen / legs).
        // Legs: PartPose zRot ±π/4 hinge at torso side — origin pivots + Rz, not leg offsetX as rotation.
        new EntityCuboid(6f, 8f, 4f, 14f, 16f, 12f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale); // cephalothorax
        new EntityCuboid(0f, 8f, 2f, 10f, 16f, 14f, 0, 11, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale); // abdomen
        for (var i = 0; i < 4; i++)
        {
            var z = 4.5f + i * 2.2f;
            var spread = (legSpread + i * 0.08f) * 0.45f;
            var baseAngle = MathF.PI / 4f + i * 0.04f;
            var leftAttach = Matrix4x4.CreateTranslation(6f, 10f, z + 1f);
            new EntityCuboid(-16f, -1f, -1f, 0f, 1f, 1f, 18, 0) { RotationPivot = Vector3.Zero }.Emit(b, Matrix4x4.Multiply(leftAttach, Matrix4x4.CreateRotationZ(baseAngle + spread)), p.LegScale);
            var rightAttach = Matrix4x4.CreateTranslation(14f, 10f, z + 1f);
            new EntityCuboid(0f, -1f, -1f, 16f, 1f, 1f, 18, 0) { RotationPivot = Vector3.Zero }.Emit(b, Matrix4x4.Multiply(rightAttach, Matrix4x4.CreateRotationZ(-baseAngle - spread)), p.LegScale);
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }



    /// <summary>CreakingModel (1.21.11 javap hdy): 64×64 diffuse, head/body/limb extents.</summary>
    private static MergedJavaBlockModel BuildCreaking(string texRef, MinecraftNativeProfile profile, bool isBaby, float lean)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.74f, 1.08f, 0.76f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.82f, 1.04f, 0.80f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 64);
        var headPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, -8f, 0f), Matrix4x4.CreateRotationZ(lean));
        new EntityCuboid(-3f, -10f, -3f, 3f, 0f, 3f, 28, 31).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-3f, 0f, -3f, 3f, 10f, 3f, 12, 40).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(0f, -3f, -3f, 6f, 13f, 5f, 24, 0).Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(-2f, -1.5f, -1.5f, 3f, 21f, 3f, 46, 0, XRot: 0f, YRot: lean * 0.5f, ZRot: 0f) { RotationPivot = new Vector3(0.5f, -1.5f, 0.75f) }.Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(-3f, -1.5f, -1.5f, 0f, 16f, 3f, 52, 12, XRot: 0f, YRot: -lean * 0.5f, ZRot: 0f) { RotationPivot = new Vector3(-1.5f, -1.5f, 0.75f) }.Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(-1.5f, 0f, -1.5f, 1.5f, 16f, 1.5f, 42, 40).Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(-3f, 0f, -1.5f, 0f, 19f, 1.5f, 0, 34).Emit(b, Matrix4x4.Identity, p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// CopperGolemModel.createBodyLayer (Minecraft Java 26.1.2): 64×64 sheet; root children <c>body</c> (torso + head + arms) and legs.
    /// Eyes/emissive variants reuse this topology (renderer applies <c>createEyesLayer</c> UV remap).
    /// </summary>
    private static MergedJavaBlockModel BuildCopperGolem(string texRef, MinecraftNativeProfile profile, bool isBaby, float armSwing)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.62f, 1.0f, 0.64f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.70f, 1.0f, 0.72f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 64);
        var bodyRoot = Matrix4x4.CreateTranslation(0f, -5f, 0f);
        // hbq.e(): body texOffs(0,15); Java addBox origin (−4,−6,−3) size (8,6,6) — RigBuilder uses diagonal corners, not origin+size.
        new EntityCuboid(-4f, -6f, -3f, 4f, 0f, 3f, 0, 15).Emit(b, bodyRoot, p.BodyScale);

        var headRoot = Matrix4x4.Multiply(bodyRoot, Matrix4x4.CreateTranslation(0f, -6f, 0f));
        new EntityCuboid(-4f, -5f, -5f, 4f, 0f, 5f, 0, 0).Emit(b, headRoot, p.HeadScale);
        // texOffs(56,0): origin (-1,-2,-6) + dimensions (2,3,2)
        new EntityCuboid(-1f, -2f, -6f, 1f, 1f, -4f, 56, 0).Emit(b, headRoot, p.HeadScale);
        new EntityCuboid(-1f, -9f, -1f, 1f, -5f, 1f, 37, 8).Emit(b, headRoot, p.HeadScale);
        new EntityCuboid(-2f, -13f, -2f, 2f, -9f, 2f, 37, 0).Emit(b, headRoot, p.HeadScale);

        var swing = MathF.Sin(armSwing) * 0.28f;
        var rightArm = Matrix4x4.Multiply(bodyRoot, Matrix4x4.CreateTranslation(-4f, -6f, 0f));
        new EntityCuboid(-3f, -1f, -2f, 0f, 9f, 2f, 36, 16, XRot: swing, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(0f, -1f, 1f) }.Emit(b, rightArm, p.LegScale);

        var leftArm = Matrix4x4.Multiply(bodyRoot, Matrix4x4.CreateTranslation(4f, -6f, 0f));
        new EntityCuboid(0f, -1f, -2f, 3f, 9f, 2f, 50, 16, MirrorUv: true, XRot: -swing, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(1.5f, -1f, 1f) }.Emit(b, leftArm, p.LegScale);

        var legRoot = Matrix4x4.CreateTranslation(0f, -5f, 0f);
        new EntityCuboid(-4f, 0f, -2f, 0f, 5f, 2f, 0, 27).Emit(b, legRoot, p.LegScale);
        new EntityCuboid(0f, 0f, -2f, 4f, 5f, 2f, 16, 27, MirrorUv: true) { RotationPivot = null }.Emit(b, legRoot, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }
}
