using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{    partial void OnBatchFolderPathChanged(string? value)
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

    partial void OnMinecraftAssetsDirectoryChanged(string? value)
    {
        _ = value;
        SaveSettings();
        if (_loadingSettings || !IsPreview3D || string.IsNullOrWhiteSpace(PreviewArchivePath))
        {
            return;
        }

        RefreshPreviewGrassColormapState();
        Push3DPreviewStateToGpu();
    }

    partial void OnDebugModeChanged(bool value)
    {
        _ = value;
        SaveSettings();
    }

}
