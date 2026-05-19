using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Boss-scale rigs: ravager, wither, warden, ender dragon.

    /// <summary>
    /// Vanilla <c>RavagerModel.setupAnim</c> (26.1.2 <c>client.jar</c>): <c>RavagerRenderState</c> drives neck/mouth/head and
    /// leg <c>cos(walkAnimationPos * 0.6662f ± π) * 0.4f * walkAnimationSpeed</c>.
    /// </summary>
    private static MergedJavaBlockModel BuildRavager(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float xRotDegrees,
        float yRotDegrees,
        float walkAnimationPos,
        float walkAnimationSpeed,
        float attackTicksRemaining,
        float stunnedTicksRemaining,
        float roarAnimation)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.62f, 1.02f, 0.64f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.70f, 1.0f, 0.72f) : BabyProfile.Adult);
        var b = new RigBuilder(128, 128);
        const float deg = MathF.PI / 180f;

        float neckTx;
        float neckTy;
        float neckTz;
        float neckXRotRad;
        float mouthXRotRad;

        if (attackTicksRemaining > 0f)
        {
            var tw = VanillaMthTriangleWave(attackTicksRemaining, 10f);
            var f6 = (1f + tw) * 0.5f;
            var f7 = f6 * f6 * f6 * 12f;
            const float neckXRotBeforeRad = 0f;
            var f8 = MathF.Sin(neckXRotBeforeRad) * f7;
            neckTx = 0f;
            neckTz = -6.5f + f7;
            neckTy = -7f - f8;
            neckXRotRad = 0f;
            if (attackTicksRemaining > 5f)
            {
                mouthXRotRad = MathF.Sin(((attackTicksRemaining - 4f) / 4f) * MathF.PI) * MathF.PI * 0.4f;
            }
            else
            {
                mouthXRotRad = 0.15707964f * MathF.Sin(attackTicksRemaining * (MathF.PI / 10f));
            }
        }
        else
        {
            const float neckXRotBeforeRad = 0f;
            neckTy = -7f + MathF.Sin(neckXRotBeforeRad);
            neckTx = stunnedTicksRemaining > 0f
                ? MathF.Sin((stunnedTicksRemaining / 40f) * 10f) * 3f
                : 0f;
            neckTz = 5.5f;
            neckXRotRad = stunnedTicksRemaining > 0f ? 0.21991149f : 0f;
            if (stunnedTicksRemaining > 0f)
            {
                mouthXRotRad = MathF.PI * 0.05f;
            }
            else if (roarAnimation > 0f)
            {
                mouthXRotRad = (MathF.PI / 2f) * MathF.Sin(roarAnimation * MathF.PI * 0.25f);
            }
            else
            {
                mouthXRotRad = MathF.PI * 0.01f;
            }
        }

        var neckPose = Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(neckTx, neckTy, neckTz),
            Matrix4x4.CreateRotationX(neckXRotRad));
        new EntityCuboid(-5f, -1f, -18f, 5f, 9f, 0f, 68, 73).Emit(b, neckPose, p.BodyScale);

        var headPose = Matrix4x4.Multiply(
            neckPose,
            Matrix4x4.Multiply(
                Matrix4x4.CreateTranslation(0f, 16f, -17f),
                Matrix4x4.Multiply(Matrix4x4.CreateRotationY(yRotDegrees * deg), Matrix4x4.CreateRotationX(xRotDegrees * deg))));
        new EntityCuboid(-8f, -20f, -14f, 8f, 0f, 2f, 0, 0).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2f, -6f, -18f, 2f, 2f, -14f, 0, 0).Emit(b, headPose, p.HeadScale); // nose
        new EntityCuboid(0f, -14f, -2f, 2f, 0f, 2f, 74, 55).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-10f, -14f, -8f), Matrix4x4.CreateRotationX(1.0995574f))), p.HeadScale);
        new EntityCuboid(0f, -14f, -2f, 2f, 0f, 2f, 74, 55).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, -14f, -8f), Matrix4x4.CreateRotationX(1.0995574f))), p.HeadScale);
        var mouthPose = Matrix4x4.Multiply(
            headPose,
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, -2f, 2f), Matrix4x4.CreateRotationX(mouthXRotRad)));
        new EntityCuboid(-8f, 0f, -16f, 8f, 3f, 0f, 0, 36).Emit(b, mouthPose, p.HeadScale);

        var bodyPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 1f, 2f), Matrix4x4.CreateRotationX(MathF.PI / 2f));
        new EntityCuboid(-7f, -10f, -7f, 7f, 6f, 13f, 0, 55).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-6f, 6f, -7f, 6f, 19f, 11f, 0, 91).Emit(b, bodyPose, p.BodyScale); // saddle-ish block

        var w = walkAnimationPos;
        var legMul = 0.4f * walkAnimationSpeed;
        var rh = MathF.Cos(w * 0.6662f) * legMul;
        var lh = MathF.Cos((w * 0.6662f) + MathF.PI) * legMul;
        var rf = MathF.Cos((w * 0.6662f) + MathF.PI) * legMul;
        var lf = MathF.Cos(w * 0.6662f) * legMul;
        var legP = new Vector3(0f, 0f, 0f);
        new EntityCuboid(-4f, 0f, -4f, 4f, 37f, 4f, 96, 0, XRot: rh, YRot: 0f, ZRot: 0f) { RotationPivot = legP }.Emit(b, Matrix4x4.CreateTranslation(-8f, -13f, 18f), p.LegScale);
        new EntityCuboid(-4f, 0f, -4f, 4f, 37f, 4f, 96, 0, XRot: lh, YRot: 0f, ZRot: 0f) { RotationPivot = legP }.Emit(b, Matrix4x4.CreateTranslation(8f, -13f, 18f), p.LegScale);
        new EntityCuboid(-4f, 0f, -4f, 4f, 37f, 4f, 64, 0, XRot: rf, YRot: 0f, ZRot: 0f) { RotationPivot = legP }.Emit(b, Matrix4x4.CreateTranslation(-8f, -13f, -5f), p.LegScale);
        new EntityCuboid(-4f, 0f, -4f, 4f, 37f, 4f, 64, 0, XRot: lf, YRot: 0f, ZRot: 0f) { RotationPivot = legP }.Emit(b, Matrix4x4.CreateTranslation(8f, -13f, -5f), p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildWither(string texRef, MinecraftNativeProfile profile, bool isBaby, float wave)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.68f, 1.0f, 0.70f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.76f, 1.0f, 0.78f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 64);
        new EntityCuboid(-10f, 3.9f, -0.5f, 10f, 6.9f, 2.5f, 0, 16).Emit(b, Matrix4x4.Identity, p.BodyScale); // shoulders
        var ribPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-2f, 6.9f, -0.5f), Matrix4x4.CreateRotationX(0.20420352f + MathF.Sin(wave) * 0.05f));
        new EntityCuboid(0f, 0f, 0f, 3f, 10f, 3f, 0, 22).Emit(b, ribPose, p.BodyScale);
        new EntityCuboid(-4f, 1.5f, 0.5f, 7f, 3.5f, 2.5f, 24, 22).Emit(b, ribPose, p.BodyScale);
        new EntityCuboid(-4f, 4f, 0.5f, 7f, 6f, 2.5f, 24, 22).Emit(b, ribPose, p.BodyScale);
        new EntityCuboid(-4f, 6.5f, 0.5f, 7f, 8.5f, 2.5f, 24, 22).Emit(b, ribPose, p.BodyScale);
        var tailPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-2f, 6.9f + MathF.Cos(0.20420352f) * 10f, -0.5f + MathF.Sin(0.20420352f) * 10f),
            Matrix4x4.CreateRotationX(0.83252203f + MathF.Sin(wave) * 0.07f));
        new EntityCuboid(0f, 0f, 0f, 3f, 6f, 3f, 12, 22).Emit(b, tailPose, p.BodyScale);
        new EntityCuboid(-4f, -4f, -4f, 4f, 4f, 4f, 0, 0).Emit(b, Matrix4x4.CreateTranslation(0f, 0f, 0f), p.HeadScale); // center
        new EntityCuboid(-4f, -4f, -4f, 2f, 2f, 2f, 32, 0).Emit(b, Matrix4x4.CreateTranslation(-8f, 4f, 0f), p.HeadScale); // right head (6x6x6 at offset)
        new EntityCuboid(-4f, -4f, -4f, 2f, 2f, 2f, 32, 0).Emit(b, Matrix4x4.CreateTranslation(10f, 4f, 0f), p.HeadScale); // left head (6x6x6 at offset)
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>WardenModel.createBodyLayer</c> — Java 1.21.11 client <c>hfr.a()</c> (<c>128×128</c>): bone <c>T(0,24,0)</c>;
    /// body <c>T(0,-21,0)</c> + <c>texOffs(0,0)</c> <c>(-9,-13,-4)+18×21×11</c>;
    /// ribcages <c>texOffs(90,11)</c> <c>(-2,-11,-0.1)+9×21×0</c> (mirrored left) at <c>(±7,-2,-4)</c>;
    /// head <c>texOffs(0,32)</c> <c>(-8,-16,-5)+16×16×10</c> at <c>(0,-13,0)</c> under body;
    /// tendrils <c>texOffs(52,32)/(58,0)</c> flat <c>16×16×0</c> → preview thickness <c>1</c> with integer UV footprint;
    /// arms <c>8×28×8</c> <c>texOffs(44,50)/(0,58)</c> at <c>(∓13,-13,1)</c>; legs <c>6×13×6</c> on bone at <c>(∓5.9,-13,0)</c> with mirrored x origins.
    /// </summary>
    private static MergedJavaBlockModel BuildWarden(string texRef, MinecraftNativeProfile profile, bool isBaby, float sway)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.62f, 1.0f, 0.64f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.70f, 1.0f, 0.72f) : BabyProfile.Adult);
        var b = new RigBuilder(128, 128);
        var bone = EntityParityTemplate.T(0f, 24f, 0f);
        var body = EntityParityTemplate.Mul(bone, EntityParityTemplate.T(0f, -21f, 0f));
        new EntityCuboid(-9f, -13f, -4f, 9f, 8f, 7f, 0, 0, UvSizeW: 18, UvSizeH: 21, UvSizeD: 11).Emit(b, body, p.BodyScale);

        var ribRight = EntityParityTemplate.Mul(body, EntityParityTemplate.T(-7f, -2f, -4f));
        new EntityCuboid(-2f, -11f, -0.1f, 7f, 10f, 0.9f, 90, 11, UvSizeW: 9, UvSizeH: 21, UvSizeD: 1).Emit(b, ribRight, p.BodyScale);
        var ribLeft = EntityParityTemplate.Mul(body, EntityParityTemplate.T(7f, -2f, -4f));
        new EntityCuboid(-2f, -11f, -0.1f, 7f, 10f, 0.9f, 90, 11, UvSizeW: 9, UvSizeH: 21, UvSizeD: 1, MirrorUv: true).Emit(b, ribLeft, p.BodyScale);

        var head = EntityParityTemplate.Mul(body, EntityParityTemplate.T(0f, -13f, 0f));
        new EntityCuboid(-8f, -16f, -5f, 8f, 0f, 5f, 0, 32, UvSizeW: 16, UvSizeH: 16, UvSizeD: 10).Emit(b, head, p.HeadScale);
        var jaw = EntityParityTemplate.Mul(head, EntityParityTemplate.T(0f, 4f, -8f));
        new EntityCuboid(-6f, 0f, -16f, 6f, 4f, 0f, 176, 65, UvSizeW: 12, UvSizeH: 4, UvSizeD: 16).Emit(b, jaw, p.HeadScale);

        var tendrilRight = EntityParityTemplate.Mul(head, EntityParityTemplate.T(-8f, -12f, 0f));
        new EntityCuboid(-16f, -13f, -0.5f, 0f, 3f, 0.5f, 52, 32, UvSizeW: 16, UvSizeH: 16, UvSizeD: 1).Emit(b, tendrilRight, p.HeadScale);
        var tendrilLeft = EntityParityTemplate.Mul(head, EntityParityTemplate.T(8f, -12f, 0f));
        new EntityCuboid(0f, -13f, -0.5f, 16f, 3f, 0.5f, 58, 0, UvSizeW: 16, UvSizeH: 16, UvSizeD: 1).Emit(b, tendrilLeft, p.HeadScale);

        var armRight = EntityParityTemplate.Mul(body, EntityParityTemplate.T(-13f, -13f, 1f));
        new EntityCuboid(-4f, 0f, -4f, 4f, 28f, 4f, 44, 50, UvSizeW: 8, UvSizeH: 28, UvSizeD: 8).Emit(b, armRight, p.LegScale);
        var armLeft = EntityParityTemplate.Mul(body, EntityParityTemplate.T(13f, -13f, 1f));
        new EntityCuboid(-4f, 0f, -4f, 4f, 28f, 4f, 0, 58, UvSizeW: 8, UvSizeH: 28, UvSizeD: 8).Emit(b, armLeft, p.LegScale);

        var legSway = MathF.Sin(sway) * 0.12f;
        var legRight = EntityParityTemplate.Mul(bone, EntityParityTemplate.T(-5.9f, -13f, 0f));
        new EntityCuboid(-3.1f, 0f, -3f, 2.9f, 13f, 3f, 76, 48, UvSizeW: 6, UvSizeH: 13, UvSizeD: 6, XRot: legSway, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(0f, 13f, 0f) }.Emit(b, legRight, p.LegScale);
        var legLeft = EntityParityTemplate.Mul(bone, EntityParityTemplate.T(5.9f, -13f, 0f));
        new EntityCuboid(-2.9f, 0f, -3f, 3.1f, 13f, 3f, 76, 76, UvSizeW: 6, UvSizeH: 13, UvSizeD: 6, XRot: -legSway, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(0f, 13f, 0f) }.Emit(b, legLeft, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>EnderDragonModel.createBodyLayer</c> — Java 1.21.11 client <c>hec.a()</c> (<c>256×256</c>): root head <c>T(0,20,-62)</c> + jaw;
    /// neck <c>5×</c> <c>10×10×10</c> @ <c>(0,20,-12−10i)</c>; tail <c>12×</c> same cuboid @ <c>(0,10,60+10i)</c>;
    /// body <c>T(0,3,8)</c> + <c>(-12,1,-16)+24×24×64</c> + three dorsal spikes <c>texOffs(220,53)</c>;
    /// wings <c>56×8×8</c> / tips <c>56×4×4</c> with preview <paramref name="wingSweep"/> yaw on wing roots;
    /// legs mirror vanilla three-bone chains (front <c>8×24×8</c>, hind <c>16×32×16</c> + tips + feet).
    /// </summary>
    private static MergedJavaBlockModel BuildEnderDragon(string texRef, MinecraftNativeProfile profile, bool isBaby, float wingSweep)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.68f, 1.12f, 0.74f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.76f, 1.06f, 0.80f) : BabyProfile.Adult);
        var b = new RigBuilder(256, 256);
        var root = Matrix4x4.Identity;
        var headPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20f, -62f));
        new EntityCuboid(-6f, -1f, -24f, 6f, 4f, -8f, 176, 44, UvSizeW: 12, UvSizeH: 5, UvSizeD: 16).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-8f, -8f, -10f, 8f, 8f, 6f, 112, 30, UvSizeW: 16, UvSizeH: 16, UvSizeD: 16).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-5f, -12f, -4f, -3f, -8f, 2f, 0, 0, UvSizeW: 2, UvSizeH: 4, UvSizeD: 6).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-5f, -3f, -22f, -3f, -1f, -18f, 112, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 4).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(3f, -12f, -4f, 5f, -8f, 2f, 0, 0, UvSizeW: 2, UvSizeH: 4, UvSizeD: 6).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(3f, -3f, -22f, 5f, -1f, -18f, 112, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 4).Emit(b, headPose, p.HeadScale);
        var jawPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(0f, 4f, -8f));
        new EntityCuboid(-6f, 0f, -16f, 6f, 4f, 0f, 176, 65, UvSizeW: 12, UvSizeH: 4, UvSizeD: 16).Emit(b, jawPose, p.HeadScale);

        for (var i = 0; i < 5; i++)
        {
            var neckPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20f, -12f - 10f * i));
            new EntityCuboid(-5f, -5f, -5f, 5f, 5f, 5f, 192, 104, UvSizeW: 10, UvSizeH: 10, UvSizeD: 10).Emit(b, neckPose, p.BodyScale);
        }

        for (var i = 0; i < 12; i++)
        {
            var tailPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 10f, 60f + 10f * i));
            new EntityCuboid(-5f, -5f, -5f, 5f, 5f, 5f, 192, 104, UvSizeW: 10, UvSizeH: 10, UvSizeD: 10).Emit(b, tailPose, p.LegScale);
        }

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 3f, 8f));
        new EntityCuboid(-12f, 1f, -16f, 12f, 25f, 48f, 0, 0, UvSizeW: 24, UvSizeH: 24, UvSizeD: 64).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-1f, -5f, -10f, 1f, 1f, 2f, 220, 53, UvSizeW: 2, UvSizeH: 6, UvSizeD: 12).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-1f, -5f, 10f, 1f, 1f, 22f, 220, 53, UvSizeW: 2, UvSizeH: 6, UvSizeD: 12).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-1f, -5f, 30f, 1f, 1f, 42f, 220, 53, UvSizeW: 2, UvSizeH: 6, UvSizeD: 12).Emit(b, bodyPose, p.BodyScale);

        var leftWingRoot = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(12f, 2f, -6f), EntityParityTemplate.Ry(wingSweep)));
        new EntityCuboid(0f, -4f, -4f, 56f, 4f, 4f, 112, 88, UvSizeW: 56, UvSizeH: 8, UvSizeD: 8).Emit(b, leftWingRoot, p.BodyScale);
        var leftWingTip = EntityParityTemplate.Mul(leftWingRoot, EntityParityTemplate.T(56f, 0f, 0f));
        new EntityCuboid(0f, -2f, -2f, 56f, 2f, 2f, 112, 136, UvSizeW: 56, UvSizeH: 4, UvSizeD: 4).Emit(b, leftWingTip, p.BodyScale);

        var rightWingRoot = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(-12f, 2f, -6f), EntityParityTemplate.Ry(-wingSweep)));
        new EntityCuboid(-56f, -4f, -4f, 0f, 4f, 4f, 112, 88, UvSizeW: 56, UvSizeH: 8, UvSizeD: 8, MirrorUv: true).Emit(b, rightWingRoot, p.BodyScale);
        var rightWingTip = EntityParityTemplate.Mul(rightWingRoot, EntityParityTemplate.T(-56f, 0f, 0f));
        new EntityCuboid(-56f, -2f, -2f, 0f, 2f, 2f, 112, 136, UvSizeW: 56, UvSizeH: 4, UvSizeD: 4, MirrorUv: true).Emit(b, rightWingTip, p.BodyScale);

        AppendEnderDragonLegChain(b, bodyPose, p, left: true, front: true);
        AppendEnderDragonLegChain(b, bodyPose, p, left: false, front: true);
        AppendEnderDragonLegChain(b, bodyPose, p, left: true, front: false);
        AppendEnderDragonLegChain(b, bodyPose, p, left: false, front: false);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static void AppendEnderDragonLegChain(RigBuilder b, Matrix4x4 bodyPose, BabyProfile p, bool left, bool front)
    {
        if (front)
        {
            var x = left ? 12f : -12f;
            var thigh = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(x, 17f, -6f), EntityParityTemplate.Er(1.3f, 0f, 0f)));
            new EntityCuboid(-4f, -4f, -4f, 4f, 20f, 4f, 112, 104, UvSizeW: 8, UvSizeH: 24, UvSizeD: 8).Emit(b, thigh, p.LegScale);
            var shin = EntityParityTemplate.Mul(thigh, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 20f, -1f), EntityParityTemplate.Er(-0.5f, 0f, 0f)));
            new EntityCuboid(-3f, -1f, -3f, 3f, 23f, 3f, 226, 138, UvSizeW: 6, UvSizeH: 24, UvSizeD: 6).Emit(b, shin, p.LegScale);
            var foot = EntityParityTemplate.Mul(shin, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 23f, 0f), EntityParityTemplate.Er(0.75f, 0f, 0f)));
            new EntityCuboid(-4f, 0f, -12f, 4f, 4f, 4f, 144, 104, UvSizeW: 8, UvSizeH: 4, UvSizeD: 16).Emit(b, foot, p.LegScale);
        }
        else
        {
            var x = left ? 16f : -16f;
            var thigh = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(x, 13f, 34f), EntityParityTemplate.Er(1f, 0f, 0f)));
            new EntityCuboid(-8f, -4f, -8f, 8f, 28f, 8f, 0, 0, UvSizeW: 16, UvSizeH: 32, UvSizeD: 16).Emit(b, thigh, p.LegScale);
            var shin = EntityParityTemplate.Mul(thigh, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 32f, -4f), EntityParityTemplate.Er(0.5f, 0f, 0f)));
            new EntityCuboid(-6f, -2f, -6f, 6f, 30f, 6f, 196, 0, UvSizeW: 12, UvSizeH: 32, UvSizeD: 12).Emit(b, shin, p.LegScale);
            var foot = EntityParityTemplate.Mul(shin, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 31f, 4f), EntityParityTemplate.Er(0.75f, 0f, 0f)));
            new EntityCuboid(-9f, 0f, -20f, 9f, 6f, 4f, 112, 0, UvSizeW: 18, UvSizeH: 6, UvSizeD: 24).Emit(b, foot, p.LegScale);
        }
    }
}
