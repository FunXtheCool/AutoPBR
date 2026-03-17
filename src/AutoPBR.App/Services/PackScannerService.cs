using AutoPBR.App.Models;

namespace AutoPBR.App.Services;

/// <summary>Builds a lightweight index of a zip/jar archive (path → immediate children, file count). Only .png files are indexed.</summary>
internal static class PackScannerService
{
    /// <summary>Build a lightweight index (path → immediate children) and file count. Only .png files are indexed; only entry names are read.</summary>
    public static ScannedArchiveData BuildArchiveIndex(string zipPath,
        IProgress<(int completed, int total)>? progress = null)
    {
        var childLists = new Dictionary<string, List<ArchiveChildEntry>>(StringComparer.OrdinalIgnoreCase);
        var fileCount = 0;
        using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
        var entries = zip.Entries.ToList();
        var total = entries.Count;
        var completed = 0;
        foreach (var entry in entries)
        {
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
                progress?.Report((completed, total));
                completed++;
                continue;
            }

            var segments = full.Split('/');
            var current = "";
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var isLast = i == segments.Length - 1;
                var isFile = isLast && !isEntryFolder;
                var path = current.Length == 0 ? segment : current + "/" + segment;
                var parentPath = current;
                if (!childLists.TryGetValue(parentPath, out var siblingList))
                {
                    siblingList = new List<ArchiveChildEntry>();
                    childLists[parentPath] = siblingList;
                }

                if (siblingList.Exists(c => c.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                {
                    current = path;
                    continue;
                }

                siblingList.Add(new ArchiveChildEntry(segment, path, !isFile));
                if (isFile)
                {
                    fileCount++;
                }

                current = path;
            }

            progress?.Report((completed, total));
            completed++;
        }

        var index = childLists.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<ArchiveChildEntry>)kv.Value.AsReadOnly(),
            StringComparer.OrdinalIgnoreCase);
        return new ScannedArchiveData(index, fileCount);
    }
}
