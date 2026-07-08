using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>Writes a zip file with parallel deflate compression and bounded memory use.</summary>
internal static class ParallelZipWriter
{
    private const uint LocalFileHeaderSignature = 0x04034b50;
    private const uint CentralFileHeaderSignature = 0x02014b50;
    private const uint EndOfCentralDirSignature = 0x06054b50;
    private const uint Zip64EndOfCentralDirSignature = 0x06064b50;
    private const uint Zip64EndOfCentralDirLocatorSignature = 0x07064b50;
    private const ushort CompressionMethodDeflate = 8;
    private const ushort VersionNeeded = 20;
    private const ushort Zip64VersionNeeded = 45;
    private const ushort Zip64ExtraId = 0x0001;
    private const uint Zip32Max = 0xFFFFFFFF;
    private const ushort Zip16Max = 0xFFFF;

    private sealed record PreparedEntry(
        string RelativePath,
        uint Crc32,
        long UncompressedSize,
        long CompressedSize,
        string CompressedPath);

    public static void WriteZip(
        string outputPath,
        IReadOnlyList<string> files,
        string basePath,
        AutoPBROptions options,
        IProgress<ConversionProgress>? progress,
        ConversionStage stage,
        CancellationToken cancellationToken)
    {
        var total = files.Count;
        progress?.Report(new ConversionProgress(stage, 0, total));

        var tempRoot = Path.Combine(Path.GetTempPath(), "AutoPBR", "zip_compress", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var degree = Math.Max(1, Math.Min(ThreadingUtil.GetZipParallelism(options), Math.Max(1, files.Count)));
            var entries = new PreparedEntry[files.Count];
            var completed = 0;

            Parallel.For(0, files.Count, new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cancellationToken }, i =>
            {
                ThreadingUtil.SetThreadName("AutoPBR.Pack");
                cancellationToken.ThrowIfCancellationRequested();
                var fullPath = files[i];
                var relativePath = Path.GetRelativePath(basePath, fullPath).Replace('\\', '/');
                var compressedPath = Path.Combine(
                    tempRoot,
                    i.ToString("D8", System.Globalization.CultureInfo.InvariantCulture) + ".deflate");
                var (crc32, uncompressedSize, compressedSize) = CompressToFile(fullPath, compressedPath, cancellationToken);
                entries[i] = new PreparedEntry(relativePath, crc32, uncompressedSize, compressedSize, compressedPath);
                var n = Interlocked.Increment(ref completed);
                progress?.Report(new ConversionProgress(stage, n, total));
            });

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var centralDirEntries = new List<(PreparedEntry Entry, long LocalHeaderOffset)>(entries.Length);

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var localHeaderOffset = fs.Position;
                WriteLocalFileHeader(fs, entry);
                using (var compressed = new FileStream(entry.CompressedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    compressed.CopyTo(fs);
                }

                centralDirEntries.Add((entry, localHeaderOffset));
            }

            var centralDirOffset = fs.Position;
            foreach (var (entry, localHeaderOffset) in centralDirEntries)
            {
                WriteCentralFileHeader(fs, entry, localHeaderOffset);
            }

            var centralDirSize = fs.Position - centralDirOffset;
            var needsZip64 =
                centralDirEntries.Count >= Zip16Max ||
                centralDirOffset > Zip32Max ||
                centralDirSize > Zip32Max ||
                centralDirEntries.Any(static e =>
                    e.LocalHeaderOffset > Zip32Max ||
                    e.Entry.CompressedSize > Zip32Max ||
                    e.Entry.UncompressedSize > Zip32Max);

            if (needsZip64)
            {
                WriteZip64EndOfCentralDirectory(fs, centralDirEntries.Count, centralDirSize, centralDirOffset);
            }

            WriteEndOfCentralDirectory(fs, centralDirEntries.Count, centralDirSize, centralDirOffset, needsZip64);
            fs.Flush(flushToDisk: true);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best-effort temp cleanup.
            }
        }
    }

    private static (uint Crc32, long UncompressedSize, long CompressedSize) CompressToFile(
        string sourcePath,
        string compressedPath,
        CancellationToken cancellationToken)
    {
        var crc = new Crc32();
        long uncompressedSize = 0;
        var buffer = new byte[128 * 1024];
        using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var output = new FileStream(compressedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var n = input.Read(buffer, 0, buffer.Length);
                if (n <= 0)
                {
                    break;
                }

                crc.Append(buffer.AsSpan(0, n));
                deflate.Write(buffer, 0, n);
                uncompressedSize += n;
            }
        }

        var crcBytes = crc.GetCurrentHash();
        var crc32 = BinaryPrimitives.ReadUInt32LittleEndian(crcBytes);
        var compressedSize = new FileInfo(compressedPath).Length;
        return (crc32, uncompressedSize, compressedSize);
    }

    private static void WriteLocalFileHeader(Stream s, PreparedEntry entry)
    {
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(entry.RelativePath);
        var needsZip64 = entry.CompressedSize > Zip32Max || entry.UncompressedSize > Zip32Max;
        var (time, date) = GetDosDateTime(DateTimeOffset.Now);
        WriteU32(s, LocalFileHeaderSignature);
        WriteU16(s, needsZip64 ? Zip64VersionNeeded : VersionNeeded);
        WriteU16(s, 0);
        WriteU16(s, CompressionMethodDeflate);
        WriteU16(s, time);
        WriteU16(s, date);
        WriteU32(s, entry.Crc32);
        WriteU32(s, needsZip64 ? Zip32Max : (uint)entry.CompressedSize);
        WriteU32(s, needsZip64 ? Zip32Max : (uint)entry.UncompressedSize);
        WriteU16(s, checked((ushort)nameBytes.Length));
        WriteU16(s, needsZip64 ? (ushort)20 : (ushort)0);
        s.Write(nameBytes, 0, nameBytes.Length);
        if (needsZip64)
        {
            WriteU16(s, Zip64ExtraId);
            WriteU16(s, 16);
            WriteU64(s, (ulong)entry.UncompressedSize);
            WriteU64(s, (ulong)entry.CompressedSize);
        }
    }

    private static void WriteCentralFileHeader(Stream s, PreparedEntry entry, long localHeaderOffset)
    {
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(entry.RelativePath);
        var needsZip64 =
            entry.CompressedSize > Zip32Max ||
            entry.UncompressedSize > Zip32Max ||
            localHeaderOffset > Zip32Max;
        var (time, date) = GetDosDateTime(DateTimeOffset.Now);
        WriteU32(s, CentralFileHeaderSignature);
        WriteU16(s, needsZip64 ? Zip64VersionNeeded : VersionNeeded);
        WriteU16(s, needsZip64 ? Zip64VersionNeeded : VersionNeeded);
        WriteU16(s, 0);
        WriteU16(s, CompressionMethodDeflate);
        WriteU16(s, time);
        WriteU16(s, date);
        WriteU32(s, entry.Crc32);
        WriteU32(s, entry.CompressedSize > Zip32Max ? Zip32Max : (uint)entry.CompressedSize);
        WriteU32(s, entry.UncompressedSize > Zip32Max ? Zip32Max : (uint)entry.UncompressedSize);
        WriteU16(s, checked((ushort)nameBytes.Length));
        WriteU16(s, needsZip64 ? (ushort)28 : (ushort)0);
        WriteU16(s, 0);
        WriteU16(s, 0);
        WriteU16(s, 0);
        WriteU32(s, 0u);
        WriteU32(s, localHeaderOffset > Zip32Max ? Zip32Max : (uint)localHeaderOffset);
        s.Write(nameBytes, 0, nameBytes.Length);
        if (needsZip64)
        {
            WriteU16(s, Zip64ExtraId);
            WriteU16(s, 24);
            WriteU64(s, (ulong)entry.UncompressedSize);
            WriteU64(s, (ulong)entry.CompressedSize);
            WriteU64(s, (ulong)localHeaderOffset);
        }
    }

    private static void WriteZip64EndOfCentralDirectory(
        Stream s,
        int entryCount,
        long centralDirSize,
        long centralDirOffset)
    {
        var zip64Offset = s.Position;
        WriteU32(s, Zip64EndOfCentralDirSignature);
        WriteU64(s, 44);
        WriteU16(s, Zip64VersionNeeded);
        WriteU16(s, Zip64VersionNeeded);
        WriteU32(s, 0);
        WriteU32(s, 0);
        WriteU64(s, (ulong)entryCount);
        WriteU64(s, (ulong)entryCount);
        WriteU64(s, (ulong)centralDirSize);
        WriteU64(s, (ulong)centralDirOffset);

        WriteU32(s, Zip64EndOfCentralDirLocatorSignature);
        WriteU32(s, 0);
        WriteU64(s, (ulong)zip64Offset);
        WriteU32(s, 1);
    }

    private static void WriteEndOfCentralDirectory(
        Stream s,
        int entryCount,
        long centralDirSize,
        long centralDirOffset,
        bool zip64)
    {
        WriteU32(s, EndOfCentralDirSignature);
        WriteU16(s, 0);
        WriteU16(s, 0);
        WriteU16(s, zip64 || entryCount >= Zip16Max ? Zip16Max : (ushort)entryCount);
        WriteU16(s, zip64 || entryCount >= Zip16Max ? Zip16Max : (ushort)entryCount);
        WriteU32(s, zip64 || centralDirSize > Zip32Max ? Zip32Max : (uint)centralDirSize);
        WriteU32(s, zip64 || centralDirOffset > Zip32Max ? Zip32Max : (uint)centralDirOffset);
        WriteU16(s, 0);
    }

    private static (ushort time, ushort date) GetDosDateTime(DateTimeOffset dt)
    {
        var time = (ushort)((dt.Hour << 11) | (dt.Minute << 5) | (dt.Second / 2));
        var date = (ushort)(((dt.Year - 1980) << 9) | (dt.Month << 5) | dt.Day);
        return (time, date);
    }

    private static void WriteU16(Stream s, ushort value)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, value);
        s.Write(b);
    }

    private static void WriteU32(Stream s, uint value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        s.Write(b);
    }

    private static void WriteU64(Stream s, ulong value)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(b, value);
        s.Write(b);
    }
}
