namespace AutoPBR.App.Models;

/// <summary>Result of scanning an archive: child index (path → immediate children) and total file count. No full tree in memory.</summary>
public sealed class ScannedArchiveData(
    IReadOnlyDictionary<string, IReadOnlyList<ArchiveChildEntry>> childIndex,
    int fileCount,
    bool isBatch = false,
    string? batchFolderPath = null,
    IReadOnlyDictionary<string, string>? batchPackRootToPath = null)
{
    public IReadOnlyDictionary<string, IReadOnlyList<ArchiveChildEntry>> ChildIndex => childIndex;

    public int FileCount => fileCount;

    /// <summary>True when the index merges multiple packs under a folder (each top-level name maps to a .zip/.jar).</summary>
    public bool IsBatch => isBatch;

    /// <summary>Folder that was scanned for batch mode; used for tag persistence and scan identity.</summary>
    public string? BatchFolderPath => batchFolderPath;

    /// <summary>Top-level tree folder name (unique) → absolute path to that pack archive on disk.</summary>
    public IReadOnlyDictionary<string, string>? BatchPackRootToPath => batchPackRootToPath;

    /// <summary>Immediate children of <paramref name="parentPath"/> in the index, or null if the path is unknown.</summary>
    public IReadOnlyList<ArchiveChildEntry>? GetChildren(string parentPath)
    {
        return ChildIndex.GetValueOrDefault(parentPath);
    }

    /// <summary>Enumerate all file paths (not directories) in the archive by walking the index.</summary>
    public IEnumerable<string> EnumerateAllFilePaths()
    {
        var queue = new Queue<string>();
        queue.Enqueue("");
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            var children = GetChildren(parent);
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
                else
                {
                    yield return c.FullPath;
                }
            }
        }
    }
}
