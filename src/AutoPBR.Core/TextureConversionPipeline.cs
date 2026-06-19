using AutoPBR.Core.Models;
using Colourful;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.Core;

internal static class TextureConversionPipeline
{
    public static Task GenerateNormalsAndSpecularAsync(
        IReadOnlyList<TextureWorkItem> textures,
        AutoPbrOptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var normalTotal = textures.Count(t =>
                !t.SpecularOnly &&
                !(FoliageModeResolver.IsIgnoreAll(options.FoliageMode) && t.Sprite2DFoliageTarget));
            var total = textures.Count;
            var completed = 0;

            var deepBumpFallback = new NormalHeightGenerator.DeepBumpFallbackTracker(
                ConversionStage.GeneratingNormals,
                normalTotal,
                progress);
            using var deepBumpGenerator = NormalHeightGenerator.CreateDeepBumpGenerator(options);
            if (deepBumpGenerator is { IsUsingGpu: false })
            {
                progress?.Report(new ConversionProgress(
                    ConversionStage.GeneratingNormals,
                    0,
                    normalTotal,
                    null,
                    "DeepBump: CUDA not available, using CPU."));
            }

            using var rgbToLabByThread = new ThreadLocal<IColorConverter<RGBColor, LabColor>>(
                () => new ConverterBuilder()
                    .FromRGB(RGBWorkingSpaces.sRGB)
                    .ToLab(Illuminants.D65)
                    .Build());
            using var de2000ByThread = new ThreadLocal<CIEDE2000ColorDifference>(
                () => new CIEDE2000ColorDifference());

            Parallel.ForEach(
                textures,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = ThreadingUtil.GetConversionParallelism(options),
                    CancellationToken = ct
                },
                t =>
                {
                    ThreadingUtil.SetThreadName("AutoPBR.Convert");
                    ct.ThrowIfCancellationRequested();

                    var shouldGenerateNormal =
                        !t.SpecularOnly &&
                        !(FoliageModeResolver.IsIgnoreAll(options.FoliageMode) && t.Sprite2DFoliageTarget);
                    var shouldGenerateSpecular =
                        !(FoliageModeResolver.IsIgnoreAll(options.FoliageMode) && t.Sprite2DFoliageTarget);

                    if (!shouldGenerateNormal && !shouldGenerateSpecular)
                    {
                        var skipped = Interlocked.Increment(ref completed);
                        progress?.Report(new ConversionProgress(
                            ConversionStage.GeneratingNormals,
                            skipped,
                            total,
                            t.Name));
                        return;
                    }

                    string? info = null;
                    using var diffuse = Image.Load<Rgba32>(t.DiffusePath);
                    if (shouldGenerateNormal)
                    {
                        info = NormalHeightGenerator.GenerateLoaded(
                            t,
                            diffuse,
                            options,
                            deepBumpGenerator,
                            deepBumpFallback,
                            normalTotal,
                            ct);
                    }

                    if (shouldGenerateSpecular)
                    {
                        var rgbToLab = rgbToLabByThread.Value ??
                                       throw new InvalidOperationException("Thread-local Lab converter not available.");
                        var de2000 = de2000ByThread.Value ??
                                     throw new InvalidOperationException("Thread-local CIEDE2000 not available.");
                        var specLog = SpecularGenerator.GenerateLoaded(
                            t,
                            diffuse,
                            options,
                            rgbToLab,
                            de2000,
                            total,
                            ct);
                        info = string.Join(" | ", new[] { info, specLog }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    }

                    var n = Interlocked.Increment(ref completed);
                    progress?.Report(new ConversionProgress(
                        ConversionStage.GeneratingNormals,
                        n,
                        total,
                        t.Name,
                        string.IsNullOrWhiteSpace(info) ? null : info));
                });

            deepBumpFallback.ReportSummary();
        }, ct);
    }
}
