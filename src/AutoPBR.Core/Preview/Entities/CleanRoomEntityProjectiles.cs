using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Projectiles, effects, and misc non-mob entities.

    /// <summary>
    /// Vanilla <c>ShulkerBulletModel</c> (26.1.2): three <c>addBox</c> on <c>main</c>; <c>setupAnim</c> sets <c>main.yRot</c>/<c>xRot</c> from <c>ShulkerBulletRenderState</c> (degrees × <c>π/180</c>).
    /// </summary>
    private static MergedJavaBlockModel BuildShulkerBullet(string texRef, MinecraftNativeProfile profile, bool isBaby, float yRotDegrees, float xRotDegrees)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        var deg = MathF.PI / 180f;
        var rot = Matrix4x4.Multiply(
            Matrix4x4.CreateRotationY(yRotDegrees * deg),
            Matrix4x4.CreateRotationX(xRotDegrees * deg));
        new EntityCuboid(-4f, -4f, -1f, 4f, 4f, 1f, 0, 0).Emit(b, rot, 1f);
        new EntityCuboid(-1f, -4f, -4f, 1f, 4f, 4f, 0, 10).Emit(b, rot, 1f);
        new EntityCuboid(-4f, -1f, -4f, 4f, 1f, 4f, 20, 0).Emit(b, rot, 1f);
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildEndCrystal(string texRef, MinecraftNativeProfile profile, bool isBaby, float spin)
    {
        // EndCrystalModel (1.21.11 obf. hgx): base (0,16) 12×4×12; outer glass 8³ @ T(0,24,0)+Ry(spin); inner ×0.875; core (32,0) 8³ ×0.765625.
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        new EntityCuboid(-6f, 0f, -6f, 6f, 4f, 6f, 0, 16).Emit(b, Matrix4x4.Identity, 1f); // base

        var outer = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 24f, 0f), Matrix4x4.CreateRotationY(spin * (MathF.PI / 180f)));
        new EntityCuboid(-4f, -4f, -4f, 4f, 4f, 4f, 0, 0).Emit(b, outer, 1f); // outer glass

        var inner = Matrix4x4.Multiply(outer, Matrix4x4.CreateScale(0.875f));
        new EntityCuboid(-4f, -4f, -4f, 4f, 4f, 4f, 0, 0).Emit(b, inner, 1f); // inner glass

        var cube = Matrix4x4.Multiply(inner, Matrix4x4.CreateScale(0.765625f));
        new EntityCuboid(-4f, -4f, -4f, 4f, 4f, 4f, 32, 0).Emit(b, cube, 1f); // core cube
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildEvokerFangs(string texRef, MinecraftNativeProfile profile, bool isBaby, float bitePhase)
    {
        // EvokerFangsModel (1.21.11 obf. hcy): root scales + lifts when bite>0.9; base y -= (bite+sin(bite*2.7))*7.2;
        // jaws use PartPose yRot 2.042035 / 4.2411504 with bite-driven zRot π ± open*0.35*π (open = 1 - min(bite*2,1)³).
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        var bite = bitePhase;
        var open = 1f - MathF.Pow(MathF.Min(1f, bite * 2f), 3f);
        var bob = (bite + MathF.Sin(bite * 2.7f)) * 7.2f;
        var baseLocalY = 24f - bob;
        var shrink = bite > 0.9f ? (1f - bite) / 0.1f : 1f;
        var rootY = 24f - 20f * shrink;
        var rootMat = Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(0f, rootY, 0f),
            Matrix4x4.Multiply(Matrix4x4.CreateScale(shrink), Matrix4x4.CreateTranslation(-5f, baseLocalY, -5f)));
        new EntityCuboid(0f, 0f, 0f, 10f, 12f, 10f, 0, 0).Emit(b, rootMat, 1f);
        new EntityCuboid(0f, 0f, 0f, 4f, 14f, 8f, 40, 0, XRot: 0f, YRot: 2.042035f, ZRot: MathF.PI - open * 0.35f * MathF.PI).Emit(b, Matrix4x4.Multiply(rootMat, Matrix4x4.CreateTranslation(6.5f, 0f, 1f)), 1f);
        new EntityCuboid(0f, 0f, 0f, 4f, 14f, 8f, 40, 0, XRot: MathF.PI, YRot: 4.2411504f, ZRot: MathF.PI + open * 0.35f * MathF.PI).Emit(b, Matrix4x4.Multiply(rootMat, Matrix4x4.CreateTranslation(3.5f, 0f, 9f)), 1f);
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildLlamaSpit(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        // LlamaSpitModel (1.21.11 obf. hbv): seven addBox 2×2×2 on "main" at origin star — offsets (-4,0,0)/(0,-4,0)/(0,0,-4)/(0,0,0)/(2,0,0)/(0,2,0)/(0,0,2).
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        // Seven 2×2×2 cubes (same UV footprint each).
        new EntityCuboid(-4f, 0f, 0f, -2f, 2f, 2f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(0f, -4f, 0f, 2f, -2f, 2f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(0f, 0f, -4f, 2f, 2f, -2f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(0f, 0f, 0f, 2f, 2f, 2f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(2f, 0f, 0f, 4f, 2f, 2f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(0f, 2f, 0f, 2f, 4f, 2f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(0f, 0f, 2f, 2f, 2f, 4f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        return b.Build(texRef);
    }

    /// <summary>
    /// <c>ArrowModel.createBodyLayer</c> — Java 1.21.11 client <c>hhe.a()</c> (<c>32×32</c>): <c>back</c> sheet <c>5×5×0</c> @ <c>(0,0)</c>
    /// with <c>PartPose.offsetAndRotation(-11,0,0, π/4,0,0)</c> + uniform scale <c>0.8</c>; <c>cross_1</c>/<c>cross_2</c> share a <c>16×4×0</c> slab (inflate path) at <c>Rx(±π/4)</c> on root. Zero-depth faces use preview thickness <c>1</c> with integer UV.
    /// </summary>
    private static MergedJavaBlockModel BuildArrow(string texRef, MinecraftNativeProfile profile, bool isBaby, float wobble)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(32, 32);
        var xBob = wobble * 0.02f;
        var backPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(-11f, 0f, 0f),
            EntityParityTemplate.Rx(0.7853982f + xBob));
        // Vanilla `5×5×0` in Z — preview thickness `1` at `z≈-2.5` with integer UV footprint.
        new EntityCuboid(0f, -2.5f, -2.5f, 5f, 2.5f, -1.5f, 0, 0, UvSizeW: 5, UvSizeH: 5, UvSizeD: 1).Emit(b, backPose, 0.8f);

        var cross1Pose = EntityParityTemplate.Rx(0.7853982f + xBob);
        var cross2Pose = EntityParityTemplate.Rx(2.3561945f + xBob);
        new EntityCuboid(-12f, -2f, -0.5f, 4f, 2f, 0.5f, 0, 0, UvSizeW: 16, UvSizeH: 4, UvSizeD: 1).Emit(b, cross1Pose, 1f);
        new EntityCuboid(-12f, -2f, -0.5f, 4f, 2f, 0.5f, 0, 0, UvSizeW: 16, UvSizeH: 4, UvSizeD: 1).Emit(b, cross2Pose, 1f);
        return b.Build(texRef);
    }

    /// <summary>
    /// <c>WindChargeModel.createBodyLayer</c> — Java 1.21.11 client <c>hhh.a()</c> (<c>64×32</c>): <c>bone</c> root;
    /// <c>wind</c> child with <c>PartPose.offsetAndRotation(0,0,0, 0, −π/4, 0)</c> and two inflated cubes <c>8×2×8</c> @ <c>(15,20)</c>, <c>6×4×6</c> @ <c>(0,9)</c>;
    /// <c>wind_charge</c> <c>4³</c> @ <c>(0,0)</c>. Preview applies opposite yaw multipliers to match vanilla <c>setupAnim</c> spin sign.
    /// </summary>
    private static MergedJavaBlockModel BuildWindCharge(string texRef, MinecraftNativeProfile profile, bool isBaby, float spin)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        var bone = Matrix4x4.Identity;
        var spinRad = spin * 16f * (MathF.PI / 180f);
        var windPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(bone, EntityParityTemplate.Ry(spinRad)),
            EntityParityTemplate.Ry(-MathF.PI / 4f));
        new EntityCuboid(-4f, -1f, -4f, 4f, 1f, 4f, 15, 20, UvSizeW: 8, UvSizeH: 2, UvSizeD: 8).Emit(b, windPose, 1f);
        new EntityCuboid(-3f, -2f, -3f, 3f, 2f, 3f, 0, 9, UvSizeW: 6, UvSizeH: 4, UvSizeD: 6).Emit(b, windPose, 1f);
        var chargePose = EntityParityTemplate.Mul(bone, EntityParityTemplate.Ry(-spinRad));
        new EntityCuboid(-2f, -2f, -2f, 2f, 2f, 2f, 0, 0, UvSizeW: 4, UvSizeH: 4, UvSizeD: 4).Emit(b, chargePose, 1f);
        return b.Build(texRef);
    }

    /// <summary>TridentModel (1.21.11 <c>javap</c> <c>hhg</c>): <c>32×32</c>; <c>pole</c> <c>1×25×1</c> @ <c>(0,6)</c>; <c>base</c> <c>3×2×1</c> @ <c>(4,0)</c>; spikes <c>1×4×1</c> @ <c>(4,3)</c>/<c>(0,0)</c> with <c>right_spike</c> from mirrored builder.</summary>
    private static MergedJavaBlockModel BuildTrident(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(32, 32);
        new EntityCuboid(-0.5f, 2f, -0.5f, 0.5f, 27f, 0.5f, 0, 6).Emit(b, Matrix4x4.Identity, 1f); // pole 1x25x1
        new EntityCuboid(-1.5f, 0f, -0.5f, 1.5f, 2f, 0.5f, 4, 0).Emit(b, Matrix4x4.Identity, 1f); // base 3x2x1
        new EntityCuboid(-2.5f, -3f, -0.5f, -1.5f, 1f, 0.5f, 4, 3).Emit(b, Matrix4x4.Identity, 1f); // left spike
        new EntityCuboid(-0.5f, -4f, -0.5f, 0.5f, 0f, 0.5f, 0, 0).Emit(b, Matrix4x4.Identity, 1f); // middle spike
        new EntityCuboid(1.5f, -3f, -0.5f, 2.5f, 1f, 0.5f, 4, 3).Emit(b, Matrix4x4.Identity, 1f); // right spike
        return b.Build(texRef);
    }

    /// <summary>BeeStingerModel (~Java 1.21.11 <c>hab</c>): two crossed blades sharing one cuboid UV (0,0); atlas <c>16×16</c>. Vanilla Z thickness is 0 — rendered as thin solid for preview bake.</summary>
    private static MergedJavaBlockModel BuildBeeStinger(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.78f, 1.0f, 0.78f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.86f, 1.0f, 0.86f) : BabyProfile.Adult);
        var b = new RigBuilder(16, 16);
        const float thin = 0.06f;
        void AddBlade(Matrix4x4 localPose) =>
            new EntityCuboid(-1f, -0.5f, -thin, 2f, 1f, thin, 0, 0, UvSizeW: 3, UvSizeH: 1, UvSizeD: 1).Emit(b, localPose, p.BodyScale);

        AddBlade(Matrix4x4.CreateRotationX(MathF.PI / 4f));
        AddBlade(Matrix4x4.CreateRotationX(3f * MathF.PI / 4f));
        return b.Build(texRef);
    }

    /// <summary>
    /// DragonFireballRenderer (1.21.11 <c>javap</c> <c>hwh</c>): no <c>EntityModel</c>; a <c>1×1</c> XY quad
    /// (<c>(−0.5,−0.25)</c>–<c>(0.5,0.75)</c> at <c>z=0</c>) then <c>scale(2,2,2)</c> and camera billboard.
    /// Preview uses one thin north/south slab (single plane pair), not a solid cube.
    /// </summary>
    private static MergedJavaBlockModel BuildDragonFireball(string texRef, MinecraftNativeProfile profile, bool isBaby, float framePick01)
    {
        _ = profile;
        _ = isBaby;
        _ = framePick01;
        const float halfThick = 0.04f;
        // Quad × scale(2): half width/height 1.0 in Java space → ×16 for entity texel space (see <see cref="MinecraftModelBaker"/> W()).
        const float halfW = 16f;
        const float halfH = 16f;
        const float centerY = 8f;
        var b = new RigBuilder(64, 32);
        b.AddBillboardPlane(
            "#skin",
            halfW,
            halfH,
            halfThick,
            centerY,
            u0: 0f,
            v0: 0f,
            u1: 64f,
            v1: 32f,
            Matrix4x4.Identity);
        return b.Build(texRef);
    }

    /// <summary>
    /// ExperienceOrbRenderer (1.21.11 <c>javap</c> <c>hwu</c>): no cuboid model; UV window is a <c>16×16</c> tile on <c>64×64</c>
    /// from <c>texU=(value%4)·16</c>, <c>texV=(value/4)·16</c>; a <c>1×1</c> XY quad, <c>translate(0,0.1·bob,0)</c>, <c>scale(0.3,0.3,0.3)</c>, billboard.
    /// Preview uses one thin north/south slab sized to that quad (×16 texel space) and the same tile UV;
    /// <paramref name="spritePick01"/> picks pseudo <c>value</c> in <c>0..10</c> for the tile.
    /// </summary>
    private static MergedJavaBlockModel BuildExperienceOrb(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float bob,
        float spritePick01)
    {
        _ = profile;
        _ = isBaby;
        const float halfThick = 0.04f;
        var value = Math.Clamp((int)(spritePick01 * 9973f) % 11, 0, 10);
        var texU = (float)((value % 4) * 16);
        var texV = (float)((value / 4) * 16);
        // Unit quad half 0.5 × scale 0.3 → 0.15 Java units; ×16 → 2.4 texel units. Quad center y=0.25 × 0.3 × 16 = 1.2.
        const float halfW = 2.4f;
        const float halfH = 2.4f;
        const float centerY = 1.2f;
        var root = Matrix4x4.CreateTranslation(0f, bob, 0f);
        var b = new RigBuilder(64, 64);
        b.AddBillboardPlane(
            "#skin",
            halfW,
            halfH,
            halfThick,
            centerY,
            texU,
            texV,
            texU + 16f,
            texV + 16f,
            root);
        return b.Build(texRef);
    }

    /// <summary>
    /// FishingHookRenderer (1.21.11 <c>javap</c> <c>hwx</c>): no <c>EntityModel</c> cuboids; after <c>scale(0.5)</c> and camera rotation,
    /// submits four vertices for a <c>1×1</c> XY quad at <c>z=0</c> with full-texture UVs (billboard).
    /// Preview uses <see cref="RigBuilder.AddBillboardPlane"/> (same UV on north/south) instead of stem/barb proxy boxes.
    /// </summary>
    private static MergedJavaBlockModel BuildFishingHook(string texRef, MinecraftNativeProfile profile, bool isBaby, float sway)
    {
        _ = profile;
        _ = isBaby;
        const float halfThick = 0.04f;
        // Unit quad half-extent 0.5 in Java space ×16 → 8 texel units (see <see cref="MinecraftModelBaker"/> W()).
        const float halfW = 8f;
        const float halfH = 8f;
        const float centerY = 8f;
        var root = Matrix4x4.CreateRotationZ(sway);
        var b = new RigBuilder(64, 64);
        b.AddBillboardPlane(
            "#skin",
            halfW,
            halfH,
            halfThick,
            centerY,
            u0: 0f,
            v0: 0f,
            u1: 32f,
            v1: 32f,
            root);
        return b.Build(texRef);
    }
}
