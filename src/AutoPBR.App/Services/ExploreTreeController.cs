using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AutoPBR.App.Models;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Services;

/// <summary>Owns scanned archive data, path overrides, folder visibility cache, and the explore tree root. Implements <see cref="IArchiveNodeHost"/> for lazy loading and override storage.</summary>
internal sealed class ExploreTreeController : IArchiveNodeHost, IDisposable
{
    private static readonly HashSet<string> TextureTypeFolderNames = new(StringComparer.OrdinalIgnoreCase)
        { "block", "blocks", "item", "items", "entity", "particle" };

    private static readonly HashSet<string> IgnoredOptifineFolders = new(StringComparer.OrdinalIgnoreCase)
        { "anim", "colormap", "sky" };

    private string? _scannedArchivePath;
    private readonly ConcurrentDictionary<string, bool?> _pathOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _tagAdded = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _tagRemoved = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _folderVisibilityCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bitmap?> _batchPackIconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _batchPackIconLoading = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _batchPackIconSync = new();
    private string _exploreFilter = "";
    private string? _exploreTagFilterId;
    private Func<IReadOnlyList<TagRule>>? _tagRulesProvider;
    private Func<MaterialTagSemanticOptions?>? _materialTagSemanticOptionsProvider;
    private IBackgroundTaskSink? _backgroundTaskSink;
    private Action<string>? _debugSink;

    /// <summary>Maps texture storage key → effective tag ids for &quot;Show tag&quot; filtering (avoids re-running ML/keywords per node on every filter pass).</summary>
    private Dictionary<string, HashSet<string>>? _exploreTagFilterCache;

    /// <summary>Pre-computed effective tags per archive path. Populated by background refresh; read by <see cref="IArchiveNodeHost.GetEffectiveTags"/>.</summary>
    private readonly ConcurrentDictionary<string, IReadOnlyList<(string Id, string DisplayName, TagRuleKind Kind)>> _effectiveTagCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _effectiveTagIdsByStorageKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _optifineFolderMaterialHintIdsByRuleKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _effectiveTagComputeInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _finalSemanticTagPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Bumped when tag semantics are refreshed so in-flight per-path background computes cannot overwrite newer cache entries.</summary>
    private int _effectiveTagEpoch;

    private CancellationTokenSource? _tagRefreshCts;
    private CancellationTokenSource? _refreshDisplayTagsDebounceCts;

    /// <summary>Background tag work: per-path deferred MiniLM/dictionary and full-tree refresh. Conversion waits for this to reach 0.</summary>
    private int _tagAsyncWorkPending;

    /// <summary>Last full-tree tag refresh; conversion may await this so ONNX work does not overlap convert.</summary>
    private Task? _tagRefreshAllTask;

    /// <summary>Limits parallel per-texture ML/dictionary work so completion callbacks do not flood the UI thread.</summary>
    private readonly SemaphoreSlim _tagComputeConcurrency = new(4, 4);

    public ArchiveNode? Root { get; private set; }
    public ScannedArchiveData? Data { get; private set; }

    /// <summary>Optional UI progress for background tag work (dictionary + ML per texture).</summary>
    public void SetBackgroundTaskSink(IBackgroundTaskSink? sink) => _backgroundTaskSink = sink;
    public void SetDebugSink(Action<string>? sink) => _debugSink = sink;

