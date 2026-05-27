using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

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
