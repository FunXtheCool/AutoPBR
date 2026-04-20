using Avalonia.Threading;

namespace AutoPBR.App.Services;

/// <summary>
/// Debounces repeated settings writes and provides an explicit flush point before long-running operations.
/// </summary>
internal sealed class SettingsPersistenceCoordinator
    (Dispatcher dispatcher, TimeSpan debounceDelay, Action persistAction)
{
    private CancellationTokenSource? _pendingCts;
    private Task _pendingTask = Task.CompletedTask;

    public void RequestSave()
    {
        var cts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _pendingCts, cts);
        oldCts?.Cancel();
        oldCts?.Dispose();
        _pendingTask = PersistDebouncedAsync(cts);
    }

    public async Task FlushAsync()
    {
        var pending = _pendingTask;
        if (!pending.IsCompleted)
        {
            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer pending write.
            }
        }
    }

    private async Task PersistDebouncedAsync(CancellationTokenSource debounceCts)
    {
        try
        {
            var debounceMs = (int)Math.Max(1, debounceDelay.TotalMilliseconds);
            await Task.Delay(debounceMs, debounceCts.Token).ConfigureAwait(false);
            await dispatcher.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_pendingCts, debounceCts))
                {
                    return;
                }

                persistAction();
            });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer pending write.
        }
        finally
        {
            if (ReferenceEquals(_pendingCts, debounceCts))
            {
                _pendingCts = null;
            }

            debounceCts.Dispose();
        }
    }
}
