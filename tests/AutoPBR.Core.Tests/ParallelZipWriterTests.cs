using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using AutoPBR.Core.Models;
using Xunit;

namespace AutoPBR.Core.Tests;

public class ParallelZipWriterTests
{
    /// <summary>ISO 3309 / PKZIP CRC-32 of the ASCII string <c>123456789</c> (zlib test vector).</summary>
    [Fact]
    public void Crc32HashMatchesStandardTestVector()
    {
        var ascii = "123456789"u8.ToArray();
        const uint expected = 0xCBF43926u;
        var v = BinaryPrimitives.ReadUInt32LittleEndian(Crc32.Hash(ascii));
        Assert.Equal(expected, v);
    }

    /// <summary>
    /// Regression: C# overload resolution had <c>WriteLe(s, 0)</c> prefer the <see cref="uint"/> overload,
    /// writing 4 bytes for 2-byte ZIP fields and corrupting local/central headers (CRC errors on extract).
    /// </summary>
    [Fact]
    public void WriteZipLocalHeaderTwoByteFieldsNotWidenedToFour()
    {
        var temp = Path.Combine(Path.GetTempPath(), "AutoPBR", "zip_layout", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var filePath = Path.Combine(temp, "ab.png");
        File.WriteAllBytes(filePath, [0x89, 0x50]);

        var outZip = Path.Combine(temp, "out.zip");
        try
        {
            ParallelZipWriter.WriteZip(
                outZip,
                [filePath],
                temp,
                new AutoPbrOptions(),
                progress: null,
                ConversionStage.Packing,
                CancellationToken.None);

            var zipBytes = File.ReadAllBytes(outZip);
            // scan for PK\x03\x04
            var i = 0;
            while (i < zipBytes.Length - 4)
            {
                if (zipBytes[i] == 0x50 && zipBytes[i + 1] == 0x4b && zipBytes[i + 2] == 0x03 && zipBytes[i + 3] == 0x04)
                {
                    break;
                }

                i++;
            }

            Assert.InRange(i, 0, zipBytes.Length - 4);
            // compression method at +8: must be 0x0008 (deflate), 2 bytes only
            Assert.Equal(8, zipBytes[i + 8]);
            Assert.Equal(0, zipBytes[i + 9]);
            // file name "ab.png" length 6 at +26
            Assert.Equal(6, zipBytes[i + 26]);
            Assert.Equal(0, zipBytes[i + 27]);
            // next byte must be first char of name 'a' (0x61), not extra length high byte
            Assert.Equal((byte)'a', zipBytes[i + 30]);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch
            {
                /* best-effort */
            }
        }
    }

    [Fact]
    public void WriteZipStoresCorrectCrc32AndRoundTrips()
    {
        var temp = Path.Combine(Path.GetTempPath(), "AutoPBR", "zip_test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var filePath = Path.Combine(temp, "test.bin");
        var bytes = new byte[] { 1, 2, 3, 4, 5, 0xff };
        File.WriteAllBytes(filePath, bytes);
        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(Crc32.Hash(bytes));

        var outZip = Path.Combine(temp, "out.zip");
        try
        {
            ParallelZipWriter.WriteZip(
                outZip,
                [filePath],
                temp,
                new AutoPbrOptions(),
                progress: null,
                ConversionStage.Packing,
                CancellationToken.None);

            using var zip = ZipFile.OpenRead(outZip);
            var entry = Assert.Single(zip.Entries);
            Assert.Equal("test.bin", entry.Name);
            Assert.Equal(expectedCrc, entry.Crc32);
            using var s = entry.Open();
            var read = new MemoryStream();
            s.CopyTo(read);
            Assert.Equal(bytes, read.ToArray());
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch
            {
                /* best-effort */
            }
        }
    }
}
