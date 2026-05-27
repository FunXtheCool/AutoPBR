using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{


    private static MergedJavaBlockModel BuildGuardian(string texRef, MinecraftNativeProfile profile, bool isBaby, float spinePulse, float geometryScale = 1f)
    {
        // GuardianModel (1.21.11 obf. hek): compound head (12×12×16 core + 2×12×12 sides + 12×2×12 lids), 12 spikes `2×9×2` @ (0,0)
        // with pose arrays + animated xyz from limbSwing/spine retraction; eye `2×2×1` @ (8,0); tail chain under head.
        // Elder mesh uses LayerDefinition.e() with MeshTransformer.scaling(2.35f) — baked here as geometryScale only.
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.72f, 1.04f, 0.74f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.0f, 0.82f) : BabyProfile.Adult);
        var bs = p.BodyScale * geometryScale;
        var hs = p.HeadScale * geometryScale;
        var b = new RigBuilder(64, 64);

        static Matrix4x4 GuardianEuler(float xRot, float yRot, float zRot)
        {
            if (xRot == 0f && yRot == 0f && zRot == 0f)
            {
                return Matrix4x4.Identity;
            }

            return Matrix4x4.Multiply(
                Matrix4x4.CreateRotationZ(zRot),
                Matrix4x4.Multiply(Matrix4x4.CreateRotationY(yRot), Matrix4x4.CreateRotationX(xRot)));
        }

        float[] spikeRx =
        [
            1.75f, 0.25f, 0f, 0f, 0.5f, 0.5f, 0.5f, 0.5f, 1.25f, 0.75f, 0f, 0f
        ];
        float[] spikeRy =
        [
            0f, 0f, 0f, 0f, 0.25f, 1.75f, 1.25f, 0.75f, 0f, 0f, 0f, 0f
        ];
        float[] spikeRz =
        [
            0f, 0f, 0.25f, 1.75f, 0f, 0f, 0f, 0f, 0f, 0f, 0.75f, 1.25f
        ];
        float[] spikeFx =
        [
            0f, 0f, 8f, -8f, -8f, 8f, 8f, -8f, 0f, 0f, 8f, -8f
        ];
        float[] spikeFy =
        [
            -8f, -8f, -8f, -8f, 0f, 0f, 0f, 0f, 8f, 8f, 8f, 8f
        ];
        float[] spikeFz =
        [
            8f, -8f, 0f, 0f, -8f, -8f, 8f, 8f, 8f, -8f, 0f, 0f
        ];

        var limbSwing = spinePulse * 8f;
        var spineRetract = (1f - spinePulse) * 0.55f;
        var tailSwing = spinePulse * MathF.PI * 4f;
        var tailRy0 = MathF.Sin(tailSwing) * MathF.PI * 0.05f;
        var tailRy1 = MathF.Sin(tailSwing) * MathF.PI * 0.1f;
        var tailRy2 = MathF.Sin(tailSwing) * MathF.PI * 0.15f;

        float SpikeAnimTerm(int idx)
        {
            var w = 1f + 0.01f * MathF.Cos(limbSwing * 1.5f + idx) - spineRetract;
            return w;
        }

        float SpikeTx(int i) => spikeFx[i] * SpikeAnimTerm(i);
        float SpikeTy(int i) => 16f + spikeFy[i] * SpikeAnimTerm(i);
        float SpikeTz(int i) => spikeFz[i] * SpikeAnimTerm(i);

        // Head volume (child space of "head"; vanilla PartPose.ZERO on head root).
        new EntityCuboid(-6f, 10f, -8f, 6f, 22f, 8f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, hs);
        new EntityCuboid(-8f, 10f, -6f, -6f, 22f, 6f, 0, 28, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, hs);
        new EntityCuboid(6f, 10f, -6f, 8f, 22f, 6f, 16, 40, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, hs);
        new EntityCuboid(-6f, 8f, -6f, 6f, 10f, 6f, 16, 40, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, hs);
        new EntityCuboid(-6f, 22f, -6f, 6f, 24f, 6f, 16, 40, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, hs);

        for (var i = 0; i < 12; i++)
        {
            var pose = Matrix4x4.Multiply(
                Matrix4x4.CreateTranslation(SpikeTx(i), SpikeTy(i), SpikeTz(i)),
                GuardianEuler(MathF.PI * spikeRx[i], MathF.PI * spikeRy[i], MathF.PI * spikeRz[i]));
            new EntityCuboid(-1f, -4.5f, -1f, 1f, 4.5f, 1f, 0, 0).Emit(b, pose, bs);
        }

        new EntityCuboid(-1f, 15f, 0f, 1f, 17f, 1f, 8, 0).Emit(b, Matrix4x4.CreateTranslation(0f, 0f, -8.25f), hs);

        var tail0 = Matrix4x4.CreateRotationY(tailRy0);
        new EntityCuboid(-2f, 14f, 7f, 2f, 18f, 15f, 40, 0).Emit(b, tail0, bs);

        var tail1 = Matrix4x4.Multiply(tail0, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-1.5f, 0.5f, 14f), Matrix4x4.CreateRotationY(tailRy1)));
        new EntityCuboid(0f, 14f, 0f, 3f, 17f, 7f, 0, 54).Emit(b, tail1, bs);

        var tail2 = Matrix4x4.Multiply(tail1, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0.5f, 0.5f, 6f), Matrix4x4.CreateRotationY(tailRy2)));
        new EntityCuboid(0f, 14f, 0f, 2f, 16f, 6f, 41, 32).Emit(b, tail2, bs);
        new EntityCuboid(1f, 10.5f, 3f, 10f, 19.5f, 12f, 25, 19).Emit(b, tail2, bs);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>TurtleModel.createBodyLayer</c> — Java 1.21.11 client <c>hcu.a()</c> (<c>128×64</c>): head <c>6×5×6</c> @ <c>texOffs(3,0)</c> <c>T(0,19,-10)</c>;
    /// <c>body</c> / <c>egg_belly</c> share <c>PartPose.offsetAndRotation(0,11,-10, π/2,0,0)</c> — shell <c>19×20×6</c> <c>(7,37)</c>, belly <c>11×18×3</c> <c>(31,1)</c>, egg belly <c>9×18×1</c> <c>(70,33)</c>;
    /// flippers: hind <c>4×1×10</c> <c>(1,23)/(1,12)</c> at <c>(∓3.5,22,11)</c>; front <c>13×1×5</c> <c>(27,30)/(27,24)</c> at <c>(∓5,21,-4)</c>.
    /// </summary>
    private static MergedJavaBlockModel BuildTurtle(string texRef, MinecraftNativeProfile profile, bool isBaby, float swimLift)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildBabyTurtle(texRef, swimLift);
        }

        var p = isBaby ? new BabyProfile(0.84f, 1.0f, 0.84f) : BabyProfile.Adult;
        var b = new RigBuilder(128, 64);
        var root = Matrix4x4.Identity;

        var headPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 19f, -10f));
        new EntityCuboid(-3f, -1f, -3f, 3f, 4f, 3f, 3, 0, UvSizeW: 6, UvSizeH: 5, UvSizeD: 6, XRot: 0f, YRot: swimLift * 0.08f, ZRot: 0f) { RotationPivot = new Vector3(0f, 1.5f, 0f) }.Emit(b, headPose, p.HeadScale);

        var carapacePose = EntityParityTemplate.Mul(
            root,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 11f, -10f), EntityParityTemplate.Rx(MathF.PI / 2f)));
        new EntityCuboid(-9.5f, 3f, -10f, 9.5f, 23f, -4f, 7, 37, UvSizeW: 19, UvSizeH: 20, UvSizeD: 6, OffsetX: 0f, OffsetY: swimLift * 0.12f, OffsetZ: 0f).Emit(b, carapacePose, p.BodyScale);
        new EntityCuboid(-5.5f, 3f, -13f, 5.5f, 21f, -10f, 31, 1, UvSizeW: 11, UvSizeH: 18, UvSizeD: 3, OffsetX: 0f, OffsetY: swimLift * 0.12f, OffsetZ: 0f).Emit(b, carapacePose, p.BodyScale);
        new EntityCuboid(-4.5f, 3f, -14f, 4.5f, 21f, -13f, 70, 33, UvSizeW: 9, UvSizeH: 18, UvSizeD: 1, OffsetX: 0f, OffsetY: swimLift * 0.12f, OffsetZ: 0f).Emit(b, carapacePose, p.BodyScale);

        var hindRight = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-3.5f, 22f, 11f));
        new EntityCuboid(-2f, 0f, 0f, 2f, 1f, 10f, 1, 23, UvSizeW: 4, UvSizeH: 1, UvSizeD: 10, XRot: 0f, YRot: 0f, ZRot: -swimLift * 0.1f) { RotationPivot = new Vector3(0f, 0.5f, 5f) }.Emit(b, hindRight, p.LegScale);
        var hindLeft = EntityParityTemplate.Mul(root, EntityParityTemplate.T(3.5f, 22f, 11f));
        new EntityCuboid(-2f, 0f, 0f, 2f, 1f, 10f, 1, 12, UvSizeW: 4, UvSizeH: 1, UvSizeD: 10, MirrorUv: true, XRot: 0f, YRot: 0f, ZRot: swimLift * 0.1f) { RotationPivot = new Vector3(0f, 0.5f, 5f) }.Emit(b, hindLeft, p.LegScale);

        var frontRight = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-5f, 21f, -4f));
        new EntityCuboid(-13f, 0f, -2f, 0f, 1f, 3f, 27, 30, UvSizeW: 13, UvSizeH: 1, UvSizeD: 5, XRot: 0f, YRot: 0f, ZRot: swimLift * 0.12f) { RotationPivot = new Vector3(-6.5f, 0.5f, 0.5f) }.Emit(b, frontRight, p.LegScale);
        var frontLeft = EntityParityTemplate.Mul(root, EntityParityTemplate.T(5f, 21f, -4f));
        new EntityCuboid(0f, 0f, -2f, 13f, 1f, 3f, 27, 24, UvSizeW: 13, UvSizeH: 1, UvSizeD: 5, MirrorUv: true, XRot: 0f, YRot: 0f, ZRot: -swimLift * 0.12f) { RotationPivot = new Vector3(6.5f, 0.5f, 0.5f) }.Emit(b, frontLeft, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>PufferfishSmallModel.createBodyLayer</c> — Java 1.21.11 client <c>hbb.a()</c> (<c>32×32</c>): <c>body</c> <c>3×2×3</c> @ <c>(0,27)</c> <c>T(0,23,0)</c>;
    /// <c>right_eye</c>/<c>left_eye</c> <c>1³</c> @ <c>(24,6)</c>/<c>(28,6)</c> + <c>T(0,20,0)</c> with mirrored X;
    /// <c>back_fin</c> <c>3×0×3</c> @ <c>(-3,0)</c> → preview thickness; side fins <c>1×0×2</c> @ <c>(25,0)</c> with <c>T(∓1.5,22,∓1.5)</c>; <c>setupAnim</c> fin flap on <c>right_fin</c>/<c>left_fin</c> parts (preview uses static mesh). <paramref name="puff"/> applies a small extra scale on the body only.
    /// </summary>
    private static MergedJavaBlockModel BuildPufferfish(string texRef, MinecraftNativeProfile profile, bool isBaby, float puff)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.84f, 1.0f, 0.84f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.92f, 1.0f, 0.92f) : BabyProfile.Adult);
        var b = new RigBuilder(32, 32);
        var bodyPose = EntityParityTemplate.T(0f, 23f, 0f);
        new EntityCuboid(-1.5f, -2f, -1.5f, 1.5f, 0f, 1.5f, 0, 27, UvSizeW: 3, UvSizeH: 2, UvSizeD: 3).Emit(b, bodyPose, p.BodyScale * (1f + puff * 0.02f));

        var eyePose = EntityParityTemplate.T(0f, 20f, 0f);
        new EntityCuboid(-1.5f, 0f, -1.5f, -0.5f, 1f, -0.5f, 24, 6, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, eyePose, p.LegScale);
        new EntityCuboid(0.5f, 0f, -1.5f, 1.5f, 1f, -0.5f, 28, 6, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, eyePose, p.LegScale);

        var backFinPose = EntityParityTemplate.T(0f, 22f, 1.5f);
        new EntityCuboid(-1.5f, -0.5f, 0f, 1.5f, 0.5f, 3f, 29, 0, UvSizeW: 3, UvSizeH: 1, UvSizeD: 3).Emit(b, backFinPose, p.BodyScale);

        var rightFinPose = EntityParityTemplate.T(-1.5f, 22f, -1.5f);
        new EntityCuboid(-1f, -1f, 0f, 1f, 1f, 4f, 25, 0, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2).Emit(b, rightFinPose, p.LegScale);
        var leftFinPose = EntityParityTemplate.T(1.5f, 22f, -1.5f);
        new EntityCuboid(-1f, -1f, 0f, 1f, 1f, 4f, 25, 0, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2, MirrorUv: true).Emit(b, leftFinPose, p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildSquid(string texRef, MinecraftNativeProfile profile, bool isBaby, float tentacleWave)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildBabySquid(texRef, tentacleWave);
        }

        var p = isBaby ? new BabyProfile(0.78f, 1.0f, 0.80f) : BabyProfile.Adult;
        var b = new RigBuilder(64, 32);
        // SquidModel.createBodyLayer (1.21.11 client <c>hcs</c>): body <c>texOffs(0,0)</c> <c>addBox(-6,-8,-6, 12,16,12)</c> + <c>CubeDeformation(0.02f)</c>,
        // child <c>PartPose.offset(0,8,0)</c>; eight tentacles <c>texOffs(48,0)</c> <c>addBox(-1,0,-1, 2,18,2)</c> with
        // <c>offset(5·cos(i·2π/8), 15, 5·sin(i·2π/8))</c> and <c>yRot = π/2 − i·2π/8</c>. <c>GlowSquidRenderer</c> reuses this model.
        // Renderer <c>ModelTransforms.scaling(0.5f)</c> on <c>SquidModel</c> is not folded here (same policy as ghast root scale).
        const float bodyD = 0.02f;
        var bodyPose = Matrix4x4.CreateTranslation(0f, 8f, 0f);
        new EntityCuboid(-6f - bodyD, -8f - bodyD, -6f - bodyD, 6f + bodyD, 8f + bodyD, 6f + bodyD, 0, 0, UvSizeW: 12, UvSizeH: 16, UvSizeD: 12).Emit(b, bodyPose, p.BodyScale);
        for (var i = 0; i < 8; i++)
        {
            var theta = i * MathF.PI / 4f;
            var rx = 5f * MathF.Cos(theta);
            var rz = 5f * MathF.Sin(theta);
            var yRot = MathF.PI / 2f - theta;
            var sway = tentacleWave * (i % 2 == 0 ? 0.8f : -0.7f);
            var tentaclePose = Matrix4x4.Multiply(
                Matrix4x4.CreateTranslation(rx, 15f, rz),
                Matrix4x4.CreateRotationY(yRot + sway));
            new EntityCuboid(-1f, 0f, -1f, 1f, 18f, 1f, 48, 0).Emit(b, tentaclePose, p.LegScale);
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// NautilusModel (1.21.11 javap hby): shell + body core cuboids in model space.
    /// Swim motion uses vanilla <c>NautilusAnimation.SWIMMING</c> (26.1.2): <c>body</c> scale from
    /// <see cref="DefinitionAnimationPreviewSampling"/> for swim channels; <c>upper_mouth</c> pitch X
    /// prefers LINEAR samples from shipped animation IR when available.
    /// </summary>
    private static MergedJavaBlockModel BuildNautilusMob(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float animationTimeSeconds)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.72f, 1.06f, 0.74f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.02f, 0.82f) : BabyProfile.Adult);
        var b = new RigBuilder(128, 128);
        new EntityCuboid(-7f, -10f, -7f, 7f, 0f, 9f, 0, 26).Emit(b, Matrix4x4.Identity, p.BodyScale);

        var bodyScale = DefinitionAnimationPreviewSampling.SampleScale(
            profile,
            "net.minecraft.client.animation.definitions.NautilusAnimation",
            "SWIMMING",
            "body",
            animationTimeSeconds,
            out var bodyScaleVec)
            ? bodyScaleVec
            : Vector3.One;
        var upperJawDegX = DefinitionAnimationPreviewSampling.SampleRotationDegrees(
            profile,
            "net.minecraft.client.animation.definitions.NautilusAnimation",
            "SWIMMING",
            "upper_mouth",
            animationTimeSeconds,
            out var jawEuler)
            ? jawEuler.X
            : 0f;
        const float jawTiltBlend = 0.38f;
        var tiltRad = upperJawDegX * (MathF.PI / 180f) * jawTiltBlend;
        var pivot = new Vector3(0f, (-4.51f + 3.49f) * 0.5f, (-3f + 11f) * 0.5f);
        var innerPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(pivot.X, pivot.Y, pivot.Z),
            EntityParityTemplate.Mul(
                Matrix4x4.CreateScale(bodyScale.X, bodyScale.Y, bodyScale.Z),
                EntityParityTemplate.Mul(EntityParityTemplate.Er(tiltRad, 0f, 0f), EntityParityTemplate.T(-pivot.X, -pivot.Y, -pivot.Z))));
        new EntityCuboid(-5f, -4.51f, -3f, 5f, 3.49f, 11f, 0, 76).Emit(b, innerPose, p.BodyScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

}
