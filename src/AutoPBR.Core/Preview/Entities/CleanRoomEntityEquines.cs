using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Equine horse/donkey/mule meshes + saddle equipment overlays.

    private static MergedJavaBlockModel BuildEquineSaddle(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        const float saddleD = 0.5f;
        const float headSaddleD = 0.22f;
        const float mouthWrapD = 0.2f;
        const float thin = 0.08f;
        var b = new RigBuilder(64, 64);

        // har texOffs / sizes from client bytecode (CubeDeformation where noted).
        new EntityCuboid(-5f - saddleD, -8f - saddleD, -9f - saddleD, 5f + saddleD, 1f + saddleD, 0f + saddleD, 26, 0, UvSizeW: 10, UvSizeH: 9, UvSizeD: 9).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-3f - headSaddleD, -11f - headSaddleD, -1.9f - headSaddleD, 3f + headSaddleD, -6f + headSaddleD, 4.1f + headSaddleD, 1, 1, UvSizeW: 6, UvSizeH: 5, UvSizeD: 6).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-2f - mouthWrapD, -11f - mouthWrapD, -4f - mouthWrapD, 2f + mouthWrapD, -6f + mouthWrapD, -2f + mouthWrapD, 19, 0, UvSizeW: 4, UvSizeH: 5, UvSizeD: 2).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(2f, -9f, -6f, 3f, -7f, -4f, 29, 5, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-3f, -9f, -6f, -2f, -7f, -4f, 29, 5, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2).Emit(b, Matrix4x4.Identity, 1f);

        var leftLinePose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(3.1f, -6f, -8f), Matrix4x4.CreateRotationX(-0.5235988f));
        new EntityCuboid(-thin, 0f, 0f, thin, 3f, 16f, 32, 2, UvSizeW: 1, UvSizeH: 3, UvSizeD: 16).Emit(b, leftLinePose, 1f);
        var rightLinePose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-3.1f, -6f, -8f), Matrix4x4.CreateRotationX(-0.5235988f));
        new EntityCuboid(-thin, 0f, 0f, thin, 3f, 16f, 32, 2, UvSizeW: 1, UvSizeH: 3, UvSizeD: 16).Emit(b, rightLinePose, 1f);
        return ApplyEquineLivingEntityRendererPreviewBasis(b.Build(texRef), modelScale: 1f);
    }

    private static MergedJavaBlockModel BuildEquipmentBodyOverlay(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.76f, 1f, 0.78f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.84f, 1f, 0.86f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 64);
        var bodyT = Matrix4x4.CreateTranslation(0f, 11f, 5f);
        new EntityCuboid(-5f, -8f, -17f, 5f, 2f, 5f, 0, 32).Emit(b, bodyT, p.BodyScale);
        return ApplyEquineLivingEntityRendererPreviewBasis(b.Build(texRef), modelScale: 1f);
    }

    private static MergedJavaBlockModel BuildHorse(string texRef, MinecraftNativeProfile profile, bool isBaby, float neckBend) =>
        BuildEquineHorseLike(texRef, profile, isBaby, neckBend, donkeyEars: false, donkeyChests: false, modelScale: 1f);

    private static MergedJavaBlockModel BuildHorseDonkeyMule(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float neckBend,
        bool donkeyChests = false)
    {
        var scale = texRef.Contains("/mule", StringComparison.OrdinalIgnoreCase) ? 0.92f : 0.87f;
        return BuildEquineHorseLike(texRef, profile, isBaby, neckBend, donkeyEars: true, donkeyChests: donkeyChests, modelScale: scale);
    }

    private static void AppendEquineDonkeyChestPair(RigBuilder b, Matrix4x4 bodyPose, float skinScale)
    {
        var leftChestPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(6f, -8f, 0f), EntityParityTemplate.Ry(-MathF.PI / 2f)));
        var rightChestPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-6f, -8f, 0f), EntityParityTemplate.Ry(MathF.PI / 2f)));
        new EntityCuboid(-4f, 0f, -2f, 4f, 8f, 1f, 26, 21).Emit(b, leftChestPose, skinScale);
        new EntityCuboid(-4f, 0f, -2f, 4f, 8f, 1f, 26, 21).Emit(b, rightChestPose, skinScale);
    }

    private static MergedJavaBlockModel BuildEquineHorseLike(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float neckBend,
        bool donkeyEars,
        bool donkeyChests,
        float modelScale)
    {
        if (isBaby)
        {
            return BuildBabyEquineHorseLike(texRef, donkeyEars, donkeyChests, modelScale, neckBend);
        }

        _ = profile; // Kept for parity dispatch signature; adult equine mesh uses <see cref="BabyProfile.Adult"/> scales only.
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);

        static float Lerp(float t, float a, float b) => a + ((b - a) * t);

        // 26.1.2 javap parity: AbstractEquineModel.createBodyMesh + DonkeyModel.modifyMesh.
        // setupAnim hard-port (idle baseline): stand/eat/feed/walk terms set to neutral defaults.
        var stand = 0f;
        var eat = 0f;
        var feed = 0f;
        var walkSpeed = 0f;
        var walkPos = 0f;
        var ageInTicks = 0f;
        var ageScale = 1f;
        var animateTail = false;
        var yRotDeg = 0f;
        // Deterministic static preview profile (no frame drift): vanilla setupAnim math with a
        // family-specific idle head pitch to keep the preview from sinking under the torso.
        var xRotDeg = donkeyEars ? -16f : -20f;
        var isInWater = false;

        const float degToRad = MathF.PI / 180f;
        const float deg15 = 0.2617994f;
        const float deg30 = 0.5235988f;
        const float deg45 = 0.7853982f;
        const float deg60 = 1.0471976f;
        const float deg125 = 2.1816616f;

        var clampedYawRad = Math.Clamp(yRotDeg, -20f, 20f) * degToRad;
        var headInputPitch = xRotDeg * degToRad;
        if (walkSpeed > 0.2f)
        {
            headInputPitch += MathF.Cos(walkPos * 0.8f) * 0.15f * walkSpeed;
        }

        var sway = MathF.Cos((isInWater ? 0.2f : 1f) * walkPos * 0.6662f + MathF.PI);
        var maxStandEat = MathF.Max(stand, eat);
        var headXRot = (stand * (deg15 + headInputPitch)) +
            (eat * (deg125 + MathF.Sin(ageInTicks) * 0.05f)) +
            ((1f - maxStandEat) * (deg30 + headInputPitch + (feed * MathF.Sin(ageInTicks) * 0.05f)));
        var headYRot = (stand * clampedYawRad) + ((1f - maxStandEat) * clampedYawRad);

        var headYOffset = Lerp(eat, Lerp(stand, 0f, -8f), 7f);
        var headZ = Lerp(stand, -12f, -4f);
        var bodyXRot = (stand * -deg45);
        var legStandAngle = deg15 * stand;
        var legFrontYOffset = 12f * stand;
        var legFrontZOffset = 4f * stand;
        var legStandingXRotOffset = -deg60;
        var legLeftFrontXRot = ((legStandingXRotOffset + MathF.Cos(ageInTicks * 0.6f + MathF.PI)) * stand) + (sway * 0.8f * walkSpeed * (1f - stand));
        var legRightFrontXRot = ((legStandingXRotOffset - MathF.Cos(ageInTicks * 0.6f + MathF.PI)) * stand) - (sway * 0.8f * walkSpeed * (1f - stand));
        var legLeftHindXRot = legStandAngle - (sway * 0.5f * walkSpeed * (1f - stand));
        var legRightHindXRot = legStandAngle + (sway * 0.5f * walkSpeed * (1f - stand));
        var tailXRot = ComputeAbstractEquineTailParentPitchRad(0f, walkSpeed);
        var tailYRot = animateTail ? MathF.Cos(ageInTicks * 0.7f) : 0f;
        var tailYOffset = walkSpeed * ageScale;
        var tailZOffset = walkSpeed * 2f * ageScale;

        var bodyPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 11f, 5f), EntityParityTemplate.Rx(bodyXRot));
        new EntityCuboid(-5f, -8f, -17f, 5f, 2f, 5f, 0, 32).Emit(b, bodyPose, p.BodyScale);

        var headPartsPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 4f + headYOffset, headZ), EntityParityTemplate.Er(headXRot + neckBend, headYRot, 0f));
        new EntityCuboid(-2.05f, -6f, -2f, 1.95f, 6f, 5f, 0, 35).Emit(b, headPartsPose, p.HeadScale);

        // Root legs (left leg UV mirrored in vanilla).
        new EntityCuboid(-3f, -1.01f, -1f, 1f, 9.99f, 3f, 48, 21, MirrorUv: true, XRot: legLeftHindXRot, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(4f, 14f, 7f) }.Emit(b, EntityParityTemplate.T(4f, 14f, 7f), p.LegScale); // left_hind_leg
        new EntityCuboid(-1f, -1.01f, -1f, 3f, 9.99f, 3f, 48, 21, XRot: legRightHindXRot, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(-4f, 14f, 7f) }.Emit(b, EntityParityTemplate.T(-4f, 14f, 7f), p.LegScale); // right_hind_leg
        new EntityCuboid(-3f, -1.01f, -1.9f, 1f, 9.99f, 2.1f, 48, 21, MirrorUv: true, XRot: legLeftFrontXRot, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(4f, 14f - legFrontYOffset, -10f + legFrontZOffset) }.Emit(b, EntityParityTemplate.T(4f, 14f - legFrontYOffset, -10f + legFrontZOffset), p.LegScale); // left_front_leg
        new EntityCuboid(-1f, -1.01f, -1.9f, 3f, 9.99f, 2.1f, 48, 21, XRot: legRightFrontXRot, YRot: 0f, ZRot: 0f) { RotationPivot = new Vector3(-4f, 14f - legFrontYOffset, -10f + legFrontZOffset) }.Emit(b, EntityParityTemplate.T(-4f, 14f - legFrontYOffset, -10f + legFrontZOffset), p.LegScale); // right_front_leg

        // Body child tail.
        var tailPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -5f + tailYOffset, 2f + tailZOffset), EntityParityTemplate.Er(tailXRot, tailYRot, 0f)));
        EntityParityTemplate.AssertFinitePose(bodyPose, "equine adult bodyPose");
        EntityParityTemplate.AssertFinitePose(headPartsPose, "equine adult headPartsPose");
        EntityParityTemplate.AssertFinitePose(tailPose, "equine adult tailPose");
        new EntityCuboid(-1.5f, 0f, 0f, 1.5f, 14f, 4f, 42, 36).Emit(b, tailPose, p.BodyScale);

        // head_parts children.
        var headPose = headPartsPose;
        new EntityCuboid(-3f, -11f, -2f, 3f, -6f, 5f, 0, 13).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-1f, -11f, 5.01f, 1f, 5f, 7.01f, 56, 36).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2f, -11f, -7f, 2f, -6f, -2f, 0, 25).Emit(b, headPose, p.HeadScale);

        if (donkeyChests)
        {
            AppendEquineDonkeyChestPair(b, bodyPose, p.BodyScale);
        }

        if (donkeyEars)
        {
            var earLeftPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(1.25f, -10f, 4f), EntityParityTemplate.Er(0.2617994f, 0f, 0.2617994f)));
            var earRightPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-1.25f, -10f, 4f), EntityParityTemplate.Er(0.2617994f, 0f, -0.2617994f)));
            new EntityCuboid(-1f, -7f, 0f, 1f, 0f, 1f, 0, 12).Emit(b, earLeftPose, p.HeadScale); // 2x7x1
            new EntityCuboid(-1f, -7f, 0f, 1f, 0f, 1f, 0, 12).Emit(b, earRightPose, p.HeadScale); // 2x7x1
        }
        else
        {
            // Horse ears with CubeDeformation(-0.001f).
            new EntityCuboid(0.55f, -13f, 4f, 2.55f, -10f, 5f, 19, 16, OffsetX: -0.001f, OffsetY: -0.001f, OffsetZ: -0.001f).Emit(b, headPose, p.HeadScale);
            new EntityCuboid(-2.55f, -13f, 4f, -0.55f, -10f, 5f, 19, 16, OffsetX: -0.001f, OffsetY: -0.001f, OffsetZ: -0.001f).Emit(b, headPose, p.HeadScale);
        }

        // Strict parity sweep: keep bytecode cuboids/poses untouched and apply only vanilla->preview axis conversion.
        // Equine parity uses right-compose scale on each part (see <see cref="ApplyEquineLivingEntityRendererPreviewBasis"/>); do not replace with <see cref="ApplyLivingEntityRendererPreviewBasis"/> without retuning.
        return ApplyEquineLivingEntityRendererPreviewBasis(b.Build(texRef), modelScale);
    }

    internal static float ComputeAbstractEquineTailParentPitchRad(float tailXRotOffsetRad, float walkSpeed) =>
        tailXRotOffsetRad + 0.5235988f + (walkSpeed * 0.75f);

    internal static float ComputeBabyDonkeySetupAnimHeadPartsXRotRad(
        float eatAnimation,
        float standAnimation,
        float feedingAnimation,
        float ageInTicks,
        float entityPitchDegreesAfterBabyMutation)
    {
        var xRotRad = entityPitchDegreesAfterBabyMutation * (MathF.PI / 180f);
        var blend = 1f - MathF.Max(standAnimation, eatAnimation);
        var feedingWave = feedingAnimation * MathF.Sin(ageInTicks) * 0.05f;
        var slot6 = blend * ((MathF.PI / 6f) + xRotRad + feedingWave);
        var standTerm = standAnimation * ((MathF.PI / 12f) + xRotRad);
        var eatWave = MathF.Sin(ageInTicks) * 0.05f;
        var eatTerm = eatAnimation * ((MathF.PI / 2f) + eatWave);
        return standTerm + eatTerm + slot6;
    }

    internal static (float headPartsY, float headPartsZ) ComputeBabyHorseAnimateHeadPartsPlacement(
        float standAnimation,
        float eatAnimation,
        float baseHeadPartsY,
        float baseHeadPartsZ)
    {
        static float Lerp(float t, float a, float b) => a + ((b - a) * t);
        var yDelta = Lerp(standAnimation, Lerp(eatAnimation, 0f, -2f), 2f);
        var headPartsY = baseHeadPartsY + yDelta;
        var headPartsZ = Lerp(eatAnimation, baseHeadPartsZ, -4f);
        return (headPartsY, headPartsZ);
    }

    private static MergedJavaBlockModel BuildBabyEquineHorseLike(
        string texRef,
        bool donkeyEars,
        bool donkeyChests,
        float modelScale,
        float neckBend)
    {
        var b = new RigBuilder(64, 64);

        if (!donkeyEars)
        {
            // BabyHorseModel.createBabyMesh (26.1.2) — strict cuboids/poses.
            var bodyPose = EntityParityTemplate.T(0f, 12.5f, 0f);
            new EntityCuboid(-4f, -3.5f, -7f, 4f, 3.5f, 7f, 0, 13).Emit(b, bodyPose, 1f);

            // Tail Part: AbstractEquine tail.xRot assignment; BabyHorseModel.getTailXRotOffset() = -π/2 (replaces Layer tail pitch; idle walk=0).
            // setupAnim assigns tail.xRot (replaces mesh default); tail pose is translate then rotate like ModelPart.translateAndRotate and adult equine.
            // PartPose offsets match 26.1.2 javap (same −Z/+Z head-vs-tail relationship as adult AbstractEquine after global XY mirror).
            var babyHorseTailPitch = ComputeAbstractEquineTailParentPitchRad(-MathF.PI / 2f, walkSpeed: 0f);
            // setupAnim assigns tail.xRot (replaces mesh default); same signed pitch as javap / AbstractEquine.
            var tailPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -1f, 7f), EntityParityTemplate.Er(babyHorseTailPitch, 0f, 0f)));
            new EntityCuboid(-1.5f, -1.5f, -1f, 1.5f, 1.5f, 7f, 24, 34).Emit(b, tailPose, 1f);

            new EntityCuboid(-1.5f, -1f, -1.5f, 1.5f, 8f, 1.5f, 12, 46).Emit(b, EntityParityTemplate.T(2.4f, 16f, 5.4f), 1f);
            new EntityCuboid(-1.5f, -1f, -1.5f, 1.5f, 8f, 1.5f, 0, 46).Emit(b, EntityParityTemplate.T(-2.4f, 16f, 5.4f), 1f);
            new EntityCuboid(-1.5f, -1f, -1.5f, 1.5f, 8f, 1.5f, 12, 34).Emit(b, EntityParityTemplate.T(2.4f, 16f, -5.4f), 1f);
            new EntityCuboid(-1.5f, -1f, -1.5f, 1.5f, 8f, 1.5f, 0, 34).Emit(b, EntityParityTemplate.T(-2.4f, 16f, -5.4f), 1f);

            // Mesh layer bakes a default head_parts pitch; AbstractEquineModel.setupAnim replaces head_parts rotation each tick.
            // Idle parity: EquineRenderState.xRot/yRot = 0 → headXRot = π/6 + headInputPitch (javap), headInputPitch from entity pitch only — no adult preview calibration.
            const float degToRad = MathF.PI / 180f;
            const float deg30 = 0.5235988f;
            var standAnimation = 0f;
            var eatAnimation = 0f;
            var xRotDeg = 0f;
            var yRotDeg = 0f;
            var headInputPitch = xRotDeg * degToRad;
            var headXRot = deg30 + headInputPitch;
            var headYRot = Math.Clamp(yRotDeg, -20f, 20f) * degToRad;
            // Baby horse: AbstractEquineModel.setupAnim + BabyHorseModel.animateHeadPartsPlacement (javap).
            // neckBend: same stacking order as adult head_parts; catalog passes 0 for babies (vanilla has no adult-style preview wobble to mirror).
            const float baseHeadPartsY = 10f;
            const float baseHeadPartsZ = -6f;
            var (headPartsY, headPartsZ) = ComputeBabyHorseAnimateHeadPartsPlacement(
                standAnimation,
                eatAnimation,
                baseHeadPartsY,
                baseHeadPartsZ);
            // Root child head_parts: same Mul(T, Er) order as adult AbstractEquine head_parts / translateAndRotate.
            var headPartsPose = EntityParityTemplate.Mul(EntityParityTemplate.T(0f, headPartsY, headPartsZ), EntityParityTemplate.Er(headXRot + neckBend, headYRot, 0f));
            EntityParityTemplate.AssertFinitePose(bodyPose, "equine baby horse bodyPose");
            EntityParityTemplate.AssertFinitePose(tailPose, "equine baby horse tailPose");
            EntityParityTemplate.AssertFinitePose(headPartsPose, "equine baby horse headPartsPose");
            new EntityCuboid(-2f, -6f, -2f, 2f, 2f, 2f, 30, 0).Emit(b, headPartsPose, 1f);
            var headPose = EntityParityTemplate.Mul(headPartsPose, EntityParityTemplate.T(0f, -6.0516f, -0.2951f));
            new EntityCuboid(-3f, -3.9484f, -6.705f, 3f, 0.0516f, 2.295f, 0, 0).Emit(b, headPose, 1f);
            new EntityCuboid(-1f, -2.5f, -0.8f, 1f, 0.5f, 0.2f, 0, 4).Emit(b, EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(2f, -4.2484f, 1.9451f), EntityParityTemplate.Rz(0.2618f))), 1f);
            new EntityCuboid(-1f, -2.5f, -0.5f, 1f, 0.5f, 0.5f, 0, 0).Emit(b, EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-2f, -4.2484f, 1.645f), EntityParityTemplate.Rz(-0.2618f))), 1f);
        }
        else
        {
            // BabyDonkeyModel.createBabyLayer (26.1.2) — strict cuboids/poses.
            var bodyPose = EntityParityTemplate.T(1f, 14f, 0f);
            new EntityCuboid(-5f, -3f, -7f, 3f, 3f, 7f, 0, 13).Emit(b, bodyPose, 1f);

            // Tail Part + tail_r1: parent pitch from AbstractEquine + BabyDonkeyModel.getTailXRotOffset(-π/4); child keeps mesh X -0.7418.
            var tailParentPitch = ComputeAbstractEquineTailParentPitchRad(-MathF.PI / 4f, walkSpeed: 0f);
            var tailGroupPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -1.5f, 6.5f), EntityParityTemplate.Er(tailParentPitch, 0f, 0f)));
            new EntityCuboid(-2.5f, -1f, -0.5f, 0.5f, 2f, 7.5f, 24, 33).Emit(b, EntityParityTemplate.Mul(tailGroupPose, EntityParityTemplate.Er(-0.7418f, 0f, 0f)), 1f);

            // In BabyDonkeyModel these are body children (not root children).
            new EntityCuboid(-2.5f, -1.5f, -1.5f, 0.5f, 6.5f, 1.5f, 12, 44).Emit(b, EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(2.25f, 3.5f, 5.25f)), 1f);
            new EntityCuboid(-2.5f, -1.5f, -1.5f, 0.5f, 6.5f, 1.5f, 0, 44).Emit(b, EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(-2.4f, 3.5f, 5.4f)), 1f);
            new EntityCuboid(-2.5f, -1.5f, -1.5f, 0.5f, 6.5f, 1.5f, 12, 33).Emit(b, EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(2.4f, 3.5f, -5.3f)), 1f);
            new EntityCuboid(-2.5f, -1.5f, -1.5f, 0.5f, 6.5f, 1.5f, 0, 33).Emit(b, EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(-2.4f, 3.5f, -5.4f)), 1f);

            // BabyDonkeyModel.setupAnim overwrites head_parts.xRot after forcing renderState.xRot = -30° (idle → ~0); bake explicit layer.
            var babyDonkeyHeadPartsPitch = ComputeBabyDonkeySetupAnimHeadPartsXRotRad(
                eatAnimation: 0f,
                standAnimation: 0f,
                feedingAnimation: 0f,
                ageInTicks: 0f,
                entityPitchDegreesAfterBabyMutation: -30f);
            var headPartsPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -3f, -5f), EntityParityTemplate.Er(babyDonkeyHeadPartsPitch + neckBend, 0f, 0f)));
            EntityParityTemplate.AssertFinitePose(bodyPose, "equine baby donkey bodyPose");
            EntityParityTemplate.AssertFinitePose(tailGroupPose, "equine baby donkey tailGroupPose");
            EntityParityTemplate.AssertFinitePose(headPartsPose, "equine baby donkey headPartsPose");
            new EntityCuboid(-3f, -6f, -3f, 1f, 2f, 1f, 30, 9).Emit(b, EntityParityTemplate.Mul(headPartsPose, EntityParityTemplate.Er(0.3927f, 0f, 0f)), 1f);
            var headPose = EntityParityTemplate.Mul(headPartsPose, EntityParityTemplate.T(0f, -5f, -3f));
            new EntityCuboid(-4f, -3.6f, -8.4f, 2f, 0.4f, 0.6f, 0, 0).Emit(b, EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -1f, 1f), EntityParityTemplate.Er(0.3927f, 0f, 0f))), 1f);
            new EntityCuboid(-2f, -6.5f, -0.3f, 0f, 0.5f, 0.7f, 0, 0).Emit(b, EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(2f, -3.5f, -1f), EntityParityTemplate.Er(0.48f, 0f, 0.48f))), 1f);
            new EntityCuboid(-2f, -6.5f, -0.3f, 0f, 0.5f, 0.7f, 22, 0, MirrorUv: true).Emit(b, EntityParityTemplate.Mul(headPose, EntityParityTemplate.Mul(EntityParityTemplate.T(-2f, -3.5f, -1f), EntityParityTemplate.Er(0.48f, 0f, -0.48f))), 1f);

            if (donkeyChests)
            {
                AppendEquineDonkeyChestPair(b, bodyPose, skinScale: 1f);
            }
        }

        // Vanilla local PartPose chain is kept exactly as javap. Baby equine layer orientation is opposite adult under
        // preview-space mirror, so apply world yaw as a post-multiply correction (Local * Mirror * Yaw(π)).
        return ApplyBabyEquineLivingEntityRendererPreviewBasis(b.Build(texRef), modelScale);
    }

    private static MergedJavaBlockModel ApplyEquineLivingEntityRendererPreviewBasis(MergedJavaBlockModel model, float modelScale) =>
        ApplyGlobalTransform(model, Matrix4x4.CreateScale(-modelScale, -modelScale, modelScale));

    private static MergedJavaBlockModel ApplyBabyEquineLivingEntityRendererPreviewBasis(MergedJavaBlockModel model, float modelScale) =>
        ApplyGlobalTransform(
            model,
            Matrix4x4.CreateScale(-modelScale, -modelScale, modelScale),
            postMultiplyWorld: Matrix4x4.CreateRotationY(MathF.PI));
}