    /// <summary>Set scanned data and build the tree root. Call on UI thread. Returns the new root (also assign to VM's ScannedArchiveRoot). Loads persisted tag overrides for this pack.</summary>
    /// <param name="data">Scanned index (single archive or merged batch folder).</param>
    /// <param name="packPath">Single-pack .zip/.jar path, or when <paramref name="data"/>.<see cref="ScannedArchiveData.IsBatch"/> is true, the scanned folder path (used for tag persistence).</param>
    public ArchiveNode SetData(ScannedArchiveData data, string packPath)
    {
        ClearBatchPackIconCache();
        Data = data;
        _scannedArchivePath = data.IsBatch ? data.BatchFolderPath ?? packPath : packPath;
        _tagAdded.Clear();
        _tagRemoved.Clear();
        _effectiveTagCache.Clear();
        _effectiveTagIdsByStorageKey.Clear();
        _optifineFolderMaterialHintIdsByRuleKey.Clear();
        _finalSemanticTagPaths.Clear();
        var snapshot = TagOverridesPersistence.Load(_scannedArchivePath);
        if (snapshot?.Overrides is not null)
        {
            foreach (var kv in snapshot.Overrides)
            {
                if (kv.Value.Added is { Count: > 0 })
                {
                    _tagAdded[kv.Key] = new HashSet<string>(kv.Value.Added, StringComparer.OrdinalIgnoreCase);
                }

                if (kv.Value.Removed is { Count: > 0 })
                {
                    _tagRemoved[kv.Key] = new HashSet<string>(kv.Value.Removed, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        TryLoadEffectiveTagsCache();
        _debugSink?.Invoke("Explore: scan data loaded, tag cache restored if signature matched.");
        Root = new ArchiveNode("", "", true, null, this);
        ((IArchiveNodeHost)this).EnsureChildrenLoaded(Root);
        InvalidateExploreTagFilterCache();
        return Root;
    }

    /// <summary>Clear all state; call when user clears archive or starts a new scan.</summary>
    public void Clear()
    {
        _tagRefreshCts?.Cancel();
        _refreshDisplayTagsDebounceCts?.Cancel();
        _refreshDisplayTagsDebounceCts = null;
        Interlocked.Increment(ref _effectiveTagEpoch);
        ClearBatchPackIconCache();
        Root = null;
        Data = null;
        _scannedArchivePath = null;
        _pathOverrides.Clear();
        _tagAdded.Clear();
        _tagRemoved.Clear();
        _effectiveTagCache.Clear();
        _effectiveTagIdsByStorageKey.Clear();
        _optifineFolderMaterialHintIdsByRuleKey.Clear();
        _effectiveTagComputeInFlight.Clear();
        _finalSemanticTagPaths.Clear();
        _folderVisibilityCache.Clear();
        InvalidateExploreTagFilterCache();
    }

    public bool HaveScanForCurrentPack(string? packPath) =>
        Data is not null &&
        !Data.IsBatch &&
        _scannedArchivePath is not null &&
        !string.IsNullOrEmpty(packPath) &&
        string.Equals(Path.GetFullPath(_scannedArchivePath), Path.GetFullPath(packPath), StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the explorer has a batch index for this folder (same path as last batch scan).</summary>
    public bool HaveBatchScanForFolder(string? batchFolderPath) =>
        Data?.IsBatch == true &&
        !string.IsNullOrWhiteSpace(batchFolderPath) &&
        !string.IsNullOrWhiteSpace(Data.BatchFolderPath) &&
        string.Equals(Path.GetFullPath(Data.BatchFolderPath!), Path.GetFullPath(batchFolderPath), StringComparison.OrdinalIgnoreCase);

    bool? IArchiveNodeHost.GetOverride(string fullPath) =>
        _pathOverrides.GetValueOrDefault(fullPath);

    void IArchiveNodeHost.SetOverride(string fullPath, bool? value)
    {
        if (value.HasValue)
        {
            _pathOverrides[fullPath] = value;
        }
        else
        {
            _pathOverrides.TryRemove(fullPath, out _);
        }
    }

    void IArchiveNodeHost.EnsureChildrenLoaded(ArchiveNode node)
    {
        if (Data is null || node.Children.Count > 0)
        {
            return;
        }

        var children = Data.GetChildren(node.FullPath);
        if (children is null)
        {
            return;
        }

        var deferredTagRefresh = new List<ArchiveNode>();
        foreach (var entry in children)
        {
            if (entry.IsFolder)
            {
                if (IsIgnoredOptifineFolder(entry.FullPath))
                {
                    continue;
                }

                if (!HasVisiblePngUnder(entry.FullPath))
                {
                    continue;
                }
            }
            else
            {
                if (GetEffectiveOverrideForPath(entry.FullPath) == false)
                {
                    continue;
                }
            }

            var isBatchPackRoot = Data.IsBatch && string.IsNullOrEmpty(node.FullPath) && entry.IsFolder;
            var child = new ArchiveNode(entry.Name, entry.FullPath, entry.IsFolder, node, this, isBatchPackRoot);
            if (!entry.IsFolder)
            {
                deferredTagRefresh.Add(child);
            }

            if (isBatchPackRoot)
            {
                if (TryGetCachedBatchPackIcon(entry.FullPath, out var icon))
                {
                    child.PackIcon = icon;
                }
                else
                {
                    QueueBatchPackIconLoad(entry.FullPath);
                }
            }
            node.Children.Add(child);
        }

        ApplyExploreFilterIfNeeded();

        if (deferredTagRefresh.Count > 0)
        {
            _ = QueueBackgroundEffectiveTagComputeBatchAsync(deferredTagRefresh.Select(static n => n.FullPath).ToList());
            // Spread tag row work across frames so a folder with hundreds of textures stays responsive.
            const int chunk = 40;
            void PostChunk(int start)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var end = Math.Min(start + chunk, deferredTagRefresh.Count);
                    for (var j = start; j < end; j++)
                    {
                        deferredTagRefresh[j].RefreshDisplayTags();
                    }

                    if (end < deferredTagRefresh.Count)
                    {
                        PostChunk(end);
                    }
                }, DispatcherPriority.Background);
            }

            PostChunk(0);
        }
    }

    public bool? GetEffectiveOverrideForPath(string fullPath)
    {
        var path = fullPath;
        while (!string.IsNullOrEmpty(path))
        {
            if (_pathOverrides.TryGetValue(path, out var v) && v.HasValue)
            {
                return v;
            }

            var slash = path.LastIndexOf('/');
            if (slash < 0)
            {
                break;
            }

            path = path[..slash];
        }
        return null;
    }

    public void ApplyExploreOverridesToIgnoreSet(HashSet<string> ignore)
    {
        if (Data is null || Data.IsBatch)
        {
            return;
        }

        foreach (var fullPath in Data.EnumerateAllFilePaths())
        {
            var key = ArchivePathToTextureKey(fullPath);
            if (key is null)
            {
                continue;
            }

            var effective = GetEffectiveOverrideForPath(fullPath);
            if (!effective.HasValue)
            {
                continue;
            }

            if (effective.Value)
            {
                ignore.Remove(key);
            }
            else
            {
                ignore.Add(key);
            }
        }
    }

    /// <summary>Apply include/exclude overrides from the explorer for textures under one batch pack root (e.g. <c>MyPack.zip</c>).</summary>
    public void ApplyExploreOverridesToIgnoreSetForBatchPack(HashSet<string> ignore, string packRootFolderName)
    {
        if (Data is null || !Data.IsBatch)
        {
            return;
        }

        var prefix = packRootFolderName.TrimEnd('/') + "/";
        foreach (var fullPath in Data.EnumerateAllFilePaths())
        {
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var inner = fullPath[prefix.Length..];
            var key = ArchivePathToTextureKey(inner);
            if (key is null)
            {
                continue;
            }

            var effective = GetEffectiveOverrideForPath(fullPath);
            if (!effective.HasValue)
            {
                continue;
            }

            if (effective.Value)
            {
                ignore.Remove(key);
            }
            else
            {
                ignore.Add(key);
            }
        }
    }

    /// <summary>Paths to extract for one pack in a batch (png paths inside that zip).</summary>
    public IReadOnlyList<string> GetFilePathsUnderBatchPackRoot(string packRootFolderName)
    {
        if (Data is null || !Data.IsBatch)
        {
            return [];
        }

        var prefix = packRootFolderName.TrimEnd('/') + "/";
        return Data.EnumerateAllFilePaths()
            .Where(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(p => p[prefix.Length..])
            .ToList();
    }

    /// <summary>Manual tag overrides for conversion of one batch pack (keys are work-item RelativeKey form).</summary>
    public IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>
        GetManualTagOverridesForBatchPackRoot(string packRootFolderName)
    {
        var prefix = packRootFolderName + "|";
        var dict = new Dictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _tagAdded.Keys.Union(_tagRemoved.Keys))
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var inner = key[prefix.Length..];
            var added = _tagAdded.TryGetValue(key, out var a) ? (IReadOnlyList<string>)a.ToList() : [];
            var removed = _tagRemoved.TryGetValue(key, out var r) ? (IReadOnlyList<string>)r.ToList() : [];
            dict[inner] = (added, removed);
        }

        return dict;
    }

    /// <summary>Resolve disk .zip/.jar and entry path for preview or tools. <paramref name="entryPathInZip"/> uses forward slashes.</summary>
    public bool TryGetDiskPackAndEntryPath(string archivePathInTree, out string diskPackPath, out string entryPathInZip)
    {
        diskPackPath = "";
        entryPathInZip = "";
        if (Data is { IsBatch: true, BatchPackRootToPath: { } batchPackRoots } &&
            TryStripBatchPackRoot(archivePathInTree, out var root, out var inner))
        {
            if (batchPackRoots.TryGetValue(root, out var disk))
            {
                diskPackPath = disk;
                entryPathInZip = inner.Replace('\\', '/');
                return true;
            }

            return false;
        }

        if (Data is not null && !Data.IsBatch && _scannedArchivePath is not null)
        {
            diskPackPath = _scannedArchivePath;
            entryPathInZip = archivePathInTree.Replace('\\', '/');
            return true;
        }

        return false;
    }

    private bool TryStripBatchPackRoot(string fullPath, [NotNullWhen(true)] out string? packRoot, out string innerPath)
    {
        packRoot = null;
        innerPath = fullPath;
        if (Data?.IsBatch != true || Data.BatchPackRootToPath is null)
        {
            return false;
        }

        var norm = fullPath.Replace('\\', '/');
        var slash = norm.IndexOf('/');
        if (slash <= 0)
        {
            return false;
        }

        var root = norm[..slash];
        if (!Data.BatchPackRootToPath.ContainsKey(root))
        {
            return false;
        }

        packRoot = root;
        innerPath = norm[(slash + 1)..];
        return true;
    }

    private string? ResolveTagStorageKey(string archivePath)
    {
        if (TryStripBatchPackRoot(archivePath, out var packRoot, out var inner))
        {
            var key = ArchivePathToTextureKey(inner);
            return key is null ? null : packRoot + "|" + key;
        }

        return ArchivePathToTextureKey(archivePath);
    }

    private static string RuleRelativeKeyFromStorageKey(string storageKey)
    {
        var i = storageKey.IndexOf('|');
        return i < 0 ? storageKey : storageKey[(i + 1)..];
    }

    public static string? ArchivePathToTextureKey(string fullPath)
    {
        var parts = fullPath.Replace('\\', '/').Split('/');
        if (parts.Length < 4 || !parts[0].Equals("assets", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var ns = parts[1];
        if (parts[2].Equals("textures", StringComparison.OrdinalIgnoreCase))
        {
            var after = string.Join("\\", parts.Skip(3));
            var noExt = Path.ChangeExtension(after, null);
            return "\\" + ns + "\\" + noExt;
        }
        if (parts[2].Equals("optifine", StringComparison.OrdinalIgnoreCase))
        {
            var after = string.Join("\\", parts.Skip(3));
            var noExt = Path.ChangeExtension(after, null);
            return "\\" + ns + "\\optifine\\" + noExt;
        }
        return null;
    }

    public HashSet<string> GetTextureTypeFolderPaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Data is null)
        {
            return seen;
        }

        foreach (var fullPath in Data.EnumerateAllFilePaths())
        {
            var segments = fullPath.Split('/');
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (!segments[i].Equals("textures", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= segments.Length)
                {
                    continue;
                }

                var typeName = segments[i + 1];
                if (!TextureTypeFolderNames.Contains(typeName))
                {
                    continue;
                }

                var folderPath = string.Join("/", segments.Take(i + 2));
                seen.Add(folderPath);
            }
        }
        return seen;
    }

    private static bool GetProcessValueForTextureFolder(string folderPath, bool processBlocks, bool processItems, bool processEntity, bool processParticles)
    {
        var seg = folderPath.Split('/');
        var last = seg.Length > 0 ? seg[^1] : "";
        if (last.Equals("block", StringComparison.OrdinalIgnoreCase) || last.Equals("blocks", StringComparison.OrdinalIgnoreCase))
        {
            return processBlocks;
        }

        if (last.Equals("item", StringComparison.OrdinalIgnoreCase) || last.Equals("items", StringComparison.OrdinalIgnoreCase))
        {
            return processItems;
        }

        if (last.Equals("entity", StringComparison.OrdinalIgnoreCase))
        {
            return processEntity;
        }

        if (last.Equals("particle", StringComparison.OrdinalIgnoreCase))
        {
            return processParticles;
        }

        return true;
    }

    /// <summary>Warm up visibility cache for first few levels. Run on background thread.</summary>
    public void PrewarmFolderVisibilityCache(CancellationToken cancellationToken)
    {
        var data = Data;
        if (data is null)
        {
            return;
        }

        const int maxDepth = 3;
        var queue = new Queue<(string path, int depth)>();
        queue.Enqueue(("", 0));
        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (parent, depth) = queue.Dequeue();
            var children = data.GetChildren(parent);
            if (children is null)
            {
                continue;
            }

            foreach (var c in children)
            {
                if (!c.IsFolder)
                {
                    continue;
                }

                if (depth < maxDepth)
                {
                    queue.Enqueue((c.FullPath, depth + 1));
                }

                if (!_folderVisibilityCache.ContainsKey(c.FullPath))
                {
                    _folderVisibilityCache[c.FullPath] = ComputeFolderVisible(data, c.FullPath);
                }
            }
        }
    }

