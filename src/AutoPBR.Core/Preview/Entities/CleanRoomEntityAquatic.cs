using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Fish, dolphin, axolotl, frog, turtle, guardian, nautilus.

    /// <summary>
    /// Aquatic-family fallback when <see cref="TryBuildSpecific"/> did not match. Vanilla stems in <see cref="AquaticKeys"/> almost
    /// always resolve to dedicated rigs (cod, salmon, dolphin, …); this path is rare. Mesh/UVs match Java <c>CodModel</c>
    /// (<see cref="BuildCod"/> — <c>getTexturedModelData</c>, <c>32×32</c> atlas).
    /// Exposed <see langword="internal"/> for parity tests.
    /// </summary>
    internal static MergedJavaBlockModel BuildAquatic(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float tailSway) =>
        BuildCod(texRef, profile, isBaby, tailSway);


    private static MergedJavaBlockModel BuildCod(string texRef, MinecraftNativeProfile profile, bool isBaby, float tailSway)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.84f, 1.0f, 0.84f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.92f, 1.0f, 0.92f) : BabyProfile.Adult);

        var b = new RigBuilder(32, 32);
        _ = TryBuildCodMeshFromGeometryIr(b, profile, p, tailSway, out _);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildSalmon(string texRef, MinecraftNativeProfile profile, bool isBaby, float tailSway)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.84f, 1.0f, 0.84f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.92f, 1.0f, 0.92f) : BabyProfile.Adult);

        var b = new RigBuilder(32, 32);
        _ = TryBuildSalmonMeshFromGeometryIr(b, profile, p, tailSway, out _);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    private static MergedJavaBlockModel BuildTropicalFishA(string texRef, MinecraftNativeProfile profile, bool isBaby, float tailSway)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.84f, 1.0f, 0.84f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.92f, 1.0f, 0.92f) : BabyProfile.Adult);

        const float thin = 0.08f;
        var b = new RigBuilder(32, 32);
        // TropicalFishModelA.getTexturedModelData (~1.21.4): fins use Ry(±pi/4); tail/top use zero-width X planes → thin solids.
        var bodyPose = Matrix4x4.CreateTranslation(0f, 22f, 0f);
        new EntityCuboid(-1f, -1.5f, -3f, 1f, 1.5f, 3f, 0, 0).Emit(b, bodyPose, p.BodyScale);

        var tailPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 22f, 3f), Matrix4x4.CreateRotationY(-tailSway * 0.42f));
        new EntityCuboid(-thin, -1.5f, 0f, thin, 1.5f, 6f, 22, 26).Emit(b, tailPose, p.LegScale);

        var rightFin = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-1f, 22.5f, 0f), Matrix4x4.CreateRotationY(MathF.PI / 4f));
        new EntityCuboid(-2f, -1f, -thin, 0f, 1f, thin, 2, 16).Emit(b, rightFin, p.LegScale);

        var leftFin = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(1f, 22.5f, 0f), Matrix4x4.CreateRotationY(-MathF.PI / 4f));
        new EntityCuboid(0f, -1f, -thin, 2f, 1f, thin, 2, 12).Emit(b, leftFin, p.LegScale);

        var topPose = Matrix4x4.CreateTranslation(0f, 20.5f, -3f);
        new EntityCuboid(-thin, -3f, 0f, thin, 0f, 6f, 10, 27).Emit(b, topPose, p.BodyScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildTropicalFishB(string texRef, MinecraftNativeProfile profile, bool isBaby, float tailSway)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.84f, 1.0f, 0.84f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.92f, 1.0f, 0.92f) : BabyProfile.Adult);

        const float thin = 0.08f;
        var b = new RigBuilder(32, 32);
        // TropicalFishModelB.getTexturedModelData (~1.21.4): deeper body + bottom_fin sheet island at (20,21).
        var bodyPose = Matrix4x4.CreateTranslation(0f, 19f, 0f);
        new EntityCuboid(-1f, -3f, -3f, 1f, 3f, 3f, 0, 20).Emit(b, bodyPose, p.BodyScale);

        var tailPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 19f, 3f), Matrix4x4.CreateRotationY(-tailSway * 0.42f));
        new EntityCuboid(-thin, -3f, 0f, thin, 3f, 5f, 21, 16).Emit(b, tailPose, p.LegScale);

        var rightFin = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-1f, 20f, 0f), Matrix4x4.CreateRotationY(MathF.PI / 4f));
        new EntityCuboid(-2f, 0f, -thin, 0f, 2f, thin, 2, 16).Emit(b, rightFin, p.LegScale);

        var leftFin = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(1f, 20f, 0f), Matrix4x4.CreateRotationY(-MathF.PI / 4f));
        new EntityCuboid(0f, 0f, -thin, 2f, 2f, thin, 2, 12).Emit(b, leftFin, p.LegScale);

        var topPose = Matrix4x4.CreateTranslation(0f, 16f, -3f);
        new EntityCuboid(-thin, -4f, 0f, thin, 0f, 6f, 20, 11).Emit(b, topPose, p.BodyScale);

        var bottomPose = Matrix4x4.CreateTranslation(0f, 22f, -3f);
        new EntityCuboid(-thin, 0f, 0f, thin, 4f, 6f, 20, 21).Emit(b, bottomPose, p.BodyScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// Preview swim offset for <see cref="BuildDolphin"/> tail pitch: vanilla motion is state-driven; this adds a bounded
    /// sine on <paramref name="animationTimeSeconds"/> so dolphin previews drift over time.
    /// </summary>
    internal static float ComputePreviewDolphinSwimOscillation(float animationTimeSeconds) =>
        MathF.Sin(animationTimeSeconds * (MathF.PI * 2f * 1.2f)) * 0.08f;

    /// <summary>
    /// <c>BabyTurtleModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.turtle.BabyTurtleModel.json</c> (<c>128×64</c>).
    /// Part order matches IR DFS. Hatchling flippers use the same <paramref name="swimLift"/> preview wobble as <see cref="BuildTurtle"/>.
    /// </summary>
    private static MergedJavaBlockModel BuildBabyTurtle(string texRef, float swimLift)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(128, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 22.9f, 1f));
        new EntityCuboid(-2f, -1f, -2f, 2f, 1f, 2f, 0, 0, UvSizeW: 4, UvSizeH: 2, UvSizeD: 4).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 22.9f, -1f));
        new EntityCuboid(-1.5f, -2f, -3f, 1.5f, 1f, 0f, 0, 6, UvSizeW: 3, UvSizeH: 3, UvSizeD: 3, XRot: 0f, YRot: swimLift * 0.08f, ZRot: 0f) { RotationPivot = new Vector3(0f, -0.5f, -1.5f) }.Emit(b, headPose, p.HeadScale);

        var rightHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 23.9f, 2.5f)),
            EntityParityTemplate.Rz(-swimLift * 0.1f));
        new EntityCuboid(-2f, -0.001f, -0.5f, 0f, 0.001f, 0.5f, 0, 0, UvSizeW: 4, UvSizeH: 1, UvSizeD: 1).Emit(b, rightHindPose, p.LegScale);

        var leftHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2f, 23.9f, 2.5f)),
            EntityParityTemplate.Rz(swimLift * 0.1f));
        new EntityCuboid(0f, -0.001f, -0.5f, 2f, 0.001f, 0.5f, 0, 1, UvSizeW: 4, UvSizeH: 1, UvSizeD: 1, MirrorUv: true).Emit(b, leftHindPose, p.LegScale);

        var rightFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 23.9f, -0.5f)),
            EntityParityTemplate.Rz(swimLift * 0.12f));
        new EntityCuboid(-2f, -0.001f, -0.5f, 0f, 0.001f, 0.5f, 8, 6, UvSizeW: 4, UvSizeH: 1, UvSizeD: 1).Emit(b, rightFrontPose, p.LegScale);

        var leftFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2f, 23.9f, -0.5f)),
            EntityParityTemplate.Rz(-swimLift * 0.12f));
        new EntityCuboid(0f, -0.001f, -0.5f, 2f, 0.001f, 0.5f, 8, 7, UvSizeW: 4, UvSizeH: 1, UvSizeD: 1, MirrorUv: true).Emit(b, leftFrontPose, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BabySquidModel.createBodyLayer</c> — <c>javap -private -c</c> on <c>tools/minecraft-parity/26.1.2/client.jar</c>
    /// (<c>LayerDefinition.create(..., 32, 32)</c>; entity sheet <c>squid_baby.png</c> is <c>32×32</c>).
    /// Geometry IR <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.squid.BabySquidModel.json</c> still omits the tentacle loop; this builder matches bytecode (mantle + eight root tentacles, same angular layout as <see cref="BuildSquid"/> with radius <c>3</c>, anchor <c>y=18.5</c>, <c>texOffs(0,18)</c> <c>2×6×2</c>).
    /// </summary>
    private static MergedJavaBlockModel BuildBabySquid(string texRef, float tentacleWave)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(32, 32);
        var bodyPose = EntityParityTemplate.T(0f, 13f, 0f);
        // Body: texOffs(0,0) addBox(-4,-5,-4, 8,10,8) + CubeDeformation(0) + PartPose.offset(0,13,0).
        new EntityCuboid(-4f, -5f, -4f, 4f, 5f, 4f, 0, 0, UvSizeW: 8, UvSizeH: 10, UvSizeD: 8).Emit(b, bodyPose, p.BodyScale);
        for (var i = 0; i < 8; i++)
        {
            var theta = i * MathF.PI / 4f;
            var rx = 3f * MathF.Cos(theta);
            var rz = 3f * MathF.Sin(theta);
            var yRot = MathF.PI / 2f - theta;
            var sway = tentacleWave * (i % 2 == 0 ? 0.8f : -0.7f);
            var tentaclePose = EntityParityTemplate.Mul(
                EntityParityTemplate.T(rx, 18.5f, rz),
                EntityParityTemplate.Ry(yRot + sway));
            new EntityCuboid(-1f, -0.5f, -1f, 1f, 5.5f, 1f, 0, 18, UvSizeW: 2, UvSizeH: 6, UvSizeD: 2).Emit(b, tentaclePose, p.LegScale);
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BabyDolphinModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.dolphin.BabyDolphinModel.json</c> (<c>64×64</c>).
    /// Hierarchy matches <see cref="BuildDolphin"/> (body-anchored head/fins/tail). <paramref name="swimSway"/> drives tail pitch like the adult preview.
    /// </summary>
    private static MergedJavaBlockModel BuildBabyDolphin(string texRef, float swimSway)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;
        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 21.5f, 0f));

        new EntityCuboid(-2.5f, -4f, 6f - 0.01f, 2.5f, 4f, 6f + 0.01f, 20, 0, UvSizeW: 5, UvSizeH: 8, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(0f, 1f, -4f));
        new EntityCuboid(-3.5f, -4f, 6f - 0.01f, 1.5f, 0f, 6f + 0.01f, 0, 0, UvSizeW: 5, UvSizeH: 4, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        var nosePose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(0f, 0.5f, -4f));
        new EntityCuboid(-1f, -2f, 2f - 0.01f, 1f, 0f, 2f + 0.01f, 0, 9, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, nosePose, p.HeadScale);

        var leftFinPose = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(1.8f, 0.85f, -2.6f), EntityParityTemplate.Er(0.87266463f, 0f, 2.0943952f)));
        new EntityCuboid(-1.5f, -0.5f, 1f - 0.01f, 1.5f, 5.5f, 1f + 0.01f, 34, 18, UvSizeW: 3, UvSizeH: 6, UvSizeD: 1, MirrorUv: true).Emit(b, leftFinPose, p.BodyScale);

        var rightFinPose = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(-1.8f, 0.85f, -2.6f), EntityParityTemplate.Er(0.87266463f, 0f, -1.701696f)));
        new EntityCuboid(-1.5f, -0.5f, 1f - 0.01f, 1.5f, 5.5f, 1f + 0.01f, 48, 18, UvSizeW: 3, UvSizeH: 6, UvSizeD: 1).Emit(b, rightFinPose, p.BodyScale);

        var tailPitch = -0.10471976f - swimSway * 0.2f;
        var tailPose = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 1f, 4f), EntityParityTemplate.Er(tailPitch, 0f, 0f)));
        new EntityCuboid(-1.5f, 0f, 4f - 0.01f, 1.5f, 7f, 4f + 0.01f, 0, 13, UvSizeW: 3, UvSizeH: 7, UvSizeD: 1).Emit(b, tailPose, p.LegScale);

        var tailFinPose = EntityParityTemplate.Mul(tailPose, EntityParityTemplate.T(0f, 0f, 6f));
        new EntityCuboid(-0.5f, -1f, 8f - 0.01f, 0.5f, 3f, 8f + 0.01f, 22, 13, UvSizeW: 1, UvSizeH: 4, UvSizeD: 1).Emit(b, tailFinPose, p.LegScale);

        var backFinPose = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -1f, -2.7f), EntityParityTemplate.Rx(0.87266463f)));
        new EntityCuboid(-1f, 1f, 1f - 0.01f, 2f, 5f, 1f + 0.01f, 42, 0, UvSizeW: 3, UvSizeH: 4, UvSizeD: 1).Emit(b, backFinPose, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BabyAxolotlModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.axolotl.BabyAxolotlModel.json</c> (<c>64×64</c>).
    /// Uses <c>T(0,24,0)</c> root lift (same convention as <see cref="BuildFrog"/>), body stack + head/gills like <see cref="BuildAxolotl"/>, IR part order for limbs/tail.
    /// </summary>
    private static MergedJavaBlockModel BuildBabyAxolotl(
        string texRef,
        float idleBob,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        _ = rightHindLegPitchRad;
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var rootLift = EntityParityTemplate.T(0f, 24f, 0f);
        var bodyPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(rootLift, EntityParityTemplate.T(0f, -1.25f, 1.75f)),
            EntityParityTemplate.Rx(idleBob));

        new EntityCuboid(-0.75f, -2.75f, 4f - 0.01f, 1.25f, 3.25f, 4f + 0.01f, 0, 0, UvSizeW: 2, UvSizeH: 6, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-1.75f, -2.75f, 0f - 0.01f, 1.25f, 2.25f, 0f + 0.01f, 0, 12, UvSizeW: 3, UvSizeH: 5, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        var rfAnchor = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(-2f, 0.25f, -1.25f));
        var rfPose = EntityParityTemplate.Mul(rfAnchor, EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(0f, -0.5f, 3f - 0.01f, 0f, 0.5f, 3f + 0.01f, 20, 16, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, rfPose, p.LegScale);

        var rfR1Pose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(rfAnchor, EntityParityTemplate.Er(-1.5707964f, 0f, 1.5707964f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(0f, -0.5f, 3f - 0.01f, 0f, 0.5f, 3f + 0.01f, 20, 14, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, rfR1Pose, p.LegScale);

        var lfPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(2f, 0.25f, -1.25f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(0f, -0.5f, 3f - 0.01f, 0f, 0.5f, 3f + 0.01f, 20, 13, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, lfPose, p.LegScale);

        var lhPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(2f, 0.25f, 1.75f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(0f, -0.5f, 3f - 0.01f, 0f, 0.5f, 3f + 0.01f, 20, 14, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, lhPose, p.LegScale);

        var tailPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(0f, -0.25f, 3.25f));
        new EntityCuboid(-1.5f, -1f, 0f - 0.01f, 1.5f, 7f, 0f + 0.01f, 10, 9, UvSizeW: 3, UvSizeH: 8, UvSizeD: 1).Emit(b, tailPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(0f, 0.25f, -2.75f));
        new EntityCuboid(-2f, -4f, 6f - 0.01f, 1f, 0f, 6f + 0.01f, 0, 8, UvSizeW: 3, UvSizeH: 4, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        var leftGillsPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(3f, -0.5f, -2f));
        new EntityCuboid(-3.5f, 0f, 3f - 0.01f, 1.5f, 0f, 3f + 0.01f, 20, 8, UvSizeW: 5, UvSizeH: 1, UvSizeD: 1).Emit(b, leftGillsPose, p.HeadScale);

        var rightGillsPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(-3f, -0.5f, -2f));
        new EntityCuboid(-3.5f, 0f, 3f - 0.01f, 1.5f, 0f, 3f + 0.01f, 20, 3, UvSizeW: 5, UvSizeH: 1, UvSizeD: 1).Emit(b, rightGillsPose, p.HeadScale);

        var topGillsPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(0f, -2f, -2f));
        new EntityCuboid(-3f, 0f, 6f - 0.01f, 0f, 0f, 6f + 0.01f, 20, 0, UvSizeW: 3, UvSizeH: 1, UvSizeD: 1).Emit(b, topGillsPose, p.HeadScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

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
