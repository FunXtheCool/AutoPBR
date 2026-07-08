using AutoPBR.Core.Models;

namespace AutoPBR.Core;

public static class ResourcePackConverter
{
    public static async Task ConvertAsync(
        string inputZipPath,
        string outputZipPath,
        AutoPBROptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputZipPath))
        {

            throw new FileNotFoundException("Input pack not found.", inputZipPath);
        }


        if (options.SpecularData is null)
        {

            throw new InvalidOperationException("SpecularData is required (load textures_data.json first).");
        }


        var baseTemp = string.IsNullOrWhiteSpace(options.TempDirectory)
            ? Path.GetTempPath()
            : options.TempDirectory;
        var tempRoot = Path.Combine(baseTemp, "AutoPBR", Guid.NewGuid().ToString("N"));
        var extracted = Path.Combine(tempRoot, "pack_unzipped");
        Directory.CreateDirectory(extracted);

        try
        {
            await Task.Run(() =>
            {
                if (options.UseLegacyExtractor)
                {
                    PackExtractionService.ExtractPack(inputZipPath, extracted, options, progress, cancellationToken);
                }
                else
                {

                    ParallelZipReader.ExtractZip(inputZipPath, extracted, options, progress, ConversionStage.Extracting,
                        cancellationToken, options.EntriesToExtractOnly);
                }

            }, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // ScanTextures enumerates PNGs and resolves per-texture material/flag tags (keyword + MiniLM + post-process + manual overrides).
            // This must finish before conversion so brick/ore/organic rules apply to TextureWorkItem.Overrides.
            progress?.Report(new ConversionProgress(ConversionStage.ScanningTextures, 0, 1));
            var textures = TextureScanner.ScanTextures(
                extracted,
                options,
                progress,
                cachePackPath: inputZipPath,
                cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Normals/height before specular so brick mortar probe can cache invert specular R (TextureOverrides.BrickProbeAppliedGlobalInvert).
            await TextureConversionPipeline.GenerateNormalsAndSpecularAsync(textures, options, progress, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            PackWriter.WritePackMetadata(extracted);

            Directory.CreateDirectory(Path.GetDirectoryName(outputZipPath) ?? ".");
            if (File.Exists(outputZipPath))
            {
                File.Delete(outputZipPath);
            }


            await Task.Run(() => PackWriter.CreateOutputZip(extracted, outputZipPath, textures, options, progress, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            progress?.Report(new ConversionProgress(ConversionStage.Done, 0, 0));
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                /* best-effort */
            }
        }
    }
}
