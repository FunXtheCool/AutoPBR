using System.Collections.ObjectModel;

using AutoPBR.Core.Preview;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    public ObservableCollection<EntityPreviewPoseOption> PreviewPoseOptions { get; } = [];

    [ObservableProperty] private string? _selectedPreviewPoseId;

    private bool _syncingPreviewPoseOptions;

    public bool IsPreviewPoseSelectorVisible => PreviewPoseOptions.Count > 1;

    partial void OnPreviewArchivePathChanged(string? value)
    {
        RefreshPreviewPoseOptions(value);
    }

    partial void OnSelectedPreviewPoseIdChanged(string? value)
    {
        _ = value;
        if (_loadingSettings || _syncingPreviewPoseOptions)
        {
            return;
        }

        ScheduleRefreshPreviewIfActive(0);
    }

    private void RefreshPreviewPoseOptions(string? archivePath)
    {
        _syncingPreviewPoseOptions = true;
        try
        {
            RefreshPreviewPoseOptionsCore(archivePath);
        }
        finally
        {
            _syncingPreviewPoseOptions = false;
        }
    }

    private void RefreshPreviewPoseOptionsCore(string? archivePath)
    {
        PreviewPoseOptions.Clear();
        SelectedPreviewPoseId = null;

        if (string.IsNullOrWhiteSpace(archivePath))
        {
            OnPropertyChanged(nameof(IsPreviewPoseSelectorVisible));
            return;
        }

        var norm = archivePath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(norm);
        var builderMethod = EntityTextureParityCatalog.ResolveRule(norm, stem)?.BuilderMethod;
        if (!EntityPreviewPoseCatalog.TryGetPoseOptions(norm, builderMethod, out var options))
        {
            OnPropertyChanged(nameof(IsPreviewPoseSelectorVisible));
            return;
        }

        foreach (var option in options)
        {
            PreviewPoseOptions.Add(option);
        }

        SelectedPreviewPoseId = options.FirstOrDefault(o => o.IsDefault)?.Id ?? options[0].Id;
        OnPropertyChanged(nameof(IsPreviewPoseSelectorVisible));
    }
}