    /// <summary>Apply block/item/entity/particle overrides from process flags and refresh tree. Returns node to restore focus (same path) or null.</summary>
    public ArchiveNode? ApplyTextureTypeOverridesToExplore(string? previousFocusPath, bool processBlocks, bool processItems, bool processEntity, bool processParticles)
    {
        var paths = GetTextureTypeFolderPaths();
        if (paths.Count == 0)
        {
            return null;
        }

        _folderVisibilityCache.Clear();
        foreach (var path in paths)
        {
            var include = GetProcessValueForTextureFolder(path, processBlocks, processItems, processEntity, processParticles);
            _pathOverrides[path] = include;
        }
        NotifyOverrideChangedForPaths(paths);
        RefreshExploreTreeFilter();
        if (string.IsNullOrEmpty(previousFocusPath))
        {
            return null;
        }

        return FindNodeByFullPath(previousFocusPath);
    }

    public void RefreshExploreTreeFilter()
    {
        if (Root is null)
        {
            return;
        }

        InvalidateExploreTagFilterCache();
        ClearChildrenRecursive(Root);
        ((IArchiveNodeHost)this).EnsureChildrenLoaded(Root);
        ApplyExploreFilterInternal();
    }

    public void ApplyExploreFilter(string? textFilter, string? tagFilterId)
    {
        _exploreFilter = textFilter ?? "";
        _exploreTagFilterId = string.IsNullOrEmpty(tagFilterId) ? null : tagFilterId;
        ApplyExploreFilterInternal();
    }

