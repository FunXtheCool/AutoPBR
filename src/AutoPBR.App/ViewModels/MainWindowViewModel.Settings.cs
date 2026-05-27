using System.Collections.ObjectModel;
using System.Globalization;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JetBrains.Annotations;

using AutoPBR.App.Lang;
using AutoPBR.App.Models;
using AutoPBR.App.Services;
using AutoPBR.App.ViewModels.Rulesets;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>Languages shown in the Language dropdown (display name, culture code). Top 10 most spoken worldwide.</summary>
    public ObservableCollection<LanguageOption> SupportedLanguages { get; } = new(
    [
        new LanguageOption("English", "en"),
        new LanguageOption("中文 (简体)", "zh-Hans"),
        new LanguageOption("Español", "es"),
        new LanguageOption("हिन्दी", "hi"),
        new LanguageOption("Français", "fr"),
        new LanguageOption("العربية", "ar"),
        new LanguageOption("Português", "pt"),
        new LanguageOption("Русский", "ru"),
        new LanguageOption("Deutsch", "de"),
        new LanguageOption("日本語", "ja"),
    ]);
    private void ApplyCulture(string cultureCode)
    {
        Strings = LocalizationService.ApplyCulture(cultureCode);
        OnPropertyChanged(nameof(Strings));
        foreach (var t in BackgroundTasks)
        {
            t.Label = ResolveBackgroundTaskLabel(t.Id);
        }

        RefreshFoliageModeOptions();
        RefreshMlSpecularBlendModeOptions();
        RefreshMlSpecularBlendMathOptions();
        RefreshDeepBumpOverlapOptions();
        RefreshDeepBumpInputModeOptions();
        RefreshNormalOperatorOptions();
        RefreshNormalKernelSizeOptions();
        RefreshNormalDerivativeOptions();
        RefreshColorSchemeOptions();
        UpdateStatusText();
    }

    private void RefreshMlSpecularBlendModeOptions()
    {
        MlSpecularBlendModeOptions.Clear();
        MlSpecularBlendModeOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendModeSmoothnessOnly,
            nameof(AutoPBR.Core.Models.MlSpecularHeuristicBlendMode.SmoothnessOnly)));
        MlSpecularBlendModeOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendModeAiMetalAndEmissive,
            nameof(AutoPBR.Core.Models.MlSpecularHeuristicBlendMode.AiMetalAndEmissive)));
        MlSpecularBlendModeOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendModeFull,
            nameof(AutoPBR.Core.Models.MlSpecularHeuristicBlendMode.Full)));

        SelectedMlSpecularBlendModeOption =
            MlSpecularBlendModeOptions.FirstOrDefault(x =>
                string.Equals(x.Value, MlSpecularHeuristicBlendMode, StringComparison.OrdinalIgnoreCase)) ??
            MlSpecularBlendModeOptions[0];
    }

    private void RefreshMlSpecularBlendMathOptions()
    {
        MlSpecularBlendMathOptions.Clear();
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathLinear,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.Linear)));
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathSoftLight,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.SoftLight)));
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathOverlay,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.Overlay)));
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathScreen,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.Screen)));
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathBiasGain,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.BiasGain)));
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathSigmoidCrossfade,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.SigmoidCrossfade)));

        SelectedMlSpecularBlendMathOption =
            MlSpecularBlendMathOptions.FirstOrDefault(x =>
                string.Equals(x.Value, MlSpecularBlendMath, StringComparison.OrdinalIgnoreCase)) ??
            MlSpecularBlendMathOptions[0];
    }

    private void RefreshColorSchemeOptions()
    {
        ColorSchemeOptions.Clear();
        foreach (var o in LocalizationService.GetColorSchemeOptions())
        {
            ColorSchemeOptions.Add(o);
        }


        SelectedColorSchemeOption = ColorSchemeOptions.FirstOrDefault(x =>
                                        string.Equals(x.Value, ColorScheme, StringComparison.OrdinalIgnoreCase))
                                    ?? ColorSchemeOptions[0];
    }

    private void RefreshDeepBumpOverlapOptions()
    {
        DeepBumpOverlapOptions.Clear();
        foreach (var o in LocalizationService.GetDeepBumpOverlapOptions(Strings))
        {
            DeepBumpOverlapOptions.Add(o);
        }

        SelectedDeepBumpOverlap =
            DeepBumpOverlapOptions.FirstOrDefault(x =>
                string.Equals(x.Value, DeepBumpOverlap, StringComparison.OrdinalIgnoreCase)) ??
            DeepBumpOverlapOptions[2];
    }

    private void RefreshDeepBumpInputModeOptions()
    {
        DeepBumpInputModeOptions.Clear();
        foreach (var o in LocalizationService.GetDeepBumpInputModeOptions(Strings))
        {
            DeepBumpInputModeOptions.Add(o);
        }

        SelectedDeepBumpInputMode =
            DeepBumpInputModeOptions.FirstOrDefault(x =>
                string.Equals(x.Value, DeepBumpInputMode, StringComparison.OrdinalIgnoreCase)) ??
            DeepBumpInputModeOptions[0];
    }

    private void RefreshFoliageModeOptions()
    {
        FoliageModeOptions.Clear();
        foreach (var o in LocalizationService.GetFoliageModeOptions(Strings))
        {
            FoliageModeOptions.Add(o);
        }


        SelectedFoliageMode =
            FoliageModeOptions.FirstOrDefault(x =>
                string.Equals(x.Value, FoliageMode, StringComparison.OrdinalIgnoreCase)) ?? FoliageModeOptions[0];
    }

    private void RefreshNormalOperatorOptions()
    {
        NormalOperatorOptions.Clear();
        foreach (var o in LocalizationService.GetNormalOperatorOptions())
        {
            NormalOperatorOptions.Add(o);
        }


        SelectedNormalOperator = NormalOperatorOptions.FirstOrDefault(x =>
                                     string.Equals(x.Value, NormalOperator, StringComparison.OrdinalIgnoreCase))
                                 ?? NormalOperatorOptions[0];
    }

    private void RefreshNormalKernelSizeOptions()
    {
        NormalKernelSizeOptions.Clear();
        foreach (var o in LocalizationService.GetNormalKernelSizeOptions(NormalOperator))
        {
            NormalKernelSizeOptions.Add(o);
        }


        SelectedNormalKernelSize = NormalKernelSizeOptions.FirstOrDefault(x =>
                                       string.Equals(x.Value, NormalKernelSize, StringComparison.OrdinalIgnoreCase))
                                   ?? NormalKernelSizeOptions[0];
    }

    private void RefreshNormalDerivativeOptions()
    {
        NormalDerivativeOptions.Clear();
        foreach (var o in LocalizationService.GetNormalDerivativeOptions())
        {
            NormalDerivativeOptions.Add(o);
        }


        SelectedNormalDerivative = NormalDerivativeOptions.FirstOrDefault(x =>
                                       string.Equals(x.Value, NormalDerivative, StringComparison.OrdinalIgnoreCase))
                                   ?? NormalDerivativeOptions[0];
    }
    partial void OnBatchFolderPathChanged(string? value)
    {
        _ = value;
        ScanBatchCommand.NotifyCanExecuteChanged();
        ScanCurrentInputCommand.NotifyCanExecuteChanged();
        ConvertCommand.NotifyCanExecuteChanged();
        if (_loadingSettings)
        {
            return;
        }

        SaveSettings();
    }

    partial void OnUseBatchFolderInputChanged(bool value)
    {
        _ = value;
        ScanArchiveCommand.NotifyCanExecuteChanged();
        ScanBatchCommand.NotifyCanExecuteChanged();
        ScanCurrentInputCommand.NotifyCanExecuteChanged();
        ConvertCommand.NotifyCanExecuteChanged();
        if (_loadingSettings)
        {
            return;
        }

        SaveSettings();
    }

    partial void OnOutputDirectoryChanged(string? value)
    {
        _ = value;
        RecomputeOutputZipPath();
        ConvertCommand.NotifyCanExecuteChanged();
        SaveSettings();
    }
    partial void OnFastSpecularChanged(bool value)
    {
        _ = value;
        RecomputeOutputZipPath();
        ConvertCommand.NotifyCanExecuteChanged();
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnNormalIntensityChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnHeightIntensityChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreviewBrickProbeDebugChanged(bool value)
    {
        _ = value;
        if (!_loadingSettings)
        {
            SaveSettings();
        }

        ScheduleRefreshPreviewIfActive();
    }

    partial void OnBrickHeightMapPostProcessEnabledChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnBrickHeightMinStructuralConfidenceChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnBrickHeightInvertDeltaThresholdChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnBrickLightGroutDiffuseDeltaMinChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnUseLegacyExtractorChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSmoothnessScaleChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMetallicBoostChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPorosityBiasChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPlantMaterialPorosityExtraChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularHeuristicBlendChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSelectedMlSpecularBlendModeOptionChanged(FoliageModeOption? value)
    {
        if (value is null)
        {
            return;
        }

        if (string.Equals(MlSpecularHeuristicBlendMode, value.Value, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MlSpecularHeuristicBlendMode = value.Value;
        if (!_loadingSettings)
        {
            SaveSettings();
            ScheduleRefreshPreviewIfActive();
        }
    }

    partial void OnSelectedMlSpecularBlendMathOptionChanged(FoliageModeOption? value)
    {
        if (value is null)
        {
            return;
        }

        if (string.Equals(MlSpecularBlendMath, value.Value, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MlSpecularBlendMath = value.Value;
        if (!_loadingSettings)
        {
            SaveSettings();
            ScheduleRefreshPreviewIfActive();
        }
    }

    partial void OnMaxThreadsChanged(int value)
    {
        _ = value;
        SaveSettings();
    }

    partial void OnTempDirectoryChanged(string? value)
    {
        _ = value;
        SaveSettings();
    }

    partial void OnDebugModeChanged(bool value)
    {
        _ = value;
        SaveSettings();
    }
    partial void OnColorSchemeChanged(string value)
    {
        _ = value;
        ApplyColorScheme();
        SaveSettings();
    }

    /// <summary>Maps legacy or out-of-range values onto allowed 5% steps between <see cref="MinUiScale"/> and <see cref="MaxUiScale"/>.</summary>
    public static double SnapUiScaleToAllowedStep(double value)
    {
        var v = Math.Clamp(value, MinUiScale, MaxUiScale);
        var snapped = Math.Round(v * 20.0, MidpointRounding.AwayFromZero) / 20.0;
        return Math.Clamp(snapped, MinUiScale, MaxUiScale);
    }

    private void RefreshUiScaleOptions()
    {
        UiScaleOptions.Clear();
        for (var p = 75; p <= 100; p += 5)
        {
            var scale = p / 100.0;
            UiScaleOptions.Add(new FoliageModeOption($"{p}%", scale.ToString("0.00", CultureInfo.InvariantCulture)));
        }
    }

    private void SyncSelectedUiScaleOptionToUiScale()
    {
        var target = UiScaleOptions.FirstOrDefault(o =>
                         Math.Abs(double.Parse(o.Value, CultureInfo.InvariantCulture) - UiScale) < 0.001)
                     ?? UiScaleOptions.LastOrDefault();
        if (target is null)
        {
            return;
        }

        if (string.Equals(SelectedUiScaleOption?.Value, target.Value, StringComparison.Ordinal))
        {
            return;
        }

        SelectedUiScaleOption = target;
    }

    partial void OnUiScaleChanged(double value)
    {
        var c = SnapUiScaleToAllowedStep(value);
        if (Math.Abs(c - value) > 1e-9)
        {
            UiScale = c;
            return;
        }

        if (_loadingSettings)
        {
            return;
        }

        SaveSettings();
    }

    partial void OnSelectedUiScaleOptionChanged(FoliageModeOption? value)
    {
        if (value is null || _loadingSettings)
        {
            return;
        }

        if (!double.TryParse(value.Value, CultureInfo.InvariantCulture, out var newScale))
        {
            return;
        }

        newScale = SnapUiScaleToAllowedStep(newScale);
        if (Math.Abs(newScale - UiScale) > 1e-9)
        {
            UiScale = newScale;
        }
    }

    partial void OnSelectedColorSchemeOptionChanged(FoliageModeOption? value)
    {
        if (value != null)
        {
            ColorScheme = value.Value;
        }
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedLanguage changes
    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        var code = value?.CultureCode ?? "en";
        ApplyCulture(code);
        _exploreTagFilterOptions = null;
        OnPropertyChanged(nameof(ExploreTagFilterOptions));
        _settings.Language = code;
        _settings.Save();
    }

    partial void OnProcessBlocksChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ApplyTextureTypeOverridesToExplore();
    }

    partial void OnProcessItemsChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ApplyTextureTypeOverridesToExplore();
    }

    partial void OnProcessArmorChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ApplyTextureTypeOverridesToExplore();
    }

    partial void OnProcessEntityChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ApplyTextureTypeOverridesToExplore();
    }

    partial void OnProcessParticlesChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ApplyTextureTypeOverridesToExplore();
    }

    partial void OnUseDeepBumpNormalsChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedDeepBumpOverlap changes
    partial void OnSelectedDeepBumpOverlapChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        DeepBumpOverlap = value?.Value ?? "Large";
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedDeepBumpInputMode changes
    partial void OnSelectedDeepBumpInputModeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }

        DeepBumpInputMode = value?.Value ?? nameof(AutoPBR.Core.Models.DeepBumpInputMode.Auto);
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedNormalOperator changes
    partial void OnSelectedNormalOperatorChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        NormalOperator = value?.Value ?? nameof(AutoPBR.Core.Models.NormalOperator.SobelVc);
        RefreshNormalKernelSizeOptions();
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSelectedNormalKernelSizeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        NormalKernelSize = value?.Value ?? "3";
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSelectedNormalDerivativeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        NormalDerivative = value?.Value ?? nameof(AutoPBR.Core.Models.NormalDerivative.Luminance);
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessLinearizeChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessDenoiseRadiusChanged(int value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessDenoiseBlendChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessFrequencySplitChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessFrequencyRadiusChanged(int value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessFrequencyDetailStrengthChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpForceBlue255Changed(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpNormalIntensityChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpNormalSoftClampChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpEdgeGuidedEnhanceChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpEdgeGuidedStrengthChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpEdgeGuidedGammaChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpEdgeGuidedDirectionMixChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnNormalHeightTransparentAlphaClampMaxChanged(int value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
        OnPropertyChanged(nameof(NormalHeightTransparentAlphaClampMaxSlider));
    }

    partial void OnSpecularUsePercentileRemapChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSpecularRemapLowPercentileChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSpecularRemapHighPercentileChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnUseMlSpecularPredictorChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreferOnnxTensorRtExecutionProviderChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPathChanged(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPath16Changed(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPath32Changed(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPath64Changed(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPath128Changed(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPath256Changed(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularUseEdgeChannelChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularTransparentAlphaClampMaxChanged(int value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
        OnPropertyChanged(nameof(MlSpecularTransparentAlphaClampMaxSlider));
    }

    partial void OnSpecularDebugDisableHeuristicSpecularChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSpecularDebugSkipSpecularRemapChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSpecularDebugVerboseSpecularMlChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnGenerateAoChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnAoRadiusChanged(int value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnAoStrengthChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }
    private void RecomputeOutputZipPath()
    {
        if (string.IsNullOrWhiteSpace(PackPath) || string.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputZipPath = null;
            return;
        }

        var baseName = Path.GetFileNameWithoutExtension(PackPath);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "pack";
        }

        // Always output a .zip PBR layer; same _PBR suffix for .zip and .jar (matches batch convert).
        OutputZipPath = Path.Combine(OutputDirectory, $"{baseName}_PBR.zip");
    }

    private void SaveSettings()
    {
        if (_loadingSettings)
        {
            return;
        }

        _settingsPersistence.RequestSave();
    }
    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedFoliageMode changes
    partial void OnSelectedFoliageModeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        FoliageMode = value?.Value ?? "Ignore All";
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }
}
