using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using JetBrains.Annotations;

using AutoPBR.App.Models;
using AutoPBR.App.Services;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{    partial void OnColorSchemeChanged(string value)
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

}
