using System;
using System.Collections.Generic;
using System.Numerics;
using AutoPBR.Core.Models;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    /// <summary>
    /// Vanilla <c>IllagerModel.setupAnim</c> (26.1.2 <c>client.jar</c>): head look, riding vs walk limbs, <c>IllagerArmPose</c> arms,
    /// folded <c>arms</c> vs separate arms visibility. Hat cuboid omitted — Java ctor forces <c>hat.visible = false</c>.
    /// Baby illagers reuse the adult <c>IllagerModel</c> with uniform <c>LivingEntity.getAgeScale()</c> (see <c>IllagerRenderer</c> → <c>MobRenderer</c>);
    /// IR: <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.illager.IllagerModel.json</c>.
    /// </summary>
    private static MergedJavaBlockModel BuildIllager(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        float wave,
        EntityIllagerPreviewArmPose armPose)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? BabyProfile.VanillaUniformBaby : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.12f, 0.84f) : BabyProfile.Adult);

        const float degToRad = 0.017453292f;
        var headYawRad = (wave * 8f + idlePhase01 * 6f) * degToRad;
        var headPitchRad = (idlePhase01 * 10f + wave * 4f) * degToRad;
        var showFoldedArms = armPose == EntityIllagerPreviewArmPose.Crossed;
        var showSeparateArms = !showFoldedArms;
        var attachVindicatorAxe = armPose == EntityIllagerPreviewArmPose.AttackingWeapon;

        IllagerPreviewPoseSupport.ComputeIllagerPreviewArmRotations(
            armPose,
            idlePhase01,
            animationTimeSeconds,
            wave,
            isRiding: false,
            out var rlX,
            out var rlY,
            out var rlZ,
            out var llX,
            out var llY,
            out var llZ,
            out var raX,
            out var raY,
            out var raZ,
            out var laX,
            out var laY,
            out var laZ);

        var b = new RigBuilder(64, 64);
        var headWorld = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 32f, 8f), EntityParityTemplate.Er(headPitchRad, headYawRad, 0f));
        new EntityCuboid(-4f, -10f, -4f, 4f, 0f, 4f, 0, 0).Emit(b, headWorld, p.HeadScale);
        new EntityCuboid(-1f, -1f, -6f, 1f, 3f, -4f, 24, 0).Emit(b, Matrix4x4.Multiply(headWorld, Matrix4x4.CreateTranslation(0f, -2f, 0f)), p.HeadScale);

        var bodyPose = Matrix4x4.CreateTranslation(8f, 12f, 8f);
        new EntityCuboid(-4f, 0f, -3f, 4f, 12f, 3f, 16, 20).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-4f, 0f, -3f, 4f, 20f, 3f, 0, 38).Emit(b, bodyPose, p.BodyScale);

        if (showFoldedArms)
        {
            var foldedArmsPose = Matrix4x4.Multiply(
                Matrix4x4.CreateTranslation(8f, 15f, 7f),
                Matrix4x4.CreateRotationX(-0.75f));
            new EntityCuboid(-8f, -2f, -2f, -4f, 6f, 2f, 44, 22).Emit(b, foldedArmsPose, p.BodyScale);
            new EntityCuboid(-4f, 2f, -2f, 4f, 6f, 2f, 40, 38).Emit(b, foldedArmsPose, p.BodyScale);
            new EntityCuboid(4f, -2f, -2f, 8f, 6f, 2f, 44, 22).Emit(b, foldedArmsPose, p.BodyScale);
        }

        if (showSeparateArms)
        {
            var rightArmPose = Matrix4x4.CreateTranslation(3f, 14f, 8f);
            var leftArmPose = Matrix4x4.CreateTranslation(13f, 14f, 8f);
            new EntityCuboid(-3f, -2f, -2f, 1f, 10f, 2f, 40, 46, XRot: raX, YRot: raY, ZRot: raZ) { RotationPivot = Vector3.Zero }.Emit(b, rightArmPose, p.BodyScale);
            new EntityCuboid(-1f, -2f, -2f, 3f, 10f, 2f, 40, 46, XRot: laX, YRot: laY, ZRot: laZ) { RotationPivot = Vector3.Zero }.Emit(b, leftArmPose, p.BodyScale);
            if (attachVindicatorAxe)
            {
                new EntityCuboid(-3.6f, 8.2f, -0.9f, -1.0f, 11.4f, 0.9f, 56, 0, XRot: raX, YRot: raY, ZRot: raZ) { RotationPivot = Vector3.Zero }.Emit(b, rightArmPose, p.LegScale);
            }
        }

        new EntityCuboid(-2f, 0f, -2f, 2f, 12f, 2f, 0, 22, XRot: rlX, YRot: rlY, ZRot: rlZ) { RotationPivot = Vector3.Zero }.Emit(b, Matrix4x4.CreateTranslation(6f, 0f, 8f), p.LegScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 12f, 2f, 0, 22, XRot: llX, YRot: llY, ZRot: llZ) { RotationPivot = Vector3.Zero }.Emit(b, Matrix4x4.CreateTranslation(10f, 0f, 8f), p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>Vanilla <c>WitchModel.setupAnim</c> (26.1.2 <c>client.jar</c>).</summary>
    private static MergedJavaBlockModel BuildWitch(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float yRotDegrees,
        float xRotDegrees,
        float walkAnimationPos,
        float walkAnimationSpeed,
        int entityId,
        float ageInTicks,
        bool isHoldingItem)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.74f, 1.22f, 0.76f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.14f, 0.84f) : BabyProfile.Adult);

        const float k6662 = 0.6662f;
        const float degToRad = 0.017453292f;
        var headYawRad = yRotDegrees * degToRad;
        var headPitchRad = xRotDegrees * degToRad;
        var rlX = MathF.Cos(walkAnimationPos * k6662) * 1.4f * walkAnimationSpeed * 0.5f;
        var llX = MathF.Cos(walkAnimationPos * k6662 + MathF.PI) * 1.4f * walkAnimationSpeed * 0.5f;
        var idRem = ((entityId % 10) + 10) % 10;
        var wobble = 0.01f * idRem;
        var noseX = MathF.Sin(ageInTicks * wobble) * 4.5f * degToRad;
        var noseZ = MathF.Cos(ageInTicks * wobble) * 2.5f * degToRad;
        if (isHoldingItem)
        {
            noseX = -0.9f;
        }

        var b = new RigBuilder(64, 128);
        var headWorld = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 32f, 8f), EntityParityTemplate.Er(headPitchRad, headYawRad, 0f));
        new EntityCuboid(-4f, -10f, -4f, 4f, 0f, 4f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, headWorld, p.HeadScale);
        var noseLocal = isHoldingItem
            ? Matrix4x4.Multiply(headWorld, Matrix4x4.CreateTranslation(0f, 1f, -1.5f))
            : Matrix4x4.Multiply(headWorld, Matrix4x4.CreateTranslation(0f, -2f, 0f));
        new EntityCuboid(-1f, -1f, -6f, 1f, 3f, -4f, 24, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: noseX, YRot: 0f, ZRot: noseZ) { RotationPivot = Vector3.Zero }.Emit(b, noseLocal, p.HeadScale);
        new EntityCuboid(0f, 1f, -6.75f, 1f, 2f, -5.75f, 0, 0, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, Matrix4x4.Multiply(headWorld, Matrix4x4.CreateTranslation(0f, -4f, 0f)), p.HeadScale);

        var hatBase = Matrix4x4.Multiply(headWorld, Matrix4x4.CreateTranslation(-5f, -10.03125f, -5f));
        new EntityCuboid(0f, 0f, 0f, 10f, 2f, 10f, 0, 64, OffsetX: 0, OffsetY: 0, OffsetZ: 0) { RotationPivot = Vector3.Zero }.Emit(b, hatBase, p.HeadScale);
        var hat2 = Matrix4x4.Multiply(hatBase, Matrix4x4.CreateTranslation(1.75f, -4f, 2f));
        new EntityCuboid(0f, 0f, 0f, 7f, 4f, 7f, 0, 76, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: -0.05235988f, YRot: 0f, ZRot: 0.02617994f) { RotationPivot = Vector3.Zero }.Emit(b, hat2, p.HeadScale);
        var hat3 = Matrix4x4.Multiply(hat2, Matrix4x4.CreateTranslation(1.75f, -4f, 2f));
        new EntityCuboid(0f, 0f, 0f, 4f, 4f, 4f, 0, 87, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: -0.10471976f, YRot: 0f, ZRot: 0.05235988f) { RotationPivot = Vector3.Zero }.Emit(b, hat3, p.HeadScale);
        var hatTip = Matrix4x4.Multiply(hat3, Matrix4x4.CreateTranslation(1.75f, -2f, 2f));
        new EntityCuboid(0f, 0f, 0f, 1.5f, 2.5f, 1.5f, 0, 95, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: -0.20943952f, YRot: 0f, ZRot: 0.10471976f) { RotationPivot = Vector3.Zero }.Emit(b, hatTip, p.HeadScale);

        var bodyPose = Matrix4x4.CreateTranslation(8f, 12f, 8f);
        new EntityCuboid(-4f, 0f, -3f, 4f, 12f, 3f, 16, 20, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-4f, 0f, -3f, 4f, 20f, 3f, 0, 38, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, bodyPose, p.BodyScale);
        var foldedArmsPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(8f, 15f, 7f), Matrix4x4.CreateRotationX(-0.75f));
        new EntityCuboid(-8f, -2f, -2f, -4f, 6f, 2f, 44, 22, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, foldedArmsPose, p.BodyScale);
        new EntityCuboid(-4f, 2f, -2f, 4f, 6f, 2f, 40, 38, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, foldedArmsPose, p.BodyScale);
        new EntityCuboid(4f, -2f, -2f, 8f, 6f, 2f, 44, 22, OffsetX: 0, OffsetY: 0, OffsetZ: 0).Emit(b, foldedArmsPose, p.BodyScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 12f, 2f, 0, 22, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: rlX, YRot: 0f, ZRot: 0f) { RotationPivot = Vector3.Zero }.Emit(b, Matrix4x4.CreateTranslation(6f, 0f, 8f), p.LegScale);
        new EntityCuboid(-2f, 0f, -2f, 2f, 12f, 2f, 0, 22, OffsetX: 0, OffsetY: 0, OffsetZ: 0, XRot: llX, YRot: 0f, ZRot: 0f) { RotationPivot = Vector3.Zero }.Emit(b, Matrix4x4.CreateTranslation(10f, 0f, 8f), p.LegScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildEvoker(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        float wave,
        string normalizedAssetPath) =>
        BuildIllager(
            texRef,
            profile,
            isBaby,
            idlePhase01,
            animationTimeSeconds,
            wave,
            EntityPreviewPoseCatalog.ResolveEffectiveIllagerArmPose(
                normalizedAssetPath,
                "Evoker",
                EntityPreviewBuildContext.CurrentPoseId));


    private static MergedJavaBlockModel BuildVindicator(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        float wave,
        string normalizedAssetPath) =>
        BuildIllager(
            texRef,
            profile,
            isBaby,
            idlePhase01,
            animationTimeSeconds,
            wave,
            EntityPreviewPoseCatalog.ResolveEffectiveIllagerArmPose(
                normalizedAssetPath,
                "Vindicator",
                EntityPreviewBuildContext.CurrentPoseId));

}
