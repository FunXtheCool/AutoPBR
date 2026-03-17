using System.Collections.Concurrent;
using AutoPBR.App.Models;

namespace AutoPBR.App.Services;

/// <summary>Owns scanned archive data, path overrides, folder visibility cache, and the explore tree root. Implements <see cref="IArchiveNodeHost"/> for lazy loading and override storage.</summary>
internal sealed class ExploreTreeController : IArchiveNodeHost
{
    private static readonly HashSet<string> TextureTypeFolderNames = new(StringComparer.OrdinalIgnoreCase)
        { "block", "blocks", "item", "items", "entity", "particle" };

    private static readonly HashSet<string> IgnoredOptifineFolders = new(StringComparer.OrdinalIgnoreCase)
        { "anim", "colormap", "sky" };

    private ScannedArchiveData? _data;
    private string? _scannedArchivePath;
    private readonly ConcurrentDictionary<string, bool?> _pathOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _folderVisibilityCache = new(StringComparer.OrdinalIgnoreCase);
    private ArchiveNode? _root;
    private string _exploreFilter = "";

    public ArchiveNode? Root => _root;
    public ScannedArchiveData? Data => _data;

    /// <summary>Set scanned data and build the tree root. Call on UI thread. Returns the new root (also assign to VM's ScannedArchiveRoot).</summary>
    public ArchiveNode SetData(ScannedArchiveData data, string packPath)
    {
        _data = data;
        _scannedArchivePath = packPath;
        _root = new ArchiveNode("", "", true, null, this);
        ((IArchiveNodeHost)this).EnsureChildrenLoaded(_root);
        return _root;
    }

    /// <summary>Clear all state; call when user clears archive or starts a new scan.</summary>
    public void Clear()
    {
        _root = null;
        _data = null;
        _scannedArchivePath = null;
        _pathOverrides.Clear();
        _folderVisibilityCache.Clear();
    }

    public bool HaveScanForCurrentPack(string? packPath) =>
        _data is not null && _scannedArchivePath is not null && !string.IsNullOrEmpty(packPath) &&
        string.Equals(Path.GetFullPath(_scannedArchivePath), Path.GetFullPath(packPath), StringComparison.OrdinalIgnoreCase);

    bool? IArchiveNodeHost.GetOverride(string fullPath) =>
        _pathOverrides.GetValueOrDefault(fullPath);

    void IArchiveNodeHost.SetOverride(string fullPath, bool? value)
    {
        if (value.HasValue)
            _pathOverrides[fullPath] = value;
        else
            _pathOverrides.TryRemove(fullPath, out _);
    }

    void IArchiveNodeHost.EnsureChildrenLoaded(ArchiveNode node)
    {
        if (_data is null || node.Children.Count > 0)
            return;
        var children = _data.GetChildren(node.FullPath);
        if (children is null)
            return;
        foreach (var entry in children)
        {
            if (entry.IsFolder)
            {
                if (IsIgnoredOptifineFolder(entry.FullPath))
                    continue;
                if (!HasVisiblePngUnder(entry.FullPath))
                    continue;
            }
            else
            {
                if (GetEffectiveOverrideForPath(entry.FullPath) == false)
                    continue;
            }

            var child = new ArchiveNode(entry.Name, entry.FullPath, entry.IsFolder, node, this);
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
                return v;
            var slash = path.LastIndexOf('/');
            if (slash < 0)
                break;
            path = path[..slash];
        }
        return null;
    }

    public void ApplyExploreOverridesToIgnoreSet(HashSet<string> ignore)
    {
        if (_data is null)
            return;
        foreach (var fullPath in _data.EnumerateAllFilePaths())
        {
            var key = ArchivePathToTextureKey(fullPath);
            if (key is null)
                continue;
            var effective = GetEffectiveOverrideForPath(fullPath);
            if (!effective.HasValue)
                continue;
            if (effective.Value)
                ignore.Remove(key);
            else
                ignore.Add(key);
        }
    }

