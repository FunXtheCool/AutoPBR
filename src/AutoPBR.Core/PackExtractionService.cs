using System.IO.Compression;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// Handles extracting whole packs or individual entries with progress reporting and cancellation.
/// </summary>
internal static class PackExtractionService
{
    public static void ExtractPack(
        string inputZipPath,
        string extracted,
        AutoPbrOptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ThreadingUtil.SetThreadName("AutoPBR.Extract");
        List<string> entryNames;
        var extractOnly = options.EntriesToExtractOnly;
        if (extractOnly is { Count: > 0 })
        {
            entryNames = extractOnly.ToList();
        }
        else
        {
            using var archive = ZipFile.OpenRead(inputZipPath);
            entryNames = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Select(e => e.FullName).ToList();
        }

        var total = entryNames.Count;
        progress?.Report(new ConversionProgress(ConversionStage.Extracting, 0, total));

        var completed = 0;
        var lastReported = -1;
        var reportLock = new object();

        void ReportProgress()
        {
            var current = Interlocked.Increment(ref completed);
            lock (reportLock)
            {
                if (current <= total && current > lastReported)
                {
                    lastReported = current;
                    progress?.Report(new ConversionProgress(ConversionStage.Extracting, current, total));
                }
            }
        }

        var degree = Math.Min(ThreadingUtil.GetZipParallelism(options), entryNames.Count);
        if (degree <= 1)
        {
            using var archive = ZipFile.OpenRead(inputZipPath);
            foreach (var fullName in entryNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = archive.GetEntry(fullName);
                if (entry is null) continue;
                var destPath = Path.Combine(extracted, fullName);
                var dir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                if (!string.IsNullOrEmpty(entry.Name))
                    entry.ExtractToFile(destPath, overwrite: true);
                ReportProgress();
            }

            return;
        }

        var partitionSize = (entryNames.Count + degree - 1) / degree;
        var partitions = new List<List<string>>(degree);
        for (var i = 0; i < degree; i++)
        {
            var start = i * partitionSize;
            var count = Math.Min(partitionSize, entryNames.Count - start);
            if (count > 0)
                partitions.Add(entryNames.GetRange(start, count));
        }

        Parallel.ForEach(
            partitions,
            new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cancellationToken },
            partition =>
            {
                ThreadingUtil.SetThreadName("AutoPBR.Extract");
                using var archive = ZipFile.OpenRead(inputZipPath);
                foreach (var fullName in partition)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = archive.GetEntry(fullName);
                    if (entry is null) continue;
                    var destPath = Path.Combine(extracted, fullName);
                    var dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    if (!string.IsNullOrEmpty(entry.Name))
                        entry.ExtractToFile(destPath, overwrite: true);
                    ReportProgress();
                }
            });
    }

    public static void ExtractEntry(
        string inputZipPath,
        string archivePath,
        string extractedRoot)
    {
        using var archive = ZipFile.OpenRead(inputZipPath);
        var entry = archive.GetEntry(archivePath)
                    ?? throw new FileNotFoundException("Texture entry not found in pack.", archivePath);

        var destPath = Path.Combine(extractedRoot, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        if (!string.IsNullOrEmpty(entry.Name))
            entry.ExtractToFile(destPath, overwrite: true);
    }
}

