using CommunityToolkit.Mvvm.ComponentModel;

using JetBrains.Annotations;

using AutoPBR.App.Models;
using AutoPBR.Core.Models;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{    partial void OnFastSpecularChanged(bool value)
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

        DeepBumpInputMode = value?.Value ?? nameof(AutoPBR.Contracts.Ml.DeepBumpInputMode.Auto);
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
