using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Bat, chicken, bee, ghast, blaze, vex, phantom, parrot.


    private static MergedJavaBlockModel BuildBat(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float rightWingYawRad,
        float leftWingYawRad,
        float restingWingPivotZRight = 0f,
        float restingWingPivotZLeft = 0f)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.84f, 1.06f, 0.85f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.92f, 1.04f, 0.92f) : BabyProfile.Adult);

        const float thin = 0.08f;
        var b = new RigBuilder(32, 32);
        // BatModel.createBat (~1.21.4): texture 32x32; vanilla bat ears/wings are zero-thickness → thin solids.
        var bodyRoot = Matrix4x4.CreateTranslation(0f, 17f, 0f);
        new EntityCuboid(-1.5f, 0f, -1f, 1.5f, 5f, 1f, 0, 0).Emit(b, bodyRoot, p.BodyScale);

        var headRoot = Matrix4x4.CreateTranslation(0f, 17f, 0f);
        new EntityCuboid(-2f, -3f, -1f, 2f, 0f, 1f, 0, 7).Emit(b, headRoot, p.HeadScale);

        var headEarBase = headRoot;
        var rightEarBat = Matrix4x4.Multiply(headEarBase, Matrix4x4.CreateTranslation(-1.5f, -2f, 0f));
        new EntityCuboid(-2.5f, -4f, -thin, 0.5f, 1f, thin, 1, 15).Emit(b, rightEarBat, p.HeadScale);

        var leftEarBat = Matrix4x4.Multiply(headEarBase, Matrix4x4.CreateTranslation(1.1f, -3f, 0f));
        new EntityCuboid(-0.1f, -3f, -thin, 2.9f, 2f, thin, 8, 15).Emit(b, leftEarBat, p.HeadScale);

        var rightWing = Matrix4x4.Multiply(
            Matrix4x4.Multiply(bodyRoot, Matrix4x4.CreateTranslation(-1.5f, 0f, restingWingPivotZRight)),
            Matrix4x4.CreateRotationY(-rightWingYawRad));
        new EntityCuboid(-2f, -2f, -thin, 0f, 5f, thin, 12, 0).Emit(b, rightWing, p.BodyScale);

        var rightTip = Matrix4x4.Multiply(rightWing, Matrix4x4.CreateTranslation(-2f, 0f, 0f));
        new EntityCuboid(-6f, -2f, -thin, 0f, 8f, thin, 16, 0).Emit(b, rightTip, p.BodyScale);

        var leftWing = Matrix4x4.Multiply(
            Matrix4x4.Multiply(bodyRoot, Matrix4x4.CreateTranslation(1.5f, 0f, restingWingPivotZLeft)),
            Matrix4x4.CreateRotationY(leftWingYawRad));
        new EntityCuboid(0f, -2f, -thin, 2f, 5f, thin, 12, 7).Emit(b, leftWing, p.BodyScale);

        var leftTip = Matrix4x4.Multiply(leftWing, Matrix4x4.CreateTranslation(2f, 0f, 0f));
        new EntityCuboid(0f, -2f, -thin, 6f, 8f, thin, 16, 8).Emit(b, leftTip, p.BodyScale);

        var feetPose = Matrix4x4.Multiply(bodyRoot, Matrix4x4.CreateTranslation(0f, 5f, 0f));
        new EntityCuboid(-1.5f, 0f, -thin, 1.5f, 2f, thin, 16, 16).Emit(b, feetPose, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// Flying-family fallback when <see cref="TryBuildSpecific"/> did not match: delegates to the same cuboids/poses as
    /// <see cref="BuildPhantom"/> (Java <c>PhantomModel</c> / <c>het</c>) so UVs and rig match vanilla; <paramref name="wingSpread"/>
    /// drives the same flap phase as <c>setupAnim</c>. The eyes layer reuses <paramref name="texRef"/> so callers without
    /// <c>phantom_eyes.png</c> still resolve textures.
    /// Exposed <see langword="internal"/> for parity tests — prefer dedicated rigs in <see cref="TryBuildSpecific"/>.
    /// </summary>
    internal static MergedJavaBlockModel BuildFlying(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float wingSpread) =>
        BuildPhantom(
            normalizedAssetPath: "assets/minecraft/textures/entity/phantom/phantom.png",
            texRef,
            profile,
            isBaby,
            flapTime: wingSpread,
            eyesTextureRefOverride: texRef);

    internal static void ComputeChickenParityPreviewDrivers(
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        out float headPitchRad,
        out float headYawRad,
        out float wingZRadians,
        out float rightLegPitchRad,
        out float leftLegPitchRad)
    {
        var flapSpeed = 0.18f + Math.Clamp(0.22f + idlePhase01 * 0.18f + wave * 0.12f, 0.05f, 0.95f) * 0.55f;
        var state = PreviewRenderStateSynthesis.ForChicken(
            animationTimeSeconds,
            idlePhase01,
            wave,
            (idlePhase01 * 8f) + (wave * 5f),
            wave * 10f,
            flapSpeed);
        headPitchRad = headYawRad = wingZRadians = rightLegPitchRad = leftLegPitchRad = 0f;
        const string chickenModel = "net.minecraft.client.model.animal.chicken.ChickenModel";
        if (!VanillaSetupAnimRuntime.TryEvaluate(chickenModel, state, animationTimeSeconds, out var pose))
        {
            VanillaSetupAnimRuntime.TryEvaluate("net.minecraft.client.model.QuadrupedModel", state, animationTimeSeconds, out pose);
        }

        if (pose.Parts.TryGetValue("head", out var head))
        {
            headPitchRad = head.XRot;
            headYawRad = head.YRot;
        }
        else if (state.TryGetValue("xRot", out var pitchDeg) && state.TryGetValue("yRot", out var yawDeg))
        {
            headPitchRad = pitchDeg * PreviewRenderStateSynthesis.DegToRad;
            headYawRad = yawDeg * PreviewRenderStateSynthesis.DegToRad;
        }

        if (pose.Parts.TryGetValue("rightWing", out var rw))
        {
            wingZRadians = rw.ZRot;
        }

        if (pose.Parts.TryGetValue("rightLeg", out var rl))
        {
            rightLegPitchRad = rl.XRot;
        }

        if (pose.Parts.TryGetValue("leftLeg", out var ll))
        {
            leftLegPitchRad = ll.XRot;
        }
    }

    /// <summary>Adult <c>chicken_cold.png</c> uses <c>ColdChickenModel.createBodyLayer</c> (26.1.2 javap), not the temperate head/beak/wattle split.</summary>
    private static bool IsAdultColdChickenStem(string stemLower) =>
        string.Equals(stemLower, "chicken_cold", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>BabyChickenModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.chicken.BabyChickenModel.json</c>
    /// (<c>64×32</c>): <c>body</c> @ <c>T(0,20.25,-1.25)</c> + two cuboids; <c>left_leg</c> / <c>right_leg</c> @ <c>T(1,22,0.5)</c> / <c>T(-1,22,0.5)</c> two cuboids each;
    /// wings @ <c>T(2,20,0)</c> / <c>T(-2,20,0)</c>. Part order matches IR DFS: body, left_leg, right_leg, right_wing, left_wing.
    /// <c>setupAnim</c> uses the same leg cosine / wing <c>zRot</c> family as adult <see cref="BuildChicken"/> (shared preview limb/flap drivers).
    /// </summary>
    private static MergedJavaBlockModel BuildBabyChicken(
        string texRef,
        float rightLegPitchRad,
        float leftLegPitchRad,
        float wingZRadians)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 32);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20.25f, -1.25f));
        new EntityCuboid(-2f, -2.25f, -0.75f, 2f, 1.75f, 3.25f, 0, 0, UvSizeW: 4, UvSizeH: 4, UvSizeD: 4).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-1f, -0.25f, -1.75f, 1f, 0.75f, -0.75f, 10, 8, UvSizeW: 2, UvSizeH: 1, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        var leftLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 22f, 0.5f)),
            EntityParityTemplate.Rx(leftLegPitchRad));
        new EntityCuboid(-0.5f, 0f, 0f, 0.5f, 2f, 0f, 2, 2, UvSizeW: 1, UvSizeH: 2, UvSizeD: 1).Emit(b, leftLegPose, p.LegScale);
        new EntityCuboid(-0.5f, 2f, -1f, 0.5f, 2f, 0f, 0, 1, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, leftLegPose, p.LegScale);

        var rightLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1f, 22f, 0.5f)),
            EntityParityTemplate.Rx(rightLegPitchRad));
        new EntityCuboid(-0.5f, 0f, 0f, 0.5f, 2f, 0f, 0, 2, UvSizeW: 1, UvSizeH: 2, UvSizeD: 1).Emit(b, rightLegPose, p.LegScale);
        new EntityCuboid(-0.5f, 2f, -1f, 0.5f, 2f, 0f, 0, 0, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, rightLegPose, p.LegScale);

        var rightWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2f, 20f, 0f)),
            EntityParityTemplate.Rz(wingZRadians));
        new EntityCuboid(0f, 0f, -1f, 1f, 0f, 1f, 6, 8, UvSizeW: 1, UvSizeH: 1, UvSizeD: 2).Emit(b, rightWingPose, p.BodyScale);

        var leftWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 20f, 0f)),
            EntityParityTemplate.Rz(-wingZRadians));
        new EntityCuboid(-1f, 0f, -1f, 0f, 0f, 1f, 4, 8, UvSizeW: 1, UvSizeH: 1, UvSizeD: 2).Emit(b, leftWingPose, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>ChickenModel.createBodyLayer</c> — same static mesh as geometry IR
    /// <c>geometry/&lt;version&gt;/net.minecraft.client.model.animal.chicken.ChickenModel.json</c> (see
    /// <c>docs/generated/geometry-index-26.1.2.json</c> / <c>geometry-index-1.21.11.json</c>): <c>64×32</c> atlas;
    /// head <c>4×6×3</c> @ <c>T(0,15,-4)</c> + beak <c>4×2×2</c> + <c>red_thing</c> <c>2×2×2</c> (beak / wattle share head look rotation);
    /// body <c>6×8×6</c> @ <c>texOffs(0,9)</c> <c>PartPose.offsetAndRotation(0,16,0, π/2,0,0)</c>; legs <c>3×5×3</c> @ <c>(26,0)</c>
    /// <c>T(∓2,19,1)/(1,19,1)</c>; wings <c>1×4×6</c> @ <c>(24,13)</c> <c>T(∓4,13,0)</c>. Vanilla lists only <c>right_leg</c> in the mesh factory;
    /// the mirrored left leg uses the same cuboid + UV island. <c>setupAnim</c>: head <c>xRot/yRot</c> from render-state look (deg→rad),
    /// wings <c>zRot ±(sin(flap)+1)·flapSpeed</c>; legs from lifted <c>ChickenModel</c> / <c>QuadrupedModel</c> setupAnim IR via <see cref="VanillaSetupAnimRuntime"/>.
    /// </summary>
    private static MergedJavaBlockModel BuildChicken(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitchRad,
        float headYawRad,
        float wingZRadians,
        float rightLegPitchRad,
        float leftLegPitchRad)
    {
        if (isBaby)
        {
            return BuildBabyChicken(texRef, rightLegPitchRad, leftLegPitchRad, wingZRadians);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 32);
        var root = Matrix4x4.Identity;

        var headPose = EntityParityTemplate.Mul(
            root,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 15f, -4f), EntityParityTemplate.Er(headPitchRad, headYawRad, 0f)));
        new EntityCuboid(-2f, -6f, -2f, 2f, 0f, 1f, 0, 0, UvSizeW: 4, UvSizeH: 6, UvSizeD: 3).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2f, -4f, -4f, 2f, -2f, -2f, 14, 0, UvSizeW: 4, UvSizeH: 2, UvSizeD: 2).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-1f, -2f, -3f, 1f, 0f, -1f, 14, 4, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2).Emit(b, headPose, p.HeadScale);

        var bodyPose = EntityParityTemplate.Mul(
            root,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 16f, 0f), EntityParityTemplate.Rx(MathF.PI / 2f)));
        new EntityCuboid(-3f, -4f, -3f, 3f, 4f, 3f, 0, 9, UvSizeW: 6, UvSizeH: 8, UvSizeD: 6).Emit(b, bodyPose, p.BodyScale);

        var rightLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 19f, 1f)),
            EntityParityTemplate.Rx(rightLegPitchRad));
        new EntityCuboid(-1f, 0f, -3f, 2f, 5f, 0f, 26, 0, UvSizeW: 3, UvSizeH: 5, UvSizeD: 3).Emit(b, rightLegPose, p.LegScale);
        var leftLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 19f, 1f)),
            EntityParityTemplate.Rx(leftLegPitchRad));
        new EntityCuboid(-1f, 0f, -3f, 2f, 5f, 0f, 26, 0, UvSizeW: 3, UvSizeH: 5, UvSizeD: 3).Emit(b, leftLegPose, p.LegScale);

        var rightWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-4f, 13f, 0f)),
            EntityParityTemplate.Rz(wingZRadians));
        new EntityCuboid(0f, 0f, -3f, 1f, 4f, 3f, 24, 13, UvSizeW: 1, UvSizeH: 4, UvSizeD: 6).Emit(b, rightWingPose, p.BodyScale);
        var leftWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(4f, 13f, 0f)),
            EntityParityTemplate.Rz(-wingZRadians));
        new EntityCuboid(-1f, 0f, -3f, 0f, 4f, 3f, 24, 13, UvSizeW: 1, UvSizeH: 4, UvSizeD: 6).Emit(b, leftWingPose, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>ColdChickenModel.createBodyLayer</c> (26.1.2 javap): extends <c>AdultChickenModel.createBaseChickenModel</c> then
    /// <c>addOrReplaceChild</c> on <c>body</c> (main box + thin crest <c>texOffs(38,9)</c>) and <c>head</c> (main head + cold hood <c>texOffs(44,0)</c>).
    /// Legs/wings stay from the base mesh; <c>AdultChickenModel.setupAnim</c> still drives wing flap and leg swing + head look (deg→rad on head part).
    /// Merged <see cref="ModelElement"/> order follows vanilla root DFS after the replacements: <c>head</c> cuboids (2), <c>body</c> cuboids (2),
    /// then <c>right_leg</c>, <c>left_leg</c>, <c>right_wing</c>, <c>left_wing</c> — same sibling order as <c>createBaseChickenModel</c>
    /// (<c>head</c> is registered before <c>body</c>; cold bytecode replaces <c>body</c> then <c>head</c> without reordering siblings).
    /// </summary>
    private static MergedJavaBlockModel BuildColdChicken(
        string texRef,
        MinecraftNativeProfile profile,
        float headPitchRad,
        float headYawRad,
        float wingZRadians,
        float rightLegPitchRad,
        float leftLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 32);
        var root = Matrix4x4.Identity;

        var headPose = EntityParityTemplate.Mul(
            root,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 15f, -4f), EntityParityTemplate.Er(headPitchRad, headYawRad, 0f)));
        new EntityCuboid(-2f, -6f, -2f, 2f, 0f, 1f, 0, 0, UvSizeW: 4, UvSizeH: 6, UvSizeD: 3).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-3f, -7f, -2.015f, 3f, -4f, 1.985f, 44, 0, UvSizeW: 6, UvSizeH: 3, UvSizeD: 4).Emit(b, headPose, p.HeadScale);

        var bodyPose = EntityParityTemplate.Mul(
            root,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 16f, 0f), EntityParityTemplate.Rx(MathF.PI / 2f)));
        new EntityCuboid(-3f, -4f, -3f, 3f, 4f, 3f, 0, 9, UvSizeW: 6, UvSizeH: 8, UvSizeD: 6).Emit(b, bodyPose, p.BodyScale);
        // Javap: texOffs(38,9); addBox(0,3,-1, 0,3,5) — origin + size; zero X width matches IR corners (0,3,-1)-(0,6,4). UV w=1 minimum for unfold.
        new EntityCuboid(0f, 3f, -1f, 0f, 6f, 4f, 38, 9, UvSizeW: 1, UvSizeH: 3, UvSizeD: 5).Emit(b, bodyPose, p.BodyScale);

        var rightLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 19f, 1f)),
            EntityParityTemplate.Rx(rightLegPitchRad));
        new EntityCuboid(-1f, 0f, -3f, 2f, 5f, 0f, 26, 0, UvSizeW: 3, UvSizeH: 5, UvSizeD: 3).Emit(b, rightLegPose, p.LegScale);
        var leftLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 19f, 1f)),
            EntityParityTemplate.Rx(leftLegPitchRad));
        new EntityCuboid(-1f, 0f, -3f, 2f, 5f, 0f, 26, 0, UvSizeW: 3, UvSizeH: 5, UvSizeD: 3).Emit(b, leftLegPose, p.LegScale);

        var rightWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-4f, 13f, 0f)),
            EntityParityTemplate.Rz(wingZRadians));
        new EntityCuboid(0f, 0f, -3f, 1f, 4f, 3f, 24, 13, UvSizeW: 1, UvSizeH: 4, UvSizeD: 6).Emit(b, rightWingPose, p.BodyScale);
        var leftWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(4f, 13f, 0f)),
            EntityParityTemplate.Rz(-wingZRadians));
        new EntityCuboid(-1f, 0f, -3f, 0f, 4f, 3f, 24, 13, UvSizeW: 1, UvSizeH: 4, UvSizeD: 6).Emit(b, leftWingPose, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BlazeModel.createBodyLayer</c> — Java 1.21.11 client (same layout 1.21.x): <c>head</c> <c>texOffs(0,0)</c> <c>addBox(-4,-4,-4,8,8,8)</c> <c>PartPose.ZERO</c>;
    /// twelve <c>part{i}</c> rods share <c>texOffs(0,16)</c> <c>addBox(-1,0,-1,2,8,2)</c> with <c>PartPose.offset(cos(-π/4 + i·π/6)·5.1, 11, sin(...)·5.1)</c> for <c>i</c> in <c>0..11</c>.
    /// Preview root <c>T(8,14,8)</c> preserves the historical CleanRoom head anchor <c>(4,10,4)–(12,18,12)</c> while aligning rod ring radii to vanilla.
    /// <c>setupAnim</c> rod <c>xRot</c> uses a small sine per index; <paramref name="rodSpin"/> drives that sway here.
    /// </summary>
    private static MergedJavaBlockModel BuildBlaze(string texRef, MinecraftNativeProfile profile, bool isBaby, float rodSpin)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.74f, 1.14f, 0.80f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.82f, 1.08f, 0.86f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 32);
        var root = EntityParityTemplate.T(8f, 14f, 8f);
        new EntityCuboid(-4f, -4f, -4f, 4f, 4f, 4f, 0, 0).Emit(b, root, p.HeadScale);

        for (var i = 0; i < 12; i++)
        {
            var baseAngle = -MathF.PI / 4f + (MathF.PI / 6f) * i;
            var ox = MathF.Cos(baseAngle) * 5.1f;
            var oz = MathF.Sin(baseAngle) * 5.1f;
            var rodBase = EntityParityTemplate.Mul(root, EntityParityTemplate.T(ox, 11f, oz));
            var rodXRot = 0.2f * MathF.Sin(rodSpin * 2f + i * 0.15f);
            var rodPose = EntityParityTemplate.Mul(rodBase, EntityParityTemplate.Rx(rodXRot));
            new EntityCuboid(-1f, 0f, -1f, 1f, 8f, 1f, 0, 16).Emit(b, rodPose, p.BodyScale);
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// HappyGhastModel (~Java 1.21.11 client <c>hbm</c>): skin atlas <c>64×64</c>; body cube <c>16³</c> at PartPose <c>(0,16,0)</c>;
    /// nine tentacles <c>2×h×2</c> with per-index heights; optional baby <c>inner_body</c> layer at tex <c>(0,32)</c> with dilation preview inset.
    /// Vanilla renderer applies root ModelTransforms.scaling(<c>4</c>) — not folded into this mesh (matches omission on <see cref="BuildGhast"/> vs GhastModel scaling).
    /// </summary>
    private static MergedJavaBlockModel BuildHappyGhast(string texRef, MinecraftNativeProfile profile, bool isBaby, float tentacleSway)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.70f, 1.0f, 0.72f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.78f, 1.0f, 0.80f) : BabyProfile.Adult);

        var b = new RigBuilder(64, 64);
        var bodyRoot = Matrix4x4.CreateTranslation(0f, 16f, 0f);
        // hbm body texOffs (0,0) 16³ (+ optional deformation from vanilla LayerDefinition).
        new EntityCuboid(-8f, -8f, -8f, 8f, 8f, 8f, 0, 0, UvSizeW: 16, UvSizeH: 16, UvSizeD: 16).Emit(b, bodyRoot, p.BodyScale);

        if (isBaby)
        {
            var innerRoot = Matrix4x4.Multiply(bodyRoot, Matrix4x4.CreateTranslation(0f, 8f, 0f));
            // inner_body: texOffs (0,32) 16³ with dilation preview inset (~15³ mesh).
            new EntityCuboid(-7.5f, -7.5f, -7.5f, 7.5f, 7.5f, 7.5f, 0, 32, UvSizeW: 16, UvSizeH: 16, UvSizeD: 16).Emit(b, innerRoot, p.BodyScale);
        }

        ReadOnlySpan<float> tentacleH = [5f, 7f, 4f, 5f, 5f, 7f, 8f, 8f, 5f];
        ReadOnlySpan<(float X, float Z)> tentaclePose =
        [
            (-3.75f, -5f), (1.25f, -5f), (6.25f, -5f),
            (-6.25f, 0f), (-1.25f, 0f), (3.75f, 0f),
            (-3.75f, 5f), (1.25f, 5f), (6.25f, 5f),
        ];

        for (var i = 0; i < 9; i++)
        {
            var sway = tentacleSway * ((i % 2 == 0) ? 0.7f : -0.55f);
            var tentacleRoot = Matrix4x4.Multiply(bodyRoot, Matrix4x4.CreateTranslation(tentaclePose[i].X, 7f, tentaclePose[i].Z));
            var uh = Math.Max(1, (int)MathF.Round(tentacleH[i]));
            // All tentacles share texOffs (0,0); footprint height varies per column (hbm).
            new EntityCuboid(-1f, 0f, -1f, 1f, tentacleH[i], 1f, 0, 0, UvSizeW: 2, UvSizeH: uh, UvSizeD: 2, OffsetX: sway, OffsetY: 0f, OffsetZ: 0f).Emit(b, tentacleRoot, p.LegScale);
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// HappyGhastHarnessModel (~Java 1.21.11 client <c>hbl</c>): <c>harness</c> + <c>goggles</c> on <c>64×64</c>;
    /// baby applies HappyGhastModel BABY_TRANSFORMER scale <c>0.2375</c> (javap <c>hbm.b</c>);
    /// goggles use <c>CubeDeformation</c> extension <c>+0.15</c> on the cuboid (happy path here); pose interpolates equipped vs idle (<c>setupAnim</c> xRot / Y pivot).
    /// Root renderer scaling <c>4</c> omitted like other ghast rigs.
    /// </summary>
    private static MergedJavaBlockModel BuildHappyGhastHarness(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float gogglesEquippedBlend)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.70f, 1.0f, 0.72f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.78f, 1.0f, 0.80f) : BabyProfile.Adult);

        var geo = isBaby ? 0.2375f : 1f;
        var equip = Math.Clamp(gogglesEquippedBlend, 0f, 1f);
        var gY = (9f + equip * 5f) * geo;
        var gZ = -5.5f * geo;
        var gRx = -(1f - equip) * (MathF.PI / 4f);

        var b = new RigBuilder(64, 64);
        var harnessPose = Matrix4x4.CreateTranslation(0f, 24f * geo, 0f);
        // hbl harness texOffs (0,0) 16³; goggles (0,32) 16×5×5 + CubeDeformation(+0.15).
        new EntityCuboid(-8f * geo, -16f * geo, -8f * geo, 8f * geo, 0f, 8f * geo, 0, 0, UvSizeW: 16, UvSizeH: 16, UvSizeD: 16).Emit(b, harnessPose, p.BodyScale);

        var gogglesPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, gY, gZ), Matrix4x4.CreateRotationX(gRx));
        const float gogglesDilation = 0.15f;
        new EntityCuboid((-8f - gogglesDilation) * geo, (-2.5f - gogglesDilation) * geo, (-2.5f - gogglesDilation) * geo, (8f + gogglesDilation) * geo, (2.5f + gogglesDilation) * geo, (2.5f + gogglesDilation) * geo, 0, 32, UvSizeW: 16, UvSizeH: 5, UvSizeD: 5).Emit(b, gogglesPose, p.HeadScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildGhast(string texRef, MinecraftNativeProfile profile, bool isBaby, float tentacleSway)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.70f, 1.0f, 0.72f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.78f, 1.0f, 0.80f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 32);
        // GhastModel (gch): body 16x16x16 and 9 tentacles with deterministic lengths from Random(1660).
        // Vanilla animates tentacles in X (pitch), not Y.
        new EntityCuboid(0f, 9.6f, 0f, 16f, 25.6f, 16f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale);
        ReadOnlySpan<float> tentacleLengths = [8f, 13f, 9f, 11f, 11f, 10f, 12f, 9f, 12f];
        for (var i = 0; i < 9; i++)
        {
            var gx = i % 3;
            var gz = i / 3;
            var x0 = 3f + gx * 4f;
            var z0 = 3f + gz * 4f;
            var pitch = 0.4f + 0.2f * MathF.Sin(tentacleSway * 1.5f + i * 0.3f);
            new EntityCuboid(x0, -tentacleLengths[i], z0, x0 + 2f, 0f, z0 + 2f, 0, 0, XRot: pitch, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(x0 + 1f, 24.6f, z0 + 1f) }.Emit(b, Matrix4x4.Identity, p.LegScale);
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BabyBeeModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.bee.BabyBeeModel.json</c> (<c>64×64</c>).
    /// Part order follows IR DFS. Wing flap adds the same idle <c>xRot</c> delta as <see cref="BuildBee"/> (±<c>0.2618</c> rad) on top of IR wing part Eulers.
    /// </summary>
    private static MergedJavaBlockModel BuildBabyBee(string texRef, float wingFlap)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;
        const float wingY = 0.06f;
        const float legZ = 0.06f;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 1.3333f, 2.3567f));
        new EntityCuboid(-2f, -2f, -2.5f, 2f, 2f, 2.5f, 0, 0, UvSizeW: 4, UvSizeH: 4, UvSizeD: 5).Emit(b, bodyPose, p.BodyScale);

        var bonePose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 19.6667f, -1.8567f));
        new EntityCuboid(1f, -1.6667f, -2.1633f, 2f, 0.3333f, -0.1633f, 6, 12, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2).Emit(b, bonePose, p.HeadScale);
        new EntityCuboid(-2f, -1.6667f, -2.1933f, -1f, 0.3333f, -0.1933f, 0, 12, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2, MirrorUv: true).Emit(b, bonePose, p.HeadScale);

        var stingerPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 0.5f, 2.5f));
        const float stx = 0.06f;
        new EntityCuboid(-stx, -0.5f, 0f, stx, 0.5f, 1f, 13, 2, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, stingerPose, p.BodyScale);

        var rightWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1f, -0.6667f, 0.8567f)),
            EntityParityTemplate.Mul(EntityParityTemplate.Er(0.2182f, 0.3491f, 0f), EntityParityTemplate.Rx(-0.2618f - wingFlap)));
        new EntityCuboid(-3f, -wingY, 0f, 0f, wingY, 3f, 3, 9, UvSizeW: 3, UvSizeH: 1, UvSizeD: 3).Emit(b, rightWingPose, p.BodyScale);

        var leftWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, -0.6667f, 0.8567f)),
            EntityParityTemplate.Mul(EntityParityTemplate.Er(0.2182f, -0.3491f, 0f), EntityParityTemplate.Rx(0.2618f + wingFlap)));
        new EntityCuboid(0f, -wingY, 0f, 3f, wingY, 3f, 0, 9, UvSizeW: 3, UvSizeH: 1, UvSizeD: 3, MirrorUv: true).Emit(b, leftWingPose, p.BodyScale);

        var frontLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 3.3333f, 1.8567f));
        new EntityCuboid(-1.5f, 0f, -legZ, 1.5f, 1f, legZ, 13, 0, UvSizeW: 3, UvSizeH: 1, UvSizeD: 1).Emit(b, frontLegPose, p.LegScale);
        var middleLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 3.3333f, 2.8567f));
        new EntityCuboid(-1.5f, 0f, -legZ, 1.5f, 1f, legZ, 13, 1, UvSizeW: 3, UvSizeH: 1, UvSizeD: 1).Emit(b, middleLegPose, p.LegScale);
        var backLegPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 3.3333f, 3.8567f));
        new EntityCuboid(-1.5f, 0f, -legZ, 1.5f, 1f, legZ, 13, 2, UvSizeW: 3, UvSizeH: 1, UvSizeD: 1).Emit(b, backLegPose, p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildBee(string texRef, MinecraftNativeProfile profile, bool isBaby, float wingFlap)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildBabyBee(texRef, wingFlap);
        }

        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.78f, 1.0f, 0.78f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.86f, 1.0f, 0.86f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 64);
        // BeeModel (gbf): body 7x7x10, stinger/legs are paper-thin in vanilla, represented as 0.12f depth sheets.
        new EntityCuboid(4.5f, 15f, 3f, 11.5f, 22f, 13f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(8f, 18f, 13f, 8.12f, 19f, 15f, 26, 7, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale); // stinger
        new EntityCuboid(9.5f, 17f, 5f, 10.5f, 19f, 8f, 2, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // left antenna
        new EntityCuboid(5.5f, 17f, 5f, 6.5f, 19f, 8f, 2, 3, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // right antenna
        new EntityCuboid(-1.5f, 15f, 5f, 7.5f, 15.12f, 11f, 0, 18, XRot: 0f, YRot: -0.2618f - wingFlap, ZRot: 0f) { RotationPivot = new Vector3(6.5f, 15f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(9.5f, 15f, 5f, 18.5f, 15.12f, 11f, 0, 18, XRot: 0f, YRot: 0.2618f + wingFlap, ZRot: 0f) { RotationPivot = new Vector3(9.5f, 15f, 8f) }.Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(4.5f, 19f, 6f, 11.5f, 21f, 6.12f, 26, 1, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // front legs
        new EntityCuboid(4.5f, 19f, 8f, 11.5f, 21f, 8.12f, 26, 3, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // middle legs
        new EntityCuboid(4.5f, 19f, 10f, 11.5f, 21f, 10.12f, 26, 5, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // back legs
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildAllay(string texRef, MinecraftNativeProfile profile, bool isBaby, float wingFlap)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.80f, 1.0f, 0.82f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.88f, 1.0f, 0.90f) : BabyProfile.Adult);
        var b = new RigBuilder(32, 32);
        // AllayModel (gat): head 5x5x5, body 3x4x2 with a 3x5x2 overlay, arms 1x4x2, wings 0x5x8.
        new EntityCuboid(5.5f, 14.5f, 5.5f, 10.5f, 19.5f, 10.5f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale);
        new EntityCuboid(6.5f, 10.5f, 7f, 9.5f, 14.5f, 9f, 0, 10, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(6.5f, 10.5f, 7f, 9.5f, 15.5f, 9f, 0, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(4.25f, 10f, 7f, 5.25f, 14f, 9f, 23, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(10.75f, 10f, 7f, 11.75f, 14f, 9f, 23, 6, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(7.5f, 11f, 9.6f, 7.62f, 16f, 17.6f, 16, 14, XRot: 0f, YRot: -0.7853982f - wingFlap, ZRot: 0.43633232f) { RotationPivot = new Vector3(7.5f, 11f, 9.6f) }.Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(8.38f, 11f, 9.6f, 8.5f, 16f, 17.6f, 16, 14, XRot: 0f, YRot: 0.7853982f + wingFlap, ZRot: 0.43633232f) { RotationPivot = new Vector3(8.38f, 11f, 9.6f) }.Emit(b, Matrix4x4.Identity, p.BodyScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static void VexApplyArmsCharging(
        bool rightHandHoldingItem,
        bool leftHandHoldingItem,
        float f2,
        out float rightX,
        out float rightY,
        out float rightZ,
        out float leftX,
        out float leftY,
        out float leftZ)
    {
        rightX = 0f;
        rightY = 0f;
        rightZ = 0f;
        leftX = 0f;
        leftY = 0f;
        leftZ = 0f;
        if (!rightHandHoldingItem && !leftHandHoldingItem)
        {
            rightX = -1.2217305f;
            rightY = 0.2617994f;
            rightZ = -0.47123888f - f2;
            leftX = -1.2217305f;
            leftY = -0.2617994f;
            leftZ = 0.47123888f + f2;
            return;
        }

        if (rightHandHoldingItem)
        {
            rightX = 3.6651914f;
            rightY = 0.2617994f;
            rightZ = -0.47123888f - f2;
        }

        if (leftHandHoldingItem)
        {
            leftX = 3.6651914f;
            leftY = -0.2617994f;
            leftZ = 0.47123888f + f2;
        }
    }

    /// <summary>
    /// Vanilla <c>VexModel.setupAnim</c> (26.1.2 <c>client.jar</c>): head from <c>VexRenderState</c> degrees, arm <c>zRot</c> wobble
    /// from <c>cos(ageInTicks * 5.5°)</c>, charging branch + <c>setArmsCharging</c>, wings from <c>ageInTicks</c> cosine term.
    /// </summary>
    private static MergedJavaBlockModel BuildVex(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float yRotDegrees,
        float xRotDegrees,
        float ageInTicks,
        bool isCharging,
        bool rightHandHoldingItem,
        bool leftHandHoldingItem)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.78f, 1.0f, 0.80f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.86f, 1.0f, 0.88f) : BabyProfile.Adult);
        var b = new RigBuilder(32, 32);
        const float deg = MathF.PI / 180f;
        var f2 = MathF.Cos(ageInTicks * 5.5f * deg) * 0.1f;

        float bodyXRot;
        float rightArmX;
        float rightArmY;
        float rightArmZ;
        float leftArmX;
        float leftArmY;
        float leftArmZ;
        if (isCharging)
        {
            bodyXRot = 0f;
            VexApplyArmsCharging(rightHandHoldingItem, leftHandHoldingItem, f2, out rightArmX, out rightArmY, out rightArmZ, out leftArmX, out leftArmY, out leftArmZ);
            if (rightHandHoldingItem && !leftHandHoldingItem)
            {
                leftArmX = 0f;
                leftArmY = 0f;
                leftArmZ = -(0.62831855f + f2);
            }

            if (leftHandHoldingItem && !rightHandHoldingItem)
            {
                rightArmX = 0f;
                rightArmY = 0f;
                rightArmZ = 0.62831855f + f2;
            }
        }
        else
        {
            bodyXRot = 0.15707964f;
            rightArmX = 0f;
            rightArmY = 0f;
            leftArmX = 0f;
            leftArmY = 0f;
            rightArmZ = 0.62831855f + f2;
            leftArmZ = -(0.62831855f + f2);
        }

        var leftWingY = 1.0995574f + (MathF.Cos(ageInTicks * 45.836624f * deg) * deg * 16.2f);
        var rightWingY = -leftWingY;
        const float wingX = 0.47123888f;
        const float leftWingZ = -0.47123888f;
        const float rightWingZ = 0.47123888f;

        var headYaw = yRotDegrees * deg;
        var headPitch = xRotDegrees * deg;
        var headPivot = new Vector3(8f, 20f, 8f);
        var bodyPivot = new Vector3(8f, 15.5f, 8f);

        new EntityCuboid(5.5f, 17.5f, 5.5f, 10.5f, 22.5f, 10.5f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: headPitch, YRot: headYaw, ZRot: 0f) { RotationPivot = headPivot }.Emit(b, Matrix4x4.Identity, p.HeadScale);
        new EntityCuboid(6.5f, 13.5f, 7f, 9.5f, 17.5f, 9f, 0, 10, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: bodyXRot, YRot: 0f, ZRot: 0f) { RotationPivot = bodyPivot }.Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(6.5f, 14.5f, 7f, 9.5f, 19.5f, 9f, 0, 16, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: bodyXRot, YRot: 0f, ZRot: 0f) { RotationPivot = bodyPivot }.Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(4.25f, 13.25f, 7f, 6.25f, 17.25f, 9f, 23, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: rightArmX, YRot: rightArmY, ZRot: rightArmZ) { RotationPivot = new Vector3(5.25f, 15.25f, 8f) }.Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(9.75f, 13.25f, 7f, 11.75f, 17.25f, 9f, 23, 6, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: leftArmX, YRot: leftArmY, ZRot: leftArmZ) { RotationPivot = new Vector3(10.75f, 15.25f, 8f) }.Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(8.38f, 14.5f, 10f, 8.5f, 19.5f, 18f, 16, 14, XRot: wingX, YRot: leftWingY, ZRot: leftWingZ) { RotationPivot = new Vector3(8.38f, 14.5f, 10f) }.Emit(b, Matrix4x4.Identity, p.LegScale);
        new EntityCuboid(7.5f, 14.5f, 10f, 7.62f, 19.5f, 18f, 16, 14, XRot: wingX, YRot: rightWingY, ZRot: rightWingZ) { RotationPivot = new Vector3(7.5f, 14.5f, 10f) }.Emit(b, Matrix4x4.Identity, p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildPhantom(
        string normalizedAssetPath,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float flapTime,
        string? eyesTextureRefOverride = null)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.74f, 1.06f, 0.76f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.82f, 1.0f, 0.84f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 64);
        // PhantomModel 26.1.2 — net.minecraft.client.model.monster.phantom.PhantomModel#createBodyLayer + setupAnim (flapTime).
        const float degToRad = 0.017453292f;
        var phi = flapTime * 7.448451f * degToRad;
        var wingZ = MathF.Cos(phi) * 16f * degToRad;
        var tailX = -(5f + 5f * MathF.Cos(2f * phi)) * degToRad;

        var bodyRoot = Matrix4x4.CreateTranslation(8f, 18f, 8f);
        var bodyPitch = Matrix4x4.CreateRotationX(-0.1f);
        // Java PartPose.rotation(-0.1,0,0) on the body part — compose into parent so RigBuilder doesn't apply Euler around cuboid center.
        var bodyWorldRot = Matrix4x4.Multiply(bodyRoot, bodyPitch);

        // Body texOffs (0,8); Java addBox(-3,-2,-8, 5,3,9) → RigBuilder diagonal corners (−3,−2,−8)-(2,1,1).
        new EntityCuboid(-3f, -2f, -8f, 2f, 1f, 1f, 0, 8).Emit(b, bodyWorldRot, p.BodyScale);

        var tailBaseParent = Matrix4x4.Multiply(bodyWorldRot, Matrix4x4.CreateTranslation(0f, -2f, 1f));
        new EntityCuboid(-2f, 0f, 0f, 1f, 2f, 6f, 3, 20, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: tailX, YRot: 0f, ZRot: 0f) { RotationPivot = Vector3.Zero }.Emit(b, tailBaseParent, p.BodyScale);

        var tailTipParent = Matrix4x4.Multiply(tailBaseParent, Matrix4x4.CreateTranslation(0f, 0.5f, 6f));
        new EntityCuboid(-1f, 0f, 0f, 0f, 1f, 6f, 4, 29, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: tailX, YRot: 0f, ZRot: 0f) { RotationPivot = Vector3.Zero }.Emit(b, tailTipParent, p.BodyScale);

        var lwBaseParent = Matrix4x4.Multiply(bodyWorldRot,
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(2f, -2f, -8f), Matrix4x4.CreateRotationZ(wingZ)));
        new EntityCuboid(0f, 0f, 0f, 6f, 2f, 9f, 23, 12, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, lwBaseParent, p.LegScale);

        var lwTipParent = Matrix4x4.Multiply(lwBaseParent,
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(6f, 0f, 0f), Matrix4x4.CreateRotationZ(wingZ)));
        new EntityCuboid(0f, 0f, 0f, 13f, 1f, 9f, 16, 24, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, lwTipParent, p.LegScale);

        var rwBaseParent = Matrix4x4.Multiply(bodyWorldRot,
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-3f, -2f, -8f), Matrix4x4.CreateRotationZ(-wingZ)));
        new EntityCuboid(-6f, 0f, 0f, 0f, 2f, 9f, 23, 12, MirrorUv: true, OffsetX: 0, OffsetY: 0, OffsetZ: 0) { RotationPivot = Vector3.Zero }.Emit(b, rwBaseParent, p.LegScale);

        var rwTipParent = Matrix4x4.Multiply(rwBaseParent,
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-6f, 0f, 0f), Matrix4x4.CreateRotationZ(-wingZ)));
        new EntityCuboid(-13f, 0f, 0f, 0f, 1f, 9f, 16, 24, MirrorUv: true, OffsetX: 0, OffsetY: 0, OffsetZ: 0) { RotationPivot = Vector3.Zero }.Emit(b, rwTipParent, p.LegScale);

        var headParent = Matrix4x4.Multiply(bodyWorldRot,
            Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 1f, -7f), Matrix4x4.CreateRotationX(0.2f)));
        // Head texOffs (0,0); Java addBox(-4,-2,-5, 7,3,5) → diagonal corners (−4,−2,−5)-(3,1,0).
        new EntityCuboid(-4f, -2f, -5f, 3f, 1f, 0f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, headParent, p.HeadScale);

        // Vanilla PhantomEyesLayer uses textures/entity/**/phantom_eyes — same head UV footprint (override for flying fallback).
        var eyesRef = eyesTextureRefOverride ?? CompanionDiffuseTextureRefFromSiblingFileStem(normalizedAssetPath, "phantom_eyes");
        var headEyesParent = Matrix4x4.Multiply(headParent, Matrix4x4.CreateTranslation(0f, 0f, 0.04f));
        new EntityCuboid(-4f, -2f, -5f, 3f, 1f, 0f, 0, 0).Emit(b, headEyesParent, p.HeadScale, "#eyes");
        return ApplyLivingEntityRendererPreviewBasis(
            b.Build(texRef, new Dictionary<string, string>(StringComparer.Ordinal) { ["eyes"] = eyesRef }));
    }


    private static MergedJavaBlockModel BuildParrot(string texRef, MinecraftNativeProfile profile, bool isBaby, float wingFlap)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.82f, 1.0f, 0.84f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.90f, 1.0f, 0.92f) : BabyProfile.Adult);
        var b = new RigBuilder(32, 32);
        // ParrotModel (gda): body 3x6x3, tail 3x4x1, head 2x3x2 + beak layers, wings 1x5x3, legs 1x2x1.
        new EntityCuboid(6.5f, 16.5f, 5f, 9.5f, 22.5f, 8f, 2, 8, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: 0.4937f, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(8f, 16.5f, 5f) }.Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(6.5f, 20.07f, 8.16f, 9.5f, 24.07f, 9.16f, 22, 1, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: 1.015f, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(8f, 20.07f, 8.16f) }.Emit(b, Matrix4x4.Identity, p.BodyScale);
        new EntityCuboid(6.5f, 15.69f, 5.24f, 8.5f, 18.69f, 7.24f, 2, 2, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // head
        new EntityCuboid(7f, 15.19f, 3.24f, 9f, 16.19f, 7.24f, 10, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // head2
        new EntityCuboid(7.5f, 14.69f, 4.74f, 8.5f, 16.69f, 5.74f, 11, 7, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // beak1
        new EntityCuboid(7.5f, 15.69f, 3.74f, 8.5f, 16.69f, 4.74f, 16, 7, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.HeadScale); // beak2
        new EntityCuboid(9.5f, 16.94f, 4.24f, 10.5f, 21.94f, 7.24f, 19, 8, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: -0.6981f, YRot: -MathF.PI + wingFlap, ZRot: 0f) { RotationPivot = new Vector3(9.5f, 16.94f, 4.24f) }.Emit(b, Matrix4x4.Identity, p.LegScale); // left wing
        new EntityCuboid(5.5f, 16.94f, 4.24f, 6.5f, 21.94f, 7.24f, 19, 8, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: -0.6981f, YRot: -MathF.PI - wingFlap, ZRot: 0f) { RotationPivot = new Vector3(6.5f, 16.94f, 4.24f) }.Emit(b, Matrix4x4.Identity, p.LegScale); // right wing
        new EntityCuboid(7f, 22f, 6f, 8f, 24f, 7f, 14, 18, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // left leg
        new EntityCuboid(8f, 22f, 6f, 9f, 24f, 7f, 14, 18, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Identity, p.LegScale); // right leg
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }
}
