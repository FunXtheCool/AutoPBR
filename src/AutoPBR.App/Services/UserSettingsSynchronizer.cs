using AutoPBR.App.Models;
using AutoPBR.App.Rendering.OpenGL;
using AutoPBR.App.ViewModels;
using AutoPBR.Core;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Services;

/// <summary>Two-way sync between MainWindowViewModel and UserSettings persistence.</summary>
internal static class UserSettingsSynchronizer
{
    public static void LoadInto(MainWindowViewModel vm, UserSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputDirectory))
        {
            vm.OutputDirectory = settings.OutputDirectory;
        }

        if (!string.IsNullOrWhiteSpace(settings.BatchFolderPath))
        {
            vm.BatchFolderPath = settings.BatchFolderPath;
        }

        vm.UseBatchFolderInput = settings.UseBatchFolderInput;

        vm.NormalIntensity = settings.NormalIntensity;
        vm.HeightIntensity = settings.HeightIntensity;
        vm.BrickHeightMapPostProcessEnabled = settings.BrickHeightMapPostProcessEnabled;
        vm.BrickHeightMinStructuralConfidence = settings.BrickHeightMinStructuralConfidence;
        vm.BrickHeightInvertDeltaThreshold = settings.BrickHeightInvertDeltaThreshold;
        vm.BrickLightGroutDiffuseDeltaMin = settings.BrickLightGroutDiffuseDeltaMin;
        vm.PreviewBrickProbeDebug = settings.PreviewBrickProbeDebug;
        vm.PreviewDisplayMode = Math.Clamp(settings.PreviewDisplayMode, 0, 1);
        vm.Preview3DAutoRotate = settings.Preview3DAutoRotate;
        vm.Preview3DEntityAnimationSpeed = settings.Preview3DEntityAnimationSpeed <= 0
            ? 1.0
            : Math.Clamp(settings.Preview3DEntityAnimationSpeed, 0.0, 4.0);
        vm.Preview3DEntityAnimationAmplitude = settings.Preview3DEntityAnimationAmplitude < 0
            ? 1.0
            : Math.Clamp(settings.Preview3DEntityAnimationAmplitude, 0.0, 2.0);
        vm.Preview3DEnableEntityAnimation = settings.Preview3DEnableEntityAnimation;
        vm.Preview3DEnableLegacyEntityWobble = settings.Preview3DEnableLegacyEntityWobble;
        vm.Preview3DPauseEntityIdleAnimation = settings.Preview3DPauseEntityIdleAnimation;
        vm.Preview3DShowGrid = settings.Preview3DShowGrid;
        vm.Preview3DShowGroundMesh = settings.Preview3DShowGroundMesh;
        vm.Preview3DShowAxes = settings.Preview3DShowAxes;
        vm.Preview3DEnableParallax = settings.Preview3DEnableParallax;
        vm.Preview3DEnableNormalMap = settings.Preview3DEnableNormalMap;
        vm.Preview3DEnableSpecularMap = settings.Preview3DEnableSpecularMap;
        vm.Preview3DParallaxHeightStrength = settings.Preview3DParallaxHeightStrength <= 0
            ? 0.05
            : Math.Clamp(settings.Preview3DParallaxHeightStrength, 0.0, 0.35);
        vm.Preview3DEnableSss = settings.Preview3DEnableSss;
        vm.Preview3DEnableParallaxShadow = settings.Preview3DEnableParallaxShadow;
        vm.Preview3DEnableParallaxAo = settings.Preview3DEnableParallaxAo;
        vm.Preview3DParallaxAoStrength = settings.Preview3DParallaxAoStrength <= 0
            ? 1.0
            : Math.Clamp(settings.Preview3DParallaxAoStrength, 0.0, 2.0);
        vm.Preview3DEnableIbl = settings.Preview3DEnableIbl;
        vm.Preview3DEnableAtmosphericSky = settings.Preview3DEnableAtmosphericSky;
        vm.Preview3DAtmosphereTurbidity = settings.Preview3DAtmosphereTurbidity <= 0
            ? 2.6
            : Math.Clamp(settings.Preview3DAtmosphereTurbidity, 1.2, 10.0);
        vm.Preview3DAtmosphereSunIntensity = settings.Preview3DAtmosphereSunIntensity <= 0
            ? 10.0
            : Math.Clamp(settings.Preview3DAtmosphereSunIntensity, 0.2, 64.0);
        vm.Preview3DAtmosphereHorizonFalloff = settings.Preview3DAtmosphereHorizonFalloff <= 0
            ? 1.35
            : Math.Clamp(settings.Preview3DAtmosphereHorizonFalloff, 0.25, 4.0);
        vm.Preview3DAtmosphereSkyExposure = settings.Preview3DAtmosphereSkyExposure <= 0
            ? 0.85
            : Math.Clamp(settings.Preview3DAtmosphereSkyExposure, 0.1, 3.0);
        vm.Preview3DAtmosphereSunDiscStrength = settings.Preview3DAtmosphereSunDiscStrength < 0
            ? 0.35
            : Math.Clamp(settings.Preview3DAtmosphereSunDiscStrength, 0.0, 2.0);
        vm.Preview3DEnableShadows = settings.Preview3DEnableShadows;
        vm.Preview3DLightYawDegrees = Math.Clamp(settings.Preview3DLightYawDegrees, -180.0, 180.0);
        vm.Preview3DLightPitchDegrees = Math.Clamp(settings.Preview3DLightPitchDegrees, -89.0, 89.0);
        vm.Preview3DTimeOfDayHours = settings.Preview3DTimeOfDayHours is > 0 and <= 24
            ? settings.Preview3DTimeOfDayHours
            : PreviewLightMath.TimeOfDayFromLightYawPitch(
                settings.Preview3DLightYawDegrees,
                settings.Preview3DLightPitchDegrees);
        vm.Preview3DAnimateTimeOfDay = settings.Preview3DAnimateTimeOfDay;
        vm.Preview3DTimeOfDaySpeed = settings.Preview3DTimeOfDaySpeed <= 0
            ? 1.0
            : Math.Clamp(settings.Preview3DTimeOfDaySpeed, 0.1, 4.0);
        vm.Preview3DHorizonFogStrength = settings.Preview3DHorizonFogStrength < 0
            ? 0
            : Math.Clamp(settings.Preview3DHorizonFogStrength, 0.0, 2.0);
        vm.Preview3DEnableGodRays = settings.Preview3DEnableGodRays;
        vm.Preview3DEnableVolumetricClouds = settings.Preview3DEnableVolumetricClouds;
        vm.Preview3DVolumetricQuality = Math.Clamp(settings.Preview3DVolumetricQuality, 0, 2);
        vm.Preview3DGodRayStrength = settings.Preview3DGodRayStrength <= 0
            ? 0.45
            : Math.Clamp(settings.Preview3DGodRayStrength, 0.0, 2.0);
        vm.Preview3DEnableShadowCascades = settings.Preview3DEnableShadowCascades;
        vm.Preview3DSpritePlaneCount = settings.Preview3DSpritePlaneCount <= 0
            ? 2
            : Math.Clamp(settings.Preview3DSpritePlaneCount, 1, 8);
        vm.Preview3DCameraOrbitSensitivity = settings.Preview3DCameraOrbitSensitivity <= 0
            ? 0.006
            : Math.Clamp(settings.Preview3DCameraOrbitSensitivity, 0.0008, 0.04);
        vm.Preview3DCameraPanSensitivity = settings.Preview3DCameraPanSensitivity <= 0
            ? 0.0022
            : Math.Clamp(settings.Preview3DCameraPanSensitivity, 0.0003, 0.02);
        vm.Preview3DCameraZoomSensitivity = settings.Preview3DCameraZoomSensitivity <= 0
            ? 0.12
            : Math.Clamp(settings.Preview3DCameraZoomSensitivity, 0.02, 0.5);
        var boomDefault = Math.Sqrt(3.6 * 3.6 + 2.6 * 2.6 + 3.6 * 3.6);
        vm.Preview3DCameraOrbitBoomDistance = settings.Preview3DCameraOrbitBoomDistance <= 0
            ? boomDefault
            : Math.Clamp(settings.Preview3DCameraOrbitBoomDistance, 1.05, 120.0);
        vm.Preview3DCameraResetKey = string.IsNullOrWhiteSpace(settings.Preview3DCameraResetKey)
            ? "R"
            : settings.Preview3DCameraResetKey.Trim();
        vm.Preview3DItemUseAlphaBlend = settings.Preview3DItemUseAlphaBlend;
        vm.Preview3DEntityAlphaMode = Math.Clamp(settings.Preview3DEntityAlphaMode, 0, 2);
        vm.Preview3DEnableEntityLabPbrShading = settings.Preview3DEnableEntityLabPbrShading;
        vm.Preview3DEnableEntityParallax = settings.Preview3DEnableEntityParallax;
        vm.FastSpecular = settings.FastSpecular;
        vm.FoliageMode = string.IsNullOrWhiteSpace(settings.FoliageMode) ? "No Height" : settings.FoliageMode;
        vm.UseLegacyExtractor = settings.UseLegacyExtractor;
        vm.SmoothnessScale = settings.SmoothnessScale;
        vm.MetallicBoost = settings.MetallicBoost;
        vm.PorosityBias = settings.PorosityBias;
        vm.PlantMaterialPorosityExtra = Math.Clamp(
            settings.PlantMaterialPorosityExtra ?? AutoPbrDefaults.DefaultPlantMaterialPorosityExtra,
            -128,
            128);
        vm.MaxThreads = settings.MaxThreads;
        vm.TempDirectory = settings.TempDirectory;
        vm.DebugMode = settings.DebugMode;
        vm.ColorScheme = string.IsNullOrWhiteSpace(settings.ColorScheme) ? "Dark" : settings.ColorScheme;
        vm.UiScale = settings.UiScale <= 0
            ? 1.0
            : Math.Clamp(settings.UiScale, MainWindowViewModel.MinUiScale, MainWindowViewModel.MaxUiScale);
        vm.ProcessBlocks = settings.ProcessBlocks;
        vm.ProcessItems = settings.ProcessItems;
        vm.ProcessArmor = settings.ProcessArmor;
        vm.ProcessEntity = settings.ProcessEntity;
        vm.ProcessParticles = settings.ProcessParticles;
        vm.UseDeepBumpNormals = settings.UseDeepBumpNormals;
        vm.DeepBumpOverlap = string.IsNullOrWhiteSpace(settings.DeepBumpOverlap) ? "Large" : settings.DeepBumpOverlap;
        vm.DeepBumpInputMode = string.IsNullOrWhiteSpace(settings.DeepBumpInputMode)
            ? nameof(DeepBumpInputMode.Auto)
            : settings.DeepBumpInputMode;
        vm.DeepBumpForceBlue255 = settings.DeepBumpForceBlue255;
        vm.DeepBumpNormalIntensity = settings.DeepBumpNormalIntensity <= 0
            ? AutoPbrDefaults.DefaultNormalIntensity
            : settings.DeepBumpNormalIntensity;
        vm.DeepBumpNormalSoftClamp = Math.Clamp(settings.DeepBumpNormalSoftClamp, 0.0, 2.0);
        vm.DeepBumpEdgeGuidedEnhance = settings.DeepBumpEdgeGuidedEnhance;
        vm.DeepBumpEdgeGuidedStrength = Math.Clamp(settings.DeepBumpEdgeGuidedStrength, 0.0, 6.0);
        vm.DeepBumpEdgeGuidedGamma = Math.Clamp(settings.DeepBumpEdgeGuidedGamma, 0.1, 8.0);
        vm.DeepBumpEdgeGuidedDirectionMix = Math.Clamp(settings.DeepBumpEdgeGuidedDirectionMix, 0.0, 1.0);
        vm.NormalHeightTransparentAlphaClampMax = Math.Clamp(settings.NormalHeightTransparentAlphaClampMax, 0, 255);
        vm.NormalOperator = string.IsNullOrWhiteSpace(settings.NormalOperator)
            ? nameof(NormalOperator.SobelVc)
            : settings.NormalOperator;
        vm.NormalKernelSize = string.IsNullOrWhiteSpace(settings.NormalKernelSize) ? "3" : settings.NormalKernelSize;
        vm.NormalDerivative = string.IsNullOrWhiteSpace(settings.NormalDerivative)
            ? nameof(NormalDerivative.Luminance)
            : settings.NormalDerivative;

        vm.PreprocessLinearize = settings.PreprocessLinearize;
        vm.PreprocessDenoiseRadius = settings.PreprocessDenoiseRadius;
        vm.PreprocessDenoiseBlend = settings.PreprocessDenoiseBlend;
        vm.PreprocessFrequencySplit = settings.PreprocessFrequencySplit;
        vm.PreprocessFrequencyRadius = settings.PreprocessFrequencyRadius;
        vm.PreprocessFrequencyDetailStrength = settings.PreprocessFrequencyDetailStrength;

        vm.SpecularUsePercentileRemap = settings.SpecularUsePercentileRemap;
        vm.SpecularRemapLowPercentile = settings.SpecularRemapLowPercentile;
        vm.SpecularRemapHighPercentile = settings.SpecularRemapHighPercentile;
        vm.SpecularForceNoEmissive = settings.SpecularForceNoEmissive;
        vm.UseMlSpecularPredictor = settings.UseMlSpecularPredictor;
        vm.MlSpecularModelPath = settings.MlSpecularModelPath;
        vm.MlSpecularModelPath16 = settings.MlSpecularModelPath16;
        vm.MlSpecularModelPath32 = settings.MlSpecularModelPath32;
        vm.MlSpecularModelPath64 = settings.MlSpecularModelPath64;
        vm.MlSpecularModelPath128 = settings.MlSpecularModelPath128;
        vm.MlSpecularModelPath256 = settings.MlSpecularModelPath256;
        vm.MlSpecularHeuristicBlend = Math.Clamp(
            settings.MlSpecularHeuristicBlend ?? AutoPbrDefaults.DefaultMlSpecularHeuristicBlend,
            0.0,
            1.0);
        {
            var mode = string.IsNullOrWhiteSpace(settings.MlSpecularHeuristicBlendMode)
                ? nameof(MlSpecularHeuristicBlendMode.SmoothnessOnly)
                : settings.MlSpecularHeuristicBlendMode.Trim();
            if (!Enum.TryParse<MlSpecularHeuristicBlendMode>(mode, ignoreCase: true, out _))
            {
                mode = nameof(MlSpecularHeuristicBlendMode.SmoothnessOnly);
            }

            vm.MlSpecularHeuristicBlendMode = mode;
        }
        {
            var math = string.IsNullOrWhiteSpace(settings.MlSpecularBlendMath)
                ? nameof(MlSpecularBlendMath.Linear)
                : settings.MlSpecularBlendMath.Trim();
            if (!Enum.TryParse<MlSpecularBlendMath>(math, ignoreCase: true, out _))
            {
                math = nameof(MlSpecularBlendMath.Linear);
            }

            vm.MlSpecularBlendMath = math;
        }
        vm.MlSpecularUseEdgeChannel = settings.MlSpecularUseEdgeChannel;
        vm.MlSpecularTransparentAlphaClampMax = Math.Clamp(settings.MlSpecularTransparentAlphaClampMax, 0, 255);
        vm.SpecularDebugDisableHeuristicSpecular = settings.SpecularDebugDisableHeuristicSpecular;
        vm.SpecularDebugSkipSpecularRemap = settings.SpecularDebugSkipSpecularRemap;
        vm.SpecularDebugVerboseSpecularMl = settings.SpecularDebugVerboseSpecularMl;

        vm.GenerateAo = settings.GenerateAo;
        vm.AoRadius = settings.AoRadius;
        vm.AoStrength = settings.AoStrength;
        vm.PreferOnnxTensorRtExecutionProvider = settings.PreferOnnxTensorRtExecutionProvider;

        vm.UseSemanticMaterialTags = settings.UseSemanticMaterialTags;
        vm.MaterialTagMinSimilarity = settings.MaterialTagMinSimilarity <= 0 || settings.MaterialTagMinSimilarity > 1
            ? 0.25
            : settings.MaterialTagMinSimilarity;
        vm.MaterialTagCertaintyThreshold = settings.MaterialTagCertaintyThreshold <= 0 || settings.MaterialTagCertaintyThreshold > 1
            ? 0.35
            : settings.MaterialTagCertaintyThreshold;
        vm.MaterialTagMaxCount = Math.Clamp(settings.MaterialTagMaxCount <= 0 ? 3 : settings.MaterialTagMaxCount, 1, 16);
        vm.DictionaryEvidenceEnabled = settings.DictionaryEvidenceEnabled;
        vm.DictionaryEvidenceWeight = Math.Clamp(settings.DictionaryEvidenceWeight, 0.0, 1.0);
        vm.DictionaryMinEvidenceScore = Math.Clamp(settings.DictionaryMinEvidenceScore, -1.0, 1.0);
        vm.DictionaryRequestTimeoutMs = Math.Clamp(settings.DictionaryRequestTimeoutMs <= 0 ? 900 : settings.DictionaryRequestTimeoutMs, 100, 5000);

        vm.CustomTagRules.Clear();
        foreach (var entry in settings.CustomTagRules)
        {
            vm.CustomTagRules.Add(entry);
        }
    }

    public static void SaveFrom(MainWindowViewModel vm, UserSettings settings)
    {
        settings.OutputDirectory = vm.OutputDirectory;
        settings.BatchFolderPath = vm.BatchFolderPath;
        settings.UseBatchFolderInput = vm.UseBatchFolderInput;
        settings.NormalIntensity = vm.NormalIntensity;
        settings.HeightIntensity = vm.HeightIntensity;
        settings.BrickHeightMapPostProcessEnabled = vm.BrickHeightMapPostProcessEnabled;
        settings.BrickHeightMinStructuralConfidence = vm.BrickHeightMinStructuralConfidence;
        settings.BrickHeightInvertDeltaThreshold = vm.BrickHeightInvertDeltaThreshold;
        settings.BrickLightGroutDiffuseDeltaMin = vm.BrickLightGroutDiffuseDeltaMin;
        settings.PreviewBrickProbeDebug = vm.PreviewBrickProbeDebug;
        settings.PreviewDisplayMode = Math.Clamp(vm.PreviewDisplayMode, 0, 1);
        settings.Preview3DAutoRotate = vm.Preview3DAutoRotate;
        settings.Preview3DEntityAnimationSpeed = Math.Clamp(vm.Preview3DEntityAnimationSpeed, 0.0, 4.0);
        settings.Preview3DEntityAnimationAmplitude = Math.Clamp(vm.Preview3DEntityAnimationAmplitude, 0.0, 2.0);
        settings.Preview3DEnableEntityAnimation = vm.Preview3DEnableEntityAnimation;
        settings.Preview3DEnableLegacyEntityWobble = vm.Preview3DEnableLegacyEntityWobble;
        settings.Preview3DPauseEntityIdleAnimation = vm.Preview3DPauseEntityIdleAnimation;
        settings.Preview3DShowGrid = vm.Preview3DShowGrid;
        settings.Preview3DShowGroundMesh = vm.Preview3DShowGroundMesh;
        settings.Preview3DShowAxes = vm.Preview3DShowAxes;
        settings.Preview3DEnableParallax = vm.Preview3DEnableParallax;
        settings.Preview3DEnableNormalMap = vm.Preview3DEnableNormalMap;
        settings.Preview3DEnableSpecularMap = vm.Preview3DEnableSpecularMap;
        settings.Preview3DParallaxHeightStrength = Math.Clamp(vm.Preview3DParallaxHeightStrength, 0.0, 0.35);
        settings.Preview3DEnableSss = vm.Preview3DEnableSss;
        settings.Preview3DEnableParallaxShadow = vm.Preview3DEnableParallaxShadow;
        settings.Preview3DEnableParallaxAo = vm.Preview3DEnableParallaxAo;
        settings.Preview3DParallaxAoStrength = Math.Clamp(vm.Preview3DParallaxAoStrength, 0.0, 2.0);
        settings.Preview3DEnableIbl = vm.Preview3DEnableIbl;
        settings.Preview3DEnableAtmosphericSky = vm.Preview3DEnableAtmosphericSky;
        settings.Preview3DAtmosphereTurbidity = Math.Clamp(vm.Preview3DAtmosphereTurbidity, 1.2, 10.0);
        settings.Preview3DAtmosphereSunIntensity = Math.Clamp(vm.Preview3DAtmosphereSunIntensity, 0.2, 64.0);
        settings.Preview3DAtmosphereHorizonFalloff = Math.Clamp(vm.Preview3DAtmosphereHorizonFalloff, 0.25, 4.0);
        settings.Preview3DAtmosphereSkyExposure = Math.Clamp(vm.Preview3DAtmosphereSkyExposure, 0.1, 3.0);
        settings.Preview3DAtmosphereSunDiscStrength = Math.Clamp(vm.Preview3DAtmosphereSunDiscStrength, 0.0, 2.0);
        settings.Preview3DTimeOfDayHours = Math.Clamp(vm.Preview3DTimeOfDayHours, 0.0, 24.0);
        settings.Preview3DAnimateTimeOfDay = vm.Preview3DAnimateTimeOfDay;
        settings.Preview3DTimeOfDaySpeed = Math.Clamp(vm.Preview3DTimeOfDaySpeed, 0.1, 4.0);
        settings.Preview3DHorizonFogStrength = Math.Clamp(vm.Preview3DHorizonFogStrength, 0.0, 2.0);
        settings.Preview3DEnableGodRays = vm.Preview3DEnableGodRays;
        settings.Preview3DEnableVolumetricClouds = vm.Preview3DEnableVolumetricClouds;
        settings.Preview3DVolumetricQuality = Math.Clamp(vm.Preview3DVolumetricQuality, 0, 2);
        settings.Preview3DGodRayStrength = Math.Clamp(vm.Preview3DGodRayStrength, 0.0, 2.0);
        settings.Preview3DEnableShadows = vm.Preview3DEnableShadows;
        settings.Preview3DLightYawDegrees = Math.Clamp(vm.Preview3DLightYawDegrees, -180.0, 180.0);
        settings.Preview3DLightPitchDegrees = Math.Clamp(vm.Preview3DLightPitchDegrees, -89.0, 89.0);
        settings.Preview3DEnableShadowCascades = vm.Preview3DEnableShadowCascades;
        settings.Preview3DSpritePlaneCount = Math.Clamp(vm.Preview3DSpritePlaneCount, 1, 8);
        settings.Preview3DCameraOrbitSensitivity = Math.Clamp(vm.Preview3DCameraOrbitSensitivity, 0.0008, 0.04);
        settings.Preview3DCameraPanSensitivity = Math.Clamp(vm.Preview3DCameraPanSensitivity, 0.0003, 0.02);
        settings.Preview3DCameraZoomSensitivity = Math.Clamp(vm.Preview3DCameraZoomSensitivity, 0.02, 0.5);
        settings.Preview3DCameraOrbitBoomDistance = Math.Clamp(vm.Preview3DCameraOrbitBoomDistance, 1.05, 120.0);
        settings.Preview3DCameraResetKey = string.IsNullOrWhiteSpace(vm.Preview3DCameraResetKey)
            ? "R"
            : vm.Preview3DCameraResetKey.Trim();
        settings.Preview3DItemUseAlphaBlend = vm.Preview3DItemUseAlphaBlend;
        settings.Preview3DEntityAlphaMode = Math.Clamp(vm.Preview3DEntityAlphaMode, 0, 2);
        settings.Preview3DEnableEntityLabPbrShading = vm.Preview3DEnableEntityLabPbrShading;
        settings.Preview3DEnableEntityParallax = vm.Preview3DEnableEntityParallax;
        settings.FastSpecular = vm.FastSpecular;
        settings.FoliageMode = vm.FoliageMode;
        settings.UseLegacyExtractor = vm.UseLegacyExtractor;
        settings.SmoothnessScale = vm.SmoothnessScale;
        settings.MetallicBoost = vm.MetallicBoost;
        settings.PorosityBias = vm.PorosityBias;
        settings.PlantMaterialPorosityExtra = vm.PlantMaterialPorosityExtra;
        settings.MaxThreads = vm.MaxThreads;
        settings.TempDirectory = vm.TempDirectory;
        settings.DebugMode = vm.DebugMode;
        settings.ColorScheme = vm.ColorScheme;
        settings.UiScale = Math.Clamp(vm.UiScale, MainWindowViewModel.MinUiScale, MainWindowViewModel.MaxUiScale);
        settings.Language = vm.SelectedLanguage?.CultureCode ?? "en";
        settings.ProcessBlocks = vm.ProcessBlocks;
        settings.ProcessItems = vm.ProcessItems;
        settings.ProcessArmor = vm.ProcessArmor;
        settings.ProcessEntity = vm.ProcessEntity;
        settings.ProcessParticles = vm.ProcessParticles;
        settings.UseDeepBumpNormals = vm.UseDeepBumpNormals;
        settings.DeepBumpOverlap = vm.DeepBumpOverlap;
        settings.DeepBumpInputMode = vm.DeepBumpInputMode;
        settings.DeepBumpForceBlue255 = vm.DeepBumpForceBlue255;
        settings.DeepBumpNormalIntensity = vm.DeepBumpNormalIntensity;
        settings.DeepBumpNormalSoftClamp = Math.Clamp(vm.DeepBumpNormalSoftClamp, 0.0, 2.0);
        settings.DeepBumpEdgeGuidedEnhance = vm.DeepBumpEdgeGuidedEnhance;
        settings.DeepBumpEdgeGuidedStrength = Math.Clamp(vm.DeepBumpEdgeGuidedStrength, 0.0, 6.0);
        settings.DeepBumpEdgeGuidedGamma = Math.Clamp(vm.DeepBumpEdgeGuidedGamma, 0.1, 8.0);
        settings.DeepBumpEdgeGuidedDirectionMix = Math.Clamp(vm.DeepBumpEdgeGuidedDirectionMix, 0.0, 1.0);
        settings.NormalHeightTransparentAlphaClampMax = Math.Clamp(vm.NormalHeightTransparentAlphaClampMax, 0, 255);
        settings.NormalOperator = vm.NormalOperator;
        settings.NormalKernelSize = vm.NormalKernelSize;
        settings.NormalDerivative = vm.NormalDerivative;

        settings.PreprocessLinearize = vm.PreprocessLinearize;
        settings.PreprocessDenoiseRadius = vm.PreprocessDenoiseRadius;
        settings.PreprocessDenoiseBlend = vm.PreprocessDenoiseBlend;
        settings.PreprocessFrequencySplit = vm.PreprocessFrequencySplit;
        settings.PreprocessFrequencyRadius = vm.PreprocessFrequencyRadius;
        settings.PreprocessFrequencyDetailStrength = vm.PreprocessFrequencyDetailStrength;

        settings.SpecularUsePercentileRemap = vm.SpecularUsePercentileRemap;
        settings.SpecularRemapLowPercentile = vm.SpecularRemapLowPercentile;
        settings.SpecularRemapHighPercentile = vm.SpecularRemapHighPercentile;
        settings.SpecularForceNoEmissive = vm.SpecularForceNoEmissive;
        settings.UseMlSpecularPredictor = vm.UseMlSpecularPredictor;
        settings.MlSpecularModelPath = vm.MlSpecularModelPath;
        settings.MlSpecularModelPath16 = vm.MlSpecularModelPath16;
        settings.MlSpecularModelPath32 = vm.MlSpecularModelPath32;
        settings.MlSpecularModelPath64 = vm.MlSpecularModelPath64;
        settings.MlSpecularModelPath128 = vm.MlSpecularModelPath128;
        settings.MlSpecularModelPath256 = vm.MlSpecularModelPath256;
        settings.MlSpecularHeuristicBlend = Math.Clamp(vm.MlSpecularHeuristicBlend, 0.0, 1.0);
        settings.MlSpecularHeuristicBlendMode = vm.MlSpecularHeuristicBlendMode;
        settings.MlSpecularBlendMath = vm.MlSpecularBlendMath;
        settings.MlSpecularUseEdgeChannel = vm.MlSpecularUseEdgeChannel;
        settings.MlSpecularTransparentAlphaClampMax = Math.Clamp(vm.MlSpecularTransparentAlphaClampMax, 0, 255);
        settings.SpecularDebugDisableHeuristicSpecular = vm.SpecularDebugDisableHeuristicSpecular;
        settings.SpecularDebugSkipSpecularRemap = vm.SpecularDebugSkipSpecularRemap;
        settings.SpecularDebugVerboseSpecularMl = vm.SpecularDebugVerboseSpecularMl;

        settings.GenerateAo = vm.GenerateAo;
        settings.AoRadius = vm.AoRadius;
        settings.AoStrength = vm.AoStrength;
        settings.PreferOnnxTensorRtExecutionProvider = vm.PreferOnnxTensorRtExecutionProvider;
        settings.UseSemanticMaterialTags = vm.UseSemanticMaterialTags;
        settings.MaterialTagMinSimilarity = Math.Clamp(vm.MaterialTagMinSimilarity, 0.05, 0.99);
        settings.MaterialTagCertaintyThreshold = Math.Clamp(vm.MaterialTagCertaintyThreshold, 0.05, 0.99);
        settings.MaterialTagMaxCount = Math.Clamp(vm.MaterialTagMaxCount, 1, 16);
        settings.DictionaryEvidenceEnabled = vm.DictionaryEvidenceEnabled;
        settings.DictionaryEvidenceWeight = Math.Clamp(vm.DictionaryEvidenceWeight, 0.0, 1.0);
        settings.DictionaryMinEvidenceScore = Math.Clamp(vm.DictionaryMinEvidenceScore, -1.0, 1.0);
        settings.DictionaryRequestTimeoutMs = Math.Clamp(vm.DictionaryRequestTimeoutMs, 100, 5000);
        settings.CustomTagRules = vm.CustomTagRules.ToList();
        settings.Save();
    }
}
