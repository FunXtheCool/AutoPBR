using AutoPBR.App.Models;
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
                ? nameof(AutoPBR.Core.Models.MlSpecularHeuristicBlendMode.SmoothnessOnly)
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
        settings.FastSpecular = vm.FastSpecular;
        settings.FoliageMode = vm.FoliageMode;
        settings.UseLegacyExtractor = vm.UseLegacyExtractor;
        settings.SmoothnessScale = vm.SmoothnessScale;
        settings.MetallicBoost = vm.MetallicBoost;
        settings.PorosityBias = vm.PorosityBias;
        settings.PlantMaterialPorosityExtra = vm.PlantMaterialPorosityExtra;
        settings.MaxThreads = vm.MaxThreads;
        settings.TempDirectory = vm.TempDirectory;
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
