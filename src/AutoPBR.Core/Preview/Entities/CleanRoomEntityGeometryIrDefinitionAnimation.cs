using System.Numerics;
using System.Text.Json;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    /// <summary>
    /// Applies <see cref="DefinitionAnimationPreviewSampling"/> channels to geometry-IR catalog meshes
    /// (same part-origin delta path as <see cref="ApplySetupAnimToGeometryIrMesh"/>).
    /// </summary>
    private static void TryApplyDefinitionAnimationGeometryIrPreviewPass(
        string builderMethod,
        string normalizedAssetPath,
        MinecraftNativeProfile profile,
        bool isBaby,
        float animationTimeSeconds,
        MergedJavaBlockModel merged,
        JsonElement geometryRoot,
        GeometryIrMeshEmitOptions emitOptions,
        bool skipBreezeIdleWind = false)
    {
        if (!TryBuildDefinitionAnimationGeometryIrPose(
                builderMethod,
                normalizedAssetPath,
                profile,
                isBaby,
                animationTimeSeconds,
                skipBreezeIdleWind,
                out var pose))
        {
            return;
        }

        _ = ApplySetupAnimToGeometryIrMesh(
            merged,
            geometryRoot,
            pose,
            GeometryIrPartWorldPoseIndex.Build(geometryRoot),
            emitOptions);
    }

    private static bool TryBuildDefinitionAnimationGeometryIrPose(
        string builderMethod,
        string normalizedAssetPath,
        MinecraftNativeProfile profile,
        bool isBaby,
        float animationTimeSeconds,
        bool skipBreezeIdleWind,
        out VanillaSetupAnimRuntime.PoseResult pose)
    {
        pose = new VanillaSetupAnimRuntime.PoseResult();
        switch (builderMethod)
        {
            case "Armadillo":
                return TryAddArmadilloDefinitionAnimationPose(profile, isBaby, animationTimeSeconds, pose);
            case "Breeze":
                return TryAddBreezeDefinitionAnimationPose(
                    normalizedAssetPath, profile, animationTimeSeconds, pose, skipBreezeIdleWind);
            case "Fox":
                return TryAddFoxDefinitionAnimationPose(profile, isBaby, animationTimeSeconds, pose);
            default:
                return false;
        }
    }

    private static bool TryAddArmadilloDefinitionAnimationPose(
        MinecraftNativeProfile profile,
        bool isBaby,
        float animationTimeSeconds,
        VanillaSetupAnimRuntime.PoseResult pose)
    {
        if (isBaby)
        {
            if (!DefinitionAnimationPreviewSampling.TrySampleBabyArmadilloWalkTailRotationDegrees(
                    profile, animationTimeSeconds, out var babyTailDeg))
            {
                return false;
            }

            pose.Parts["tail"] = new VanillaSetupAnimRuntime.PartPose
            {
                XRot = babyTailDeg.X * (MathF.PI / 180f),
            };
            return true;
        }

        if (!DefinitionAnimationPreviewSampling.TrySampleArmadilloWalkTailRotationDegrees(
                profile, animationTimeSeconds, out var adultTailDeg))
        {
            return false;
        }

        pose.Parts["tail"] = new VanillaSetupAnimRuntime.PartPose
        {
            XRot = adultTailDeg.X * (MathF.PI / 180f),
        };
        return true;
    }

    private static bool TryAddBreezeDefinitionAnimationPose(
        string normalizedAssetPath,
        MinecraftNativeProfile profile,
        float animationTimeSeconds,
        VanillaSetupAnimRuntime.PoseResult pose,
        bool skipIdleWind = false)
    {
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        var isWindTexture = norm.Contains("breeze_wind", StringComparison.OrdinalIgnoreCase);
        var isEyesTexture = norm.Contains("breeze_eyes", StringComparison.OrdinalIgnoreCase);
        var wind = !skipIdleWind && !isEyesTexture &&
                   TryAddBreezeIdleWindDefinitionPose(profile, animationTimeSeconds, pose);
        var head = !isWindTexture && TryAddBreezeShootHeadDefinitionPose(profile, animationTimeSeconds, pose);
        var rods = !isWindTexture && !isEyesTexture &&
                   TryAddBreezeIdleRodsDefinitionPose(profile, animationTimeSeconds, pose);
        return wind || head || rods;
    }

    private static bool TryAddBreezeIdleRodsDefinitionPose(
        MinecraftNativeProfile profile,
        float animationTimeSeconds,
        VanillaSetupAnimRuntime.PoseResult pose)
    {
        if (!DefinitionAnimationPreviewSampling.TrySampleBreezeIdleRodsRotationDegrees(
                profile, animationTimeSeconds, out var rodsDeg))
        {
            return false;
        }

        pose.Parts["rods"] = new VanillaSetupAnimRuntime.PartPose
        {
            XRot = rodsDeg.X * (MathF.PI / 180f),
            YRot = rodsDeg.Y * (MathF.PI / 180f),
            ZRot = rodsDeg.Z * (MathF.PI / 180f),
        };
        return true;
    }

    private static bool TryAddBreezeIdleWindDefinitionPose(
        MinecraftNativeProfile profile,
        float animationTimeSeconds,
        VanillaSetupAnimRuntime.PoseResult pose)
    {
        if (!DefinitionAnimationPreviewSampling.TryResolveCatalogBreezeIdleWindTranslations(
                profile, animationTimeSeconds, out var windMid, out var windTop))
        {
            return false;
        }

        pose.Parts["wind_mid"] = new VanillaSetupAnimRuntime.PartPose
        {
            X = windMid.X,
            Y = windMid.Y,
            Z = windMid.Z,
        };
        pose.Parts["wind_top"] = new VanillaSetupAnimRuntime.PartPose
        {
            X = windTop.X,
            Y = windTop.Y,
            Z = windTop.Z,
        };
        return true;
    }

    private static bool TryAddBreezeShootHeadDefinitionPose(
        MinecraftNativeProfile profile,
        float animationTimeSeconds,
        VanillaSetupAnimRuntime.PoseResult pose)
    {
        var headPose = new VanillaSetupAnimRuntime.PartPose();
        var any = false;
        if (DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadRotationDegrees(
                profile, animationTimeSeconds, out var shootHeadDeg))
        {
            headPose.XRot = shootHeadDeg.X * (MathF.PI / 180f);
            any = true;
        }

        if (DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadPosition(
                profile, animationTimeSeconds, out var shootHeadTranslation))
        {
            headPose.X = shootHeadTranslation.X;
            headPose.Y = shootHeadTranslation.Y;
            headPose.Z = shootHeadTranslation.Z;
            any = true;
        }

        if (!any)
        {
            return false;
        }

        pose.Parts["head"] = headPose;
        return true;
    }

    private static bool TryAddFoxDefinitionAnimationPose(
        MinecraftNativeProfile profile,
        bool isBaby,
        float animationTimeSeconds,
        VanillaSetupAnimRuntime.PoseResult pose)
    {
        if (!isBaby ||
            !DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkRightHindLegRotationDegrees(
                profile, animationTimeSeconds, out var foxBabyRhDeg))
        {
            return false;
        }

        pose.Parts["right_hind_leg"] = new VanillaSetupAnimRuntime.PartPose
        {
            XRot = foxBabyRhDeg.X * (MathF.PI / 180f),
        };
        return true;
    }
}
