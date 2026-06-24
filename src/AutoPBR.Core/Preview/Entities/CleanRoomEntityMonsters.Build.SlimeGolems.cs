using System;
using System.Collections.Generic;
using System.Numerics;

using AutoPBR.Core.Models;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    private static MergedJavaBlockModel BuildSlime(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        // createOuterBodyLayer — semi-transparent shell (SlimeOuterLayer / entityTranslucent).
        new EntityCuboid(-4f, 16f, -4f, 4f, 24f, 4f, 0, 0)
        {
            DepthLayerKind = PreviewDepthLayerKind.TranslucentOverlay,
        }.Emit(b, Matrix4x4.Identity, 1f);
        // createInnerBodyLayer — opaque core + face parts on atlas UV (0,16) and (32,*).
        new EntityCuboid(-3f, 17f, -3f, 3f, 23f, 3f, 0, 16).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-3.25f, 18f, -3.5f, -1.25f, 20f, -1.5f, 32, 0)
        {
            DepthLayerKind = PreviewDepthLayerKind.CosmeticOverlay,
        }.Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(1.25f, 18f, -3.5f, 3.25f, 20f, -1.5f, 32, 4)
        {
            DepthLayerKind = PreviewDepthLayerKind.CosmeticOverlay,
        }.Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(0f, 21f, -3.5f, 1f, 22f, -2.5f, 32, 8)
        {
            DepthLayerKind = PreviewDepthLayerKind.CosmeticOverlay,
        }.Emit(b, Matrix4x4.Identity, 1f);
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

}
