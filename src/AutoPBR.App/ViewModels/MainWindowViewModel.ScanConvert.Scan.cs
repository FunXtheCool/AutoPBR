using Avalonia.Threading;

using CommunityToolkit.Mvvm.Input;

using AutoPBR.App.Lang;
using AutoPBR.App.Models;
using AutoPBR.App.Services;
using AutoPBR.Core;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{    private static bool IsPackPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));

    private bool CanScanArchive() => !IsConverting && !IsBusy && IsPackPath(PackPath) && File.Exists(PackPath);
    [RelayCommand(CanExecute = nameof(CanScanArchive))]
    public async Task ScanArchiveAsync()
    {
        if (!IsPackPath(PackPath) || !File.Exists(PackPath))
        {
            return;
        }


        IsBusy = true;
        ScanArchiveCommand.NotifyCanExecuteChanged();
        ProgressValue = 0;
        ProgressMax = 1;
        SetStatus("Status_ScanningTexturesInPack");
        AddLogLine(Resources.GetStatusString("Log_ScanningArchive", PackPath ?? ""));

        if (_scanCts is { } oldScanCts)
        {
            await oldScanCts.CancelAsync().ConfigureAwait(false);
            oldScanCts.Dispose();
        }

        _scanCts = new CancellationTokenSource();
        var scanToken = _scanCts.Token;

        var scanProgress = CreateScanProgressReporter(p =>
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressMax = Math.Max(1, p.total);
                ProgressValue = p.completed;
                SetStatus("Status_ScanningPackProgress", p.completed, p.total);
            });
        });
        try
        {
            var data = await Task.Run(() => PackScannerService.BuildArchiveIndex(PackPath!, scanProgress), scanToken)
                .ConfigureAwait(false);
            scanToken.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ScannedArchiveRoot = _exploreController.SetData(data, PackPath!);
                ApplyTextureTypeOverridesToExplore();
                FocusedArchiveNode ??= ExploreTreeController.FindChildByName(ScannedArchiveRoot!, "assets");
                PreloadExpandersForCurrentView();
                SetStatus("Status_LoadedTextures", data.FileCount);
                AddLogLine(Resources.GetStatusString("Log_ArchiveContentsLoaded", data.FileCount));
            });
            await Task.Run(() => _exploreController.PrewarmFolderVisibilityCache(scanToken), scanToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // User cleared archive or started new scan; no error message
        }
        catch (Exception ex)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetStatus("Status_FailedToScan");
                AddLogLine(ex.ToString());
            });
        }
        finally
        {
            // ReSharper disable once MethodHasAsyncOverload
            _scanCts?.Dispose();
            _scanCts = null;
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
                ScanArchiveCommand.NotifyCanExecuteChanged();
                ScanBatchCommand.NotifyCanExecuteChanged();
                ScanCurrentInputCommand.NotifyCanExecuteChanged();
                ConvertCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private bool CanScanCurrentInput() =>
        UseBatchFolderInput ? CanScanBatch() : CanScanArchive();

    [RelayCommand(CanExecute = nameof(CanScanCurrentInput))]
    public async Task ScanCurrentInputAsync()
    {
        if (UseBatchFolderInput)
        {
            await ScanBatchAsync().ConfigureAwait(false);
        }
        else
        {
            await ScanArchiveAsync().ConfigureAwait(false);
        }
    }

    private bool CanScanBatch() =>
        !IsConverting &&
        !IsBusy &&
        !string.IsNullOrWhiteSpace(BatchFolderPath) &&
        Directory.Exists(BatchFolderPath);

    [RelayCommand(CanExecute = nameof(CanScanBatch))]
    public async Task ScanBatchAsync()
    {
        if (string.IsNullOrWhiteSpace(BatchFolderPath) || !Directory.Exists(BatchFolderPath))
        {
            return;
        }

        IsBusy = true;
        ScanBatchCommand.NotifyCanExecuteChanged();
        ScanArchiveCommand.NotifyCanExecuteChanged();
        ProgressValue = 0;
        ProgressMax = 1;
        SetStatus("Status_ScanningBatchFolder");
        AddLogLine(Resources.GetStatusString("Log_ScanningBatchFolder", BatchFolderPath));

        if (_scanCts is { } oldBatchScan)
        {
            await oldBatchScan.CancelAsync().ConfigureAwait(false);
            oldBatchScan.Dispose();
        }

        _scanCts = new CancellationTokenSource();
        var scanToken = _scanCts.Token;

        var scanProgress = CreateScanProgressReporter(p =>
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressMax = Math.Max(1, p.total);
                ProgressValue = p.completed;
                SetStatus("Status_ScanningPackProgress", p.completed, p.total);
            });
        });
        try
        {
            var data = await Task.Run(() => PackScannerService.BuildBatchArchiveIndex(BatchFolderPath!, scanProgress), scanToken)
                .ConfigureAwait(false);
            scanToken.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ScannedArchiveRoot = _exploreController.SetData(data, Path.GetFullPath(BatchFolderPath!));
                ApplyTextureTypeOverridesToExplore();
                FocusedArchiveNode = null;
                PreloadExpandersForCurrentView();
                var packCount = data.BatchPackRootToPath?.Count ?? 0;
                SetStatus("Status_BatchScanDone", packCount, data.FileCount);
                AddLogLine(Resources.GetStatusString("Log_BatchScanDone", packCount, data.FileCount));
            });
            await Task.Run(() => _exploreController.PrewarmFolderVisibilityCache(scanToken), scanToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // cancelled
        }
        catch (Exception ex)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetStatus("Status_FailedToScan");
                AddLogLine(ex.ToString());
            });
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
                ScanBatchCommand.NotifyCanExecuteChanged();
                ScanArchiveCommand.NotifyCanExecuteChanged();
                ScanCurrentInputCommand.NotifyCanExecuteChanged();
                ConvertCommand.NotifyCanExecuteChanged();
            });
        }
    }

}
