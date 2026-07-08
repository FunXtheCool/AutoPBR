using AutoPBR.Core.Models;
using AutoPBR.Core.Atlas;
using Colourful;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AutoPBR.Core;

/// <summary>
/// Generates LabPBR-compatible specular (_s) textures from diffuse inputs.
/// </summary>
internal static partial class SpecularGenerator
{
    public static Task GenerateAsync(
        IReadOnlyList<TextureWorkItem> textures,
        AutoPBROptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var stage = ConversionStage.GeneratingSpecular;
            var total = textures.Count;

            var rgbToLabByThread = new ThreadLocal<IColorConverter<RGBColor, LabColor>>(
                () => new ConverterBuilder()
                    .FromRGB(RGBWorkingSpaces.sRGB)
                    .ToLab(Illuminants.D65)
                    .Build());
            var de2000ByThread = new ThreadLocal<CIEDE2000ColorDifference>(() => new CIEDE2000ColorDifference());
            var completed = 0;

            Parallel.ForEach(
                textures,
                new ParallelOptions
                { MaxDegreeOfParallelism = ThreadingUtil.GetConversionParallelism(options), CancellationToken = ct },
                t =>
                {
                    ThreadingUtil.SetThreadName("AutoPBR.Specular");
                    ct.ThrowIfCancellationRequested();
                    if (FoliageModeResolver.IsIgnoreAll(options.FoliageMode) && t.Sprite2DFoliageTarget)
                    {
                        var skipped = Interlocked.Increment(ref completed);
                        progress?.Report(new ConversionProgress(stage, skipped, total, t.Name));
                        return;
                    }

                    var rgbToLab = rgbToLabByThread.Value ?? throw new InvalidOperationException("Thread-local Lab converter not available.");
                    var de2000 = de2000ByThread.Value ?? throw new InvalidOperationException("Thread-local CIEDE2000 not available.");
                    using var img = Image.Load<Rgba32>(t.DiffusePath);
                    var combinedLog = GenerateLoaded(t, img, options, rgbToLab, de2000, total, ct);

                    var n = Interlocked.Increment(ref completed);
                    progress?.Report(new ConversionProgress(stage, n, total, t.Name, string.IsNullOrWhiteSpace(combinedLog) ? null : combinedLog));
                });
        }, ct);
    }

    internal static string? GenerateLoaded(
        TextureWorkItem t,
        Image<Rgba32> img,
        AutoPBROptions options,
        IColorConverter<RGBColor, LabColor> rgbToLab,
        CIEDE2000ColorDifference de2000,
        int totalForTileParallelism,
        CancellationToken ct)
    {
        var atlasPlan = t.HasUvWrap
            ? AtlasTiling.Decide(
                img.Width,
                img.Height,
                preferredTileSize: t.PackBaseTileSize)
            : AtlasTiling.None;
        string? mlDiagnostic = null;
        var anyUseMlSpec = false;
        var hasData = false;

        if (atlasPlan.IsAtlas)
        {
            var tiles = AtlasTiling.EnumerateTiles(atlasPlan).ToList();
            using var atlasOut = new Image<Rgba32>(img.Width, img.Height);
            var tileWorkerCount = totalForTileParallelism <= 1
                ? Math.Min(Math.Max(1, ThreadingUtil.GetConversionParallelism(options)), Math.Max(1, tiles.Count))
                : 1;
            if (tileWorkerCount <= 1)
            {
                foreach (var tile in tiles)
                {
                    ct.ThrowIfCancellationRequested();
                    using var tileDiffuse = AtlasTiling.ExtractTile(img, tile);
                    var tileResult = GenerateSpecularForDiffuseTile(
                        tileDiffuse,
                        t,
                        options,
                        rgbToLab,
                        de2000);
                    anyUseMlSpec |= tileResult.UseMlSpec;
                    mlDiagnostic ??= tileResult.MlDiagnostic;
                    if (!tileResult.HasData || tileResult.Image is null)
                    {
                        continue;
                    }

                    hasData = true;
                    using var tileImage = tileResult.Image;
                    AtlasTiling.PasteTile(atlasOut, tile, tileImage);
                }
            }
            else
            {
                var tileDiffuses = new Image<Rgba32>?[tiles.Count];
                var tileResults = new SpecularTileResult?[tiles.Count];
                try
                {
                    for (var i = 0; i < tiles.Count; i++)
                    {
                        tileDiffuses[i] = AtlasTiling.ExtractTile(img, tiles[i]);
                    }

                    Parallel.For(
                        0,
                        tiles.Count,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = tileWorkerCount,
                            CancellationToken = ct
                        },
                        i =>
                        {
                            var tileDiffuse = tileDiffuses[i] ?? throw new InvalidOperationException("Atlas tile extraction failed.");
                            var tileRgbToLab = new ConverterBuilder()
                                .FromRGB(RGBWorkingSpaces.sRGB)
                                .ToLab(Illuminants.D65)
                                .Build();
                            var tileDe2000 = new CIEDE2000ColorDifference();
                            tileResults[i] = GenerateSpecularForDiffuseTile(
                                tileDiffuse,
                                t,
                                options,
                                tileRgbToLab,
                                tileDe2000);
                        });

                    for (var i = 0; i < tiles.Count; i++)
                    {
                        var tileResult = tileResults[i];
                        if (tileResult is null)
                        {
                            continue;
                        }

                        anyUseMlSpec |= tileResult.UseMlSpec;
                        mlDiagnostic ??= tileResult.MlDiagnostic;
                        if (!tileResult.HasData || tileResult.Image is null)
                        {
                            continue;
                        }

                        hasData = true;
                        AtlasTiling.PasteTile(atlasOut, tiles[i], tileResult.Image);
                    }
                }
                finally
                {
                    for (var i = 0; i < tileResults.Length; i++)
                    {
                        tileResults[i]?.Image?.Dispose();
                        tileDiffuses[i]?.Dispose();
                    }
                }
            }

            if (hasData)
            {
                atlasOut.Save(t.SpecularPath);
            }
            else if (File.Exists(t.SpecularPath))
            {
                File.Delete(t.SpecularPath);
            }
        }
        else
        {
            using var cropped = CropToSquare(img);
            var result = GenerateSpecularForDiffuseTile(cropped, t, options, rgbToLab, de2000);
            anyUseMlSpec = result.UseMlSpec;
            mlDiagnostic = result.MlDiagnostic;
            if (result.Image is not null)
            {
                using var outImg = result.Image;
                outImg.Save(t.SpecularPath);
            }
            else if (File.Exists(t.SpecularPath))
            {
                File.Delete(t.SpecularPath);
            }
        }

        return BuildSpecularModelLogLine(
            t.Name,
            options,
            anyUseMlSpec,
            mlDiagnostic);
    }
}
