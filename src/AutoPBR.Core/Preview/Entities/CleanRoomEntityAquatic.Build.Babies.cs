using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    /// <summary>
    /// <c>BabyTurtleModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.turtle.BabyTurtleModel.json</c> (<c>128×64</c>).
    /// Part order matches IR DFS. Hatchling flippers use the same <paramref name="swimLift"/> preview wobble as <see cref="BuildTurtle"/>.
    /// </summary>
    private static MergedJavaBlockModel BuildBabyTurtle(string texRef, float swimLift)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(128, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 22.9f, 1f));
        new EntityCuboid(-2f, -1f, -2f, 2f, 1f, 2f, 0, 0, UvSizeW: 4, UvSizeH: 2, UvSizeD: 4).Emit(b, bodyPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 22.9f, -1f));
        new EntityCuboid(-1.5f, -2f, -3f, 1.5f, 1f, 0f, 0, 6, UvSizeW: 3, UvSizeH: 3, UvSizeD: 3, XRot: 0f, YRot: swimLift * 0.08f, ZRot: 0f) { RotationPivot = new Vector3(0f, -0.5f, -1.5f) }.Emit(b, headPose, p.HeadScale);

        var rightHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 23.9f, 2.5f)),
            EntityParityTemplate.Rz(-swimLift * 0.1f));
        new EntityCuboid(-2f, -0.001f, -0.5f, 0f, 0.001f, 0.5f, 0, 0, UvSizeW: 4, UvSizeH: 1, UvSizeD: 1).Emit(b, rightHindPose, p.LegScale);

        var leftHindPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2f, 23.9f, 2.5f)),
            EntityParityTemplate.Rz(swimLift * 0.1f));
        new EntityCuboid(0f, -0.001f, -0.5f, 2f, 0.001f, 0.5f, 0, 1, UvSizeW: 4, UvSizeH: 1, UvSizeD: 1, MirrorUv: true).Emit(b, leftHindPose, p.LegScale);

        var rightFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 23.9f, -0.5f)),
            EntityParityTemplate.Rz(swimLift * 0.12f));
        new EntityCuboid(-2f, -0.001f, -0.5f, 0f, 0.001f, 0.5f, 8, 6, UvSizeW: 4, UvSizeH: 1, UvSizeD: 1).Emit(b, rightFrontPose, p.LegScale);

        var leftFrontPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2f, 23.9f, -0.5f)),
            EntityParityTemplate.Rz(-swimLift * 0.12f));
        new EntityCuboid(0f, -0.001f, -0.5f, 2f, 0.001f, 0.5f, 8, 7, UvSizeW: 4, UvSizeH: 1, UvSizeD: 1, MirrorUv: true).Emit(b, leftFrontPose, p.LegScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BabySquidModel.createBodyLayer</c> — <c>javap -private -c</c> on <c>tools/minecraft-parity/26.1.2/client.jar</c>
    /// (<c>LayerDefinition.create(..., 32, 32)</c>; entity sheet <c>squid_baby.png</c> is <c>32×32</c>).
    /// Geometry IR <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.squid.BabySquidModel.json</c> still omits the tentacle loop; this builder matches bytecode (mantle + eight root tentacles, same angular layout as <see cref="BuildSquid"/> with radius <c>3</c>, anchor <c>y=18.5</c>, <c>texOffs(0,18)</c> <c>2×6×2</c>).
    /// </summary>
    private static MergedJavaBlockModel BuildBabySquid(string texRef, float tentacleWave)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(32, 32);
        var bodyPose = EntityParityTemplate.T(0f, 13f, 0f);
        // Body: texOffs(0,0) addBox(-4,-5,-4, 8,10,8) + CubeDeformation(0) + PartPose.offset(0,13,0).
        new EntityCuboid(-4f, -5f, -4f, 4f, 5f, 4f, 0, 0, UvSizeW: 8, UvSizeH: 10, UvSizeD: 8).Emit(b, bodyPose, p.BodyScale);
        for (var i = 0; i < 8; i++)
        {
            var theta = i * MathF.PI / 4f;
            var rx = 3f * MathF.Cos(theta);
            var rz = 3f * MathF.Sin(theta);
            var yRot = MathF.PI / 2f - theta;
            var sway = tentacleWave * (i % 2 == 0 ? 0.8f : -0.7f);
            var tentaclePose = EntityParityTemplate.Mul(
                EntityParityTemplate.T(rx, 18.5f, rz),
                EntityParityTemplate.Ry(yRot + sway));
            new EntityCuboid(-1f, -0.5f, -1f, 1f, 5.5f, 1f, 0, 18, UvSizeW: 2, UvSizeH: 6, UvSizeD: 2).Emit(b, tentaclePose, p.LegScale);
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BabyDolphinModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.dolphin.BabyDolphinModel.json</c> (<c>64×64</c>).
    /// Hierarchy matches <see cref="BuildDolphin"/> (body-anchored head/fins/tail). <paramref name="swimSway"/> drives tail pitch like the adult preview.
    /// </summary>
    private static MergedJavaBlockModel BuildBabyDolphin(string texRef, float swimSway)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;
        var bodyPose = ModelPartRenderChildTexel(root, 0f, 21.5f, 0f);

        new EntityCuboid(-2.5f, -4f, 6f - 0.01f, 2.5f, 4f, 6f + 0.01f, 20, 0, UvSizeW: 5, UvSizeH: 8, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        var headPose = ModelPartRenderChildTexel(bodyPose, 0f, 1f, -4f);
        new EntityCuboid(-3.5f, -4f, 6f - 0.01f, 1.5f, 0f, 6f + 0.01f, 0, 0, UvSizeW: 5, UvSizeH: 4, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        var nosePose = ModelPartRenderChildTexel(bodyPose, 0f, 0.5f, -4f);
        new EntityCuboid(-1f, -2f, 2f - 0.01f, 1f, 0f, 2f + 0.01f, 0, 9, UvSizeW: 2, UvSizeH: 2, UvSizeD: 1).Emit(b, nosePose, p.HeadScale);

        var leftFinPose = ModelPartRenderChildTexel(bodyPose, 1.8f, 0.85f, -2.6f, 0.87266463f, 0f, 2.0943952f);
        new EntityCuboid(-1.5f, -0.5f, 1f - 0.01f, 1.5f, 5.5f, 1f + 0.01f, 34, 18, UvSizeW: 3, UvSizeH: 6, UvSizeD: 1, MirrorUv: true).Emit(b, leftFinPose, p.BodyScale);

        var rightFinPose = ModelPartRenderChildTexel(bodyPose, -1.8f, 0.85f, -2.6f, 0.87266463f, 0f, -1.701696f);
        new EntityCuboid(-1.5f, -0.5f, 1f - 0.01f, 1.5f, 5.5f, 1f + 0.01f, 48, 18, UvSizeW: 3, UvSizeH: 6, UvSizeD: 1).Emit(b, rightFinPose, p.BodyScale);

        var tailPitch = -0.10471976f - swimSway * 0.2f;
        var tailPose = ModelPartRenderChildTexel(bodyPose, 0f, 1f, 4f, tailPitch);
        new EntityCuboid(-1.5f, 0f, 4f - 0.01f, 1.5f, 7f, 4f + 0.01f, 0, 13, UvSizeW: 3, UvSizeH: 7, UvSizeD: 1).Emit(b, tailPose, p.LegScale);

        var tailFinPose = ModelPartRenderChildTexel(tailPose, 0f, 0f, 6f);
        new EntityCuboid(-0.5f, -1f, 8f - 0.01f, 0.5f, 3f, 8f + 0.01f, 22, 13, UvSizeW: 1, UvSizeH: 4, UvSizeD: 1).Emit(b, tailFinPose, p.LegScale);

        var backFinPose = ModelPartRenderChildTexel(bodyPose, 0f, -1f, -2.7f, 0.87266463f);
        new EntityCuboid(-1f, 1f, 1f - 0.01f, 2f, 5f, 1f + 0.01f, 42, 0, UvSizeW: 3, UvSizeH: 4, UvSizeD: 1).Emit(b, backFinPose, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BabyAxolotlModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.axolotl.BabyAxolotlModel.json</c> (<c>64×64</c>).
    /// Uses <c>T(0,24,0)</c> root lift (same convention as <see cref="BuildFrog"/>), body stack + head/gills like <see cref="BuildAxolotl"/>, IR part order for limbs/tail.
    /// </summary>
    private static MergedJavaBlockModel BuildBabyAxolotl(
        string texRef,
        float idleBob,
        float rightHindLegPitchRad,
        float leftHindLegPitchRad,
        float rightFrontLegPitchRad,
        float leftFrontLegPitchRad)
    {
        _ = rightHindLegPitchRad;
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var rootLift = EntityParityTemplate.T(0f, 24f, 0f);
        var bodyPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(rootLift, EntityParityTemplate.T(0f, -1.25f, 1.75f)),
            EntityParityTemplate.Rx(idleBob));

        new EntityCuboid(-0.75f, -2.75f, 4f - 0.01f, 1.25f, 3.25f, 4f + 0.01f, 0, 0, UvSizeW: 2, UvSizeH: 6, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-1.75f, -2.75f, 0f - 0.01f, 1.25f, 2.25f, 0f + 0.01f, 0, 12, UvSizeW: 3, UvSizeH: 5, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        var rfAnchor = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(-2f, 0.25f, -1.25f));
        var rfPose = EntityParityTemplate.Mul(rfAnchor, EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(0f, -0.5f, 3f - 0.01f, 0f, 0.5f, 3f + 0.01f, 20, 16, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, rfPose, p.LegScale);

        var rfR1Pose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(rfAnchor, EntityParityTemplate.Er(-1.5707964f, 0f, 1.5707964f)),
            EntityParityTemplate.Rx(rightFrontLegPitchRad));
        new EntityCuboid(0f, -0.5f, 3f - 0.01f, 0f, 0.5f, 3f + 0.01f, 20, 14, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, rfR1Pose, p.LegScale);

        var lfPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(2f, 0.25f, -1.25f)),
            EntityParityTemplate.Rx(leftFrontLegPitchRad));
        new EntityCuboid(0f, -0.5f, 3f - 0.01f, 0f, 0.5f, 3f + 0.01f, 20, 13, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, lfPose, p.LegScale);

        var lhPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(2f, 0.25f, 1.75f)),
            EntityParityTemplate.Rx(leftHindLegPitchRad));
        new EntityCuboid(0f, -0.5f, 3f - 0.01f, 0f, 0.5f, 3f + 0.01f, 20, 14, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, lhPose, p.LegScale);

        var tailPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(0f, -0.25f, 3.25f));
        new EntityCuboid(-1.5f, -1f, 0f - 0.01f, 1.5f, 7f, 0f + 0.01f, 10, 9, UvSizeW: 3, UvSizeH: 8, UvSizeD: 1).Emit(b, tailPose, p.BodyScale);

        var headPose = EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(0f, 0.25f, -2.75f));
        new EntityCuboid(-2f, -4f, 6f - 0.01f, 1f, 0f, 6f + 0.01f, 0, 8, UvSizeW: 3, UvSizeH: 4, UvSizeD: 1).Emit(b, headPose, p.HeadScale);

        var leftGillsPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(3f, -0.5f, -2f));
        new EntityCuboid(-3.5f, 0f, 3f - 0.01f, 1.5f, 0f, 3f + 0.01f, 20, 8, UvSizeW: 5, UvSizeH: 1, UvSizeD: 1).Emit(b, leftGillsPose, p.HeadScale);

        var rightGillsPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(-3f, -0.5f, -2f));
        new EntityCuboid(-3.5f, 0f, 3f - 0.01f, 1.5f, 0f, 3f + 0.01f, 20, 3, UvSizeW: 5, UvSizeH: 1, UvSizeD: 1).Emit(b, rightGillsPose, p.HeadScale);

        var topGillsPose = EntityParityTemplate.Mul(headPose, EntityParityTemplate.T(0f, -2f, -2f));
        new EntityCuboid(-3f, 0f, 6f - 0.01f, 0f, 0f, 6f + 0.01f, 20, 0, UvSizeW: 3, UvSizeH: 1, UvSizeD: 1).Emit(b, topGillsPose, p.HeadScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

}
