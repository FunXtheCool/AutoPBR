using AutoPBR.App.Models;
using AutoPBR.App.ViewModels;

namespace AutoPBR.App.Services;

/// <summary>Two-way sync between MainWindowViewModel and UserSettings persistence.</summary>
internal static class UserSettingsSynchronizer
{
    public static void LoadInto(MainWindowViewModel vm, UserSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputDirectory))
            vm.OutputDirectory = settings.OutputDirectory;

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
        vm.NormalOperator = string.IsNullOrWhiteSpace(settings.NormalOperator)
            ? nameof(Core.Models.NormalOperator.SobelVc)
            : settings.NormalOperator;
        vm.NormalKernelSize = string.IsNullOrWhiteSpace(settings.NormalKernelSize) ? "3" : settings.NormalKernelSize;
        vm.NormalDerivative = string.IsNullOrWhiteSpace(settings.NormalDerivative)
            ? nameof(Core.Models.NormalDerivative.Luminance)
            : settings.NormalDerivative;
    }

    public static void SaveFrom(MainWindowViewModel vm, UserSettings settings)
    {
        settings.OutputDirectory = vm.OutputDirectory;
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
        settings.NormalOperator = vm.NormalOperator;
        settings.NormalKernelSize = vm.NormalKernelSize;
        settings.NormalDerivative = vm.NormalDerivative;
        settings.Save();
    }
}
