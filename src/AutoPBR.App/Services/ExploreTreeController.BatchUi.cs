using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AutoPBR.App.Services;

internal sealed partial class ExploreTreeController
{
    /// <summary>True when the explorer has a batch index for this folder (same path as last batch scan).</summary>
    public bool HaveBatchScanForFolder(string? batchFolderPath) =>
        Data?.IsBatch == true &&
        !string.IsNullOrWhiteSpace(batchFolderPath) &&
        !string.IsNullOrWhiteSpace(Data.BatchFolderPath) &&
        string.Equals(Path.GetFullPath(Data.BatchFolderPath!), Path.GetFullPath(batchFolderPath), StringComparison.OrdinalIgnoreCase);

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
}
