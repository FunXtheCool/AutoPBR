using System.Collections.Concurrent;

using AutoPBR.App.Models;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

using Avalonia.Threading;

namespace AutoPBR.App.Services;

internal sealed partial class ExploreTreeController
{
    private async Task QueueBackgroundEffectiveTagComputeAsync(string archivePath)
    {
        if (!_effectiveTagComputeInFlight.TryAdd(archivePath, 0))
        {
            return;
        }

        Interlocked.Increment(ref _tagAsyncWorkPending);
        try
        {
            await _tagComputeConcurrency.WaitAsync().ConfigureAwait(false);
            try
            {
                var epoch = Volatile.Read(ref _effectiveTagEpoch);
                try
                {
                    var computed = await Task.Run(() =>
                            ComputeEffectiveTags(archivePath, includeDictionaryEvidence: true, deferSemanticMl: false))
                        .ConfigureAwait(false);
                    if (Volatile.Read(ref _effectiveTagEpoch) != epoch)
                    {
                        return;
                    }

                    _effectiveTagCache[archivePath] = computed;
                    _finalSemanticTagPaths[archivePath] = 0;
                    var storageKey = ResolveTagStorageKey(archivePath);
                    if (!string.IsNullOrEmpty(storageKey))
                    {
                        _effectiveTagIdsByStorageKey[storageKey] = computed.Select(static t => t.Id).ToList();
                    }
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        FindNodeByFullPath(archivePath)?.RefreshDisplayTags();
                        if (string.IsNullOrWhiteSpace(_exploreFilter) && string.IsNullOrEmpty(_exploreTagFilterId))
                        {
                            return;
                        }

                        if (!string.IsNullOrEmpty(_exploreTagFilterId))
                        {
                            RefreshExploreTagFilterCacheEntry(archivePath);
                        }

                        ApplyExploreFilterInternal();
                    }, DispatcherPriority.Background);
                    PersistEffectiveTagCache();
                }
                catch
                {
                    // Best effort: preserve immediate result.
                }
            }
            finally
            {
                _tagComputeConcurrency.Release();
                _effectiveTagComputeInFlight.TryRemove(archivePath, out _);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _tagAsyncWorkPending);
        }
    }

    private async Task QueueBackgroundEffectiveTagComputeBatchAsync(List<string> archivePaths)
    {
        if (archivePaths.Count == 0)
        {
            return;
        }

        _debugSink?.Invoke($"Explore ML batch: queued {archivePaths.Count} path(s).");
        Interlocked.Increment(ref _tagAsyncWorkPending);
        try
        {
            foreach (var archivePath in archivePaths)
            {
                if (!_effectiveTagComputeInFlight.TryAdd(archivePath, 0))
                {
                    continue;
                }

                await _tagComputeConcurrency.WaitAsync().ConfigureAwait(false);
                try
                {
                    var epoch = Volatile.Read(ref _effectiveTagEpoch);
                    var computed = await Task.Run(() =>
                            ComputeEffectiveTags(archivePath, includeDictionaryEvidence: true, deferSemanticMl: false))
                        .ConfigureAwait(false);
                    if (Volatile.Read(ref _effectiveTagEpoch) != epoch)
                    {
                        continue;
                    }

                    _effectiveTagCache[archivePath] = computed;
                    _finalSemanticTagPaths[archivePath] = 0;
                    var storageKey = ResolveTagStorageKey(archivePath);
                    if (!string.IsNullOrEmpty(storageKey))
                    {
                        _effectiveTagIdsByStorageKey[storageKey] = computed.Select(static t => t.Id).ToList();
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        FindNodeByFullPath(archivePath)?.RefreshDisplayTags();
                        if (!string.IsNullOrEmpty(_exploreTagFilterId))
                        {
                            RefreshExploreTagFilterCacheEntry(archivePath);
                        }

                        if (!string.IsNullOrWhiteSpace(_exploreFilter) || !string.IsNullOrEmpty(_exploreTagFilterId))
                        {
                            ApplyExploreFilterInternal();
                        }
                    }, DispatcherPriority.Background);
                }
                catch
                {
                    // best effort
                }
                finally
                {
                    _tagComputeConcurrency.Release();
                    _effectiveTagComputeInFlight.TryRemove(archivePath, out _);
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _tagAsyncWorkPending);
            PersistEffectiveTagCache();
            _debugSink?.Invoke("Explore ML batch: complete.");
        }
    }

    public int GetPendingTagWorkCount()
    {
        var pending = Volatile.Read(ref _tagAsyncWorkPending);
        if (_refreshDisplayTagsDebounceCts is not null)
        {
            pending++;
        }

        var refreshTask = _tagRefreshAllTask;
        if (refreshTask is not null && !refreshTask.IsCompleted)
        {
            pending++;
        }

        return Math.Max(0, pending);
    }

    public async Task WaitForPendingTagWorkAsync(
        Action<int>? onPendingWorkChanged = null,
        CancellationToken cancellationToken = default)
    {
        onPendingWorkChanged?.Invoke(GetPendingTagWorkCount());
        for (var i = 0; i < 200 && _refreshDisplayTagsDebounceCts is not null; i++)
        {
            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            onPendingWorkChanged?.Invoke(GetPendingTagWorkCount());
        }

        while (Volatile.Read(ref _tagAsyncWorkPending) > 0)
        {
            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            onPendingWorkChanged?.Invoke(GetPendingTagWorkCount());
        }

        var t = _tagRefreshAllTask;
        if (t is not null && !t.IsCompleted)
        {
            try
            {
                await t.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Best-effort; conversion still runs full tagging in Core.
            }
        }

        onPendingWorkChanged?.Invoke(GetPendingTagWorkCount());
    }

    public void FlushTagOverridesToDisk() => PersistTagOverrides();

    public void ScheduleRefreshAllDisplayTags(int delayMilliseconds = 200)
    {
        _refreshDisplayTagsDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _refreshDisplayTagsDebounceCts = cts;
        _ = RunDebouncedRefreshAllDisplayTagsAsync(cts, delayMilliseconds);
    }

    private async Task RunDebouncedRefreshAllDisplayTagsAsync(CancellationTokenSource debounceCts, int delayMs)
    {
        try
        {
            await Task.Delay(delayMs, debounceCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ReferenceEquals(_refreshDisplayTagsDebounceCts, debounceCts))
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!ReferenceEquals(_refreshDisplayTagsDebounceCts, debounceCts))
            {
                return;
            }

            RefreshAllDisplayTags();
        });
    }

    public void RefreshAllDisplayTags()
    {
        _tagRefreshAllTask = RefreshAllDisplayTagsCoreAsync();
    }

    private async Task RefreshAllDisplayTagsCoreAsync()
    {
        if (_refreshDisplayTagsDebounceCts is { } oldDebounce)
        {
            await oldDebounce.CancelAsync().ConfigureAwait(false);
            oldDebounce.Dispose();
        }
        _refreshDisplayTagsDebounceCts = null;
        if (_tagRefreshCts is { } oldRefresh)
        {
            await oldRefresh.CancelAsync().ConfigureAwait(false);
            oldRefresh.Dispose();
        }
        var cts = new CancellationTokenSource();
        _tagRefreshCts = cts;
        Interlocked.Increment(ref _effectiveTagEpoch);

        _effectiveTagCache.Clear();
        _effectiveTagIdsByStorageKey.Clear();
        _finalSemanticTagPaths.Clear();
        _optifineFolderMaterialHintIdsByRuleKey.Clear();
        InvalidateExploreTagFilterCache();

        var paths = CollectFileNodePaths(Root);
        if (paths.Count == 0)
        {
            NotifyAllDisplayTagsRecursive(Root);
            ApplyExploreFilterIfNeeded();
            return;
        }

        Interlocked.Increment(ref _tagAsyncWorkPending);
        try
        {
            try
            {
                await Task.Run(() =>
                {
                    for (var i = 0; i < paths.Count; i++)
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        var path = paths[i];
                        // During a full refresh we want final semantic ranking (including fused score), not deferred keyword placeholders.
                        var tags = ComputeEffectiveTags(path, includeDictionaryEvidence: true, deferSemanticMl: false);
                        _effectiveTagCache[path] = tags;
                        _finalSemanticTagPaths[path] = 0;
                    }
                }, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cts.Token.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                InvalidateExploreTagFilterCache();
                NotifyAllDisplayTagsRecursive(Root);
                ApplyExploreFilterIfNeeded();
            });
            PersistEffectiveTagCache();
        }
        finally
        {
            Interlocked.Decrement(ref _tagAsyncWorkPending);
        }
    }

    public void ClearTagOverridesForCurrentPack()
    {
        _tagAdded.Clear();
        _tagRemoved.Clear();
        _effectiveTagCache.Clear();
        _effectiveTagIdsByStorageKey.Clear();
        _optifineFolderMaterialHintIdsByRuleKey.Clear();
        _finalSemanticTagPaths.Clear();
        Interlocked.Increment(ref _effectiveTagEpoch);
        InvalidateExploreTagFilterCache();
        PersistTagOverrides();
        PersistEffectiveTagCache();
        RefreshAllDisplayTags();
    }

    private static List<string> CollectFileNodePaths(ArchiveNode? root)
    {
        var list = new List<string>();
        if (root is null)
        {
            return list;
        }

        var stack = new Stack<ArchiveNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!node.IsFolder)
            {
                list.Add(node.FullPath);
            }

            foreach (var child in node.Children)
            {
                stack.Push(child);
            }
        }

        return list;
    }

    private static void NotifyAllDisplayTagsRecursive(ArchiveNode? node)
    {
        if (node is null)
        {
            return;
        }

        if (!node.IsFolder)
        {
            node.RefreshDisplayTags();
        }

        foreach (var child in node.Children)
        {
            NotifyAllDisplayTagsRecursive(child);
        }
    }

    IReadOnlyList<TagRule> IArchiveNodeHost.GetTagRules() => _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;

    /// <summary>Set the provider that returns effective tag rules (built-in + custom). Called when custom rules may have changed.</summary>
    public void SetTagRulesProvider(Func<IReadOnlyList<TagRule>>? provider)
    {
        _tagRulesProvider = provider;
    }

    public void SetMaterialTagSemanticOptionsProvider(Func<MaterialTagSemanticOptions?>? provider)
    {
        _materialTagSemanticOptionsProvider = provider;
    }
}
