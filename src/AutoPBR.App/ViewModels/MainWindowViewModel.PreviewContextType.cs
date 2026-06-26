using System.Collections.ObjectModel;

using AutoPBR.Core.Preview;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    public ObservableCollection<EntityPreviewContextTypeOption> PreviewContextTypeOptions { get; } = [];

    [ObservableProperty] private string? _selectedPreviewContextTypeId;

    private bool _syncingPreviewContextTypeOptions;

    public bool IsPreviewContextTypeSelectorVisible => PreviewContextTypeOptions.Count > 1;

    partial void OnSelectedPreviewContextTypeIdChanged(string? value)
    {
        _ = value;
        if (_loadingSettings || _syncingPreviewContextTypeOptions)
        {
            return;
        }

        ScheduleRefreshPreviewIfActive(0);
    }

    private void RefreshPreviewContextTypeOptions(string? archivePath)
    {
        _syncingPreviewContextTypeOptions = true;
        try
        {
            RefreshPreviewContextTypeOptionsCore(archivePath);
        }
        finally
        {
            _syncingPreviewContextTypeOptions = false;
        }
    }

    private void RefreshPreviewContextTypeOptionsCore(string? archivePath)
    {
        PreviewContextTypeOptions.Clear();
        SelectedPreviewContextTypeId = null;

        if (string.IsNullOrWhiteSpace(archivePath))
        {
            OnPropertyChanged(nameof(IsPreviewContextTypeSelectorVisible));
            return;
        }

        var norm = archivePath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(norm);
        var builderMethod = EntityTextureParityCatalog.ResolveRule(norm, stem)?.BuilderMethod;
        if (!EntityPreviewContextTypeCatalog.TryGetContextTypeOptions(norm, builderMethod, out var options))
        {
            OnPropertyChanged(nameof(IsPreviewContextTypeSelectorVisible));
            return;
        }

        foreach (var option in options)
        {
            PreviewContextTypeOptions.Add(option);
        }

        SelectedPreviewContextTypeId = options.FirstOrDefault(o => o.IsDefault)?.Id ?? options[0].Id;
        OnPropertyChanged(nameof(IsPreviewContextTypeSelectorVisible));
    }
}
