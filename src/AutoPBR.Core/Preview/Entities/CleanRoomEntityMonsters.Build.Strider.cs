using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    /// <summary>
    /// <c>BabyStriderModel.createBodyLayer</c> + <c>StriderModel.setupAnim</c> / <c>BabyStriderModel.customAnimations</c>
    /// (26.1.2 <c>client.jar</c> javap). Cuboids follow <c>createBodyLayer</c> float literals (the checked-in
    /// <c>BabyStriderModel.json</c> IR does not match javap for several boxes). Motion matches <c>setupAnim</c> (non-ridden)
    /// then baby <c>customAnimations</c>. Atlas <c>64×128</c> matches strider entity skins.
    /// </summary>
    private static MergedJavaBlockModel BuildBabyStrider(
        string texRef,
        float walkAnimationPos,
        float walkAnimationSpeed,
        float ageInTicks)
    {
        var b = new RigBuilder(64, 128);
        const float k10Deg = 0.17453292f;
        var w = walkAnimationPos;
        var sp = walkAnimationSpeed;
        var bodyZ = 0.4f * MathF.Sin(w * 1.5f) * sp;
        var bodyY = 17.25f - MathF.Cos(w * 1.5f) * 2f * sp;
        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, bodyY, 0f), EntityParityTemplate.Rz(bodyZ));

        new EntityCuboid(-3.5f, -3.75f, -4f, 3.5f, 3.25f, 4f, 0, 0, UvSizeW: 7, UvSizeH: 7, UvSizeD: 8).Emit(b, bodyPose, 1f);

        var leftLegX = MathF.Sin(w * 0.75f) * 2f * sp;
        var rightLegX = MathF.Sin(w * 0.75f + MathF.PI) * 2f * sp;
        var leftLegZ = k10Deg * MathF.Cos(w * 0.75f) * sp;
        var rightLegZ = k10Deg * MathF.Cos(w * 0.75f + MathF.PI) * sp;
        var leftLegY = 20f + 2f * MathF.Sin(w * 0.75f + MathF.PI) * sp;
        var rightLegY = 20f + 2f * MathF.Sin(w * 0.75f) * sp;

        var rightLegPose = EntityParityTemplate.Mul(EntityParityTemplate.T(-1.5f, rightLegY, 0f), EntityParityTemplate.Er(rightLegX, 0f, rightLegZ));
        new EntityCuboid(-1f, 0f, -1f, 1f, 4f, 1f, 0, 24, UvSizeW: 2, UvSizeH: 4, UvSizeD: 2).Emit(b, rightLegPose, 1f);

        var leftLegPose = EntityParityTemplate.Mul(EntityParityTemplate.T(1.5f, leftLegY, 0f), EntityParityTemplate.Er(leftLegX, 0f, leftLegZ));
        new EntityCuboid(-1f, 0f, -1f, 1f, 4f, 1f, 8, 24, UvSizeW: 2, UvSizeH: 4, UvSizeD: 2).Emit(b, leftLegPose, 1f);

        var bristleF = MathF.Cos(w * 1.5f + MathF.PI) * sp;
        var z2 = 0f;
        var z1 = 0f;
        var z0 = 0f;
        AccumulateStriderBristleZRotFromAnimateBristle(ageInTicks, bristleF, ref z2, ref z1, ref z0);

        var bristle0Pose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -4.25f, 2f), EntityParityTemplate.Rz(z0)));
        new EntityCuboid(-3.5f, -2.5f, 0f, 3.5f, 0.5f, 0f, 0, 21, UvSizeW: 7, UvSizeH: 3, UvSizeD: 1).Emit(b, bristle0Pose, 1f);

        var bristle1Pose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -4.25f, 0f), EntityParityTemplate.Rz(z1)));
        new EntityCuboid(-3.5f, -2.5f, 0f, 3.5f, 0.5f, 0f, 0, 18, UvSizeW: 7, UvSizeH: 3, UvSizeD: 1).Emit(b, bristle1Pose, 1f);

        var bristle2Pose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -4.25f, -2f), EntityParityTemplate.Rz(z2)));
        new EntityCuboid(-3.5f, -2.5f, 0f, 3.5f, 0.5f, 0f, 0, 15, UvSizeW: 7, UvSizeH: 3, UvSizeD: 1).Emit(b, bristle2Pose, 1f);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>AdultStriderModel.createBodyLayer</c> + <c>StriderModel.setupAnim</c> + <c>AdultStriderModel.customAnimations</c>
    /// (26.1.2 <c>client.jar</c> javap). Geometry literals align with
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.strider.AdultStriderModel.json</c>.
    /// </summary>
    private static MergedJavaBlockModel BuildStrider(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float walkAnimationPos,
        float walkAnimationSpeed,
        float ageInTicks)
    {
        _ = profile;
        if (isBaby)
        {
            return BuildBabyStrider(texRef, walkAnimationPos, walkAnimationSpeed, ageInTicks);
        }

        var b = new RigBuilder(64, 128);
        const float k10Deg = 0.17453292f;
        var w = walkAnimationPos;
        var sp = walkAnimationSpeed;
        var bodyZ = 0.4f * MathF.Sin(w * 1.5f) * sp;
        var bodyExtraY = 1f - MathF.Cos(w * 1.5f) * 2f * sp;
        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 1f + bodyExtraY, 0f), EntityParityTemplate.Rz(bodyZ));

        var leftLegX = MathF.Sin(w * 0.75f) * 2f * sp;
        var rightLegX = MathF.Sin(w * 0.75f + MathF.PI) * 2f * sp;
        var leftLegZ = k10Deg * MathF.Cos(w * 0.75f) * sp;
        var rightLegZ = k10Deg * MathF.Cos(w * 0.75f + MathF.PI) * sp;
        var leftLegY = 8f + 2f * MathF.Sin(w * 0.75f + MathF.PI) * sp;
        var rightLegY = 8f + 2f * MathF.Sin(w * 0.75f) * sp;

        var rightLegPose = EntityParityTemplate.Mul(EntityParityTemplate.T(-4f, rightLegY, 0f), EntityParityTemplate.Er(rightLegX, 0f, rightLegZ));
        new EntityCuboid(-2f, 0f, -2f, 2f, 16f, 2f, 0, 32, UvSizeW: 4, UvSizeH: 16, UvSizeD: 4).Emit(b, rightLegPose, 1f);

        var leftLegPose = EntityParityTemplate.Mul(EntityParityTemplate.T(4f, leftLegY, 0f), EntityParityTemplate.Er(leftLegX, 0f, leftLegZ));
        new EntityCuboid(-2f, 0f, -2f, 2f, 16f, 2f, 0, 55, UvSizeW: 4, UvSizeH: 16, UvSizeD: 4).Emit(b, leftLegPose, 1f);

        new EntityCuboid(-8f, -6f, -8f, 8f, 8f, 8f, 0, 0, UvSizeW: 16, UvSizeH: 14, UvSizeD: 16).Emit(b, bodyPose, 1f);

        var bristleF = MathF.Cos(w * 1.5f + MathF.PI) * sp;
        var zRt = -0.87266463f;
        var zRm = -1.134464f;
        var zRb = -1.2217305f;
        AccumulateStriderBristleZRotFromAnimateBristle(ageInTicks, bristleF, ref zRt, ref zRm, ref zRb);
        var zLt = 0.87266463f;
        var zLm = 1.134464f;
        var zLb = 1.2217305f;
        AccumulateStriderBristleZRotFromAnimateBristle(ageInTicks, bristleF, ref zLt, ref zLm, ref zLb);

        var bristleBaseRightBottom = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-8f, 4f, -8f), EntityParityTemplate.Rz(zRb)));
        var bristleBaseRightMid = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-8f, -1f, -8f), EntityParityTemplate.Rz(zRm)));
        var bristleBaseRightTop = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-8f, -5f, -8f), EntityParityTemplate.Rz(zRt)));
        var bristleBaseLeftTop = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(8f, -6f, -8f), EntityParityTemplate.Rz(zLt)));
        var bristleBaseLeftMid = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(8f, -2f, -8f), EntityParityTemplate.Rz(zLm)));
        var bristleBaseLeftBottom = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(8f, 3f, -8f), EntityParityTemplate.Rz(zLb)));

        // Vanilla sheet quads are 0-thick on Y; use a 1-texel slab so parity tests see stable non-zero extent (see RigBuilder AddBox).
        new EntityCuboid(-12f, -0.5f, 0f, 0f, 0.5f, 16f, 16, 65, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseRightBottom, 1f);
        new EntityCuboid(-12f, -0.5f, 0f, 0f, 0.5f, 16f, 16, 49, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseRightMid, 1f);
        new EntityCuboid(-12f, -0.5f, 0f, 0f, 0.5f, 16f, 16, 33, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseRightTop, 1f);
        new EntityCuboid(0f, -0.5f, 0f, 12f, 0.5f, 16f, 16, 33, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseLeftTop, 1f);
        new EntityCuboid(0f, -0.5f, 0f, 12f, 0.5f, 16f, 16, 49, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseLeftMid, 1f);
        new EntityCuboid(0f, -0.5f, 0f, 12f, 0.5f, 16f, 16, 65, UvSizeW: 12, UvSizeH: 1, UvSizeD: 16).Emit(b, bristleBaseLeftBottom, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

}
