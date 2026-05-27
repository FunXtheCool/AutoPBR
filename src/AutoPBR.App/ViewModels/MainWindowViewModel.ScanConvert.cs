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
    private static string ResolveBackgroundTaskLabel(string taskId) => taskId switch
    {
        BackgroundTaskIds.MaterialTags => LocalizedStrings.BackgroundTaskMaterialTags,
        BackgroundTaskIds.ExploreCache => LocalizedStrings.BackgroundTaskExploreCache,
        _ => taskId
    };

    void IBackgroundTaskSink.BeginTask(string taskId) => BackgroundSinkBegin(taskId);

    void IBackgroundTaskSink.ReportTask(string taskId, double? fraction) => BackgroundSinkReport(taskId, fraction);

    void IBackgroundTaskSink.EndTask(string taskId) => BackgroundSinkEnd(taskId);

    private void BackgroundSinkBegin(string taskId)
    {
        void Core()
        {
            var existing = BackgroundTasks.FirstOrDefault(x => x.Id == taskId);
            if (existing is not null)
            {
                existing.Label = ResolveBackgroundTaskLabel(taskId);
                existing.IsIndeterminate = true;
                return;
            }

            BackgroundTasks.Add(new BackgroundTaskItem(taskId, ResolveBackgroundTaskLabel(taskId)));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    private void BackgroundSinkReport(string taskId, double? fraction)
    {
        void Core()
        {
            var item = BackgroundTasks.FirstOrDefault(x => x.Id == taskId);
            if (item is null)
            {
                return;
            }

            if (fraction is { } f)
            {
                item.IsIndeterminate = false;
                item.Progress = Math.Clamp(f, 0, 1);
            }
            else
            {
                item.IsIndeterminate = true;
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    private void BackgroundSinkEnd(string taskId)
    {
        void Core()
        {
            var item = BackgroundTasks.FirstOrDefault(x => x.Id == taskId);
            if (item is null)
            {
                return;
            }

            BackgroundTasks.Remove(item);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }
    private static bool IsPackPath(string? path) =>
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
            ((IBackgroundTaskSink)this).BeginTask(BackgroundTaskIds.ExploreCache);
            try
            {
                await Task.Run(() => _exploreController.PrewarmFolderVisibilityCache(scanToken), scanToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ((IBackgroundTaskSink)this).EndTask(BackgroundTaskIds.ExploreCache);
            }
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
            ((IBackgroundTaskSink)this).BeginTask(BackgroundTaskIds.ExploreCache);
            try
            {
                await Task.Run(() => _exploreController.PrewarmFolderVisibilityCache(scanToken), scanToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ((IBackgroundTaskSink)this).EndTask(BackgroundTaskIds.ExploreCache);
            }
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
                var scanData = await Task.Run(() => PackScannerService.BuildArchiveIndex(PackPath!, scanProg));
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

    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsConverting;

    /// <summary>
    /// Per-resolution paths: explicit text box overrides bundled <c>Data/ONNX-AI/SpecLab</c> models when empty.
    /// </summary>
    private Dictionary<int, string>? BuildMergedSpecularResolutionMap()
    {
        var d = new Dictionary<int, string>();
        var baseDir = AppContext.BaseDirectory;
        void Put(int res, string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                d[res] = explicitPath.Trim();
                return;
            }

            var bundled = MlSpecularBundledModelPaths.TryResolveExistingBundledPath(res, baseDir);
            if (bundled is not null)
            {
                d[res] = bundled;
            }
        }

        Put(16, MlSpecularModelPath16);
        Put(32, MlSpecularModelPath32);
        Put(64, MlSpecularModelPath64);
        Put(128, MlSpecularModelPath128);
        Put(256, MlSpecularModelPath256);
        return d.Count > 0 ? d : null;
    }

    /// <summary>Build converter options from current VM state and scan data.</summary>
    private AutoPbrOptions BuildConversionOptions(
        HashSet<string> ignore,
        IReadOnlyList<string>? entriesToExtractOnly,
        IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>? manualTagOverrides = null,
        bool brickProbePreviewDebug = false)
    {
        var model = new ConversionSettingsModel
        {
            NormalIntensity = NormalIntensity,
            HeightIntensity = HeightIntensity,
            BrickHeightMapPostProcessEnabled = BrickHeightMapPostProcessEnabled,
            BrickHeightMinStructuralConfidence = BrickHeightMinStructuralConfidence,
            BrickHeightInvertDeltaThreshold = BrickHeightInvertDeltaThreshold,
            BrickLightGroutDiffuseDeltaMin = BrickLightGroutDiffuseDeltaMin,
            BrickProbePreviewDebug = brickProbePreviewDebug,
            FastSpecular = FastSpecular,
            UseLegacyExtractor = UseLegacyExtractor,
            SmoothnessScale = SmoothnessScale,
            MetallicBoost = MetallicBoost,
            PorosityBias = PorosityBias,
            PlantMaterialPorosityExtra = PlantMaterialPorosityExtra,
            SpecularUsePercentileRemap = SpecularUsePercentileRemap,
            SpecularRemapLowPercentile = SpecularRemapLowPercentile,
            SpecularRemapHighPercentile = SpecularRemapHighPercentile,
            SpecularForceNoEmissive = SpecularForceNoEmissive,
            UseMlSpecularPredictor = UseMlSpecularPredictor,
            MlSpecularModelPath = MlSpecularModelPath,
            MlSpecularModelPathsByResolution = BuildMergedSpecularResolutionMap(),
            MlSpecularHeuristicBlend = MlSpecularHeuristicBlend,
            MlSpecularHeuristicBlendMode = MlSpecularHeuristicBlendMode,
            MlSpecularBlendMath = MlSpecularBlendMath,
            MlSpecularUseEdgeChannel = MlSpecularUseEdgeChannel,
            MlSpecularTransparentAlphaClampMax = MlSpecularTransparentAlphaClampMax,
            SpecularDebugDisableHeuristicSpecular = SpecularDebugDisableHeuristicSpecular,
            SpecularDebugSkipSpecularRemap = SpecularDebugSkipSpecularRemap,
            SpecularDebugVerboseSpecularMl = SpecularDebugVerboseSpecularMl,
            MaxThreads = MaxThreads,
            TempDirectory = TempDirectory,
            ProcessBlocks = ProcessBlocks,
            ProcessItems = ProcessItems,
            ProcessArmor = ProcessArmor,
            ProcessEntity = ProcessEntity,
            ProcessParticles = ProcessParticles,
            GenerateAo = GenerateAo,
            AoRadius = AoRadius,
            AoStrength = AoStrength,
            FoliageMode = FoliageMode,
            UseDeepBumpNormals = UseDeepBumpNormals,
            DeepBumpOverlap = DeepBumpOverlap,
            DeepBumpInputMode = DeepBumpInputMode,
            DeepBumpForceBlue255 = DeepBumpForceBlue255,
            DeepBumpNormalIntensity = DeepBumpNormalIntensity,
            DeepBumpNormalSoftClamp = DeepBumpNormalSoftClamp,
            DeepBumpEdgeGuidedEnhance = DeepBumpEdgeGuidedEnhance,
            DeepBumpEdgeGuidedStrength = DeepBumpEdgeGuidedStrength,
            DeepBumpEdgeGuidedGamma = DeepBumpEdgeGuidedGamma,
            DeepBumpEdgeGuidedDirectionMix = DeepBumpEdgeGuidedDirectionMix,
            NormalHeightTransparentAlphaClampMax = NormalHeightTransparentAlphaClampMax,
            NormalOperator = NormalOperator,
            NormalKernelSize = NormalKernelSize,
            NormalDerivative = NormalDerivative,
            PreprocessLinearize = PreprocessLinearize,
            PreprocessDenoiseRadius = PreprocessDenoiseRadius,
            PreprocessDenoiseBlend = PreprocessDenoiseBlend,
            PreprocessFrequencySplit = PreprocessFrequencySplit,
            PreprocessFrequencyRadius = PreprocessFrequencyRadius,
            PreprocessFrequencyDetailStrength = PreprocessFrequencyDetailStrength,
            PreferOnnxTensorRtExecutionProvider = PreferOnnxTensorRtExecutionProvider
        };
        return model.ToAutoPbrOptions(_specularData, ignore, entriesToExtractOnly,
            manualTagOverrides ?? _exploreController.GetManualTagOverrides(), GetEffectiveTagRules(),
            BuildMaterialTagSemanticOptions());
    }

}
