using AutoPBR.App.Lang;
using AutoPBR.App.Rendering.OpenGL;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    private readonly bool _appliedPreviewUseOpenGl4 = PreviewOpenGlSession.RequestedDesktopGl4;

    [ObservableProperty] private bool _previewUseOpenGl4;

    [ObservableProperty] private string _previewOpenGlActiveContextText = string.Empty;

    public bool PreviewOpenGlRestartRequired => PreviewUseOpenGl4 != _appliedPreviewUseOpenGl4;

    public bool PreviewOpenGlRestartHintVisible => PreviewOpenGlRestartRequired;

    partial void OnPreviewUseOpenGl4Changed(bool value)
    {
        _ = value;
        OnPropertyChanged(nameof(PreviewOpenGlRestartRequired));
        OnPropertyChanged(nameof(PreviewOpenGlRestartHintVisible));
        if (_loadingSettings)
        {
            return;
        }

        SaveSettings();
    }

    internal void UpdatePreviewOpenGlActiveContextText(string? contextSummary)
    {
        if (string.IsNullOrWhiteSpace(contextSummary))
        {
            PreviewOpenGlActiveContextText = string.Empty;
            return;
        }

        PreviewOpenGlActiveContextText = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            LocalizedStrings.PreviewOpenGlActiveContext,
            contextSummary);
    }
}
