using AutoPBR.App.Models;
using AutoPBR.App.ViewModels;
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

        vm.NormalIntensity = settings.NormalIntensity;
        vm.HeightIntensity = settings.HeightIntensity;
        vm.FastSpecular = settings.FastSpecular;
        vm.FoliageMode = string.IsNullOrWhiteSpace(settings.FoliageMode) ? "Ignore All" : settings.FoliageMode;
        vm.UseLegacyExtractor = settings.UseLegacyExtractor;
        vm.SmoothnessScale = settings.SmoothnessScale;
        vm.MetallicBoost = settings.MetallicBoost;
        vm.PorosityBias = settings.PorosityBias;
        vm.MaxThreads = settings.MaxThreads;
        vm.TempDirectory = settings.TempDirectory;
        vm.ColorScheme = string.IsNullOrWhiteSpace(settings.ColorScheme) ? "Dark" : settings.ColorScheme;
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
        vm.NormalOperator = string.IsNullOrWhiteSpace(settings.NormalOperator)
            ? nameof(NormalOperator.SobelVc)
            : settings.NormalOperator;
        vm.NormalKernelSize = string.IsNullOrWhiteSpace(settings.NormalKernelSize) ? "3" : settings.NormalKernelSize;
        vm.NormalDerivative = string.IsNullOrWhiteSpace(settings.NormalDerivative)
            ? nameof(NormalDerivative.Luminance)
            : settings.NormalDerivative;

        vm.QualityProfile = string.IsNullOrWhiteSpace(settings.QualityProfile)
            ? nameof(QualityProfile.Balanced)
            : settings.QualityProfile;

        vm.PreprocessLinearize = settings.PreprocessLinearize;
        vm.PreprocessDenoiseRadius = settings.PreprocessDenoiseRadius;
        vm.PreprocessDenoiseBlend = settings.PreprocessDenoiseBlend;
        vm.PreprocessFrequencySplit = settings.PreprocessFrequencySplit;
        vm.PreprocessFrequencyRadius = settings.PreprocessFrequencyRadius;
        vm.PreprocessFrequencyDetailStrength = settings.PreprocessFrequencyDetailStrength;

        vm.SpecularUsePercentileRemap = settings.SpecularUsePercentileRemap;
        vm.SpecularRemapLowPercentile = settings.SpecularRemapLowPercentile;
        vm.SpecularRemapHighPercentile = settings.SpecularRemapHighPercentile;
        vm.MetalHeuristicSubstrings = settings.MetalHeuristicSubstrings;

        vm.GenerateAo = settings.GenerateAo;
        vm.AoRadius = settings.AoRadius;
        vm.AoStrength = settings.AoStrength;

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
        settings.NormalIntensity = vm.NormalIntensity;
        settings.HeightIntensity = vm.HeightIntensity;
        settings.FastSpecular = vm.FastSpecular;
        settings.FoliageMode = vm.FoliageMode;
        settings.UseLegacyExtractor = vm.UseLegacyExtractor;
        settings.SmoothnessScale = vm.SmoothnessScale;
        settings.MetallicBoost = vm.MetallicBoost;
        settings.PorosityBias = vm.PorosityBias;
        settings.MaxThreads = vm.MaxThreads;
        settings.TempDirectory = vm.TempDirectory;
        settings.ColorScheme = vm.ColorScheme;
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
        settings.NormalOperator = vm.NormalOperator;
        settings.NormalKernelSize = vm.NormalKernelSize;
        settings.NormalDerivative = vm.NormalDerivative;
        settings.QualityProfile = vm.QualityProfile;

        settings.PreprocessLinearize = vm.PreprocessLinearize;
        settings.PreprocessDenoiseRadius = vm.PreprocessDenoiseRadius;
        settings.PreprocessDenoiseBlend = vm.PreprocessDenoiseBlend;
        settings.PreprocessFrequencySplit = vm.PreprocessFrequencySplit;
        settings.PreprocessFrequencyRadius = vm.PreprocessFrequencyRadius;
        settings.PreprocessFrequencyDetailStrength = vm.PreprocessFrequencyDetailStrength;

        settings.SpecularUsePercentileRemap = vm.SpecularUsePercentileRemap;
        settings.SpecularRemapLowPercentile = vm.SpecularRemapLowPercentile;
        settings.SpecularRemapHighPercentile = vm.SpecularRemapHighPercentile;
        settings.MetalHeuristicSubstrings = vm.MetalHeuristicSubstrings;

        settings.GenerateAo = vm.GenerateAo;
        settings.AoRadius = vm.AoRadius;
        settings.AoStrength = vm.AoStrength;
        settings.CustomTagRules = vm.CustomTagRules.ToList();
        settings.Save();
    }
}
