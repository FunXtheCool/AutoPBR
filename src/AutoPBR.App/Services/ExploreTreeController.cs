using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AutoPBR.App.Lang;
using AutoPBR.App.Models;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Services;

/// <summary>Owns scanned archive data, path overrides, folder visibility cache, and the explore tree root. Implements <see cref="IArchiveNodeHost"/> for lazy loading and override storage.</summary>
internal sealed class ExploreTreeController : IArchiveNodeHost
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

    public ArchiveNode? Root { get; private set; }
    public ScannedArchiveData? Data { get; private set; }

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
        var snapshot = TagOverridesPersistence.Load(packPath);
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

        Root = new ArchiveNode("", "", true, null, this);
        ((IArchiveNodeHost)this).EnsureChildrenLoaded(Root);
        return Root;
    }

    /// <summary>Clear all state; call when user clears archive or starts a new scan.</summary>
    public void Clear()
    {
        ClearBatchPackIconCache();
        Root = null;
        Data = null;
        _scannedArchivePath = null;
        _pathOverrides.Clear();
        _tagAdded.Clear();
        _tagRemoved.Clear();
        _folderVisibilityCache.Clear();
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

        ApplyExploreFilterInternal();
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

    private void ApplyExploreFilterInternal()
    {
        if (Root is null)
        {
            return;
        }

        var f = _exploreFilter.Trim();
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
                var tags = ((IArchiveNodeHost)this).GetEffectiveTags(node.FullPath);
                var hasTag = tags.Any(t => string.Equals(t.Id, _exploreTagFilterId, StringComparison.OrdinalIgnoreCase));
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

    IReadOnlyList<(string Id, string DisplayName)> IArchiveNodeHost.GetEffectiveTags(string archivePath)
    {
        var storageKey = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(storageKey))
        {
            return [];
        }

        var ruleKey = RuleRelativeKeyFromStorageKey(storageKey);
        var name = Path.GetFileNameWithoutExtension(archivePath);
        var rules = _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;
        var autoIds = TagRulePresets.GetMatchingTagIds(name, ruleKey, rules);
        var added = _tagAdded.GetValueOrDefault(storageKey);
        var removed = _tagRemoved.GetValueOrDefault(storageKey);
        var effectiveIds = autoIds.Except(removed ?? []).Union(added ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<(string Id, string DisplayName)>();
        foreach (var rule in rules)
        {
            if (effectiveIds.Contains(rule.Id, StringComparer.OrdinalIgnoreCase))
            {
                result.Add((rule.Id, rule.DisplayName));
            }
        }

        return result;
    }

    void IArchiveNodeHost.SetTagRemoved(string archivePath, string tagId)
    {
        var key = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        var set = _tagRemoved.GetOrAdd(key, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        lock (set) { set.Add(tagId); }
        PersistTagOverrides();
        var node = FindNodeByFullPath(archivePath);
        node?.RefreshDisplayTags();
    }

    void IArchiveNodeHost.SetTagAdded(string archivePath, string tagId)
    {
        var key = ResolveTagStorageKey(archivePath);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        var set = _tagAdded.GetOrAdd(key, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        lock (set) { set.Add(tagId); }
        PersistTagOverrides();
        var node = FindNodeByFullPath(archivePath);
        node?.RefreshDisplayTags();
    }

    private void PersistTagOverrides()
    {
        if (string.IsNullOrEmpty(_scannedArchivePath))
        {
            return;
        }

        TagOverridesPersistence.Save(_scannedArchivePath, GetManualTagOverrides());
    }

    /// <summary>Refresh displayed tags on all loaded nodes (e.g. after tag rules changed).</summary>
    public void RefreshAllDisplayTags()
    {
        RefreshAllDisplayTagsRecursive(Root);
    }

    /// <summary>Clear all manual tag add/remove for the current pack, persist, and refresh displayed tags.</summary>
    public void ClearTagOverridesForCurrentPack()
    {
        _tagAdded.Clear();
        _tagRemoved.Clear();
        PersistTagOverrides();
        RefreshAllDisplayTagsRecursive(Root);
    }

    private static void RefreshAllDisplayTagsRecursive(ArchiveNode? node)
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
            RefreshAllDisplayTagsRecursive(child);
        }
    }

    IReadOnlyList<TagRule> IArchiveNodeHost.GetTagRules() => _tagRulesProvider?.Invoke() ?? TagRulePresets.Default;

    /// <summary>Set the provider that returns effective tag rules (built-in + custom). Called when custom rules may have changed.</summary>
    public void SetTagRulesProvider(Func<IReadOnlyList<TagRule>>? provider)
    {
        _tagRulesProvider = provider;
    }

    string IArchiveNodeHost.GetTagMenuHeader(string displayName, bool isApplied) =>
        isApplied ? string.Format(Resources.TagMenuDontApply, displayName) : string.Format(Resources.TagMenuApply, displayName);

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
}
