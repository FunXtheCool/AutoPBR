using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    /// <summary>
    /// <c>DolphinModel.createBodyLayer</c> — Java 1.21.11 client <c>han.a()</c> (<c>64×64</c>): <c>body</c> <c>T(0,22,-5)</c> + hull <c>8×7×13</c> <c>(22,0)</c>;
    /// <c>back_fin</c> <c>1×4×5</c> @ <c>(51,0)</c> <c>Rx(π/3)</c>; pectorals <c>1×4×7</c> @ <c>(48,20)</c> with mirrored poses; <c>tail</c> <c>4×5×11</c> @ <c>(0,19)</c> + child <c>tail_fin</c> <c>10×1×6</c> @ <c>(19,20)</c> <c>T(0,0,9)</c>;
    /// <c>head</c> <c>8×7×6</c> + <c>nose</c> <c>2×2×4</c> @ <c>(0,13)</c>. Preview adds <paramref name="swimSway"/> to tail pitch (callers may include <see cref="ComputePreviewDolphinSwimOscillation"/>).
    /// </summary>
    private static MergedJavaBlockModel BuildDolphin(string texRef, MinecraftNativeProfile profile, bool isBaby, float swimSway)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildBabyDolphin(texRef, swimSway);
        }

        var p = isBaby ? new BabyProfile(0.80f, 1.08f, 0.84f) : BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;
        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 22f, -5f));

        new EntityCuboid(-4f, -7f, 0f, 4f, 0f, 13f, 22, 0, UvSizeW: 8, UvSizeH: 7, UvSizeD: 13).Emit(b, bodyPose, p.BodyScale);

        var backFinPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Rx(1.0471976f));
        new EntityCuboid(-0.5f, 0f, 8f, 0.5f, 4f, 13f, 51, 0, UvSizeW: 1, UvSizeH: 4, UvSizeD: 5).Emit(b, backFinPose, p.BodyScale);

        var leftFinPose = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(2f, -2f, 4f), EntityParityTemplate.Er(1.0471976f, 0f, 2.0943952f)));
        new EntityCuboid(-0.5f, -4f, 0f, 0.5f, 0f, 7f, 48, 20, UvSizeW: 1, UvSizeH: 4, UvSizeD: 7, MirrorUv: true).Emit(b, leftFinPose, p.BodyScale);

        var rightFinPose = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(-2f, -2f, 4f), EntityParityTemplate.Er(1.0471976f, 0f, -2.0943952f)));
        new EntityCuboid(-0.5f, -4f, 0f, 0.5f, 0f, 7f, 48, 20, UvSizeW: 1, UvSizeH: 4, UvSizeD: 7).Emit(b, rightFinPose, p.BodyScale);

        var tailPitch = -0.10471976f - swimSway * 0.2f;
        var tailPose = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -2.5f, 11f), EntityParityTemplate.Er(tailPitch, 0f, 0f)));
        new EntityCuboid(-2f, -2.5f, 0f, 2f, 2.5f, 11f, 0, 19, UvSizeW: 4, UvSizeH: 5, UvSizeD: 11).Emit(b, tailPose, p.LegScale);

        var tailFinPose = EntityParityTemplate.Mul(tailPose, EntityParityTemplate.T(0f, 0f, 9f));
        new EntityCuboid(-5f, -0.5f, 0f, 5f, 0.5f, 6f, 19, 20, UvSizeW: 10, UvSizeH: 1, UvSizeD: 6).Emit(b, tailFinPose, p.LegScale);

        var headPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(0f, -4f, -3f));
        new EntityCuboid(-4f, -3f, -3f, 4f, 4f, 3f, 0, 0, UvSizeW: 8, UvSizeH: 7, UvSizeD: 6).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-1f, 2f, -7f, 1f, 4f, -3f, 0, 13, UvSizeW: 2, UvSizeH: 2, UvSizeD: 4).Emit(b, headPose, p.HeadScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>Bind-pose hand builder for parity tests (no swim sway).</summary>
    internal static MergedJavaBlockModel BuildDolphinHandMeshForTests(string texRef, MinecraftNativeProfile profile, bool isBaby = false) =>
        BuildDolphin(texRef, profile, isBaby, swimSway: 0f);


    /// <summary>
    /// Legacy hand-tuned mesh used only when parity-catalog geometry IR emit fails and no <c>ok</c> shard exists.
    /// Catalogued axolotl textures use <see cref="TryBuildParityCatalogMeshFromGeometryIr"/> instead.
    /// </summary>
    private static MergedJavaBlockModel BuildAxolotl(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idleBob,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildBabyAxolotl(
                texRef,
                idleBob,
                rightHindLegPitchRad,
                leftHindLegPitchRad,
                rightFrontLegPitchRad,
                leftFrontLegPitchRad);
        }

        var p = isBaby ? new BabyProfile(0.80f, 1.10f, 0.82f) : BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        // AxolotlModel.createBodyLayer (~1.21.4): body at PartPose (0,20,5); head (0,0,-9); tail + four legs; gills on head.
        // Some vanilla cuboids use zero thickness on one axis; preview uses 1–2 unit thickness so RigBuilder UV extents stay valid.
        // Leg roots (+X vs −X, Z −1 hind / −8 front): preview <c>xRot</c> from lifted quadruped setupAnim IR when available.
        var bodyBase = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 20f, 5f), Matrix4x4.CreateRotationX(idleBob));
        new EntityCuboid(-4f, -2f, -9f, 4f, 2f, 1f, 0, 11).Emit(b, bodyBase, p.BodyScale);

        var headBase = Matrix4x4.Multiply(bodyBase, Matrix4x4.CreateTranslation(0f, 0f, -9f));
        new EntityCuboid(-4f, -3f, -5f, 4f, 2f, 0f, 0, 1).Emit(b, headBase, p.HeadScale);

        var topGills = Matrix4x4.Multiply(headBase, Matrix4x4.CreateTranslation(0f, -3f, -1f));
        new EntityCuboid(-4f, -3f, -1f, 4f, 0f, 1f, 3, 37).Emit(b, topGills, p.HeadScale);

        var leftGills = Matrix4x4.Multiply(headBase, Matrix4x4.CreateTranslation(-4f, 0f, -1f));
        new EntityCuboid(-3f, -5f, -1f, 0f, 2f, 1f, 0, 40).Emit(b, leftGills, p.HeadScale);

        var rightGills = Matrix4x4.Multiply(headBase, Matrix4x4.CreateTranslation(4f, 0f, -1f));
        new EntityCuboid(0f, -5f, -1f, 3f, 2f, 1f, 11, 40).Emit(b, rightGills, p.HeadScale);

        // Hind / front legs share UV layout; mirrored origins (-1,0,0) vs (-2,0,0) from javap.
        new EntityCuboid(-1f, 0f, -1f, 2f, 5f, 1f, 2, 13).Emit(b, EntityParityTemplate.Mul(bodyBase, EntityParityTemplate.Mul(EntityParityTemplate.T(3.5f, 1f, -1f), EntityParityTemplate.Rx(rightHindLegPitchRad))), p.LegScale);
        new EntityCuboid(-1f, 0f, -1f, 2f, 5f, 1f, 2, 13).Emit(b, EntityParityTemplate.Mul(bodyBase, EntityParityTemplate.Mul(EntityParityTemplate.T(3.5f, 1f, -8f), EntityParityTemplate.Rx(rightFrontLegPitchRad))), p.LegScale);
        new EntityCuboid(-2f, 0f, -1f, 1f, 5f, 1f, 2, 13).Emit(b, EntityParityTemplate.Mul(bodyBase, EntityParityTemplate.Mul(EntityParityTemplate.T(-3.5f, 1f, -1f), EntityParityTemplate.Rx(leftHindLegPitchRad))), p.LegScale);
        new EntityCuboid(-2f, 0f, -1f, 1f, 5f, 1f, 2, 13).Emit(b, EntityParityTemplate.Mul(bodyBase, EntityParityTemplate.Mul(EntityParityTemplate.T(-3.5f, 1f, -8f), EntityParityTemplate.Rx(leftFrontLegPitchRad))), p.LegScale);

        var tailBase = Matrix4x4.Multiply(bodyBase, Matrix4x4.CreateTranslation(0f, 0f, 1f));
        new EntityCuboid(-1f, -3f, -1f, 1f, 2f, 11f, 2, 19).Emit(b, tailBase, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>Bind-pose hand builder for axolotl parity tests (no leg animation).</summary>
    internal static MergedJavaBlockModel BuildAxolotlHandMeshForTests(string texRef, MinecraftNativeProfile profile, bool isBaby = false) =>
        BuildAxolotl(texRef, profile, isBaby, idleBob: 0f, rightHindLegPitchRad: 0f, leftHindLegPitchRad: 0f, rightFrontLegPitchRad: 0f, leftFrontLegPitchRad: 0f);


    private static MergedJavaBlockModel BuildFrog(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float croakInflate,
        float walkLeftLegPitchRad = 0f,
        float walkRightLegPitchRad = 0f,
        float walkLeftArmXRad = 0f,
        float walkLeftArmYRad = 0f,
        float walkLeftArmZRad = 0f,
        float walkRightArmXRad = 0f,
        float walkRightArmYRad = 0f,
        float walkRightArmZRad = 0f,
        Vector3 walkLeftArmPos = default,
        Vector3 walkRightArmPos = default,
        Vector3 walkLeftLegPos = default,
        Vector3 walkRightLegPos = default)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.72f, 1.14f, 0.74f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.08f, 0.82f) : BabyProfile.Adult);
        var b = new RigBuilder(48, 48);
        var croakDy = 2f + croakInflate * 0.25f;
        // FrogModel.createBodyLayer (~1.21.4): root (0,24,0); body (0,-2,4); head on body; eyes/arms/tongue/croaking on body stack;
        // legs on root. Degenerate-thickness vanilla quads are replaced with thin solids for stable UV packing.
        var root = Matrix4x4.CreateTranslation(0f, 24f, 0f);
        var bodyBase = Matrix4x4.Multiply(root, Matrix4x4.CreateTranslation(0f, -2f, 4f));
        new EntityCuboid(-3.5f, -2f, -8f, 3.5f, 1f, 1f, 3, 1).Emit(b, bodyBase, p.BodyScale);

        var headBase = Matrix4x4.Multiply(bodyBase, Matrix4x4.CreateTranslation(0f, -2f, -1f));
        new EntityCuboid(-3.5f, -2f, -7f, 3.5f, 1f, 2f, 0, 13).Emit(b, headBase, p.HeadScale);

        var eyesBase = Matrix4x4.Multiply(headBase, Matrix4x4.CreateTranslation(-0.5f, 0f, 2f));
        var rEye = Matrix4x4.Multiply(eyesBase, Matrix4x4.CreateTranslation(-1.5f, -3f, -6.5f));
        var lEye = Matrix4x4.Multiply(eyesBase, Matrix4x4.CreateTranslation(2.5f, -3f, -6.5f));
        new EntityCuboid(-1.5f, -1f, -1.5f, 1.5f, 1f, 1.5f, 0, 0).Emit(b, rEye, p.HeadScale);
        new EntityCuboid(-1.5f, -1f, -1.5f, 1.5f, 1f, 1.5f, 0, 5).Emit(b, lEye, p.HeadScale);

        var croak = Matrix4x4.Multiply(bodyBase, Matrix4x4.CreateTranslation(0f, -1f, -5f));
        new EntityCuboid(-3.5f, -0.1f, -2.9f, 3.5f, -0.1f + croakDy, 0.1f, 26, 5).Emit(b, croak, p.BodyScale);

        var tongue = Matrix4x4.Multiply(bodyBase, Matrix4x4.CreateTranslation(0f, -1.01f, 1f));
        new EntityCuboid(-2f, 0f, -7.1f, 2f, 1f, -0.1f, 17, 13).Emit(b, tongue, p.BodyScale);

        var leftArm = EntityParityTemplate.Mul(
            bodyBase,
            EntityParityTemplate.Mul(
                EntityParityTemplate.T(4f + walkLeftArmPos.X, -1f + walkLeftArmPos.Y, -6.5f + walkLeftArmPos.Z),
                EntityParityTemplate.Er(walkLeftArmXRad, walkLeftArmYRad, walkLeftArmZRad)));
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 2f, 0, 32).Emit(b, leftArm, p.BodyScale);
        var leftHand = Matrix4x4.Multiply(leftArm, Matrix4x4.CreateTranslation(0f, 3f, -1f));
        new EntityCuboid(-4f, 0f, -4f, 4f, 2f, 4f, 18, 40).Emit(b, leftHand, p.BodyScale);

        var rightArm = EntityParityTemplate.Mul(
            bodyBase,
            EntityParityTemplate.Mul(
                EntityParityTemplate.T(-4f + walkRightArmPos.X, -1f + walkRightArmPos.Y, -6.5f + walkRightArmPos.Z),
                EntityParityTemplate.Er(walkRightArmXRad, walkRightArmYRad, walkRightArmZRad)));
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 2f, 0, 38).Emit(b, rightArm, p.BodyScale);
        var rightHand = Matrix4x4.Multiply(rightArm, Matrix4x4.CreateTranslation(0f, 3f, 0f));
        new EntityCuboid(-4f, 0f, -5f, 4f, 2f, 3f, 2, 40).Emit(b, rightHand, p.BodyScale);

        var leftLeg = Matrix4x4.Multiply(
            Matrix4x4.Multiply(
                root,
                Matrix4x4.CreateTranslation(3.5f + walkLeftLegPos.X, -3f + walkLeftLegPos.Y, 4f + walkLeftLegPos.Z)),
            Matrix4x4.CreateRotationX(walkLeftLegPitchRad));
        new EntityCuboid(-1f, 0f, -2f, 2f, 3f, 2f, 14, 25).Emit(b, leftLeg, p.LegScale);
        var leftFoot = Matrix4x4.Multiply(leftLeg, Matrix4x4.CreateTranslation(2f, 3f, 0f));
        new EntityCuboid(-4f, 0f, -4f, 4f, 2f, 4f, 2, 32).Emit(b, leftFoot, p.LegScale);

        var rightLeg = Matrix4x4.Multiply(
            Matrix4x4.Multiply(
                root,
                Matrix4x4.CreateTranslation(-3.5f + walkRightLegPos.X, -3f + walkRightLegPos.Y, 4f + walkRightLegPos.Z)),
            Matrix4x4.CreateRotationX(walkRightLegPitchRad));
        new EntityCuboid(-2f, 0f, -2f, 1f, 3f, 2f, 0, 25).Emit(b, rightLeg, p.LegScale);
        var rightFoot = Matrix4x4.Multiply(rightLeg, Matrix4x4.CreateTranslation(-2f, 3f, 0f));
        new EntityCuboid(-4f, 0f, -4f, 4f, 2f, 4f, 18, 32).Emit(b, rightFoot, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>TadpoleModel.createBodyLayer</c> — Java 1.21.11 client <c>hbj.a()</c> (<c>16×16</c>): <c>body</c> <c>3×2×3</c> @ <c>(0,0)</c>
    /// <c>PartPose.offset(0,22,-3)</c>; <c>tail</c> <c>0×2×7</c> sheet @ same UV origin + <c>PartPose.offset(0,22,0)</c>; <c>setupAnim</c> drives tail <c>yRot</c> (preview <paramref name="tailSway"/>).
    /// </summary>
    private static MergedJavaBlockModel BuildTadpole(string texRef, MinecraftNativeProfile profile, bool isBaby, float tailSway)
    {
        _ = isBaby;
        _ = profile;
        var b = new RigBuilder(16, 16);
        var bodyPose = EntityParityTemplate.T(0f, 22f, -3f);
        new EntityCuboid(-1.5f, -1f, 0f, 1.5f, 1f, 3f, 0, 0, UvSizeW: 3, UvSizeH: 2, UvSizeD: 3).Emit(b, bodyPose, 1f);

        var tailPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 22f, 0f), EntityParityTemplate.Ry(tailSway));
        new EntityCuboid(-0.5f, -1f, 0f, 0.5f, 1f, 7f, 0, 0, UvSizeW: 1, UvSizeH: 2, UvSizeD: 7).Emit(b, tailPose, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

}
