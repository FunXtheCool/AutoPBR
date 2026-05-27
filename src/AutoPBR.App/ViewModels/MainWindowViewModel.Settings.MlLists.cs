using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using AutoPBR.App.Lang;
using AutoPBR.App.Models;
using AutoPBR.App.Services;
using AutoPBR.Core.Models;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{    private void RefreshMlSpecularBlendModeOptions()
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

}
