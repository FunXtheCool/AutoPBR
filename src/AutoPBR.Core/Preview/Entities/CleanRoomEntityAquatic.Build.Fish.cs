using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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

}
