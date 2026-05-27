using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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
