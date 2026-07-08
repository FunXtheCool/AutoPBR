using AutoPBR.App.Models;
using AutoPBR.Preview;

namespace AutoPBR.App.Services;

/// <summary>Builds a lightweight index of a zip/jar archive (path → immediate children, file count). Only .png files are indexed; only entry names are read.</summary>
internal static class PackScannerService
{
    /// <summary>Build a lightweight index (path → immediate children) and file count. Only .png files are indexed; only entry names are read.</summary>
    public static ScannedArchiveData BuildArchiveIndex(
        string zipPath,
        IProgress<(int completed, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var childLists = new Dictionary<string, List<ArchiveChildEntry>>(StringComparer.OrdinalIgnoreCase);
        var seenChildren = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var inventoryPaths = new List<string>();
        var fileCount = 0;
        AddZipToIndex(zipPath, pathPrefix: "", childLists, seenChildren, inventoryPaths, ref fileCount, progress, cancellationToken);
        var index = ToReadOnlyIndex(childLists);
        var inventory = ArchiveModelInventoryBuilder.BuildFromArchivePaths(inventoryPaths);
        return new ScannedArchiveData(index, fileCount, modelInventory: inventory);
    }

    /// <summary>Index all .zip/.jar files in <paramref name="directory"/> (non-recursive). Root shows one folder per pack; inner paths are <c>{packRoot}/assets/...</c>.</summary>
    public static ScannedArchiveData BuildBatchArchiveIndex(
        string directory,
        IProgress<(int completed, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(directory);
        }

        var packFiles = Directory.EnumerateFiles(directory)
            .Where(f =>
            {
                var e = Path.GetExtension(f);
                return e.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                       e.Equals(".jar", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var childLists = new Dictionary<string, List<ArchiveChildEntry>>(StringComparer.OrdinalIgnoreCase);
        var seenChildren = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var inventoryPaths = new List<string>();
        var rootList = new List<ArchiveChildEntry>();
        childLists[""] = rootList;
        var rootSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        seenChildren[""] = rootSeen;
        var batchMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fileCount = 0;

        foreach (var packPath in packFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var baseName = Path.GetFileName(packPath);
            var uniqueRoot = baseName;
            for (var i = 0; rootSeen.Contains(uniqueRoot); i++)
            {
                uniqueRoot = $"{i}_{baseName}";
            }

            batchMap[uniqueRoot] = packPath;
            rootList.Add(new ArchiveChildEntry(uniqueRoot, uniqueRoot, true));
            rootSeen.Add(uniqueRoot);
            AddZipToIndex(packPath, uniqueRoot, childLists, seenChildren, inventoryPaths, ref fileCount, progress, cancellationToken);
        }

        var index = ToReadOnlyIndex(childLists);
        var inventory = ArchiveModelInventoryBuilder.BuildFromArchivePaths(inventoryPaths);
        return new ScannedArchiveData(
            index,
            fileCount,
            isBatch: true,
            batchFolderPath: Path.GetFullPath(directory),
            batchPackRootToPath: batchMap,
            modelInventory: inventory);
    }

    private static Dictionary<string, IReadOnlyList<ArchiveChildEntry>> ToReadOnlyIndex(
        Dictionary<string, List<ArchiveChildEntry>> childLists) =>
        childLists.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<ArchiveChildEntry>)kv.Value.AsReadOnly(),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>Add zip entries under optional <paramref name="pathPrefix"/> (e.g. "" or "MyPack.zip").</summary>
    private static void AddZipToIndex(
        string zipPath,
        string pathPrefix,
        Dictionary<string, List<ArchiveChildEntry>> childLists,
        Dictionary<string, HashSet<string>> seenChildren,
        List<string> inventoryPaths,
        ref int fileCount,
        IProgress<(int completed, int total)>? progress,
        CancellationToken cancellationToken)
    {
        using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
        var entries = zip.Entries.ToList();
        var total = entries.Count;
        var completed = 0;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var full = entry.FullName.TrimEnd('/');
            if (string.IsNullOrEmpty(full))
            {
                progress?.Report((completed, total));
                completed++;
                continue;
            }

            var isEntryFolder = entry.FullName.EndsWith('/');
            if (!isEntryFolder && !full.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                if (full.Contains("/models/block/", StringComparison.OrdinalIgnoreCase) &&
                    full.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    inventoryPaths.Add(string.IsNullOrEmpty(pathPrefix) ? full : $"{pathPrefix}/{full}");
                }

                progress?.Report((completed, total));
                completed++;
                continue;
            }

            // LabPBR emissive maps (*_e.png): hide from explorer index (same idea as TextureScanner skipping _e stems).
            if (!isEntryFolder && full.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var slash = full.LastIndexOf('/');
                var namePart = slash >= 0 ? full[(slash + 1)..] : full;
                if (Path.GetFileNameWithoutExtension(namePart).EndsWith("_e", StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report((completed, total));
                    completed++;
                    continue;
                }
            }

            var segments = full.Split('/');
            var current = pathPrefix;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var isLast = i == segments.Length - 1;
                var isFile = isLast && !isEntryFolder;
                var path = string.IsNullOrEmpty(current) ? segment : current + "/" + segment;
                var parentPath = current;
                if (!childLists.TryGetValue(parentPath, out var siblingList))
                {
                    siblingList = new List<ArchiveChildEntry>();
                    childLists[parentPath] = siblingList;
                }
                if (!seenChildren.TryGetValue(parentPath, out var siblingSet))
                {
                    siblingSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    seenChildren[parentPath] = siblingSet;
                }

                if (siblingSet.Contains(path))
                {
                    current = path;
                    continue;
                }

                siblingList.Add(new ArchiveChildEntry(segment, path, !isFile));
                siblingSet.Add(path);
                if (isFile)
                {
                    fileCount++;
                    if (full.Contains("/textures/block/", StringComparison.OrdinalIgnoreCase))
                    {
                        inventoryPaths.Add(string.IsNullOrEmpty(pathPrefix) ? full : $"{pathPrefix}/{full}");
                    }
                }

                current = path;
            }

            progress?.Report((completed, total));
            completed++;
        }
    }
}
