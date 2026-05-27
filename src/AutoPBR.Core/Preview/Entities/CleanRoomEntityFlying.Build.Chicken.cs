using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    /// <summary>
    /// <c>BabyChickenModel.createBodyLayer</c> — literals from geometry IR
    /// <c>docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.chicken.BabyChickenModel.json</c>
    /// (<c>64×32</c>): <c>body</c> @ <c>T(0,20.25,-1.25)</c> + two cuboids; <c>left_leg</c> / <c>right_leg</c> @ <c>T(1,22,0.5)</c> / <c>T(-1,22,0.5)</c> two cuboids each;
    /// wings @ <c>T(2,20,0)</c> / <c>T(-2,20,0)</c>. Part order matches IR DFS: body, left_leg, right_leg, right_wing, left_wing.
    /// <c>setupAnim</c> uses the same leg cosine / wing <c>zRot</c> family as adult <see cref="BuildChicken"/> (shared preview limb/flap drivers).
    /// </summary>
    private static MergedJavaBlockModel BuildBabyChicken(
        string texRef,
        float rightLegPitchRad,
        float leftLegPitchRad,
        float wingZRadians)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 32);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20.25f, -1.25f));
        new EntityCuboid(-2f, -2.25f, -0.75f, 2f, 1.75f, 3.25f, 0, 0, UvSizeW: 4, UvSizeH: 4, UvSizeD: 4).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-1f, -0.25f, -1.75f, 1f, 0.75f, -0.75f, 10, 8, UvSizeW: 2, UvSizeH: 1, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);

        var leftLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 22f, 0.5f)),
            EntityParityTemplate.Rx(leftLegPitchRad));
        new EntityCuboid(-0.5f, 0f, 0f, 0.5f, 2f, 0f, 2, 2, UvSizeW: 1, UvSizeH: 2, UvSizeD: 1).Emit(b, leftLegPose, p.LegScale);
        new EntityCuboid(-0.5f, 2f, -1f, 0.5f, 2f, 0f, 0, 1, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, leftLegPose, p.LegScale);

        var rightLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1f, 22f, 0.5f)),
            EntityParityTemplate.Rx(rightLegPitchRad));
        new EntityCuboid(-0.5f, 0f, 0f, 0.5f, 2f, 0f, 0, 2, UvSizeW: 1, UvSizeH: 2, UvSizeD: 1).Emit(b, rightLegPose, p.LegScale);
        new EntityCuboid(-0.5f, 2f, -1f, 0.5f, 2f, 0f, 0, 0, UvSizeW: 1, UvSizeH: 1, UvSizeD: 1).Emit(b, rightLegPose, p.LegScale);

        var rightWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(2f, 20f, 0f)),
            EntityParityTemplate.Rz(wingZRadians));
        new EntityCuboid(0f, 0f, -1f, 1f, 0f, 1f, 6, 8, UvSizeW: 1, UvSizeH: 1, UvSizeD: 2).Emit(b, rightWingPose, p.BodyScale);

        var leftWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 20f, 0f)),
            EntityParityTemplate.Rz(-wingZRadians));
        new EntityCuboid(-1f, 0f, -1f, 0f, 0f, 1f, 4, 8, UvSizeW: 1, UvSizeH: 1, UvSizeD: 2).Emit(b, leftWingPose, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>ChickenModel.createBodyLayer</c> — same static mesh as geometry IR
    /// <c>geometry/&lt;version&gt;/net.minecraft.client.model.animal.chicken.ChickenModel.json</c> (see
    /// <c>docs/generated/geometry-index-26.1.2.json</c> / <c>geometry-index-1.21.11.json</c>): <c>64×32</c> atlas;
    /// head <c>4×6×3</c> @ <c>T(0,15,-4)</c> + beak <c>4×2×2</c> + <c>red_thing</c> <c>2×2×2</c> (beak / wattle share head look rotation);
    /// body <c>6×8×6</c> @ <c>texOffs(0,9)</c> <c>PartPose.offsetAndRotation(0,16,0, π/2,0,0)</c>; legs <c>3×5×3</c> @ <c>(26,0)</c>
    /// <c>T(∓2,19,1)/(1,19,1)</c>; wings <c>1×4×6</c> @ <c>(24,13)</c> <c>T(∓4,13,0)</c>. Vanilla lists only <c>right_leg</c> in the mesh factory;
    /// the mirrored left leg uses the same cuboid + UV island. <c>setupAnim</c>: head <c>xRot/yRot</c> from render-state look (deg→rad),
    /// wings <c>zRot ±(sin(flap)+1)·flapSpeed</c>; legs from lifted <c>ChickenModel</c> / <c>QuadrupedModel</c> setupAnim IR via <see cref="VanillaSetupAnimRuntime"/>.
    /// </summary>
    private static MergedJavaBlockModel BuildChicken(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float headPitchRad,
        float headYawRad,
        float wingZRadians,
        float rightLegPitchRad,
        float leftLegPitchRad)
    {
        if (isBaby)
        {
            return BuildBabyChicken(texRef, rightLegPitchRad, leftLegPitchRad, wingZRadians);
        }

        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 32);
        var root = Matrix4x4.Identity;

        var headPose = EntityParityTemplate.Mul(
            root,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 15f, -4f), EntityParityTemplate.Er(headPitchRad, headYawRad, 0f)));
        new EntityCuboid(-2f, -6f, -2f, 2f, 0f, 1f, 0, 0, UvSizeW: 4, UvSizeH: 6, UvSizeD: 3).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-2f, -4f, -4f, 2f, -2f, -2f, 14, 0, UvSizeW: 4, UvSizeH: 2, UvSizeD: 2).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-1f, -2f, -3f, 1f, 0f, -1f, 14, 4, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2).Emit(b, headPose, p.HeadScale);

        var bodyPose = EntityParityTemplate.Mul(
            root,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 16f, 0f), EntityParityTemplate.Rx(MathF.PI / 2f)));
        new EntityCuboid(-3f, -4f, -3f, 3f, 4f, 3f, 0, 9, UvSizeW: 6, UvSizeH: 8, UvSizeD: 6).Emit(b, bodyPose, p.BodyScale);

        var rightLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 19f, 1f)),
            EntityParityTemplate.Rx(rightLegPitchRad));
        new EntityCuboid(-1f, 0f, -3f, 2f, 5f, 0f, 26, 0, UvSizeW: 3, UvSizeH: 5, UvSizeD: 3).Emit(b, rightLegPose, p.LegScale);
        var leftLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 19f, 1f)),
            EntityParityTemplate.Rx(leftLegPitchRad));
        new EntityCuboid(-1f, 0f, -3f, 2f, 5f, 0f, 26, 0, UvSizeW: 3, UvSizeH: 5, UvSizeD: 3).Emit(b, leftLegPose, p.LegScale);

        var rightWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-4f, 13f, 0f)),
            EntityParityTemplate.Rz(wingZRadians));
        new EntityCuboid(0f, 0f, -3f, 1f, 4f, 3f, 24, 13, UvSizeW: 1, UvSizeH: 4, UvSizeD: 6).Emit(b, rightWingPose, p.BodyScale);
        var leftWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(4f, 13f, 0f)),
            EntityParityTemplate.Rz(-wingZRadians));
        new EntityCuboid(-1f, 0f, -3f, 0f, 4f, 3f, 24, 13, UvSizeW: 1, UvSizeH: 4, UvSizeD: 6).Emit(b, leftWingPose, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>ColdChickenModel.createBodyLayer</c> (26.1.2 javap): extends <c>AdultChickenModel.createBaseChickenModel</c> then
    /// <c>addOrReplaceChild</c> on <c>body</c> (main box + thin crest <c>texOffs(38,9)</c>) and <c>head</c> (main head + cold hood <c>texOffs(44,0)</c>).
    /// Legs/wings stay from the base mesh; <c>AdultChickenModel.setupAnim</c> still drives wing flap and leg swing + head look (deg→rad on head part).
    /// Merged <see cref="ModelElement"/> order follows vanilla root DFS after the replacements: <c>head</c> cuboids (2), <c>body</c> cuboids (2),
    /// then <c>right_leg</c>, <c>left_leg</c>, <c>right_wing</c>, <c>left_wing</c> — same sibling order as <c>createBaseChickenModel</c>
    /// (<c>head</c> is registered before <c>body</c>; cold bytecode replaces <c>body</c> then <c>head</c> without reordering siblings).
    /// </summary>
    private static MergedJavaBlockModel BuildColdChicken(
        string texRef,
        MinecraftNativeProfile profile,
        float headPitchRad,
        float headYawRad,
        float wingZRadians,
        float rightLegPitchRad,
        float leftLegPitchRad)
    {
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 32);
        var root = Matrix4x4.Identity;

        var headPose = EntityParityTemplate.Mul(
            root,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 15f, -4f), EntityParityTemplate.Er(headPitchRad, headYawRad, 0f)));
        new EntityCuboid(-2f, -6f, -2f, 2f, 0f, 1f, 0, 0, UvSizeW: 4, UvSizeH: 6, UvSizeD: 3).Emit(b, headPose, p.HeadScale);
        new EntityCuboid(-3f, -7f, -2.015f, 3f, -4f, 1.985f, 44, 0, UvSizeW: 6, UvSizeH: 3, UvSizeD: 4).Emit(b, headPose, p.HeadScale);

        var bodyPose = EntityParityTemplate.Mul(
            root,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 16f, 0f), EntityParityTemplate.Rx(MathF.PI / 2f)));
        new EntityCuboid(-3f, -4f, -3f, 3f, 4f, 3f, 0, 9, UvSizeW: 6, UvSizeH: 8, UvSizeD: 6).Emit(b, bodyPose, p.BodyScale);
        // Javap: texOffs(38,9); addBox(0,3,-1, 0,3,5) — origin + size; zero X width matches IR corners (0,3,-1)-(0,6,4). UV w=1 minimum for unfold.
        new EntityCuboid(0f, 3f, -1f, 0f, 6f, 4f, 38, 9, UvSizeW: 1, UvSizeH: 3, UvSizeD: 5).Emit(b, bodyPose, p.BodyScale);

        var rightLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-2f, 19f, 1f)),
            EntityParityTemplate.Rx(rightLegPitchRad));
        new EntityCuboid(-1f, 0f, -3f, 2f, 5f, 0f, 26, 0, UvSizeW: 3, UvSizeH: 5, UvSizeD: 3).Emit(b, rightLegPose, p.LegScale);
        var leftLegPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(1f, 19f, 1f)),
            EntityParityTemplate.Rx(leftLegPitchRad));
        new EntityCuboid(-1f, 0f, -3f, 2f, 5f, 0f, 26, 0, UvSizeW: 3, UvSizeH: 5, UvSizeD: 3).Emit(b, leftLegPose, p.LegScale);

        var rightWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(-4f, 13f, 0f)),
            EntityParityTemplate.Rz(wingZRadians));
        new EntityCuboid(0f, 0f, -3f, 1f, 4f, 3f, 24, 13, UvSizeW: 1, UvSizeH: 4, UvSizeD: 6).Emit(b, rightWingPose, p.BodyScale);
        var leftWingPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(root, EntityParityTemplate.T(4f, 13f, 0f)),
            EntityParityTemplate.Rz(-wingZRadians));
        new EntityCuboid(-1f, 0f, -3f, 0f, 4f, 3f, 24, 13, UvSizeW: 1, UvSizeH: 4, UvSizeD: 6).Emit(b, leftWingPose, p.BodyScale);

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

}
