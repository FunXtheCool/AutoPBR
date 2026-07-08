using AutoPBR.Core.Models;

namespace AutoPBR.Core.Tests;

public sealed class PreviewScanReproTests
{
    [Fact]
    public void PreviewScan_IncludesItemTextures_WhenFoliageModeIsIgnoreAll()
    {
        var jar = @"C:\Users\John_Phoenix\AppData\Roaming\PrismLauncher\libraries\com\mojang\minecraft\26.1.2\minecraft-26.1.2-client.jar";
        if (!File.Exists(jar))
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "AutoPBR_PreviewTest", Guid.NewGuid().ToString("N"));
        var extracted = Path.Combine(tempRoot, "pack_unzipped");
        Directory.CreateDirectory(extracted);
        try
        {
            const string archivePath = "assets/minecraft/textures/item/acacia_boat.png";
            PackExtractionService.ExtractEntry(jar, archivePath, extracted);

            var options = new AutoPBROptions
            {
                ProcessItems = true,
                FoliageMode = "Ignore All"
            };

            var conversionScan = TextureScanner.ScanTextures(extracted, options, cachePackPath: jar);
            Assert.Empty(conversionScan);

            var previewScan = TextureScanner.ScanTextures(
                extracted,
                options,
                cachePackPath: jar,
                applyFoliageIgnoreFilter: false);
            Assert.NotEmpty(previewScan);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* ignore */ }
        }
    }
}
