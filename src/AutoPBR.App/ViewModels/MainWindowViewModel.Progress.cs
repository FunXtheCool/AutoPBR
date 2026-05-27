using System.Globalization;

using Avalonia.Threading;

using AutoPBR.App.Lang;
using AutoPBR.Core.Models;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>Progress reporter that coalesces high-frequency updates before they hit the UI thread.</summary>
    private CoalescingConversionProgressReporter CreateConversionProgressReporter() =>
        new(OnConversionProgress, ConversionUiProgressUpdateInterval);

    private static CoalescingProgressReporter<(int completed, int total)> CreateScanProgressReporter(
        Action<(int completed, int total)> sink) =>
        new(
            sink,
            ScanUiProgressUpdateInterval,
            p => p.completed >= p.total);

    private void OnConversionProgress(ConversionProgress p)
    {
        void ApplyProgress()
        {
            var now = DateTime.UtcNow;
            if (_currentStage.HasValue && _currentStage.Value != p.Stage)
            {
                var elapsed = (now - _stageStartUtc).TotalSeconds;
                var stageName = GetStageDisplayName(_currentStage.Value);
                AddLogLine(Resources.GetStatusString("Log_StageCompleted", stageName, elapsed));
            }

            if (p.Stage != ConversionStage.Done)
            {
                _currentStage = p.Stage;
                _stageStartUtc = now;
            }
            else
            {
                var totalSec = (now - _conversionStartUtc).TotalSeconds;
                AddLogLine(Resources.GetStatusString("Log_TotalTime", totalSec));
            }

            ProgressMax = Math.Max(1, p.Total);
            ProgressValue = p.Completed;
            UpdateConversionTiming(now, p);
            if (!string.IsNullOrEmpty(p.InfoMessage))
            {
                AddLogLine(p.InfoMessage);
            }

            if (p.Stage == ConversionStage.ScanningTextures)
            {
                var shouldUpdateScanDebug =
                    !string.IsNullOrWhiteSpace(p.InfoMessage) ||
                    now - _lastScanDebugUpdateUtc >= ScanDebugUpdateInterval ||
                    p.Completed == p.Total;
                if (shouldUpdateScanDebug)
                {
                    ConversionScanDebugText = p.Total > 0
                        ? $"Scanning debug: {p.Completed}/{p.Total} {(string.IsNullOrWhiteSpace(p.CurrentTextureName) ? "" : p.CurrentTextureName)}"
                        : "Scanning debug: phase started";
                    if (!string.IsNullOrWhiteSpace(p.InfoMessage))
                    {
                        ConversionScanDebugText = $"{ConversionScanDebugText} | {p.InfoMessage}";
                    }

                    _lastScanDebugUpdateUtc = now;
                }
            }

            if (p.Stage == ConversionStage.Extracting && p is { Completed: 0, Total: > 0 })
            {
                AddLogLine(Resources.GetString("Log_Extracting"));
            }

            if (p.Stage == ConversionStage.Packing && p is { Completed: 0, Total: > 0 })
            {
                AddLogLine(Resources.GetString("Log_Packing"));
            }

            if (!string.IsNullOrEmpty(p.CurrentTextureName))
            {
                if ((now - _lastLogWriteUtc).TotalMilliseconds >= LogWriteIntervalMs)
                {
                    _lastLogWriteUtc = now;
                    var stageLabel = GetStageDisplayName(p.Stage);
                    AddLogLine(Resources.GetStatusString("Log_StageCurrent", stageLabel, p.CurrentTextureName));
                }
            }

            (_statusKey, _statusFormatArgs) = p.Stage switch
            {
                ConversionStage.Extracting => ("Status_ExtractingPack", null),
                ConversionStage.ScanningTextures => ("Status_ScanningTextures", null),
                ConversionStage.GeneratingSpecular => ("Status_StageGeneratingSpecular", null),
                ConversionStage.GeneratingNormals => ("Status_StageGeneratingNormals", null),
                ConversionStage.Packing => ("Status_PackingOutput", null),
                ConversionStage.Done => ("Status_Done", null),
                _ => (_statusKey, _statusFormatArgs)
            };
            UpdateStatusText();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyProgress();
        }
        else
        {
            Dispatcher.UIThread.Post(ApplyProgress);
        }
    }

    private void UpdateConversionTiming(DateTime now, ConversionProgress p)
    {
        var elapsed = now - _conversionStartUtc;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        ConversionElapsedText = $"Elapsed: {FormatDuration(elapsed)}";
        if (p.Stage == ConversionStage.Done)
        {
            ConversionEtaText = "ETA: 00:00";
            ConversionTotalText = $"Total: {FormatDuration(elapsed)}";
            return;
        }

        ConversionTotalText = "";
        if (p.Total > 0 && p.Completed > 0 && p.Completed < p.Total)
        {
            var remaining = p.Total - p.Completed;
            var etaSeconds = elapsed.TotalSeconds * (remaining / (double)p.Completed);
            etaSeconds = Math.Max(0, Math.Min(etaSeconds, 365 * 24 * 3600));
            ConversionEtaText = $"ETA: {FormatDuration(TimeSpan.FromSeconds(etaSeconds))}";
        }
        else
        {
            ConversionEtaText = "ETA: --:--";
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);
        }

        return duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private static string GetStageDisplayName(ConversionStage stage)
    {
        var key = stage switch
        {
            ConversionStage.Extracting => "Log_StageExtracting",
            ConversionStage.ScanningTextures => "Log_StageScanning",
            ConversionStage.GeneratingSpecular => "Log_StageSpecular",
            ConversionStage.GeneratingNormals => "Log_StageNormals",
            ConversionStage.Packing => "Log_StagePacking",
            _ => null
        };
        return key != null ? Resources.GetString(key) : stage.ToString();
    }

    private sealed class CoalescingConversionProgressReporter : IProgress<ConversionProgress>, IDisposable
    {
        private readonly Action<ConversionProgress> _sink;
        private readonly TimeSpan _minInterval;
        private readonly object _gate = new();
        private ConversionProgress? _pending;
        private DateTime _lastDispatchUtc = DateTime.MinValue;
        private ConversionStage? _lastDispatchedStage;
        private bool _drainScheduled;
        private bool _disposed;

        public CoalescingConversionProgressReporter(Action<ConversionProgress> sink, TimeSpan minInterval)
        {
            _sink = sink;
            _minInterval = minInterval;
        }

        public void Report(ConversionProgress value)
        {
            ConversionProgress? dispatchNow = null;
            TimeSpan delay = default;
            var scheduleDrain = false;

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                var forceImmediate = value.Stage == ConversionStage.Done ||
                                     (_lastDispatchedStage.HasValue && value.Stage != _lastDispatchedStage.Value);
                var elapsed = _lastDispatchUtc == DateTime.MinValue
                    ? _minInterval
                    : now - _lastDispatchUtc;
                if (forceImmediate || elapsed >= _minInterval)
                {
                    dispatchNow = value;
                    _lastDispatchUtc = now;
                    _lastDispatchedStage = value.Stage;
                    _pending = null;
                }
                else
                {
                    _pending = value;
                    if (!_drainScheduled)
                    {
                        _drainScheduled = true;
                        delay = _minInterval - elapsed;
                        scheduleDrain = true;
                    }
                }
            }

            if (dispatchNow is not null)
            {
                _sink(dispatchNow);
            }

            if (scheduleDrain)
            {
                ScheduleDrain(delay);
            }
        }

        private void ScheduleDrain(TimeSpan delay)
        {
            _ = Task.Delay(delay).ContinueWith(_ => DrainPending(), TaskScheduler.Default);
        }

        private void DrainPending()
        {
            ConversionProgress? dispatch = null;
            TimeSpan delay = default;
            var scheduleAgain = false;

            lock (_gate)
            {
                _drainScheduled = false;
                if (_disposed || _pending is null)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                var elapsed = _lastDispatchUtc == DateTime.MinValue
                    ? _minInterval
                    : now - _lastDispatchUtc;
                if (elapsed < _minInterval)
                {
                    _drainScheduled = true;
                    delay = _minInterval - elapsed;
                    scheduleAgain = true;
                }
                else
                {
                    dispatch = _pending;
                    _pending = null;
                    _lastDispatchUtc = now;
                    _lastDispatchedStage = dispatch.Stage;
                }
            }

            if (dispatch is not null)
            {
                _sink(dispatch);
            }

            if (scheduleAgain)
            {
                ScheduleDrain(delay);
            }
        }

        public void Dispose()
        {
            ConversionProgress? lastPending;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                lastPending = _pending;
                _pending = null;
            }

            if (lastPending is not null)
            {
                _sink(lastPending);
            }
        }
    }

    private sealed class CoalescingProgressReporter<T> : IProgress<T>
    {
        private readonly Action<T> _sink;
        private readonly TimeSpan _minInterval;
        private readonly Func<T, bool> _shouldFlushImmediately;
        private readonly object _gate = new();
        private T _pending = default!;
        private bool _hasPending;
        private DateTime _lastDispatchUtc = DateTime.MinValue;
        private bool _drainScheduled;

        public CoalescingProgressReporter(Action<T> sink, TimeSpan minInterval, Func<T, bool>? shouldFlushImmediately = null)
        {
            _sink = sink;
            _minInterval = minInterval;
            _shouldFlushImmediately = shouldFlushImmediately ?? (_ => false);
        }

        public void Report(T value)
        {
            T dispatchNow = default!;
            var hasDispatchNow = false;
            TimeSpan delay = default;
            var scheduleDrain = false;

            lock (_gate)
            {
                var now = DateTime.UtcNow;
                var forceImmediate = _shouldFlushImmediately(value);
                var elapsed = _lastDispatchUtc == DateTime.MinValue
                    ? _minInterval
                    : now - _lastDispatchUtc;
                if (forceImmediate || elapsed >= _minInterval)
                {
                    dispatchNow = value;
                    hasDispatchNow = true;
                    _lastDispatchUtc = now;
                    _hasPending = false;
                }
                else
                {
                    _pending = value;
                    _hasPending = true;
                    if (!_drainScheduled)
                    {
                        _drainScheduled = true;
                        delay = _minInterval - elapsed;
                        scheduleDrain = true;
                    }
                }
            }

            if (hasDispatchNow)
            {
                _sink(dispatchNow);
            }

            if (scheduleDrain)
            {
                ScheduleDrain(delay);
            }
        }

        private void ScheduleDrain(TimeSpan delay)
        {
            _ = Task.Delay(delay).ContinueWith(_ => DrainPending(), TaskScheduler.Default);
        }

        private void DrainPending()
        {
            T dispatch = default!;
            var hasDispatch = false;
            TimeSpan delay = default;
            var scheduleAgain = false;

            lock (_gate)
            {
                _drainScheduled = false;
                if (!_hasPending)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                var elapsed = _lastDispatchUtc == DateTime.MinValue
                    ? _minInterval
                    : now - _lastDispatchUtc;
                if (elapsed < _minInterval)
                {
                    _drainScheduled = true;
                    delay = _minInterval - elapsed;
                    scheduleAgain = true;
                }
                else
                {
                    dispatch = _pending;
                    _hasPending = false;
                    hasDispatch = true;
                    _lastDispatchUtc = now;
                }
            }

            if (hasDispatch)
            {
                _sink(dispatch);
            }

            if (scheduleAgain)
            {
                ScheduleDrain(delay);
            }
        }
    }
}
