using Avalonia.Threading;

using CommunityToolkit.Mvvm.Input;

using AutoPBR.App.Lang;
using AutoPBR.App.Models;
using AutoPBR.App.Services;
using AutoPBR.Core;
using AutoPBR.Core.Models;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    private bool CanBatchConvert() =>
        !IsConverting &&
        !IsBusy &&
        !string.IsNullOrWhiteSpace(BatchFolderPath) &&
        Directory.Exists(BatchFolderPath) &&
        !string.IsNullOrWhiteSpace(OutputDirectory) &&
        _exploreController.HaveBatchScanForFolder(BatchFolderPath) &&
        _exploreController.Data?.BatchPackRootToPath is { Count: > 0 };

    private async Task WaitForPendingTagWorkWithProgressAsync(CancellationToken token)
    {
        var initialPending = _exploreController.GetPendingTagWorkCount();
        if (initialPending <= 0)
        {
            await _exploreController.WaitForPendingTagWorkAsync(cancellationToken: token).ConfigureAwait(false);
            return;
        }

        var progressTotal = Math.Max(1, initialPending);
        void UpdatePending(int pending)
        {
            var remaining = Math.Clamp(pending, 0, progressTotal);
            var completed = progressTotal - remaining;
            Dispatcher.UIThread.Post(() =>
            {
                ProgressMax = progressTotal;
                ProgressValue = completed;
                SetStatus("Status_TagWorkDrainProgress", completed, progressTotal);
            });
        }

        UpdatePending(initialPending);
        await _exploreController.WaitForPendingTagWorkAsync(UpdatePending, token).ConfigureAwait(false);
        Dispatcher.UIThread.Post(() =>
        {
            ProgressMax = 1;
            ProgressValue = 0;
        });
    }

    private async Task ExecuteBatchConvertAsync()
    {
        if (!CanBatchConvert())
        {
            return;
        }

        var packs = _exploreController.Data!.BatchPackRootToPath!;
        IsConverting = true;
        IsBusy = true;
        ProgressValue = 0;
        ProgressMax = 1;
        _lastLogWriteUtc = DateTime.MinValue;
        _conversionStartUtc = DateTime.UtcNow;
        _currentStage = null;
        ConversionElapsedText = "Elapsed: 00:00";
        ConversionEtaText = "ETA: --:--";
        ConversionTotalText = "";
        CancelCommand.NotifyCanExecuteChanged();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await FlushPendingSettingsSaveAsync().ConfigureAwait(false);
            SetStatus("Status_LoadingSpecularData");
            _specularData ??=
                SpecularData.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json"));

            using var prog = CreateConversionProgressReporter();
            await WaitForPendingTagWorkWithProgressAsync(token).ConfigureAwait(false);
            _exploreController.FlushTagOverridesToDisk();
            var packIndex = 0;
            foreach (var kv in packs)
            {
                packIndex++;
                var packRoot = kv.Key;
                string diskPackPath = kv.Value;
                token.ThrowIfCancellationRequested();

                var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                _exploreController.ApplyExploreOverridesToIgnoreSetForBatchPack(ignore, packRoot);
                var innerPaths = _exploreController.GetFilePathsUnderBatchPackRoot(packRoot);
                var entries = innerPaths.ToList();
                entries.Add("pack.mcmeta");
                var manual = _exploreController.GetManualTagOverridesForBatchPackRoot(packRoot);
                var options = BuildConversionOptions(ignore, entries, manual);
                var baseName = Path.GetFileNameWithoutExtension(diskPackPath);
                var outZip = Path.Combine(OutputDirectory!, baseName + "_PBR.zip");

                AddLogLine(Resources.GetStatusString("Log_BatchConvertingPack", packIndex, packs.Count, diskPackPath, outZip));
                await ConversionCoordinator.ConvertAsync(diskPackPath, outZip, options, prog, token);
            }

            AddLogLine(Resources.GetString("Log_Done"));
        }
        catch (OperationCanceledException)
        {
            RunOnUiThread(() =>
            {
                SetStatus("Status_Cancelled");
                AddLogLine(Resources.GetString("Log_Cancelled"));
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                SetStatus("Status_ConversionFailed");
                AddLogLine(ex.ToString());
            });
        }
        finally
        {
            SaveLogToFile();
            RunOnUiThread(() =>
            {
                if (string.IsNullOrWhiteSpace(ConversionTotalText))
                {
                    var total = DateTime.UtcNow - _conversionStartUtc;
                    if (total < TimeSpan.Zero)
                    {
                        total = TimeSpan.Zero;
                    }

                    ConversionTotalText = $"Total: {FormatDuration(total)}";
                }
            });
            _cts?.Dispose();
            _cts = null;
            RunOnUiThread(() =>
            {
                IsConverting = false;
                IsBusy = false;
                CancelCommand.NotifyCanExecuteChanged();
                ConvertCommand.NotifyCanExecuteChanged();
                ScanCurrentInputCommand.NotifyCanExecuteChanged();
            });
        }
    }
    [RelayCommand(CanExecute = nameof(CanConvert))]
    public async Task ConvertAsync()
    {
        if (UseBatchFolderInput)
        {
            await ExecuteBatchConvertAsync().ConfigureAwait(false);
            return;
        }

        if (!IsPackPath(PackPath) || !File.Exists(PackPath))
        {
            return;
        }


        if (string.IsNullOrWhiteSpace(OutputZipPath))
        {
            return;
        }


        IsConverting = true;
        IsBusy = true;
        ProgressValue = 0;
        ProgressMax = 1;
        _lastLogWriteUtc = DateTime.MinValue;
        _conversionStartUtc = DateTime.UtcNow;
        _currentStage = null;
        ConversionElapsedText = "Elapsed: 00:00";
        ConversionEtaText = "ETA: --:--";
        ConversionTotalText = "";
        CancelCommand.NotifyCanExecuteChanged();

        _cts = new CancellationTokenSource();

        try
        {
            SetStatus("Status_LoadingSpecularData");
            _specularData ??=
                SpecularData.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json"));

            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            _exploreController.ApplyExploreOverridesToIgnoreSet(ignore);

            if (!HaveScanForCurrentPack())
            {
                AddLogLine(Resources.GetString("Log_ScanningPackForExtraction"));
                var scanProg = CreateScanProgressReporter(p =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        ProgressMax = Math.Max(1, p.total);
                        ProgressValue = p.completed;
                        SetStatus("Status_ScanningPackProgress", p.completed, p.total);
                    }));
                var scanData = await Task.Run(
                    () => PackScannerService.BuildArchiveIndex(PackPath!, scanProg, _cts.Token),
                    _cts.Token);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _exploreController.SetData(scanData, PackPath!);
                    ScannedArchiveRoot = _exploreController.Root;
                });
                AddLogLine(Resources.GetStatusString("Log_IndexedPngEntries", scanData.FileCount));
            }

            IReadOnlyList<string>? entriesToExtractOnly = null;
            if (_exploreController.Data is not null)
            {
                var list = _exploreController.Data.EnumerateAllFilePaths().ToList();
                list.Add("pack.mcmeta");
                entriesToExtractOnly = list;
                AddLogLine(Resources.GetString("Log_ExtractingOnlyPng"));
            }

            await WaitForPendingTagWorkWithProgressAsync(_cts.Token).ConfigureAwait(false);
            _exploreController.FlushTagOverridesToDisk();

            var options = BuildConversionOptions(ignore, entriesToExtractOnly);
            using var prog = CreateConversionProgressReporter();

            AddLogLine(Resources.GetStatusString("Log_Converting", OutputZipPath ?? ""));
            await ConversionCoordinator.ConvertAsync(PackPath!, OutputZipPath!, options, prog, _cts.Token);
            AddLogLine(Resources.GetString("Log_Done"));
        }
        catch (OperationCanceledException)
        {
            RunOnUiThread(() =>
            {
                SetStatus("Status_Cancelled");
                AddLogLine(Resources.GetString("Log_Cancelled"));
                var totalSec = (DateTime.UtcNow - _conversionStartUtc).TotalSeconds;
                AddLogLine(Resources.GetStatusString("Log_TotalTime", totalSec));
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                SetStatus("Status_ConversionFailed");
                AddLogLine(ex.ToString());
                var totalSec = (DateTime.UtcNow - _conversionStartUtc).TotalSeconds;
                AddLogLine(Resources.GetStatusString("Log_TotalTime", totalSec));
            });
        }
        finally
        {
            // Persist the log for this conversion run.
            SaveLogToFile();
            RunOnUiThread(() =>
            {
                if (string.IsNullOrWhiteSpace(ConversionTotalText))
                {
                    var total = DateTime.UtcNow - _conversionStartUtc;
                    if (total < TimeSpan.Zero)
                    {
                        total = TimeSpan.Zero;
                    }

                    ConversionTotalText = $"Total: {FormatDuration(total)}";
                }
            });

            _cts?.Dispose();
            _cts = null;
            RunOnUiThread(() =>
            {
                IsConverting = false;
                IsBusy = false;
                ClearScannedArchive();
                ConvertCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
                ScanArchiveCommand.NotifyCanExecuteChanged();
                ScanCurrentInputCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private bool CanSinglePackConvert() =>
        !IsBatchScanActive &&
        IsPackPath(PackPath) &&
        File.Exists(PackPath) &&
        !string.IsNullOrWhiteSpace(OutputZipPath);

    private bool CanConvert() =>
        !IsConverting &&
        !IsBusy &&
        (UseBatchFolderInput ? CanBatchConvert() : CanSinglePackConvert());


}
