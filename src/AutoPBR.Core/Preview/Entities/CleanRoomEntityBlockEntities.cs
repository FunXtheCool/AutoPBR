using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Block-linked entity textures (bed, boat, signs, chest).

    /// <summary>
    /// BannerModel.createBodyLayer (~1.21.11 hgi): flag (20x40x1), bar (20x2x2), and standing pole (2x42x2) on 64x64 atlas.
    /// Wall variant omits pole but keeps bar + cloth.
    /// </summary>
    private static MergedJavaBlockModel BuildBannerFlag(string texRef, MinecraftNativeProfile profile, bool isBaby, bool isWall)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);
        // Vanilla local coordinates: cloth hangs from bar. Keeping this stack in local model space preserves canonical cuboid extents.
        new EntityCuboid(-10f, 0f, -2f, 10f, 40f, -1f, 0, 0).Emit(b, Matrix4x4.Identity, 1f); // flag cloth
        new EntityCuboid(-10f, -2f, -1f, 10f, 0f, 1f, 0, 42).Emit(b, Matrix4x4.Identity, 1f); // top bar
        if (!isWall)
        {
            new EntityCuboid(-1f, -30f, -1f, 1f, 12f, 1f, 44, 0).Emit(b, Matrix4x4.Identity, 1f); // standing pole
        }

        return b.Build(texRef);
    }

    /// <summary>
    /// Entity-atlas preview facing from <c>BedRenderer.createModelTransform</c> (26.1.2): lift 9 texels, lay flat (Rx 90°),
    /// spin 180° around model center. Default <paramref name="facingYRotDegrees"/> matches south (0°).
    /// </summary>
    private static Matrix4x4 CreateBedPreviewFacingTransform(float facingYRotDegrees = 0f)
    {
        const float pivot = 8f;
        var lift = EntityParityTemplate.T(0f, 9f, 0f);
        var layFlat = EntityParityTemplate.Rx(MathF.PI / 2f);
        var spin = EntityParityTemplate.Rz(MathF.PI + facingYRotDegrees * (MathF.PI / 180f));
        var spinAroundCenter = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(EntityParityTemplate.T(pivot, pivot, pivot), spin),
            EntityParityTemplate.T(-pivot, -pivot, -pivot));
        return EntityParityTemplate.Mul(EntityParityTemplate.Mul(lift, layFlat), spinAroundCenter);
    }

    /// <summary>BedRenderer layers (26.1.2): head + foot each with main 16×16×6 slab and rotated 3×3×3 legs on atlas 64×64.</summary>
    private static MergedJavaBlockModel BuildBed(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);

        static Matrix4x4 BedPartPoseTexel(float tx, float ty, float tz, float xRad = 0f, float yRad = 0f, float zRad = 0f) =>
            BlockRowAffineToTexel(EntityParityTemplate.ModelPartRenderLocalBlock(tx, ty, tz, xRad, yRad, zRad));

        // Head piece (createHeadLayer).
        new EntityCuboid(0f, 0f, 0f, 16f, 16f, 6f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(0f, 6f, 0f, 3f, 9f, 3f, 50, 6).Emit(b, BedPartPoseTexel(0f, 0f, 0f, MathF.PI / 2f, 0f, MathF.PI / 2f), 1f);
        new EntityCuboid(-16f, 6f, 0f, -13f, 9f, 3f, 50, 18).Emit(b, BedPartPoseTexel(0f, 0f, 0f, MathF.PI / 2f, 0f, MathF.PI), 1f);

        // Foot piece (createFootLayer) merged +16Y so mattress halves abut before preview facing.
        var footLayerOffset = BedPartPoseTexel(0f, 16f, 0f);
        new EntityCuboid(0f, 0f, 0f, 16f, 16f, 6f, 0, 22).Emit(b, footLayerOffset, 1f);
        new EntityCuboid(0f, 6f, -16f, 3f, 9f, -13f, 50, 0).Emit(
            b,
            EntityParityTemplate.Mul(BedPartPoseTexel(0f, 0f, 0f, MathF.PI / 2f, 0f, 0f), footLayerOffset),
            1f);
        new EntityCuboid(-16f, 6f, -16f, -13f, 9f, -13f, 50, 12).Emit(
            b,
            EntityParityTemplate.Mul(BedPartPoseTexel(0f, 0f, 0f, MathF.PI / 2f, 0f, 3f * MathF.PI / 2f), footLayerOffset),
            1f);
        return ApplyGlobalTransform(b.Build(texRef), CreateBedPreviewFacingTransform());
    }

    /// <summary>
    /// Fallback for equipment diffuse paths that do not match a dedicated equipment folder. Uses the same body shell as
    /// <see cref="BuildEquineHorseLike"/> — <c>AbstractEquineModel.createBodyLayer</c> (<c>hap.a</c>): <c>texOffs(0,32)</c>,
    /// cuboid (−5,−8,−17)–(5,2,5), <c>PartPose.offset(0,11,5)</c> — so atlas rows align with horse-body armor sheets.
    /// </summary>

    private static MergedJavaBlockModel BuildBell(string texRef, MinecraftNativeProfile profile, bool isBaby, float swing)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(32, 32);
        var bodyPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 12f, 8f), Matrix4x4.CreateRotationX(MathF.Sin(swing) * 0.2f));
        new EntityCuboid(-3f, -6f, -3f, 3f, 1f, 3f, 0, 0).Emit(b, bodyPose, 1f); // bell body 6x7x6
        new EntityCuboid(-4f, -8f, -4f, 4f, -6f, 4f, 0, 13).Emit(b, bodyPose, 1f); // bell base 8x2x8
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildMinecart(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        // MinecartModel.createBodyLayer (~1.21.x): floor + 4 wall panels.
        // Runtime matrix application is row-vector-based; encode part pose directly as (R * T) in localToParent.
        static Matrix4x4 PartPose(float tx, float ty, float tz, float xRot = 0f, float yRot = 0f, float zRot = 0f)
        {
            var rot = Matrix4x4.Multiply(
                Matrix4x4.CreateRotationZ(zRot),
                Matrix4x4.Multiply(Matrix4x4.CreateRotationY(yRot), Matrix4x4.CreateRotationX(xRot)));
            return Matrix4x4.Multiply(rot, Matrix4x4.CreateTranslation(tx, ty, tz));
        }

        new EntityCuboid(-10f, -8f, -1f, 10f, 8f, 1f, 0, 10).Emit(b, PartPose(0f, 4f, 0f, xRot: -MathF.PI / 2f), 1f); // floor 20x16x2 (vanilla part pose)

        const float wallMinX = -8f;
        const float wallMinY = -9f;
        const float wallMinZ = -1f;
        const float wallMaxX = 8f;
        const float wallMaxY = -1f;
        const float wallMaxZ = 1f;

        new EntityCuboid(wallMinX, wallMinY, wallMinZ, wallMaxX, wallMaxY, wallMaxZ, 0, 0).Emit(b, PartPose(-9f, 4f, 0f, yRot: 3f * MathF.PI / 2f), 1f); // front
        new EntityCuboid(wallMinX, wallMinY, wallMinZ, wallMaxX, wallMaxY, wallMaxZ, 0, 0).Emit(b, PartPose(9f, 4f, 0f, yRot: MathF.PI / 2f), 1f); // back
        new EntityCuboid(wallMinX, wallMinY, wallMinZ, wallMaxX, wallMaxY, wallMaxZ, 0, 0).Emit(b, PartPose(0f, 4f, -7f, yRot: MathF.PI), 1f); // left
        new EntityCuboid(wallMinX, wallMinY, wallMinZ, wallMaxX, wallMaxY, wallMaxZ, 0, 0).Emit(b, PartPose(0f, 4f, 7f), 1f); // right

        // Uniform mesh-level correction: flip the fully assembled cart once (instead of per-part tweaks).
        // Keep vanilla texOffs-driven UV assignment from model construction.
        return ApplyGlobalTransform(b.Build(texRef), Matrix4x4.CreateRotationX(MathF.PI));
    }


    private static bool PathIsRaftBoat(string path) =>
        path.Contains("/textures/entity/boat/bamboo", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/textures/entity/chest_boat/bamboo", StringComparison.OrdinalIgnoreCase);

    private static MergedJavaBlockModel BuildBoatFamily(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        bool isChestBoat,
        string normalizedAssetPath) =>
        PathIsRaftBoat(normalizedAssetPath)
            ? BuildRaft(texRef, profile, isBaby, isChestBoat)
            : BuildBoat(texRef, profile, isBaby, isChestBoat);

    private static MergedJavaBlockModel BuildBoat(string texRef, MinecraftNativeProfile profile, bool isBaby, bool isChestBoat = false)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(128, isChestBoat ? 128 : 64);
        const float paddleZ = 0.19634955f;
        var paddlePivot = new Vector3(0f, 1f, 4f);
        new EntityCuboid(-14f, -9f, -3f, 14f, 7f, 0f, 0, 0, XRot: MathF.PI / 2f, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(0f, -1f, -1.5f) }.Emit(b, Matrix4x4.CreateTranslation(0f, 3f, 1f), 1f); // bottom 28x16x3
        new EntityCuboid(-13f, -7f, -1f, 5f, -1f, 1f, 0, 19, XRot: 0f, YRot: 3f * MathF.PI / 2f, ZRot: 0f) { RotationPivot = new Vector3(-4f, -4f, 0f) }.Emit(b, Matrix4x4.CreateTranslation(-15f, 4f, 4f), 1f); // back
        new EntityCuboid(-8f, -7f, -1f, 8f, -1f, 1f, 0, 27, XRot: 0f, YRot: MathF.PI / 2f, ZRot: 0f) { RotationPivot = new Vector3(0f, -4f, 0f) }.Emit(b, Matrix4x4.CreateTranslation(15f, 4f, 0f), 1f); // front
        new EntityCuboid(-14f, -7f, -1f, 14f, -1f, 1f, 0, 35, XRot: 0f, YRot: MathF.PI, ZRot: 0f) { RotationPivot = new Vector3(0f, -4f, 0f) }.Emit(b, Matrix4x4.CreateTranslation(0f, 4f, -9f), 1f); // right wall
        new EntityCuboid(-14f, -7f, -1f, 14f, -1f, 1f, 0, 43).Emit(b, Matrix4x4.CreateTranslation(0f, 4f, 9f), 1f); // left wall
        var leftPaddleRoot = Matrix4x4.CreateTranslation(3f, -5f, 9f);
        new EntityCuboid(-1f, 0f, -5f, 1f, 2f, 13f, 62, 0, ZRot: paddleZ) { RotationPivot = paddlePivot }.Emit(b, leftPaddleRoot, 1f);
        new EntityCuboid(-1.001f, -3f, 8f, -0.001f, 3f, 15f, 62, 0, ZRot: paddleZ) { RotationPivot = paddlePivot }.Emit(b, leftPaddleRoot, 1f);
        var rightPaddleRoot = Matrix4x4.CreateTranslation(3f, -5f, -9f);
        new EntityCuboid(-1f, 0f, -5f, 1f, 2f, 13f, 62, 20, YRot: MathF.PI, ZRot: paddleZ) { RotationPivot = paddlePivot }.Emit(b, rightPaddleRoot, 1f);
        new EntityCuboid(0.001f, -3f, 8f, 1.001f, 3f, 15f, 62, 20, YRot: MathF.PI, ZRot: paddleZ) { RotationPivot = paddlePivot }.Emit(b, rightPaddleRoot, 1f);
        if (isChestBoat)
        {
            EmitChestBoatStack(b);
        }

        return b.Build(texRef);
    }

    /// <summary>RaftModel.createRaftModel / createChestRaftModel (26.1.2 javap): flat bottom slab + paddles only.</summary>
    private static MergedJavaBlockModel BuildRaft(string texRef, MinecraftNativeProfile profile, bool isBaby, bool isChestBoat = false)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(128, isChestBoat ? 128 : 64);
        const float paddleZ = 0.19634955f;
        var paddlePivot = new Vector3(0f, 1f, 4f);
        var bottomPose = Matrix4x4.Multiply(
            Matrix4x4.CreateTranslation(0f, -2.1f, 1f),
            Matrix4x4.CreateRotationX(MathF.PI / 2f));
        new EntityCuboid(-14f, -11f, -4f, 14f, 9f, 0f, 0, 0).Emit(b, bottomPose, 1f);
        new EntityCuboid(-14f, -9f, -8f, 14f, 7f, -4f, 0, 0).Emit(b, bottomPose, 1f);
        var leftPaddleRoot = Matrix4x4.CreateTranslation(3f, -4f, 9f);
        new EntityCuboid(-1f, 0f, -5f, 1f, 2f, 13f, 0, 24, ZRot: paddleZ) { RotationPivot = paddlePivot }.Emit(b, leftPaddleRoot, 1f);
        new EntityCuboid(-1.001f, -3f, 8f, -0.001f, 3f, 15f, 0, 24, ZRot: paddleZ) { RotationPivot = paddlePivot }.Emit(b, leftPaddleRoot, 1f);
        var rightPaddleRoot = Matrix4x4.CreateTranslation(3f, -4f, -9f);
        new EntityCuboid(-1f, 0f, -5f, 1f, 2f, 13f, 40, 24, YRot: MathF.PI, ZRot: paddleZ) { RotationPivot = paddlePivot }.Emit(b, rightPaddleRoot, 1f);
        new EntityCuboid(0.001f, -3f, 8f, 1.001f, 3f, 15f, 40, 24, YRot: MathF.PI, ZRot: paddleZ) { RotationPivot = paddlePivot }.Emit(b, rightPaddleRoot, 1f);
        if (isChestBoat)
        {
            EmitChestRaftStack(b);
        }

        return b.Build(texRef);
    }

    private static void EmitChestBoatStack(RigBuilder b)
    {
        // BoatModel.createChestBoatModel: chest_bottom/lid/lock with PartPose.offsetAndRotation (T × Ry).
        var chestRoot = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-2f, -5f, -6f), Matrix4x4.CreateRotationY(-MathF.PI / 2f));
        new EntityCuboid(0f, 0f, 0f, 12f, 8f, 12f, 0, 76).Emit(b, chestRoot, 1f);
        new EntityCuboid(0f, 0f, 0f, 12f, 4f, 12f, 0, 59).Emit(b, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-2f, -9f, -6f), Matrix4x4.CreateRotationY(-MathF.PI / 2f)), 1f);
        new EntityCuboid(0f, 0f, 0f, 2f, 4f, 1f, 0, 59).Emit(b, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-1f, -6f, -1f), Matrix4x4.CreateRotationY(-MathF.PI / 2f)), 1f);
    }

    private static void EmitChestRaftStack(RigBuilder b)
    {
        var chestRoot = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-2f, -10.1f, -6f), Matrix4x4.CreateRotationY(-MathF.PI / 2f));
        new EntityCuboid(0f, 0f, 0f, 12f, 8f, 12f, 0, 76).Emit(b, chestRoot, 1f);
        new EntityCuboid(0f, 0f, 0f, 12f, 4f, 12f, 0, 59).Emit(b, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-2f, -14.1f, -6f), Matrix4x4.CreateRotationY(-MathF.PI / 2f)), 1f);
        new EntityCuboid(0f, 0f, 0f, 2f, 4f, 1f, 0, 59).Emit(b, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-1f, -11.1f, -1f), Matrix4x4.CreateRotationY(-MathF.PI / 2f)), 1f);
    }

    /// <summary>LeashKnotModel (1.21.11 <c>javap</c> <c>hhc</c>): single <c>knot</c> cuboid <c>6×8×6</c> at <c>texOffs(0,0)</c>, atlas <c>32×32</c>.</summary>
    private static MergedJavaBlockModel BuildLeashKnot(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(32, 32);
        new EntityCuboid(-3f, -8f, -3f, 3f, 0f, 3f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildStandingSignEntity(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        new EntityCuboid(-12f, 0f, -1f, 12f, 12f, 1f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-1f, 12f, -1f, 1f, 28f, 1f, 26, 0).Emit(b, Matrix4x4.Identity, 1f);
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildHangingSignEntity(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        new EntityCuboid(-10f, 2f, -1f, 10f, 12f, 1f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-1f, 12f, -1f, 1f, 22f, 1f, 22, 22).Emit(b, Matrix4x4.Identity, 1f);
        return b.Build(texRef);
    }

    /// <summary>
    /// Vanilla <c>DecoratedPotRenderer.createBaseLayer</c> / <c>createSidesLayer</c> (26.1.2 <c>javap</c> on <c>client.jar</c>).
    /// There is no <c>DecoratedPotModel</c> class; geometry uses <c>DECORATED_POT_BASE</c> (32×32) and <c>DECORATED_POT_SIDES</c> (16×16).
    /// Side-pattern UVs are packed at <c>texV = 32</c> on a 64× atlas so they do not overlap base texels in preview.
    /// </summary>
    private static MergedJavaBlockModel BuildDecoratedPotEntity(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);

        const float pi = MathF.PI;
        var neckPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 37f, 16f), EntityParityTemplate.Er(pi, 0f, 0f));

        // Base layer neck (CubeDeformation ± ignored for parity): texOffs (0,0) + (0,5).
        new EntityCuboid(4f, 17f, 4f, 12f, 20f, 12f, 0, 0).Emit(b, neckPose, 1f);
        new EntityCuboid(5f, 20f, 5f, 11f, 21f, 11f, 0, 5).Emit(b, neckPose, 1f);

        // texOffs(-14, 13).addBox(0,0,0, 14,0,14) — same mesh for top (offset 1,16,1) and bottom (1,0,1).
        // U uses 18 ≡ -14 (mod 32) for unfolded cube UV math on the 32-wide base sprite.
        var topPose = EntityParityTemplate.T(1f, 16f, 1f);
        var bottomPose = EntityParityTemplate.T(1f, 0f, 1f);
        new EntityCuboid(0f, 0f, 0f, 14f, 0f, 14f, 18, 13, UvSizeW: 14, UvSizeH: 1, UvSizeD: 14).Emit(b, topPose, 1f);
        new EntityCuboid(0f, 0f, 0f, 14f, 0f, 14f, 18, 13, UvSizeW: 14, UvSizeH: 1, UvSizeD: 14).Emit(b, bottomPose, 1f);

        // Sides: single north-only sheet in Java; full thin box with dz=0. texOffs(1,0) on 16×16 → packed below base.
        const int sideAtlasV = 32;
        var backPose = EntityParityTemplate.Mul(EntityParityTemplate.T(15f, 16f, 1f), EntityParityTemplate.Er(0f, 0f, pi));
        var leftPose = EntityParityTemplate.Mul(EntityParityTemplate.T(1f, 16f, 1f), EntityParityTemplate.Er(0f, -pi / 2f, pi));
        var rightPose = EntityParityTemplate.Mul(EntityParityTemplate.T(15f, 16f, 15f), EntityParityTemplate.Er(0f, pi / 2f, pi));
        var frontPose = EntityParityTemplate.Mul(EntityParityTemplate.T(1f, 16f, 15f), EntityParityTemplate.Er(pi, 0f, 0f));
        new EntityCuboid(0f, 0f, 0f, 14f, 16f, 0f, 1, sideAtlasV, UvSizeW: 14, UvSizeH: 16, UvSizeD: 1).Emit(b, backPose, 1f);
        new EntityCuboid(0f, 0f, 0f, 14f, 16f, 0f, 1, sideAtlasV, UvSizeW: 14, UvSizeH: 16, UvSizeD: 1).Emit(b, leftPose, 1f);
        new EntityCuboid(0f, 0f, 0f, 14f, 16f, 0f, 1, sideAtlasV, UvSizeW: 14, UvSizeH: 16, UvSizeD: 1).Emit(b, rightPose, 1f);
        new EntityCuboid(0f, 0f, 0f, 14f, 16f, 0f, 1, sideAtlasV, UvSizeW: 14, UvSizeH: 16, UvSizeD: 1).Emit(b, frontPose, 1f);

        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildConduitEntity(string texRef, MinecraftNativeProfile profile, bool isBaby, float spin)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        var rot = Matrix4x4.CreateRotationY(spin);
        new EntityCuboid(-6f, 2f, -6f, 6f, 14f, 6f, 0, 0).Emit(b, rot, 1f);
        new EntityCuboid(-3f, 5f, -3f, 3f, 11f, 3f, 0, 16).Emit(b, rot, 1f);
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildBeaconBeam(string texRef, MinecraftNativeProfile profile, bool isBaby, float scroll)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(16, 256);
        var thin = 0.25f;
        var y0 = 0f;
        var y1 = 64f;
        var r1 = Matrix4x4.CreateRotationY(scroll * MathF.PI * 2f);
        var r2 = Matrix4x4.Multiply(Matrix4x4.CreateRotationY(MathF.PI / 2f), Matrix4x4.CreateRotationY(scroll * MathF.PI * 2f));
        new EntityCuboid(-thin, y0, -thin, thin, y1, thin, 0, 0).Emit(b, r1, 1f);
        new EntityCuboid(-thin, y0, -thin, thin, y1, thin, 0, 0).Emit(b, r2, 1f);
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildBeamColumn(string texRef, MinecraftNativeProfile profile, bool isBaby, float twist)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(16, 256);
        var thin = 0.25f;
        var y0 = 0f;
        var y1 = 64f;
        var r1 = Matrix4x4.CreateRotationY(twist);
        var r2 = Matrix4x4.Multiply(Matrix4x4.CreateRotationY(MathF.PI / 2f), Matrix4x4.CreateRotationY(twist));
        new EntityCuboid(-thin, y0, -thin, thin, y1, thin, 0, 0).Emit(b, r1, 1f);
        new EntityCuboid(-thin, y0, -thin, thin, y1, thin, 0, 0).Emit(b, r2, 1f);
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildEndPortalSurface(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(16, 16);
        new EntityCuboid(-8f, 0f, -0.5f, 8f, 16f, 0.5f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildEnchantingTableBook(string texRef, MinecraftNativeProfile profile, bool isBaby, float flap)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);
        const float pageThin = 0.12f; // Vanilla book has 0-thickness lid/spine quads; preview keeps thin solids for stable UVs.
        var root = Matrix4x4.CreateTranslation(0f, 8f, 0f);
        var spread = Math.Clamp(flap, -1.2f, 1.2f);

        var leftLid = Matrix4x4.Multiply(root, Matrix4x4.CreateRotationY(-0.87266463f - spread * 0.35f));
        new EntityCuboid(-6f, -5f, -pageThin * 0.5f, 0f, 5f, pageThin * 0.5f, 0, 0).Emit(b, leftLid, 1f);

        var rightLid = Matrix4x4.Multiply(root, Matrix4x4.CreateRotationY(0.87266463f + spread * 0.35f));
        new EntityCuboid(0f, -5f, -pageThin * 0.5f, 6f, 5f, pageThin * 0.5f, 16, 0).Emit(b, rightLid, 1f);

        new EntityCuboid(-1f, -5f, -pageThin * 0.5f, 1f, 5f, pageThin * 0.5f, 12, 0).Emit(b, root, 1f); // seam
        new EntityCuboid(-5f, -4f, -0.98f, 0f, 4f, 0.02f, 0, 10).Emit(b, root, 1f); // left pages
        new EntityCuboid(0f, -4f, -0.98f, 5f, 4f, 0.02f, 12, 10).Emit(b, root, 1f); // right pages

        var flip = Math.Clamp(spread * 0.45f, -0.4f, 0.4f);
        var leftFlip = Matrix4x4.Multiply(root, Matrix4x4.CreateRotationY(flip));
        var rightFlip = Matrix4x4.Multiply(root, Matrix4x4.CreateRotationY(-flip));
        new EntityCuboid(-5f, -4f, -0.2f, 0f, 4f, 0.8f, 24, 10).Emit(b, leftFlip, 1f); // flipping_page_left
        new EntityCuboid(0f, -4f, -0.2f, 5f, 4f, 0.8f, 24, 10).Emit(b, rightFlip, 1f); // flipping_page_right
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildChestEntity(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);
        var path = texRef.Replace('\\', '/').ToLowerInvariant();

        static Matrix4x4 ChestPartPose(float tx, float ty, float tz) =>
            BlockRowAffineToTexel(EntityParityTemplate.ModelPartRenderLocalBlock(tx, ty, tz));

        var lidLockPose = ChestPartPose(0f, 9f, 1f);

        if (path.Contains("_left", StringComparison.Ordinal))
        {
            // ChestModel.createDoubleBodyLeftLayer
            new EntityCuboid(0f, 0f, 1f, 15f, 10f, 15f, 0, 19).Emit(b, Matrix4x4.Identity, 1f);
            new EntityCuboid(0f, 0f, 0f, 15f, 5f, 14f, 0, 0).Emit(b, lidLockPose, 1f);
            new EntityCuboid(0f, -2f, 14f, 1f, 2f, 15f, 0, 0).Emit(b, lidLockPose, 1f);
        }
        else if (path.Contains("_right", StringComparison.Ordinal))
        {
            // ChestModel.createDoubleBodyRightLayer
            new EntityCuboid(1f, 0f, 1f, 16f, 10f, 15f, 0, 19).Emit(b, Matrix4x4.Identity, 1f);
            new EntityCuboid(1f, 0f, 0f, 16f, 5f, 14f, 0, 0).Emit(b, lidLockPose, 1f);
            new EntityCuboid(15f, -2f, 14f, 16f, 2f, 15f, 0, 0).Emit(b, lidLockPose, 1f);
        }
        else
        {
            // ChestModel.createSingleBodyLayer
            new EntityCuboid(1f, 0f, 1f, 15f, 10f, 15f, 0, 19).Emit(b, Matrix4x4.Identity, 1f);
            new EntityCuboid(1f, 0f, 0f, 15f, 5f, 14f, 0, 0).Emit(b, lidLockPose, 1f);
            new EntityCuboid(7f, -2f, 14f, 9f, 2f, 15f, 0, 0).Emit(b, lidLockPose, 1f);
        }

        return b.Build(texRef);
    }
}
