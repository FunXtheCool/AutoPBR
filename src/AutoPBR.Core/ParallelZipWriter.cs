using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>Writes a zip file with parallel deflate compression (one thread per file up to ZipParallelism).</summary>
internal static class ParallelZipWriter
{
    private const uint LocalFileHeaderSignature = 0x04034b50;
    private const uint CentralFileHeaderSignature = 0x02014b50;
    private const uint EndOfCentralDirSignature = 0x06054b50;
    private const ushort CompressionMethodDeflate = 8;
    private const ushort VersionNeeded = 20;

    public static void WriteZip(
        string outputPath,
        IReadOnlyList<string> files,
        string basePath,
        AutoPbrOptions options,
        IProgress<ConversionProgress>? progress,
        ConversionStage stage,
        CancellationToken cancellationToken)
    {
        var total = files.Count;
        progress?.Report(new ConversionProgress(stage, 0, total));

        var degree = Math.Min(ThreadingUtil.GetZipParallelism(options), files.Count);
        var entries = new (string relativePath, uint crc32, int uncompressedSize, byte[] compressed)[files.Count];
        var completed = 0;

        Parallel.For(0, files.Count, new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cancellationToken }, i =>
        {
            ThreadingUtil.SetThreadName("AutoPBR.Pack");
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = files[i];
            var relativePath = Path.GetRelativePath(basePath, fullPath).Replace('\\', '/');
            var data = File.ReadAllBytes(fullPath);
            // Crc32.Hash returns four bytes in little-endian order (same uint layout as ZIP's LE CRC field).
            var crc32 = BinaryPrimitives.ReadUInt32LittleEndian(Crc32.Hash(data));
            var compressed = Compress(data);
            entries[i] = (relativePath, crc32, data.Length, compressed);
            var n = Interlocked.Increment(ref completed);
            progress?.Report(new ConversionProgress(stage, n, total));
        });

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var centralDirEntries = new List<(string name, uint crc32, int compressedSize, int uncompressedSize, long localHeaderOffset)>();

        foreach (var (relativePath, crc32, uncompressedSize, compressed) in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localHeaderOffset = fs.Position;
            WriteLocalFileHeader(fs, relativePath, crc32, compressed.Length, uncompressedSize);
            fs.Write(compressed, 0, compressed.Length);
            centralDirEntries.Add((relativePath, crc32, compressed.Length, uncompressedSize, localHeaderOffset));
        }

        var centralDirOffset = fs.Position;
        foreach (var (name, crc32, compressedSize, uncompressedSize, localHeaderOffset) in centralDirEntries)
        {
            WriteCentralFileHeader(fs, name, crc32, compressedSize, uncompressedSize, localHeaderOffset);
        }

        var centralDirSize = (int)(fs.Position - centralDirOffset);
        WriteEndOfCentralDirectory(fs, centralDirEntries.Count, centralDirSize, centralDirOffset);
        fs.Flush(flushToDisk: true);
    }

    private static byte[] Compress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }

        return ms.ToArray();
    }

    private static void WriteLocalFileHeader(Stream s, string fileName, uint crc32, int compressedSize, int uncompressedSize)
    {
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(fileName);
        var (time, date) = GetDosDateTime(DateTimeOffset.Now);
        WriteU32(s, LocalFileHeaderSignature);
        WriteU16(s, VersionNeeded);
        WriteU16(s, 0); // general purpose bit flags
        WriteU16(s, CompressionMethodDeflate);
        WriteU16(s, time);
        WriteU16(s, date);
        WriteU32(s, crc32);
        WriteU32(s, (uint)compressedSize);
        WriteU32(s, (uint)uncompressedSize);
        WriteU16(s, (ushort)nameBytes.Length);
        WriteU16(s, 0); // extra field length
        s.Write(nameBytes, 0, nameBytes.Length);
    }

    private static void WriteCentralFileHeader(Stream s, string fileName, uint crc32, int compressedSize, int uncompressedSize, long localHeaderOffset)
    {
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(fileName);
        var (time, date) = GetDosDateTime(DateTimeOffset.Now);
        WriteU32(s, CentralFileHeaderSignature);
        WriteU16(s, 0); // version made by
        WriteU16(s, VersionNeeded);
        WriteU16(s, 0); // general purpose bit flags
        WriteU16(s, CompressionMethodDeflate);
        WriteU16(s, time);
        WriteU16(s, date);
        WriteU32(s, crc32);
        WriteU32(s, (uint)compressedSize);
        WriteU32(s, (uint)uncompressedSize);
        WriteU16(s, (ushort)nameBytes.Length);
        WriteU16(s, 0); // extra field length
        WriteU16(s, 0); // file comment length
        WriteU16(s, 0); // disk number start
        WriteU16(s, 0); // internal file attributes
        WriteU32(s, 0u); // external file attributes (always 4 bytes; never a 2-byte field)
        WriteU32(s, (uint)localHeaderOffset);
        s.Write(nameBytes, 0, nameBytes.Length);
    }

    private static void WriteEndOfCentralDirectory(Stream s, int entryCount, int centralDirSize, long centralDirOffset)
    {
        WriteU32(s, EndOfCentralDirSignature);
        WriteU16(s, 0); // number of this disk
        WriteU16(s, 0); // number of the disk with the start of the central directory
        WriteU16(s, (ushort)entryCount); // total number of entries on this disk
        WriteU16(s, (ushort)entryCount);
        WriteU32(s, (uint)centralDirSize);
        WriteU32(s, (uint)centralDirOffset);
        WriteU16(s, 0); // .zip file comment length
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
}
