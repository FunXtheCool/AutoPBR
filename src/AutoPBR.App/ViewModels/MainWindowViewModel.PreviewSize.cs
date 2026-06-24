using System.Collections.ObjectModel;

using AutoPBR.Core.Preview;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    public ObservableCollection<EntityPreviewSizeOption> PreviewSizeOptions { get; } = [];

    [ObservableProperty] private string? _selectedPreviewSizeId;

    private bool _syncingPreviewSizeOptions;

    public bool IsPreviewSizeSelectorVisible => PreviewSizeOptions.Count > 1;

    partial void OnSelectedPreviewSizeIdChanged(string? value)
    {
        _ = value;
        if (_loadingSettings || _syncingPreviewSizeOptions)
        {
            return;
        }

        ScheduleRefreshPreviewIfActive(0);
    }

    private void RefreshPreviewSizeOptions(string? archivePath)
    {
        _syncingPreviewSizeOptions = true;
        try
        {
            RefreshPreviewSizeOptionsCore(archivePath);
        }
        finally
        {
            _syncingPreviewSizeOptions = false;
        }
    }

    private void RefreshPreviewSizeOptionsCore(string? archivePath)
    {
        PreviewSizeOptions.Clear();
        SelectedPreviewSizeId = null;

        if (string.IsNullOrWhiteSpace(archivePath))
        {
            OnPropertyChanged(nameof(IsPreviewSizeSelectorVisible));
            return;
        }

        var norm = archivePath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(norm);
        var builderMethod = EntityTextureParityCatalog.ResolveRule(norm, stem)?.BuilderMethod;
        if (!EntityPreviewSizeCatalog.TryGetSizeOptions(norm, builderMethod, out var options))
        {
            OnPropertyChanged(nameof(IsPreviewSizeSelectorVisible));
            return;
        }

        foreach (var option in options)
        {
            PreviewSizeOptions.Add(option);
        }

        SelectedPreviewSizeId = options.FirstOrDefault(o => o.IsDefault)?.Id ?? options[0].Id;
        OnPropertyChanged(nameof(IsPreviewSizeSelectorVisible));
    }
}
