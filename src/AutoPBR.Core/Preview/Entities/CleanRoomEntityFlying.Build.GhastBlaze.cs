using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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

}
