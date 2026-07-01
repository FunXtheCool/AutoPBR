using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.Core.Tests;

public sealed class PreviewGroundMapsResolverTests
{
    [Fact]
    public void ShouldPreferScannedPack_false_for_batch_scan()
    {
        Assert.False(PreviewGroundMapsResolver.ShouldPreferScannedPack(hasScannedArchive: true, isBatchScanActive: true));
        Assert.True(PreviewGroundMapsResolver.ShouldPreferScannedPack(hasScannedArchive: true, isBatchScanActive: false));
    }

    [Fact]
    public async Task TryResolveFromDiffuseFileAsync_produces_normal_and_specular()
    {
        var diffuse = Path.Combine(Path.GetTempPath(), "autopbr_ground_" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            await File.WriteAllBytesAsync(diffuse, CreateSolidPng(16, 16, 90, 140, 55, 255));

            var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json");
            var options = new AutoPbrOptions
            {
                SpecularData = SpecularData.LoadFromFile(dataPath),
                FastSpecular = true,
                FoliageMode = "No Height",
            };

            var maps = await PreviewGroundMapsResolver.TryResolveFromDiffuseFileAsync(diffuse, options);
            Assert.NotNull(maps);
            Assert.NotNull(maps.NormalRgba);
            Assert.NotNull(maps.SpecularRgba);
            Assert.Equal(16, maps.Width);
            Assert.Equal(16, maps.Height);
        }
        finally
        {
            if (File.Exists(diffuse))
            {
                File.Delete(diffuse);
            }
        }
    }

    private static byte[] CreateSolidPng(int width, int height, byte r, byte g, byte b, byte a)
    {
        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgba32(r, g, b, a);
                }
            }
        });
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
}
