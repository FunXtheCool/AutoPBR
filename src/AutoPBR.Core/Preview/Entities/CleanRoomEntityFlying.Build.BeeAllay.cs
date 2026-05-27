using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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

}