    public static string? ArchivePathToTextureKey(string fullPath)
    {
        var parts = fullPath.Replace('\\', '/').Split('/');
        if (parts.Length < 4 || !parts[0].Equals("assets", StringComparison.OrdinalIgnoreCase))
            return null;
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
        if (_data is null)
            return seen;
        foreach (var fullPath in _data.EnumerateAllFilePaths())
        {
            var segments = fullPath.Split('/');
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (!segments[i].Equals("textures", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (i + 1 >= segments.Length)
                    continue;
                var typeName = segments[i + 1];
                if (!TextureTypeFolderNames.Contains(typeName))
                    continue;
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
            return processBlocks;
        if (last.Equals("item", StringComparison.OrdinalIgnoreCase) || last.Equals("items", StringComparison.OrdinalIgnoreCase))
            return processItems;
        if (last.Equals("entity", StringComparison.OrdinalIgnoreCase))
            return processEntity;
        if (last.Equals("particle", StringComparison.OrdinalIgnoreCase))
            return processParticles;
        return true;
    }

    /// <summary>Warm up visibility cache for first few levels. Run on background thread.</summary>
    public void PrewarmFolderVisibilityCache(CancellationToken cancellationToken)
    {
        var data = _data;
        if (data is null)
            return;
        const int maxDepth = 3;
        var queue = new Queue<(string path, int depth)>();
        queue.Enqueue(("", 0));
        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (parent, depth) = queue.Dequeue();
            var children = data.GetChildren(parent);
            if (children is null)
                continue;
            foreach (var c in children)
            {
                if (!c.IsFolder)
                    continue;
                if (depth < maxDepth)
                    queue.Enqueue((c.FullPath, depth + 1));
                if (!_folderVisibilityCache.ContainsKey(c.FullPath))
                    _folderVisibilityCache[c.FullPath] = ComputeFolderVisible(data, c.FullPath);
            }
        }
    }

    /// <summary>Apply block/item/entity/particle overrides from process flags and refresh tree. Returns node to restore focus (same path) or null.</summary>
    public ArchiveNode? ApplyTextureTypeOverridesToExplore(string? previousFocusPath, bool processBlocks, bool processItems, bool processEntity, bool processParticles)
    {
        var paths = GetTextureTypeFolderPaths();
        if (paths.Count == 0)
            return null;
        _folderVisibilityCache.Clear();
        foreach (var path in paths)
        {
            var include = GetProcessValueForTextureFolder(path, processBlocks, processItems, processEntity, processParticles);
            _pathOverrides[path] = include;
        }
        NotifyOverrideChangedForPaths(paths);
        RefreshExploreTreeFilter();
        if (string.IsNullOrEmpty(previousFocusPath))
            return null;
        return FindNodeByFullPath(previousFocusPath);
    }

    public void RefreshExploreTreeFilter()
    {
        if (_root is null)
            return;
        ClearChildrenRecursive(_root);
        ((IArchiveNodeHost)this).EnsureChildrenLoaded(_root);
        ApplyExploreFilterInternal();
    }

    public void ApplyExploreFilter(string? filter)
    {
        _exploreFilter = filter ?? "";
        ApplyExploreFilterInternal();
    }

    private void ApplyExploreFilterInternal()
    {
        if (_root is null)
            return;
        var f = _exploreFilter.Trim();
        ApplyExploreFilterRecursive(_root, f);
    }

    private static bool ApplyExploreFilterRecursive(ArchiveNode node, string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            node.IsVisibleByFilter = true;
            foreach (var child in node.Children)
                ApplyExploreFilterRecursive(child, filter);
            return true;
        }
        bool selfMatch = node.FullPath.Contains(filter, StringComparison.OrdinalIgnoreCase)
                         || node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        bool anyChildVisible = false;
        foreach (var child in node.Children)
        {
            if (ApplyExploreFilterRecursive(child, filter))
                anyChildVisible = true;
        }
        node.IsVisibleByFilter = selfMatch || anyChildVisible;
        return node.IsVisibleByFilter;
    }

    private static void ClearChildrenRecursive(ArchiveNode node)
    {
        foreach (var child in node.Children)
            ClearChildrenRecursive(child);
        node.Children.Clear();
    }

    private void NotifyOverrideChangedForPaths(HashSet<string> paths)
    {
        if (paths.Count == 0 || _root is null)
            return;
        NotifyOverrideChangedRecursive(_root, paths);
    }

    private static void NotifyOverrideChangedRecursive(ArchiveNode node, HashSet<string> paths)
    {
        if (paths.Contains(node.FullPath))
            node.NotifyOverrideChanged();
        foreach (var child in node.Children)
            NotifyOverrideChangedRecursive(child, paths);
    }

    private bool HasVisiblePngUnder(string folderPath)
    {
        if (_folderVisibilityCache.TryGetValue(folderPath, out var cached))
            return cached;
        if (_data is null)
            return false;
        var visible = ComputeFolderVisible(_data, folderPath);
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
                continue;
            foreach (var c in children)
            {
                if (c.IsFolder)
                    queue.Enqueue(c.FullPath);
                else if (GetEffectiveOverrideForPath(c.FullPath) != false)
                    return true;
            }
        }
        return false;
    }

    private static bool IsIgnoredOptifineFolder(string fullPath)
    {
        var segments = fullPath.Split('/');
        if (segments.Length < 4)
            return false;
        if (!segments[0].Equals("assets", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!segments[2].Equals("optifine", StringComparison.OrdinalIgnoreCase))
            return false;
        return IgnoredOptifineFolders.Contains(segments[3]);
    }

    public ArchiveNode? FindNodeByFullPath(string fullPath)
    {
        if (_root is null)
            return null;
        if (string.IsNullOrEmpty(fullPath))
            return _root;
        var current = _root;
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
                return null;
            current = next;
        }
        return current;
    }

    public static ArchiveNode? FindChildByName(ArchiveNode parent, string name)
    {
        foreach (var c in parent.Children)
        {
            if (c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }
}