    /// <summary>Full-tree filter pass only when a text or tag filter is active (skipped on plain navigation).</summary>
    private void ApplyExploreFilterIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(_exploreFilter) && string.IsNullOrEmpty(_exploreTagFilterId))
        {
            return;
        }

        ApplyExploreFilterInternal();
    }

    private void ApplyExploreFilterInternal()
    {
        if (Root is null)
        {
            return;
        }

        var f = _exploreFilter.Trim();
        if (!string.IsNullOrEmpty(_exploreTagFilterId))
        {
            EnsureExploreTagFilterCache();
        }

        ApplyExploreFilterRecursive(Root, f);
    }

    private bool ApplyExploreFilterRecursive(ArchiveNode node, string filter)
    {
        if (string.IsNullOrEmpty(filter) && string.IsNullOrEmpty(_exploreTagFilterId))
        {
            node.IsVisibleByFilter = true;
            foreach (var child in node.Children)
            {
                ApplyExploreFilterRecursive(child, filter);
            }

            return true;
        }

        if (node.IsFolder)
        {
            // Filtering needs descendant visibility; force-load this level for lazy nodes.
            ((IArchiveNodeHost)this).EnsureChildrenLoaded(node);
        }

        bool textMatch = string.IsNullOrEmpty(filter)
            || node.FullPath.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

        bool anyChildVisible = false;
        foreach (var child in node.Children)
        {
            if (ApplyExploreFilterRecursive(child, filter))
            {
                anyChildVisible = true;
            }
        }

        bool visible;
        if (!string.IsNullOrEmpty(_exploreTagFilterId))
        {
            if (node.IsFolder)
            {
                visible = textMatch && anyChildVisible;
            }
            else
            {
                var storageKey = ResolveTagStorageKey(node.FullPath);
                var hasTag = !string.IsNullOrEmpty(storageKey) &&
                             _exploreTagFilterCache is { } cache &&
                             cache.TryGetValue(storageKey, out var idSet) &&
                             idSet.Contains(_exploreTagFilterId!);
                visible = textMatch && hasTag;
            }
        }
        else
        {
            visible = textMatch || anyChildVisible;
        }

        node.IsVisibleByFilter = visible;
        return visible;
    }

    private void InvalidateExploreTagFilterCache() => _exploreTagFilterCache = null;

    private void EnsureExploreTagFilterCache()
    {
        if (_exploreTagFilterCache is not null || Root is null)
        {
            return;
        }

        _exploreTagFilterCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        BuildExploreTagFilterCacheRecursive(Root);
    }

    private void BuildExploreTagFilterCacheRecursive(ArchiveNode node)
    {
        if (node.IsFolder)
        {
            ((IArchiveNodeHost)this).EnsureChildrenLoaded(node);
            foreach (var child in node.Children)
            {
                BuildExploreTagFilterCacheRecursive(child);
            }

            return;
        }

        var storageKey = ResolveTagStorageKey(node.FullPath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return;
        }

        var tags = ((IArchiveNodeHost)this).GetEffectiveTags(node.FullPath);
        _exploreTagFilterCache![storageKey] = new HashSet<string>(tags.Select(t => t.Id), StringComparer.OrdinalIgnoreCase);
    }

    private void RefreshExploreTagFilterCacheEntry(string archivePath)
    {
        if (_exploreTagFilterCache is null)
        {
            return;
        }

        var storageKey = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return;
        }

        var tags = ((IArchiveNodeHost)this).GetEffectiveTags(archivePath);
        _exploreTagFilterCache[storageKey] = new HashSet<string>(tags.Select(t => t.Id), StringComparer.OrdinalIgnoreCase);
    }

    private static void ClearChildrenRecursive(ArchiveNode node)
    {
        foreach (var child in node.Children)
        {
            ClearChildrenRecursive(child);
        }

        node.Children.Clear();
    }

    private void NotifyOverrideChangedForPaths(HashSet<string> paths)
    {
        if (paths.Count == 0 || Root is null)
        {
            return;
        }

        NotifyOverrideChangedRecursive(Root, paths);
    }

    private static void NotifyOverrideChangedRecursive(ArchiveNode node, HashSet<string> paths)
    {
        if (paths.Contains(node.FullPath))
        {
            node.NotifyOverrideChanged();
        }

        foreach (var child in node.Children)
        {
            NotifyOverrideChangedRecursive(child, paths);
        }
    }

    private bool HasVisiblePngUnder(string folderPath)
    {
        if (_folderVisibilityCache.TryGetValue(folderPath, out var cached))
        {
            return cached;
        }

        if (Data is null)
        {
            return false;
        }

        var visible = ComputeFolderVisible(Data, folderPath);
        _folderVisibilityCache[folderPath] = visible;
        return visible;
    }

    private bool ComputeFolderVisible(ScannedArchiveData data, string folderPath)
    {
        var queue = new Queue<string>();
        queue.Enqueue(folderPath);
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            var children = data.GetChildren(parent);
            if (children is null)
            {
                continue;
            }

            foreach (var c in children)
            {
                if (c.IsFolder)
                {
                    queue.Enqueue(c.FullPath);
                }
                else if (GetEffectiveOverrideForPath(c.FullPath) != false)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsIgnoredOptifineFolder(string fullPath)
    {
        var segments = fullPath.Split('/');
        if (segments.Length < 4)
        {
            return false;
        }

        if (!segments[0].Equals("assets", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!segments[2].Equals("optifine", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IgnoredOptifineFolders.Contains(segments[3]);
    }

    private bool TryGetCachedBatchPackIcon(string packRootFolderName, out Bitmap? icon)
    {
        lock (_batchPackIconSync)
        {
            return _batchPackIconCache.TryGetValue(packRootFolderName, out icon);
        }
    }

    private void QueueBatchPackIconLoad(string packRootFolderName)
    {
        lock (_batchPackIconSync)
        {
            if (_batchPackIconCache.ContainsKey(packRootFolderName) || !_batchPackIconLoading.Add(packRootFolderName))
            {
                return;
            }
        }

        _ = Task.Run(() =>
        {
            var icon = LoadBatchPackIconCore(packRootFolderName);
            Dispatcher.UIThread.Post(() =>
            {
                lock (_batchPackIconSync)
                {
                    _batchPackIconLoading.Remove(packRootFolderName);
                    _batchPackIconCache[packRootFolderName] = icon;
                }

                // Update node if still present in current tree.
                var node = FindNodeByFullPath(packRootFolderName);
                if (node is not null)
                {
                    node.PackIcon = icon;
                }
            });
        });
    }

    private Bitmap? LoadBatchPackIconCore(string packRootFolderName)
    {
        try
        {
            if (Data?.BatchPackRootToPath?.TryGetValue(packRootFolderName, out var packPath) != true ||
                string.IsNullOrWhiteSpace(packPath) ||
                !File.Exists(packPath))
            {
                return null;
            }

            using var zip = ZipFile.OpenRead(packPath);
            var entry = zip.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName.Trim('/'), "pack.png", StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                return null;
            }

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return new Bitmap(ms);
        }
        catch
        {
            // Best-effort visual enhancement; ignore invalid images or archive read errors.
            return null;
        }
    }

    private void ClearBatchPackIconCache()
    {
        lock (_batchPackIconSync)
        {
            foreach (var bmp in _batchPackIconCache.Values)
            {
                bmp?.Dispose();
            }

            _batchPackIconCache.Clear();
            _batchPackIconLoading.Clear();
        }
    }

    IReadOnlyList<(string Id, string DisplayName, TagRuleKind Kind)> IArchiveNodeHost.GetEffectiveTags(string archivePath)
    {
        if (_effectiveTagCache.TryGetValue(archivePath, out var cached))
        {
            var cachedSem = _materialTagSemanticOptionsProvider?.Invoke();
            if (cachedSem is { Enabled: true, Matcher: not null } && !_finalSemanticTagPaths.ContainsKey(archivePath))
            {
                _ = QueueBackgroundEffectiveTagComputeAsync(archivePath);
            }

            return cached;
        }

        // When MiniLM is enabled, do not run ONNX/dictionary on the UI thread: keyword tags first, then async enrichment.
        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        var deferMl = sem is { Enabled: true, Matcher: not null };
        var immediate = ComputeEffectiveTags(archivePath, includeDictionaryEvidence: false, deferSemanticMl: deferMl);
        _effectiveTagCache[archivePath] = immediate;
        _finalSemanticTagPaths.TryRemove(archivePath, out _);
        if (deferMl)
        {
            _ = QueueBackgroundEffectiveTagComputeAsync(archivePath);
        }

        return immediate;
    }

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

    /// <summary>
    /// Blocks until debounced tag refresh, in-flight per-path MiniLM/dictionary work, and any full-tree tag refresh finish.
    /// Call before PBR conversion so Explore-side ONNX work does not race with <see cref="TextureScanner"/> tagging,
    /// and so manual tag overrides are fully persisted.
    /// </summary>
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

    /// <summary>Writes manual tag add/remove to disk for the current scanned pack (safe no-op if nothing scanned).</summary>
    public void FlushTagOverridesToDisk() => PersistTagOverrides();

    /// <param name="archivePath">Archive entry path for the texture being evaluated.</param>
    /// <param name="includeDictionaryEvidence">Whether dictionary evidence should influence semantic material scoring.</param>
    /// <param name="deferSemanticMl">When true with semantic ML enabled, use keyword material tags only (fast); full ML runs later on a background thread.</param>
    private List<(string Id, string DisplayName, TagRuleKind Kind)> ComputeEffectiveTags(
        string archivePath,
        bool includeDictionaryEvidence,
        bool deferSemanticMl = false)
    {
        var storageKey = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return [];
        }

        var ruleKey = RuleRelativeKeyFromStorageKey(storageKey);
        var name = Path.GetFileNameWithoutExtension(archivePath);
        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        var isNumericOptifineTile = IsNumericOnlyOptifineTile(name, ruleKey);
        if (isNumericOptifineTile)
        {
            deferSemanticMl = true;
        }

        if (deferSemanticMl && !includeDictionaryEvidence)
        {
            deferSemanticMl = ShouldDeferSemanticMl(archivePath);
        }
        var added = _tagAdded.GetValueOrDefault(storageKey);
        var removed = _tagRemoved.GetValueOrDefault(storageKey);
        var effectiveIds = ConversionEffectiveTags.ComputeEffectiveTagIds(
            name,
            ruleKey,
            texturePath: null,
            rules,
            sem,
            includeDictionaryEvidence,
            deferSemanticMl,
            added,
            removed);
        var orderedEffectiveIds = new List<string>(effectiveIds);
        var effective = new HashSet<string>(effectiveIds, StringComparer.OrdinalIgnoreCase);
        if (isNumericOptifineTile && includeDictionaryEvidence)
        {
            var parentRuleKey = GetParentRuleRelativeKey(ruleKey);
            if (!string.IsNullOrEmpty(parentRuleKey))
            {
                var inheritedMaterialIds = GetOrComputeOptifineFolderMaterialHintIds(parentRuleKey, rules, sem);
                if (inheritedMaterialIds.Count > 0)
                {
                    HashSet<string>? removedSet = null;
                    if (removed is { Count: > 0 })
                    {
                        removedSet = new HashSet<string>(removed, StringComparer.OrdinalIgnoreCase);
                    }

                    foreach (var inheritedId in inheritedMaterialIds)
                    {
                        if (removedSet is not null && removedSet.Contains(inheritedId))
                        {
                            continue;
                        }

                        if (effective.Add(inheritedId))
                        {
                            orderedEffectiveIds.Add(inheritedId);
                        }
                    }
                }
            }
        }

        var result = new List<(string Id, string DisplayName, TagRuleKind Kind)>();
        var rulesById = rules
            .GroupBy(static r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in orderedEffectiveIds)
        {
            if (!effective.Contains(id) || !seen.Add(id))
            {
                continue;
            }

            if (!rulesById.TryGetValue(id, out var rule))
            {
                continue;
            }

            result.Add((rule.Id, rule.DisplayName, rule.Kind));
        }

        _effectiveTagIdsByStorageKey[storageKey] = result.Select(static t => t.Id).ToList();
        return result;
    }

    private bool ShouldDeferSemanticMl(string archivePath)
    {
        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        if (sem is not { Enabled: true, Matcher: not null })
        {
            return false;
        }

        var storageKey = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return true;
        }

        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var ruleKey = RuleRelativeKeyFromStorageKey(storageKey);
        var name = Path.GetFileNameWithoutExtension(archivePath);
        if (IsNumericOnlyOptifineTile(name, ruleKey))
        {
            return true;
        }

        var heuristicMaterialIds = TagRulePresets.GetMatchingMaterialTagIds(name, ruleKey, rules);
        if (heuristicMaterialIds.Count > 0)
        {
            return false;
        }

        var materialRuleIds = new HashSet<string>(
            rules.Where(r => r.Kind == TagRuleKind.Material).Select(r => r.Id),
            StringComparer.OrdinalIgnoreCase);
        if (_tagAdded.TryGetValue(storageKey, out var addedSet))
        {
            lock (addedSet)
            {
                foreach (var id in addedSet)
                {
                    if (materialRuleIds.Contains(id))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool IsNumericOnlyOptifineTile(string textureName, string ruleRelativeKey)
    {
        if (!IsOptifineRuleRelativeKey(ruleRelativeKey))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(textureName))
        {
            return false;
        }

        foreach (var c in textureName)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOptifineRuleRelativeKey(string ruleRelativeKey) =>
        ruleRelativeKey.Contains("\\optifine\\", StringComparison.OrdinalIgnoreCase);

    private static string? GetParentRuleRelativeKey(string ruleRelativeKey)
    {
        var lastSlash = ruleRelativeKey.LastIndexOf('\\');
        if (lastSlash <= 0)
        {
            return null;
        }

        return ruleRelativeKey[..lastSlash];
    }

    private static string GetRuleRelativeLeafName(string ruleRelativeKey)
    {
        var lastSlash = ruleRelativeKey.LastIndexOf('\\');
        if (lastSlash < 0 || lastSlash == ruleRelativeKey.Length - 1)
        {
            return ruleRelativeKey;
        }

        return ruleRelativeKey[(lastSlash + 1)..];
    }

    private IReadOnlyList<string> GetOrComputeOptifineFolderMaterialHintIds(
        string folderRuleKey,
        IReadOnlyList<TagRule> rules,
        MaterialTagSemanticOptions? sem)
    {
        if (string.IsNullOrWhiteSpace(folderRuleKey) || !IsOptifineRuleRelativeKey(folderRuleKey))
        {
            return [];
        }

        return _optifineFolderMaterialHintIdsByRuleKey.GetOrAdd(folderRuleKey, key =>
        {
            var materialRuleIds = new HashSet<string>(
                rules.Where(r => r.Kind == TagRuleKind.Material).Select(r => r.Id),
                StringComparer.OrdinalIgnoreCase);
            if (materialRuleIds.Count == 0)
            {
                return [];
            }

            var folderName = GetRuleRelativeLeafName(key);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return [];
            }

            var heuristicMaterialIds = TagRulePresets.GetMatchingMaterialTagIds(folderName, key, rules);
            if (heuristicMaterialIds.Count > 0)
            {
                return heuristicMaterialIds
                    .Where(materialRuleIds.Contains)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (sem is not { Enabled: true, Matcher: not null })
            {
                return [];
            }

            var inferredMaterialIds = ConversionEffectiveTags.ComputeEffectiveTagIds(
                folderName,
                key,
                texturePath: null,
                rules,
                sem,
                includeDictionaryEvidence: sem.DictionaryEvidenceEnabled,
                deferSemanticMl: false,
                added: null,
                removed: null)
                .Where(materialRuleIds.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return inferredMaterialIds;
        });
    }

    void IArchiveNodeHost.ApplyManualTagToggle(string archivePath, string tagId, bool wantApplied)
    {
        var key = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (wantApplied)
        {
            RemoveTagIdForPath(_tagRemoved, key, tagId);
            if (!EffectiveTagsContainId(archivePath, tagId))
            {
                var addSet = _tagAdded.GetOrAdd(key, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                lock (addSet)
                {
                    addSet.Add(tagId);
                }
            }
        }
        else
        {
            RemoveTagIdForPath(_tagAdded, key, tagId);
            if (EffectiveTagsContainId(archivePath, tagId))
            {
                var remSet = _tagRemoved.GetOrAdd(key, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                lock (remSet)
                {
                    remSet.Add(tagId);
                }
            }
        }

        PersistTagOverrides();
        PersistEffectiveTagCache();
        _effectiveTagCache[archivePath] = ComputeEffectiveTags(archivePath, includeDictionaryEvidence: true);
        _finalSemanticTagPaths[archivePath] = 0;
        RefreshExploreTagFilterCacheEntry(archivePath);
        var node = FindNodeByFullPath(archivePath);
        node?.RefreshDisplayTags();
        ApplyExploreFilterIfNeeded();
    }

    private bool EffectiveTagsContainId(string archivePath, string tagId)
    {
        var tags = ComputeEffectiveTags(archivePath, includeDictionaryEvidence: true);
        return tags.Any(t => string.Equals(t.Id, tagId, StringComparison.OrdinalIgnoreCase));
    }

    private static void RemoveTagIdForPath(
        ConcurrentDictionary<string, HashSet<string>> dict,
        string key,
        string tagId)
    {
        if (!dict.TryGetValue(key, out var set))
        {
            return;
        }

        lock (set)
        {
            set.Remove(tagId);
        }

        if (set.Count == 0)
        {
            dict.TryRemove(key, out _);
        }
    }

    private void PersistTagOverrides()
    {
        if (string.IsNullOrEmpty(_scannedArchivePath))
        {
            return;
        }

        TagOverridesPersistence.Save(_scannedArchivePath, GetManualTagOverrides());
    }

    /// <summary>
    /// Coalesces rapid numeric setting changes (sliders/spinners): waits <paramref name="delayMilliseconds"/> after the last call, then runs <see cref="RefreshAllDisplayTags"/>.
    /// </summary>
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

    /// <summary>Refresh displayed tags on all loaded nodes (e.g. after tag rules changed). ML inference runs on a background thread.</summary>
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
            var sink = _backgroundTaskSink;
            sink?.BeginTask(BackgroundTaskIds.MaterialTags);
            try
            {
                await Task.Run(() =>
                {
                    var total = paths.Count;
                    for (var i = 0; i < paths.Count; i++)
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        var path = paths[i];
                        // During a full refresh we want final semantic ranking (including fused score), not deferred keyword placeholders.
                        var tags = ComputeEffectiveTags(path, includeDictionaryEvidence: true, deferSemanticMl: false);
                        _effectiveTagCache[path] = tags;
                        _finalSemanticTagPaths[path] = 0;
                        sink?.ReportTask(BackgroundTaskIds.MaterialTags, (double)(i + 1) / total);
                    }
                }, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                sink?.EndTask(BackgroundTaskIds.MaterialTags);
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

    /// <summary>Clear all manual tag add/remove for the current pack, persist, and refresh displayed tags.</summary>
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

    /// <summary>Optional MiniLM semantic tag options (Explore only). Null provider disables ML suggestions.</summary>
    public void SetMaterialTagSemanticOptionsProvider(Func<MaterialTagSemanticOptions?>? provider)
    {
        _materialTagSemanticOptionsProvider = provider;
    }

    /// <summary>
    /// Runs the MiniLM matcher in debug mode and returns a formatted report showing cosine scores for every
    /// material rule and their prototype phrases. Returns null when MiniLM is not enabled or the path is invalid.
    /// </summary>
    public SemanticMatchDebugReport? GetSemanticMatchDebugReport(string archivePath)
    {
        var storageKey = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return null;
        }

        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        if (sem is not { Enabled: true, Matcher: { } matcher })
        {
            return null;
        }

        var ruleKey = RuleRelativeKeyFromStorageKey(storageKey);
        var name = Path.GetFileNameWithoutExtension(archivePath);
        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var materialRules = rules.Where(r => r.Kind == TagRuleKind.Material).ToList();
        return matcher.MatchDebug(
            name,
            ruleKey,
            materialRules,
            sem.DictionaryEvidenceEnabled,
            sem.DictionaryProvider,
            sem.DictionaryEvidenceWeight,
            sem.DictionaryMinEvidenceScore,
            sem.DictionaryRequestTimeoutMs,
            sem.DictionaryLanguageCode);
    }

    public string? GetSemanticMatchDebugText(string archivePath)
    {
        var storageKey = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return null;
        }

        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        if (sem is not { Enabled: true, Matcher: { } matcher })
        {
            return null;
        }

        var ruleKey = RuleRelativeKeyFromStorageKey(storageKey);
        var name = Path.GetFileNameWithoutExtension(archivePath);
        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var materialRules = rules.Where(r => r.Kind == TagRuleKind.Material).ToList();
        var flagRules = rules.Where(r => r.Kind == TagRuleKind.Flag).ToList();
        var rulesById = rules
            .GroupBy(static r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.OrdinalIgnoreCase);

        var isNumericOptifineTile = IsNumericOnlyOptifineTile(name, ruleKey);
        var includeDictionaryEvidence = sem.DictionaryEvidenceEnabled;
        var deferSemanticMl = isNumericOptifineTile;
        var query = MaterialTagSemanticQuery.Build(name, ruleKey);
        var heuristicMaterialIds = TagRulePresets.GetMatchingMaterialTagIds(name, ruleKey, rules).ToList();
        var usedSemanticMl = false;
        List<string> semanticRawIds;
        string materialSelectionPath;
        SemanticMatchDebugReport? semanticReport = null;

        if (deferSemanticMl)
        {
            semanticRawIds = TagRulePresets.GetMatchingMaterialTagIds(name, ruleKey, rules).ToList();
            materialSelectionPath = "keyword-only (numeric OptiFine tile deferred ML)";
        }
        else if (heuristicMaterialIds.Count > 0)
        {
            semanticRawIds = heuristicMaterialIds;
            materialSelectionPath = "keyword-only (material keyword hit)";
        }
        else
        {
            semanticRawIds = matcher.Match(
                name,
                ruleKey,
                materialRules,
                sem.MinSimilarity,
                sem.MaxTags,
                sem.CertaintyThreshold,
                sem.AdditionalTagMaxGapFromBest,
                includeDictionaryEvidence && sem.DictionaryEvidenceEnabled,
                sem.DictionaryProvider,
                sem.DictionaryEvidenceWeight,
                sem.DictionaryMinEvidenceScore,
                sem.DictionaryRequestTimeoutMs,
                sem.DictionaryLanguageCode).ToList();
            usedSemanticMl = true;
            materialSelectionPath = "MiniLM semantic (fused score ranking)";
            semanticReport = GetSemanticMatchDebugReport(archivePath);
        }

        var postProcessedMaterialIds = MaterialTagMlPostProcessor.Apply(
            name,
            ruleKey,
            semanticRawIds,
            materialRules,
            sem.MaxTags);
        var postAdded = postProcessedMaterialIds.Except(semanticRawIds, StringComparer.OrdinalIgnoreCase).ToList();
        var postRemoved = semanticRawIds.Except(postProcessedMaterialIds, StringComparer.OrdinalIgnoreCase).ToList();

        var autoFlagIds = FlagTagResolver.Resolve(name, ruleKey, flagRules).ToList();
        var flagsBeforeWeighted = autoFlagIds.ToList();
        MaterialTagSemanticResolution.AppendWeightedUnweightedFlags(autoFlagIds, sem, deferSemanticMl, usedSemanticMl);
        var weightedFlag = autoFlagIds.FirstOrDefault(id =>
            id.Equals(FlagTagResolver.WeightedId, StringComparison.OrdinalIgnoreCase) ||
            id.Equals(FlagTagResolver.UnweightedId, StringComparison.OrdinalIgnoreCase));

        var autoIds = postProcessedMaterialIds.Concat(autoFlagIds).ToList();
        var added = _tagAdded.GetValueOrDefault(storageKey);
        var removed = _tagRemoved.GetValueOrDefault(storageKey);
        var removedArr = removed is null ? [] : removed.ToList();
        var addedArr = added is null ? [] : added.ToList();
        var effectiveIds = autoIds
            .Except(removedArr, StringComparer.OrdinalIgnoreCase)
            .Union(addedArr, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if ((effectiveIds.Contains("organic", StringComparer.OrdinalIgnoreCase) ||
             effectiveIds.Contains("plant", StringComparer.OrdinalIgnoreCase)) &&
            effectiveIds.Contains(FlagTagResolver.BlockId, StringComparer.OrdinalIgnoreCase) &&
            !name.Contains("block", StringComparison.OrdinalIgnoreCase))
        {
            effectiveIds = effectiveIds
                .Where(id => !id.Equals(FlagTagResolver.BlockId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        MaterialTagSemanticResolution.AppendTwoDSpriteFlagIfNeeded(effectiveIds, removed);

        var inheritedAdded = new List<string>();
        if (isNumericOptifineTile && includeDictionaryEvidence)
        {
            var parentRuleKey = GetParentRuleRelativeKey(ruleKey);
            if (!string.IsNullOrEmpty(parentRuleKey))
            {
                var inheritedMaterialIds = GetOrComputeOptifineFolderMaterialHintIds(parentRuleKey, rules, sem);
                HashSet<string>? removedSet = null;
                if (removed is { Count: > 0 })
                {
                    removedSet = new HashSet<string>(removed, StringComparer.OrdinalIgnoreCase);
                }

                foreach (var inheritedId in inheritedMaterialIds)
                {
                    if (removedSet is not null && removedSet.Contains(inheritedId))
                    {
                        continue;
                    }

                    if (!effectiveIds.Contains(inheritedId, StringComparer.OrdinalIgnoreCase))
                    {
                        effectiveIds.Add(inheritedId);
                        inheritedAdded.Add(inheritedId);
                    }
                }
            }
        }

        var finalMaterials = effectiveIds
            .Where(id => rulesById.TryGetValue(id, out var rule) && rule.Kind == TagRuleKind.Material)
            .ToList();
        var finalFlags = effectiveIds
            .Where(id => rulesById.TryGetValue(id, out var rule) && rule.Kind == TagRuleKind.Flag)
            .ToList();

        return SemanticTagDebugReportBuilder.Build(new SemanticTagDebugRenderModel
        {
            FileName = Path.GetFileName(archivePath),
            RuleKey = ruleKey,
            Query = query,
            SemanticEnabled = sem.Enabled,
            DictionaryEvidenceEnabled = sem.DictionaryEvidenceEnabled,
            DictionaryEvidenceWeight = sem.DictionaryEvidenceWeight,
            MinSimilarity = sem.MinSimilarity,
            CertaintyThreshold = sem.CertaintyThreshold,
            AdditionalTagMaxGapFromBest = sem.AdditionalTagMaxGapFromBest,
            MaxTags = sem.MaxTags,
            IsNumericOptifineTile = isNumericOptifineTile,
            MaterialSelectionPath = materialSelectionPath,
            HeuristicMaterialIds = heuristicMaterialIds,
            SemanticReport = semanticReport,
            SemanticRawIds = semanticRawIds,
            PostProcessedMaterialIds = postProcessedMaterialIds,
            PostAdded = postAdded,
            PostRemoved = postRemoved,
            FlagsBeforeWeighted = flagsBeforeWeighted,
            WeightedFlag = weightedFlag,
            ManualAdded = addedArr,
            ManualRemoved = removedArr,
            InheritedAdded = inheritedAdded,
            FinalMaterials = finalMaterials,
            FinalFlags = finalFlags,
            RulesById = rulesById
        });
    }

    /// <summary>Returns manual tag add/remove by texture key for conversion. Key = RelativeKey format (e.g. \minecraft\block\stone_brick).</summary>
    public IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)> GetManualTagOverrides()
    {
        var keys = new HashSet<string>(_tagAdded.Keys.Union(_tagRemoved.Keys), StringComparer.OrdinalIgnoreCase);
        var dict = new Dictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var added = _tagAdded.TryGetValue(key, out var a) ? (IReadOnlyList<string>)a.ToList() : [];
            var removed = _tagRemoved.TryGetValue(key, out var r) ? (IReadOnlyList<string>)r.ToList() : [];
            dict[key] = (added, removed);
        }

        return dict;
    }

    public ArchiveNode? FindNodeByFullPath(string fullPath)
    {
        if (Root is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(fullPath))
        {
            return Root;
        }

        var current = Root;
        var segments = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var host = (IArchiveNodeHost)this;
        foreach (var segment in segments)
        {
            host.EnsureChildrenLoaded(current);
            ArchiveNode? next = null;
            foreach (var child in current.Children)
            {
                if (child.Name.Equals(segment, StringComparison.OrdinalIgnoreCase))
                {
                    next = child;
                    break;
                }
            }
            if (next is null)
            {
                return null;
            }

            current = next;
        }
        return current;
    }

    public static ArchiveNode? FindChildByName(ArchiveNode parent, string name)
    {
        foreach (var c in parent.Children)
        {
            if (c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }
        return null;
    }

    public void Dispose()
    {
        PersistEffectiveTagCache();
        _tagRefreshCts?.Cancel();
        _refreshDisplayTagsDebounceCts?.Cancel();
        _tagRefreshCts?.Dispose();
        _refreshDisplayTagsDebounceCts?.Dispose();
        _tagComputeConcurrency.Dispose();
    }

    private string ComputeEffectiveTagCacheSignature()
    {
        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var sem = _materialTagSemanticOptionsProvider?.Invoke();
        var manualOverrides = new Dictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var key in _tagAdded.Keys.Union(_tagRemoved.Keys))
        {
            IReadOnlyList<string> added = [];
            if (_tagAdded.TryGetValue(key, out var addSet))
            {
                lock (addSet)
                {
                    added = addSet.ToList();
                }
            }

            IReadOnlyList<string> removed = [];
            if (_tagRemoved.TryGetValue(key, out var remSet))
            {
                lock (remSet)
                {
                    removed = remSet.ToList();
                }
            }

            manualOverrides[key] = (added, removed);
        }

        return SharedEffectiveTagsCacheSignature.Compute(rules, sem, manualOverrides);
    }

    private void TryLoadEffectiveTagsCache()
    {
        if (string.IsNullOrWhiteSpace(_scannedArchivePath))
        {
            return;
        }

        var snapshot = SharedEffectiveTagsCachePersistence.Load(_scannedArchivePath);
        if (snapshot is null)
        {
            _debugSink?.Invoke("Explore cache: no persisted effective-tag cache found.");
            return;
        }

        var signature = ComputeEffectiveTagCacheSignature();
        if (!string.Equals(snapshot.Signature, signature, StringComparison.Ordinal))
        {
            _debugSink?.Invoke("Explore cache: signature mismatch, persisted cache ignored.");
            return;
        }

        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        foreach (var kv in snapshot.EffectiveTagIdsByStorageKey)
        {
            var tuples = new List<(string Id, string DisplayName, TagRuleKind Kind)>();
            foreach (var id in kv.Value)
            {
                var rule = rules.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
                if (rule is not null)
                {
                    tuples.Add((rule.Id, rule.DisplayName, rule.Kind));
                }
            }

            var archivePath = StorageKeyToArchivePath(kv.Key);
            if (archivePath is not null)
            {
                _effectiveTagCache[archivePath] = tuples;
            }

            _effectiveTagIdsByStorageKey[kv.Key] = kv.Value;
        }

        _debugSink?.Invoke($"Explore cache: loaded {_effectiveTagIdsByStorageKey.Count} effective-tag entries.");
    }

    private void PersistEffectiveTagCache()
    {
        if (string.IsNullOrWhiteSpace(_scannedArchivePath))
        {
            return;
        }

        SharedEffectiveTagsCachePersistence.Save(
            _scannedArchivePath,
            ComputeEffectiveTagCacheSignature(),
            _effectiveTagIdsByStorageKey);
    }

    private string? StorageKeyToArchivePath(string storageKey)
    {
        if (Data is null)
        {
            return null;
        }

        if (Data.IsBatch)
        {
            var split = storageKey.IndexOf('|');
            if (split <= 0)
            {
                return null;
            }

            var packRoot = storageKey[..split];
            var key = storageKey[(split + 1)..];
            var archive = TextureKeyToArchivePath(key);
            return archive is null ? null : packRoot + "/" + archive;
        }

        return TextureKeyToArchivePath(storageKey);
    }

    private static string? TextureKeyToArchivePath(string key)
    {
        var normalized = key.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return "assets/" + normalized + ".png";
    }
}
