using AutoPBR.Core.HeightFromNormals;
using AutoPBR.Core.Models;
using AutoPBR.Core.Atlas;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AutoPBR.Core;

/// <summary>
/// Generates normal maps and encodes height information into the alpha channel.
/// </summary>
internal static partial class NormalHeightGenerator
{
    public static Task GenerateAsync(
        IReadOnlyList<TextureWorkItem> textures,
        AutoPbrOptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var stage = ConversionStage.GeneratingNormals;
            var toProcess = textures.Where(t =>
                    !t.SpecularOnly &&
                    !(FoliageModeResolver.IsIgnoreAll(options.FoliageMode) && t.Sprite2DFoliageTarget))
                .ToList();
            var total = toProcess.Count;
            var completed = 0;
            var deepBumpFallback = new DeepBumpFallbackTracker(stage, total, progress);
            DeepBumpNormalsGenerator? deepBumpGenerator = null;
            try
            {
                deepBumpGenerator = CreateDeepBumpGenerator(options);

                if (deepBumpGenerator is { IsUsingGpu: false })
                {
                    progress?.Report(new ConversionProgress(ConversionStage.GeneratingNormals, 0, total, null,
                        "DeepBump: CUDA not available, using CPU."));
                }

                var generatorForLoop = deepBumpGenerator;
                Parallel.ForEach(
                    toProcess,
                    new ParallelOptions
                    { MaxDegreeOfParallelism = ThreadingUtil.GetConversionParallelism(options), CancellationToken = ct },
                    t =>
                    {
                        ThreadingUtil.SetThreadName("AutoPBR.Normals");
                        ct.ThrowIfCancellationRequested();
                        var textureUsedDeepBumpFallback = 0;

                        if (!options.BrickProbePreviewDebug)
                        {
                            t.BrickProbeDebugText = null;
                        }

                        using var diffuseImg = Image.Load<Rgba32>(t.DiffusePath);
                        var atlasPlan = t.HasUvWrap
                            ? AtlasTiling.Decide(
                                diffuseImg.Width,
                                diffuseImg.Height,
                                preferredTileSize: t.PackBaseTileSize)
                            : AtlasTiling.None;
                        string? brickInfo = null;
                        if (atlasPlan.IsAtlas)
                        {
                            var tiles = AtlasTiling.EnumerateTiles(atlasPlan).ToList();
                            using var atlasNormal = new Image<Rgba32>(diffuseImg.Width, diffuseImg.Height);
                            var tileWorkerCount = total <= 1
                                ? Math.Min(Math.Max(1, ThreadingUtil.GetConversionParallelism(options)), Math.Max(1, tiles.Count))
                                : 1;
                            if (tileWorkerCount <= 1)
                            {
                                foreach (var tile in tiles)
                                {
                                    ct.ThrowIfCancellationRequested();
                                    using var tileDiffuse = AtlasTiling.ExtractTile(diffuseImg, tile);
                                    var deepBumpFaulted = deepBumpFallback.IsActivated;
                                    var generatorForTile = !deepBumpFaulted
                                        ? generatorForLoop
                                        : null;
                                    if (deepBumpFaulted && generatorForLoop != null)
                                    {
                                        Interlocked.Exchange(ref textureUsedDeepBumpFallback, 1);
                                    }

                                    using var tileNormal = GenerateNormalForDiffuse(
                                        tileDiffuse,
                                        t,
                                        options,
                                        generatorForTile,
                                        out var tileBrickInfo,
                                        out var tileUsedFallback,
                                        deepBumpFallback.Activate);
                                    if (tileUsedFallback)
                                    {
                                        Interlocked.Exchange(ref textureUsedDeepBumpFallback, 1);
                                    }

                                    AtlasTiling.PasteTile(atlasNormal, tile, tileNormal);
                                    brickInfo ??= tileBrickInfo;
                                }
                            }
                            else
                            {
                                var tileDiffuses = new Image<Rgba32>?[tiles.Count];
                                var tileNormals = new Image<Rgba32>?[tiles.Count];
                                var tileBrickInfos = new string?[tiles.Count];
                                try
                                {
                                    for (var i = 0; i < tiles.Count; i++)
                                    {
                                        tileDiffuses[i] = AtlasTiling.ExtractTile(diffuseImg, tiles[i]);
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
                                            var deepBumpFaulted = deepBumpFallback.IsActivated;
                                            var generatorForTile = !deepBumpFaulted
                                                ? generatorForLoop
                                                : null;
                                            if (deepBumpFaulted && generatorForLoop != null)
                                            {
                                                Interlocked.Exchange(ref textureUsedDeepBumpFallback, 1);
                                            }

                                            tileNormals[i] = GenerateNormalForDiffuse(
                                                tileDiffuse,
                                                t,
                                                options,
                                                generatorForTile,
                                                out var tileBrickInfo,
                                                out var tileUsedFallback,
                                                deepBumpFallback.Activate);
                                            if (tileUsedFallback)
                                            {
                                                Interlocked.Exchange(ref textureUsedDeepBumpFallback, 1);
                                            }

                                            tileBrickInfos[i] = tileBrickInfo;
                                        });

                                    for (var i = 0; i < tiles.Count; i++)
                                    {
                                        var tileNormal = tileNormals[i];
                                        if (tileNormal is null)
                                        {
                                            continue;
                                        }

                                        AtlasTiling.PasteTile(atlasNormal, tiles[i], tileNormal);
                                        brickInfo ??= tileBrickInfos[i];
                                    }
                                }
                                finally
                                {
                                    for (var i = 0; i < tileNormals.Length; i++)
                                    {
                                        tileNormals[i]?.Dispose();
                                        tileDiffuses[i]?.Dispose();
                                    }
                                }
                            }

                            atlasNormal.Save(t.NormalPath);
                        }
                        else
                        {
                            using var croppedDiffuse = CropToSquare(diffuseImg);
                            var deepBumpFaulted = deepBumpFallback.IsActivated;
                            var generatorForTexture = !deepBumpFaulted
                                ? generatorForLoop
                                : null;
                            if (deepBumpFaulted && generatorForLoop != null)
                            {
                                Interlocked.Exchange(ref textureUsedDeepBumpFallback, 1);
                            }

                            using var normal = GenerateNormalForDiffuse(
                                croppedDiffuse,
                                t,
                                options,
                                generatorForTexture,
                                out brickInfo,
                                out var textureUsedFallback,
                                deepBumpFallback.Activate);
                            if (textureUsedFallback)
                            {
                                Interlocked.Exchange(ref textureUsedDeepBumpFallback, 1);
                            }

                            normal.Save(t.NormalPath);
                        }

                        if (Volatile.Read(ref textureUsedDeepBumpFallback) != 0)
                        {
                            deepBumpFallback.IncrementTextureFallback();
                        }

                        var n = Interlocked.Increment(ref completed);
                        progress?.Report(new ConversionProgress(stage, n, total, t.Name, brickInfo));
                    });

                deepBumpFallback.ReportSummary();
            }
            finally
            {
                deepBumpGenerator?.Dispose();
            }
        }, ct);
    }

    private static Image<Rgba32> GenerateNormalForDiffuse(
        Image<Rgba32> diffuse,
        TextureWorkItem t,
        AutoPbrOptions options,
        DeepBumpNormalsGenerator? generatorForLoop,
        out string? brickInfo,
        out bool usedClassicFallback,
        Action<string>? onDeepBumpFailure = null)
    {
        var width = diffuse.Width;
        var height = diffuse.Height;
        brickInfo = null;
        usedClassicFallback = false;

        Image<Rgba32> normal;
        if (generatorForLoop != null)
        {
            if (TryGenerateDeepBumpNormal(
                    diffuse,
                    t,
                    options,
                    generatorForLoop,
                    out normal,
                    out var failureReason))
            {
                // DeepBump path succeeded; height/AO packing continues below.
            }
            else
            {
                usedClassicFallback = true;
                onDeepBumpFailure?.Invoke(failureReason);
                normal = GenerateNormalMap(
                    diffuse,
                    width,
                    height,
                    t.Overrides.NormalIntensity ?? options.NormalIntensity,
                    t.Overrides.InvertNormalRed,
                    t.Overrides.InvertNormalGreen,
                    options);
                brickInfo = "DeepBump inference failed; used classic normal generation fallback.";
            }
        }
        else
        {
            var normalIntensity = t.Overrides.NormalIntensity ?? options.NormalIntensity;
            normal = GenerateNormalMap(
                diffuse,
                width,
                height,
                normalIntensity,
                t.Overrides.InvertNormalRed,
                t.Overrides.InvertNormalGreen,
                options);
        }

        var heightIntensity = t.Overrides.HeightIntensity ?? options.HeightIntensity;
        var brightness = t.Overrides.HeightBrightness ?? AutoPbrDefaults.DefaultHeightBrightness;
        var heightMap = GenerateHeightMap(diffuse, width, height, heightIntensity, brightness, t.Overrides.InvertHeight, options);

        BrickHeightPostProcessResult? brickProbeResult = null;
        if (t.HasBrickMaterialTag && options.BrickHeightMapPostProcessEnabled)
        {
            var newHeightData = BrickHeightPostProcessor.Apply(
                heightMap.Data,
                width,
                height,
                diffuse,
                options,
                out var brickRes);
            brickProbeResult = brickRes;
            heightMap = ReplaceHeightMapData(heightMap, newHeightData);
            t.Overrides.BrickProbeAppliedGlobalInvert =
                brickRes is { SkippedLowConfidence: false, AppliedGlobalInvert: true };
            if (options.BrickHeightMapVerboseLog)
            {
                brickInfo =
                    $"brick: conf={brickRes.StructuralConfidence:F3} d={brickRes.DeltaMortarMinusBrick:F1} inv={brickRes.AppliedGlobalInvert} skip={brickRes.SkippedLowConfidence}";
            }
        }

        if (options.BrickProbePreviewDebug)
        {
            if (brickProbeResult is { } res)
            {
                t.BrickProbeDebugText =
                    $"name={t.Name}\nrelativeKey={t.RelativeKey}\n" +
                    (res.DebugText ?? "");
            }
            else if (!t.HasBrickMaterialTag)
            {
                t.BrickProbeDebugText =
                    "Brick probe: skipped — this texture does not have the brick material tag (keyword or semantic match).";
            }
            else if (!options.BrickHeightMapPostProcessEnabled)
            {
                t.BrickProbeDebugText =
                    "Brick probe: disabled — turn on \"Enable brick probe inversion\" under Height Tuning.";
            }
        }

        var skipHeightInAlpha = t.IsPlantForNoHeight;
        if (!skipHeightInAlpha && FoliageModeResolver.IsNoHeight(options.FoliageMode) &&
            t.Sprite2DFoliageTarget &&
            (t.Name.Contains("grass", StringComparison.OrdinalIgnoreCase) ||
             t.RelativeKey.Contains("grass", StringComparison.OrdinalIgnoreCase)))
        {
            skipHeightInAlpha = HasSignificantTransparency(diffuse);
        }

        byte[]? aoChannel = null;
        if (options.GenerateAo && !skipHeightInAlpha)
        {
            aoChannel = GenerateAoChannelFromHeight(heightMap, options.AoRadius, options.AoStrength);
        }

        normal.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < heightMap.Height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (var x = 0; x < heightMap.Width; x++)
                {
                    byte a;
                    if (skipHeightInAlpha)
                    {
                        a = 255;
                    }
                    else
                    {
                        var h = heightMap[x, y];
                        a = h == 0 ? (byte)1 : h;
                    }

                    row[x].A = a;
                    var i = y * heightMap.Width + x;
                    row[x].B = aoChannel != null ? aoChannel[i] : (byte)255;
                }
            }
        });

        if (options.NormalHeightZeroTransparentPixels)
        {
            ApplyTransparentZeroClamp(
                normal,
                diffuse,
                Math.Clamp(options.NormalHeightTransparentAlphaClampMax, 0, 255));
        }

        return normal;
    }

    private static bool HasSignificantTransparency(Image<Rgba32> cropped)
    {
        if (!cropped.DangerousTryGetSinglePixelMemory(out var mem))
        {
            return false;
        }

        var span = mem.Span;
        long sumA = 0;
        int lowAlphaCount = 0;
        var n = span.Length;
        for (var i = 0; i < n; i++)
        {
            var a = span[i].A;
            sumA += a;
            if (a < 128)
            {
                lowAlphaCount++;
            }
        }

        var meanAlpha = (int)(sumA / n);
        return meanAlpha < 200 || lowAlphaCount > 0.3 * n;
    }

    private static void ApplyTransparentZeroClamp(
        Image<Rgba32> normal,
        Image<Rgba32> diffuse,
        int alphaClampMax)
    {
        if (!normal.DangerousTryGetSinglePixelMemory(out var normalMem) ||
            !diffuse.DangerousTryGetSinglePixelMemory(out var diffuseMem))
        {
            return;
        }

        var normalSpan = normalMem.Span;
        var diffuseSpan = diffuseMem.Span;
        for (var i = 0; i < normalSpan.Length; i++)
        {
            if (diffuseSpan[i].A <= alphaClampMax)
            {
                normalSpan[i] = new Rgba32(0, 0, 0, 0);
            }
        }
    }

    private sealed class HeightMap
    {
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required byte[] Data { get; init; }

        public byte this[int x, int y] => Data[y * Width + x];
    }

    private static HeightMap ReplaceHeightMapData(HeightMap h, byte[] data)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(data.Length, h.Width * h.Height);
        return new HeightMap { Width = h.Width, Height = h.Height, Data = data };
    }

    private static HeightMap GenerateHeightMap(Image<Rgba32> cropped, int width, int height, float heightIntensity,
        float brightness, bool invertHeight, AutoPbrOptions options)
    {
        heightIntensity = MathF.Max(heightIntensity, 1e-3f);

        var grey = new byte[width * height];
        cropped.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    var p = row[x];
                    int v;
                    if (options.PreprocessLinearize)
                    {
                        var r = PreprocessUtil.SrgbToLinear(p.R);
                        var g = PreprocessUtil.SrgbToLinear(p.G);
                        var b = PreprocessUtil.SrgbToLinear(p.B);
                        v = (int)MathF.Round((r * 0.2126f + g * 0.7152f + b * 0.0722f) * 255f);
                    }
                    else
                    {
                        v = (int)MathF.Round(p.R * 0.3f + p.G * 0.6f + p.B * 0.1f);
                    }
                    grey[y * width + x] = (byte)Math.Clamp(v, 0, 255);
                }
            }
        });

        var delta = (int)MathF.Round(50f * brightness);
        delta = Math.Clamp(delta, 0, 255);
        var threshold = 255 - delta;

        for (var i = 0; i < grey.Length; i++)
        {
            var v = grey[i];
            if (v < threshold)
            {
                var nv = v + delta;
                grey[i] = (byte)(nv > 255 ? 255 : nv);
            }
        }

        var outData = new byte[grey.Length];
        for (var i = 0; i < grey.Length; i++)
        {
            var normalized = grey[i] / 255.0;
            var mapped = 255.0 * Math.Pow(normalized, heightIntensity);
            outData[i] = (byte)Math.Clamp((int)Math.Round(mapped), 0, 255);
        }

        if (invertHeight)
        {
            byte lowest = 255;
            byte highest = 0;
            for (var i = 0; i < outData.Length; i++)
            {
                var v = outData[i];
                if (v < lowest)
                {
                    lowest = v;
                }

                if (v > highest)
                {
                    highest = v;
                }
            }

            for (var i = 0; i < outData.Length; i++)
            {
                outData[i] = (byte)(highest - outData[i] + lowest);
            }
        }

        return new HeightMap { Width = width, Height = height, Data = outData };
    }

    /// <summary>
    /// LabPBR: AO in normal blue channel. Returns bytes where 0 = 100% occlusion, 255 = 0% occlusion.
    /// </summary>
    private static byte[] GenerateAoChannelFromHeight(HeightMap height, int radius, float strength)
    {
        radius = Math.Clamp(radius, 1, 64);
        strength = Math.Clamp(strength, 0f, 5f);

        var w = height.Width;
        var h = height.Height;
        var n = w * h;

        var hf = new float[n];
        for (var i = 0; i < n; i++)
        {
            hf[i] = height.Data[i] / 255f;
        }

        var blurred = new float[n];
        Array.Copy(hf, blurred, n);
        PreprocessUtil.BoxBlurInPlace(blurred, w, h, radius);

        var result = new byte[n];
        for (var i = 0; i < n; i++)
        {
            var cavity = MathF.Max(0f, blurred[i] - hf[i]);
            var ao = 1f - Math.Clamp(cavity * strength, 0f, 1f);
            result[i] = (byte)Math.Clamp((int)MathF.Round(ao * 255f), 0, 255);
        }

        return result;
    }

    private static Image<Rgba32> CropToSquare(Image<Rgba32> img)
    {
        var s = Math.Min(img.Width, img.Height);
        if (img.Width == s && img.Height == s)
        {
            return img.Clone();
        }

        var startX = (img.Width - s) / 2;
        var startY = (img.Height - s) / 2;
        return img.Clone(ctx => ctx.Crop(new Rectangle(startX, startY, s, s)));
    }
}
