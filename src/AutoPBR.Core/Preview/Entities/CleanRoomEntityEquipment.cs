using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Equipment overlays and armor stand.

    /// <summary>SkullModel (1.21.11 <c>javap</c> <c>hhl</c>): <c>head</c> <c>8³</c> @ <c>(0,0)</c>; layered <c>SkullModel.e()</c> adds <c>hat</c> same extents with <c>CubeDeformation(0.25)</c> → <c>8.5³</c> mesh, <c>8³</c> UV @ <c>(32,0)</c>. Preview applies block-style <c>T(0,8,0)·Rx(headPitch)</c>.</summary>
    private static MergedJavaBlockModel BuildSkull(string texRef, MinecraftNativeProfile profile, bool isBaby, float headPitch)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);
        var pose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 8f, 0f), Matrix4x4.CreateRotationX(headPitch));
        new EntityCuboid(-4f, -8f, -4f, 4f, 0f, 4f, 0, 0).Emit(b, pose, 1f); // head
        const float hatInflate = 0.25f;
        new EntityCuboid(-4f - hatInflate, -8f - hatInflate, -4f - hatInflate, 4f + hatInflate, 0f + hatInflate, 4f + hatInflate, 32, 0, UvSizeW: 8, UvSizeH: 8, UvSizeD: 8).Emit(b, pose, 1f);
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildPiglinSkull(string texRef, MinecraftNativeProfile profile, bool isBaby, float headPitch)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);
        var headPose = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, 8f, 0f), Matrix4x4.CreateRotationX(headPitch));
        AppendAbstractPiglinHeadBoxes(
            b,
            headPose,
            1f,
            leftEarZRotRad: -GetDefaultAbstractPiglinEarBaseRollRad(30f),
            rightEarZRotRad: GetDefaultAbstractPiglinEarBaseRollRad(30f));
        return b.Build(texRef);
    }

    /// <summary>ShieldModel (~1.21.11 hha): plate 12x22x1 at (-6,-11,-2) and handle 2x6x6 at (-1,-3,-1), atlas 64x64.</summary>
    private static MergedJavaBlockModel BuildShield(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);
        new EntityCuboid(-6f, -11f, -2f, 6f, 11f, -1f, 0, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-1f, -3f, -1f, 1f, 3f, 5f, 26, 0).Emit(b, Matrix4x4.Identity, 1f);
        return b.Build(texRef);
    }


    private static MergedJavaBlockModel BuildEquipmentLayer(string texRef, MinecraftNativeProfile profile, bool isBaby, string normalizedAssetPath)
    {
        if (normalizedAssetPath.Contains("/textures/entity/equipment/wings/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildEquipmentWings(texRef, profile, isBaby);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/nautilus_body/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildNautilusArmor(texRef, profile, isBaby);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/nautilus_saddle/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildNautilusSaddle(texRef, profile, isBaby);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/horse_body/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildHorse(texRef, profile, isBaby, neckBend: 0f);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/donkey_body/", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/textures/entity/equipment/mule_body/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildHorseDonkeyMule(texRef, profile, isBaby, neckBend: 0f, donkeyChests: true);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/skeleton_horse_body/", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/textures/entity/equipment/zombie_horse_body/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildHorse(texRef, profile, isBaby, neckBend: 0f);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/llama_body/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildLlama(texRef, profile, isBaby, neckBend: 0f);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/wolf_body/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildWolf(texRef, profile, isBaby, headPitch: 0f, 0f, 0f, 0f, 0f);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/humanoid_leggings/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildEquipmentHumanoidLeggings(texRef, profile, isBaby);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/humanoid_baby/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildHumanoid(texRef, profile, isBaby: true, armLift: 0f);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/humanoid/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildHumanoid(texRef, profile, isBaby, armLift: 0f);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/pig_saddle/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildPig(texRef, profile, isBaby, snoutBob: 0f, 0f, 0f, 0f, 0f);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/strider_saddle/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildStrider(texRef, profile, isBaby, walkAnimationPos: 0f, walkAnimationSpeed: 0f, ageInTicks: 0f);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/camel_saddle/", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/textures/entity/equipment/camel_husk_saddle/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildCamelSaddle(texRef, profile, isBaby);
        }

        if (normalizedAssetPath.Contains("/textures/entity/equipment/horse_saddle/", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/textures/entity/equipment/donkey_saddle/", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/textures/entity/equipment/mule_saddle/", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/textures/entity/equipment/skeleton_horse_saddle/", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/textures/entity/equipment/zombie_horse_saddle/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildEquineSaddle(texRef, profile, isBaby);
        }

        return BuildEquipmentBodyOverlay(texRef, profile, isBaby);
    }

    /// <summary>
    /// Humanoid leggings equipment layers cover torso + legs only (no helmet/arms), preserving biped UV channels on 64x64 sheets.
    /// </summary>
    private static MergedJavaBlockModel BuildEquipmentHumanoidLeggings(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.74f, 1.22f, 0.75f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.78f, 1.12f, 0.82f) : BabyProfile.Adult);

        var b = new RigBuilder(64, 64);
        new EntityCuboid(4f, 12f, 6f, 12f, 24f, 10f, 16, 16).Emit(b, Matrix4x4.Identity, p.BodyScale); // torso
        new EntityCuboid(4f, 0f, 6f, 8f, 12f, 10f, 0, 16).Emit(b, Matrix4x4.Identity, p.LegScale); // left leg
        new EntityCuboid(8f, 0f, 6f, 12f, 12f, 10f, 0, 16).Emit(b, Matrix4x4.Identity, p.LegScale); // right leg
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildEquipmentWings(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 32);
        // ElytraEntityModel: texOffs(22, 0), logical addBox 10×20×2 + CubeDeformation(1) → 12×22×4 mesh; second wing mirror().
        var leftRoot = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(5f, 0f, 0f),
            Matrix4x4.Multiply(Matrix4x4.CreateRotationX(0.2617994f), Matrix4x4.CreateRotationZ(-0.2617994f)));
        new EntityCuboid(-11f, -1f, -1f, 1f, 21f, 3f, 22, 0, UvSizeW: 10, UvSizeH: 20, UvSizeD: 2).Emit(b, leftRoot, 1f);
        var rightRoot = Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-5f, 0f, 0f),
            Matrix4x4.Multiply(Matrix4x4.CreateRotationX(0.2617994f), Matrix4x4.CreateRotationZ(0.2617994f)));
        new EntityCuboid(-1f, -1f, -1f, 11f, 21f, 3f, 22, 0, UvSizeW: 10, UvSizeH: 20, UvSizeD: 2, MirrorUv: true).Emit(b, rightRoot, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>NautilusArmorModel (~1.21.11 hbx): rooted shell with +0.01 deformation and muzzle plates; atlas 128x128.</summary>
    private static MergedJavaBlockModel BuildNautilusArmor(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        const float d = 0.01f;
        var b = new RigBuilder(128, 128);
        var rootPose = Matrix4x4.CreateTranslation(0f, 29f, -6f);
        var shellPose = Matrix4x4.Multiply(rootPose, Matrix4x4.CreateTranslation(0f, -13f, 5f));

        // hbx: texOffs (0,0) 14×10×16, (0,26) 14×8×20, (48,26) 14×8×0 (+0.01); third cuboid Z thickness 0 on sheet.
        new EntityCuboid(-7f - d, -10f - d, -7f - d, 7f + d, 0f + d, 9f + d, 0, 0, UvSizeW: 14, UvSizeH: 10, UvSizeD: 16).Emit(b, shellPose, 1f);
        new EntityCuboid(-7f - d, 0f - d, -7f - d, 7f + d, 8f + d, 13f + d, 0, 26, UvSizeW: 14, UvSizeH: 8, UvSizeD: 20).Emit(b, shellPose, 1f);
        new EntityCuboid(-7f, 0f, 6f, 7f, 8f, 6f, 48, 26, UvSizeW: 14, UvSizeH: 8, UvSizeD: 1).Emit(b, shellPose, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>NautilusSaddleModel (~1.21.11 hbz): rooted shell overlay with +0.2 deformation, atlas 128x128.</summary>
    private static MergedJavaBlockModel BuildNautilusSaddle(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        const float d = 0.2f;
        var b = new RigBuilder(128, 128);
        var rootPose = Matrix4x4.CreateTranslation(0f, 29f, -6f);
        var shellPose = Matrix4x4.Multiply(rootPose, Matrix4x4.CreateTranslation(0f, -13f, 5f));
        // hbz: single shell cuboid texOffs (0,0) 14×10×16 (+0.2).
        new EntityCuboid(-7f - d, -10f - d, -7f - d, 7f + d, 0f + d, 9f + d, 0, 0, UvSizeW: 14, UvSizeH: 10, UvSizeD: 16).Emit(b, shellPose, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>CamelSaddleModel (~1.21.11 hae): body saddle stack + bridle + reins, atlas 128x128.</summary>
    private static MergedJavaBlockModel BuildCamelSaddle(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        const float d = 0.05f;
        const float thin = 0.08f;
        var b = new RigBuilder(128, 128);

        // hae body/saddle + bridle + reins texOffs from client bytecode (CubeDeformation 0.05 on body/bridle stacks).
        new EntityCuboid(-4.5f - d, -17f - d, -15.5f - d, 4.5f + d, -12f + d, -4.5f + d, 74, 64, UvSizeW: 9, UvSizeH: 5, UvSizeD: 11).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-3.5f - d, -20f - d, -15.5f - d, 3.5f + d, -17f + d, -4.5f + d, 92, 114, UvSizeW: 7, UvSizeH: 3, UvSizeD: 11).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-7.5f - d, -12f - d, -23.5f - d, 7.5f + d, 0f + d, 3.5f + d, 0, 89, UvSizeW: 15, UvSizeH: 12, UvSizeD: 27).Emit(b, Matrix4x4.Identity, 1f);

        new EntityCuboid(-3.5f - d, -7f - d, -15f - d, 3.5f + d, 1f + d, 4f + d, 60, 87, UvSizeW: 7, UvSizeH: 8, UvSizeD: 19).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-3.5f - d, -21f - d, -15f - d, 3.5f + d, -7f + d, -8f + d, 21, 64, UvSizeW: 7, UvSizeH: 14, UvSizeD: 7).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-2.5f - d, -21f - d, -21f - d, 2.5f + d, -16f + d, -15f + d, 50, 64, UvSizeW: 5, UvSizeH: 5, UvSizeD: 6).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(2.5f, -19f, -18f, 3.5f, -17f, -16f, 74, 70, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2, MirrorUv: true).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-3.5f, -19f, -18f, -2.5f, -17f, -16f, 74, 70, UvSizeW: 1, UvSizeH: 2, UvSizeD: 2).Emit(b, Matrix4x4.Identity, 1f);

        new EntityCuboid(3.51f, -18f, -17f, 3.51f + thin, -11f, -2f, 98, 42, UvSizeW: 1, UvSizeH: 7, UvSizeD: 15).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-3.5f, -18f, -2f, 3.5f, -11f, -2f + thin, 84, 57, UvSizeW: 7, UvSizeH: 7, UvSizeD: 1).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-3.51f - thin, -18f, -17f, -3.51f, -11f, -2f, 98, 42, UvSizeW: 1, UvSizeH: 7, UvSizeD: 15).Emit(b, Matrix4x4.Identity, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildArmorStand(string texRef, MinecraftNativeProfile profile, bool isBaby)
    {
        _ = profile;
        _ = isBaby;
        var b = new RigBuilder(64, 64);
        // ArmorStandModel.createBodyLayer (~1.21.4): wood atlas 64x64; base plate pose (0,12,0).
        var headPose = Matrix4x4.CreateTranslation(0f, 1f, 0f);
        new EntityCuboid(-1f, -7f, -1f, 1f, 0f, 1f, 0, 0).Emit(b, headPose, 1f);
        new EntityCuboid(-6f, 0f, -1.5f, 6f, 3f, 1.5f, 0, 26).Emit(b, Matrix4x4.Identity, 1f);
        var rightArm = Matrix4x4.CreateTranslation(-5f, 2f, 0f);
        new EntityCuboid(-2f, -2f, -1f, 0f, 10f, 1f, 24, 0).Emit(b, rightArm, 1f);
        var leftArm = Matrix4x4.CreateTranslation(5f, 2f, 0f);
        new EntityCuboid(-1f, 0f, -1f, 1f, 12f, 1f, 40, 16).Emit(b, leftArm, 1f);
        var rightLeg = Matrix4x4.CreateTranslation(-1.9f, 12f, 0f);
        new EntityCuboid(-1f, 0f, -1f, 1f, 11f, 1f, 8, 0).Emit(b, rightLeg, 1f);
        var leftLeg = Matrix4x4.CreateTranslation(1.9f, 12f, 0f);
        new EntityCuboid(-1f, 0f, -1f, 1f, 11f, 1f, 40, 16).Emit(b, leftLeg, 1f);
        new EntityCuboid(-3f, 3f, -1f, -1f, 10f, 1f, 16, 0).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(1f, 3f, -1f, 3f, 10f, 1f, 48, 16).Emit(b, Matrix4x4.Identity, 1f);
        new EntityCuboid(-4f, 10f, -1f, 4f, 12f, 1f, 0, 48).Emit(b, Matrix4x4.Identity, 1f);
        var platePose = Matrix4x4.CreateTranslation(0f, 12f, 0f);
        new EntityCuboid(-6f, 11f, -6f, 6f, 12f, 6f, 0, 32).Emit(b, platePose, 1f);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }
}
