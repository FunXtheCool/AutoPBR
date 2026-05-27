using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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

}
