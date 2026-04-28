using AutoPBR.Core.HeightFromNormals;
using AutoPBR.Core.Models;
using AutoPBR.Core.Atlas;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AutoPBR.Core;

/// <summary>
/// Generates normal maps and encodes height information into the alpha channel.
/// </summary>
internal static class NormalHeightGenerator
{
    private const int VcOrientationCount = 12;
    private static readonly float[] VcCos = BuildVcCos();
    private static readonly float[] VcSin = BuildVcSin();

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
            var deepBumpFallbackActivated = 0;
            var deepBumpFallbackTextureCount = 0;
            string? deepBumpFirstFailureReason = null;
            DeepBumpNormalsGenerator? deepBumpGenerator = null;
            try
            {
                deepBumpGenerator =
                    options.UseDeepBumpNormals && !string.IsNullOrWhiteSpace(options.DeepBumpModelPath)
                        ? DeepBumpNormalsGenerator.TryCreate(
                            options.DeepBumpModelPath!,
                            maxConcurrentRuns: Math.Max(1, ThreadingUtil.GetConversionParallelism(options)),
                            preferOnnxTensorRtExecutionProvider: options.PreferOnnxTensorRtExecutionProvider)
                        : null;

                if (deepBumpGenerator is { IsUsingGpu: false })
                {
                    progress?.Report(new ConversionProgress(ConversionStage.GeneratingNormals, 0, total, null,
                        "DeepBump: CUDA not available, using CPU."));
                }

                var generatorForLoop = deepBumpGenerator;
                void ActivateDeepBumpFallback(string reason)
                {
                    if (Interlocked.CompareExchange(ref deepBumpFallbackActivated, 1, 0) != 0)
                    {
                        return;
                    }

                    var normalized = NormalizeFirstDeepBumpFailureReason(reason);
                    Interlocked.CompareExchange(ref deepBumpFirstFailureReason, normalized, null);
                    progress?.Report(new ConversionProgress(
                        ConversionStage.GeneratingNormals,
                        0,
                        total,
                        null,
                        "DeepBump failed during inference. Falling back to classic normal generation."));
                }

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
                                    var deepBumpFaulted = Volatile.Read(ref deepBumpFallbackActivated) != 0;
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
                                        ActivateDeepBumpFallback);
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
                                            var deepBumpFaulted = Volatile.Read(ref deepBumpFallbackActivated) != 0;
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
                                                ActivateDeepBumpFallback);
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
                            var deepBumpFaulted = Volatile.Read(ref deepBumpFallbackActivated) != 0;
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
                                ActivateDeepBumpFallback);
                            if (textureUsedFallback)
                            {
                                Interlocked.Exchange(ref textureUsedDeepBumpFallback, 1);
                            }

                            normal.Save(t.NormalPath);
                        }

                        if (Volatile.Read(ref textureUsedDeepBumpFallback) != 0)
                        {
                            Interlocked.Increment(ref deepBumpFallbackTextureCount);
                        }

                        var n = Interlocked.Increment(ref completed);
                        progress?.Report(new ConversionProgress(stage, n, total, t.Name, brickInfo));
                    });

                if (Volatile.Read(ref deepBumpFallbackTextureCount) > 0)
                {
                    var firstReason = Volatile.Read(ref deepBumpFirstFailureReason);
                    if (!string.IsNullOrWhiteSpace(firstReason))
                    {
                        progress?.Report(new ConversionProgress(
                            stage,
                            total,
                            total,
                            null,
                            $"DeepBump first failure: {firstReason}"));
                    }

                    progress?.Report(new ConversionProgress(
                        stage,
                        total,
                        total,
                        null,
                        $"DeepBump fallback used on {Volatile.Read(ref deepBumpFallbackTextureCount)} texture(s)."));
                }
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
            try
            {
                var overlap = options.DeepBumpOverlap switch
                {
                    "Small" => DeepBumpNormalsGenerator.Overlap.Small,
                    "Medium" => DeepBumpNormalsGenerator.Overlap.Medium,
                    _ => DeepBumpNormalsGenerator.Overlap.Large
                };

                normal = generatorForLoop.Generate(
                    diffuse,
                    overlap,
                    options.DeepBumpInputMode,
                    options.DeepBumpForceBlue255);
                var deepBumpIntensity = t.Overrides.NormalIntensity ?? options.DeepBumpNormalIntensity;
                DeepBumpEdgeGuidance? edgeGuidance = null;
                if (options.DeepBumpEdgeGuidedEnhance)
                {
                    edgeGuidance = BuildDeepBumpEdgeGuidance(diffuse, width, height, options);
                }

                ApplyDeepBumpNormalIntensity(
                    normal,
                    deepBumpIntensity,
                    options.DeepBumpNormalSoftClamp,
                    t.Overrides.InvertNormalRed,
                    t.Overrides.InvertNormalGreen,
                    edgeGuidance,
                    options.DeepBumpEdgeGuidedStrength,
                    options.DeepBumpEdgeGuidedGamma,
                    options.DeepBumpEdgeGuidedDirectionMix);
            }
            catch (OnnxRuntimeException ex)
            {
                usedClassicFallback = true;
                onDeepBumpFailure?.Invoke(ex.Message);
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

    private static Image<Rgba32> GenerateNormalMap(
        Image<Rgba32> cropped,
        int width,
        int height,
        float normalIntensity,
        bool invertR,
        bool invertG,
        AutoPbrOptions options)
    {
        normalIntensity = MathF.Max(normalIntensity, 1e-3f);

        var normalOperator = options.NormalOperator;
        var kernelSize = options.NormalKernelSize;
        var derivativeMode = options.NormalDerivative;

        var n = width * height;
        var grey = new float[n];
        cropped.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    var p = row[x];
                    if (options.PreprocessLinearize)
                    {
                        var r = PreprocessUtil.SrgbToLinear(p.R);
                        var g = PreprocessUtil.SrgbToLinear(p.G);
                        var b = PreprocessUtil.SrgbToLinear(p.B);
                        grey[y * width + x] = r * 0.2126f + g * 0.7152f + b * 0.0722f;
                    }
                    else
                    {
                        grey[y * width + x] = (p.R * 0.3f + p.G * 0.6f + p.B * 0.1f) / 255f;
                    }
                }
            }
        });

        if (options.PreprocessDenoiseRadius > 0)
        {
            var denoised = new float[n];
            Array.Copy(grey, denoised, n);
            PreprocessUtil.BoxBlurInPlace(denoised, width, height, options.PreprocessDenoiseRadius);
            var blend = Math.Clamp(options.PreprocessDenoiseBlend, 0f, 1f);
            var invBlend = 1f - blend;
            for (var i = 0; i < n; i++)
            {
                grey[i] = grey[i] * invBlend + denoised[i] * blend;
            }
        }

        if (options is { PreprocessFrequencySplit: true, PreprocessFrequencyRadius: > 0 })
        {
            var low = new float[n];
            Array.Copy(grey, low, n);
            PreprocessUtil.BoxBlurInPlace(low, width, height, options.PreprocessFrequencyRadius);
            var detail = options.PreprocessFrequencyDetailStrength;
            for (var i = 0; i < n; i++)
            {
                var high = grey[i] - low[i];
                grey[i] = Math.Clamp(low[i] + high * detail, 0f, 1f);
            }
        }

        if (derivativeMode is NormalDerivative.Luminance or NormalDerivative.ColorLuminanceBlend
            or NormalDerivative.ColorLuminanceMax)
        {
            var blurred = new float[n];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    float sum = 0;
                    var count = 0;
                    for (var oy = -1; oy <= 1; oy++)
                    {
                        for (var ox = -1; ox <= 1; ox++)
                        {
                            var rx = Reflect(x + ox, width);
                            var ry = Reflect(y + oy, height);
                            sum += grey[ry * width + rx];
                            count++;
                        }
                    }

                    blurred[y * width + x] = sum / count;
                }
            }

            const float amount = 0.5f;
            for (var i = 0; i < n; i++)
            {
                var v = grey[i] + amount * (grey[i] - blurred[i]);
                grey[i] = Math.Clamp(v, 0f, 1f);
            }
        }

        CreateGradientKernels(normalOperator, kernelSize, out var kx, out var ky, out var radius);

        var gx = new float[n];
        var gy = new float[n];

        switch (derivativeMode)
        {
            case NormalDerivative.Luminance:
                ComputeGradients(grey, width, height, kx, ky, radius, gx, gy);
                break;
            case NormalDerivative.Color:
                ComputeColorGradients(cropped, width, height, kx, ky, radius, gx, gy);
                break;
            case NormalDerivative.ColorLuminanceBlend:
                {
                    var gxL = new float[n];
                    var gyL = new float[n];
                    var gxC = new float[n];
                    var gyC = new float[n];
                    ComputeGradients(grey, width, height, kx, ky, radius, gxL, gyL);
                    ComputeColorGradients(cropped, width, height, kx, ky, radius, gxC, gyC);
                    for (var i = 0; i < n; i++)
                    {
                        gx[i] = 0.5f * gxL[i] + 0.5f * gxC[i];
                        gy[i] = 0.5f * gyL[i] + 0.5f * gyC[i];
                    }

                    break;
                }
            case NormalDerivative.ColorLuminanceMax:
                {
                    var gxL = new float[n];
                    var gyL = new float[n];
                    var gxC = new float[n];
                    var gyC = new float[n];
                    ComputeGradients(grey, width, height, kx, ky, radius, gxL, gyL);
                    ComputeColorGradients(cropped, width, height, kx, ky, radius, gxC, gyC);
                    for (var i = 0; i < n; i++)
                    {
                        var magL = MathF.Sqrt(gxL[i] * gxL[i] + gyL[i] * gyL[i]);
                        var magC = MathF.Sqrt(gxC[i] * gxC[i] + gyC[i] * gyC[i]);
                        if (magL >= magC)
                        {
                            gx[i] = gxL[i];
                            gy[i] = gyL[i];
                        }
                        else
                        {
                            gx[i] = gxC[i];
                            gy[i] = gyC[i];
                        }
                    }

                    break;
                }
        }

        var vcMag = new float[width * height];
        for (var i = 0; i < gx.Length; i++)
        {
            var gxv = gx[i];
            var gyv = gy[i];
            float sum = 0;
            for (var k = 0; k < VcOrientationCount; k++)
            {
                sum += MathF.Abs(gxv * VcCos[k] + gyv * VcSin[k]);
            }

            vcMag[i] = sum;
        }

        var gradMag = new float[width * height];
        for (var i = 0; i < gx.Length; i++)
        {
            var gxv = gx[i];
            var gyv = gy[i];
            gradMag[i] = MathF.Sqrt(gxv * gxv + gyv * gyv);
        }

        var maxGradMag = 0f;
        var maxVcMag = 0f;
        for (var i = 0; i < gradMag.Length; i++)
        {
            if (gradMag[i] > maxGradMag)
            {
                maxGradMag = gradMag[i];
            }

            if (vcMag[i] > maxVcMag)
            {
                maxVcMag = vcMag[i];
            }
        }
        const float eps = 1e-6f;
        if (maxGradMag < eps)
        {
            maxGradMag = 1f;
        }

        if (maxVcMag < eps)
        {
            maxVcMag = 1f;
        }

        var vcScale = maxGradMag / maxVcMag;
        var maxValue = 0f;
        for (var i = 0; i < gradMag.Length; i++)
        {
            var enhanced = MathF.Max(gradMag[i], vcMag[i] * vcScale);
            if (enhanced > maxValue)
            {
                maxValue = enhanced;
            }
        }

        if (maxValue < eps)
        {
            maxValue = 1f;
        }

        var intensity = 1f / normalIntensity;
        var z = intensity;

        var outImg = new Image<Rgba32>(width, height);
        outImg.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    var idx = y * width + x;
                    var gxv = gx[idx];
                    var gyv = gy[idx];
                    var mag = gradMag[idx];
                    var enhancedMag = MathF.Max(mag, vcMag[idx] * vcScale);
                    var scale = mag >= eps ? enhancedMag / mag : 0f;
                    var nx = -gxv * scale / maxValue;
                    var ny = -gyv * scale / maxValue;

                    var len = MathF.Sqrt(nx * nx + ny * ny + z * z);
                    if (len == 0)
                    {
                        len = 1;
                    }

                    nx /= len;
                    ny /= len;

                    var r = ToByte(nx);
                    var g = ToByte(ny);
                    var b = (byte)255;

                    if (invertR)
                    {
                        r = (byte)(255 - r);
                    }

                    if (invertG)
                    {
                        g = (byte)(255 - g);
                    }

                    row[x] = new Rgba32(r, g, b, 255);
                }
            }
        });

        return outImg;
    }

    private sealed class DeepBumpEdgeGuidance
    {
        public required float[] Edge01 { get; init; }
        public required float[] DirX { get; init; }
        public required float[] DirY { get; init; }
    }

    private static DeepBumpEdgeGuidance BuildDeepBumpEdgeGuidance(
        Image<Rgba32> diffuse,
        int width,
        int height,
        AutoPbrOptions options)
    {
        var n = width * height;
        var grey = new float[n];
        diffuse.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    var p = row[x];
                    if (options.PreprocessLinearize)
                    {
                        var r = PreprocessUtil.SrgbToLinear(p.R);
                        var g = PreprocessUtil.SrgbToLinear(p.G);
                        var b = PreprocessUtil.SrgbToLinear(p.B);
                        grey[y * width + x] = r * 0.2126f + g * 0.7152f + b * 0.0722f;
                    }
                    else
                    {
                        grey[y * width + x] = (p.R * 0.3f + p.G * 0.6f + p.B * 0.1f) / 255f;
                    }
                }
            }
        });

        if (options.PreprocessDenoiseRadius > 0)
        {
            var denoised = new float[n];
            Array.Copy(grey, denoised, n);
            PreprocessUtil.BoxBlurInPlace(denoised, width, height, options.PreprocessDenoiseRadius);
            var blend = Math.Clamp(options.PreprocessDenoiseBlend, 0f, 1f);
            var invBlend = 1f - blend;
            for (var i = 0; i < n; i++)
            {
                grey[i] = grey[i] * invBlend + denoised[i] * blend;
            }
        }

        if (options is { PreprocessFrequencySplit: true, PreprocessFrequencyRadius: > 0 })
        {
            var low = new float[n];
            Array.Copy(grey, low, n);
            PreprocessUtil.BoxBlurInPlace(low, width, height, options.PreprocessFrequencyRadius);
            var detail = options.PreprocessFrequencyDetailStrength;
            for (var i = 0; i < n; i++)
            {
                var high = grey[i] - low[i];
                grey[i] = Math.Clamp(low[i] + high * detail, 0f, 1f);
            }
        }

        var gx = new float[n];
        var gy = new float[n];
        int[,] kx =
        {
            { -1, 0, 1 },
            { -2, 0, 2 },
            { -1, 0, 1 }
        };
        int[,] ky =
        {
            { -1, -2, -1 },
            { 0, 0, 0 },
            { 1, 2, 1 }
        };

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                float sx = 0, sy = 0;
                for (var oy = -1; oy <= 1; oy++)
                {
                    for (var ox = -1; ox <= 1; ox++)
                    {
                        var rx = Reflect(x + ox, width);
                        var ry = Reflect(y + oy, height);
                        var v = grey[ry * width + rx];
                        sx += v * kx[oy + 1, ox + 1];
                        sy += v * ky[oy + 1, ox + 1];
                    }
                }

                var i = y * width + x;
                gx[i] = sx;
                gy[i] = sy;
            }
        }

        var edge = new float[n];
        var dirX = new float[n];
        var dirY = new float[n];
        var maxMag = 0f;
        for (var i = 0; i < n; i++)
        {
            var mag = MathF.Sqrt(gx[i] * gx[i] + gy[i] * gy[i]);
            edge[i] = mag;
            if (mag > maxMag)
            {
                maxMag = mag;
            }

            if (mag > 1e-8f)
            {
                // Match normal convention: brighter-to-darker slope points toward -gradient.
                dirX[i] = -gx[i] / mag;
                dirY[i] = -gy[i] / mag;
            }
            else
            {
                dirX[i] = 0f;
                dirY[i] = 0f;
            }
        }

        if (maxMag > 1e-8f)
        {
            var inv = 1f / maxMag;
            for (var i = 0; i < n; i++)
            {
                edge[i] = Math.Clamp(edge[i] * inv, 0f, 1f);
            }
        }

        return new DeepBumpEdgeGuidance
        {
            Edge01 = edge,
            DirX = dirX,
            DirY = dirY
        };
    }

    private static void ApplyDeepBumpNormalIntensity(
        Image<Rgba32> normal,
        float intensity,
        float softClamp,
        bool invertR,
        bool invertG,
        DeepBumpEdgeGuidance? guidance,
        float edgeGuidedStrength,
        float edgeGuidedGamma,
        float edgeGuidedDirectionMix)
    {
        intensity = MathF.Max(intensity, 1e-3f);
        softClamp = Math.Clamp(softClamp, 0f, 2f);
        edgeGuidedStrength = Math.Max(0f, edgeGuidedStrength);
        edgeGuidedGamma = Math.Clamp(edgeGuidedGamma, 0.1f, 8f);
        edgeGuidedDirectionMix = Math.Clamp(edgeGuidedDirectionMix, 0f, 1f);
        if (MathF.Abs(intensity - 1f) < 1e-4f)
        {
            if (!invertR && !invertG && softClamp <= 1e-4f && guidance is null)
            {
                return;
            }
        }

        normal.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < normal.Height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    var i = y * row.Length + x;
                    var nx = p.R / 127.5f - 1f;
                    var ny = p.G / 127.5f - 1f;
                    nx *= intensity;
                    ny *= intensity;
                    if (invertR)
                    {
                        nx = -nx;
                    }

                    if (invertG)
                    {
                        ny = -ny;
                    }

                    var xy2 = nx * nx + ny * ny;
                    if (guidance is not null)
                    {
                        var edge = guidance.Edge01[i];
                        if (edge > 1e-6f)
                        {
                            var w = MathF.Pow(edge, edgeGuidedGamma);
                            if (edgeGuidedStrength > 0f && xy2 > 1e-8f)
                            {
                                var m = MathF.Sqrt(xy2);
                                var gain = 1f + edgeGuidedStrength * w;
                                var targetM = Math.Min(0.999f, m * gain);
                                var s = targetM / m;
                                nx *= s;
                                ny *= s;
                                xy2 = nx * nx + ny * ny;
                            }

                            if (edgeGuidedDirectionMix > 0f)
                            {
                                var m = MathF.Sqrt(Math.Max(0f, xy2));
                                if (m > 1e-8f)
                                {
                                    var curX = nx / m;
                                    var curY = ny / m;
                                    var mixW = Math.Clamp(edgeGuidedDirectionMix * w, 0f, 1f);
                                    var tx = guidance.DirX[i];
                                    var ty = guidance.DirY[i];
                                    var blendX = curX * (1f - mixW) + tx * mixW;
                                    var blendY = curY * (1f - mixW) + ty * mixW;
                                    var blendLen = MathF.Sqrt(blendX * blendX + blendY * blendY);
                                    if (blendLen > 1e-8f)
                                    {
                                        blendX /= blendLen;
                                        blendY /= blendLen;
                                        nx = blendX * m;
                                        ny = blendY * m;
                                        xy2 = nx * nx + ny * ny;
                                    }
                                }
                            }
                        }
                    }

                    if (softClamp > 1e-4f && xy2 > 1e-8f)
                    {
                        var m = MathF.Sqrt(xy2);
                        const float maxM = 0.999f;
                        var t = Math.Clamp(m / maxM, 0f, 1f);
                        var curve = 1f + 3f * softClamp;
                        var curved = MathF.Tanh(t * curve) / MathF.Tanh(curve);
                        var targetM = curved * maxM;
                        var s = targetM / m;
                        nx *= s;
                        ny *= s;
                        xy2 = nx * nx + ny * ny;
                    }

                    if (xy2 > 0.999f)
                    {
                        var inv = MathF.Sqrt(0.999f / xy2);
                        nx *= inv;
                        ny *= inv;
                        xy2 = nx * nx + ny * ny;
                    }

                    var nz = MathF.Sqrt(MathF.Max(0f, 1f - xy2));
                    var r = ToByte(nx);
                    var g = ToByte(ny);
                    var b = ToByte(nz);
                    row[x] = new Rgba32(r, g, b, p.A);
                }
            }
        });
    }

    private static void ComputeGradients(
        float[] scalar,
        int width,
        int height,
        float[,] kx,
        float[,] ky,
        int radius,
        float[] gxOut,
        float[] gyOut)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                float sx = 0, sy = 0;
                for (var oy = -radius; oy <= radius; oy++)
                {
                    for (var ox = -radius; ox <= radius; ox++)
                    {
                        var rx = Reflect(x + ox, width);
                        var ry = Reflect(y + oy, height);
                        var v = scalar[ry * width + rx];
                        sx += v * kx[oy + radius, ox + radius];
                        sy += v * ky[oy + radius, ox + radius];
                    }
                }

                gxOut[y * width + x] = sx;
                gyOut[y * width + x] = sy;
            }
        }
    }

    private static void ComputeColorGradients(
        Image<Rgba32> img,
        int width,
        int height,
        float[,] kx,
        float[,] ky,
        int radius,
        float[] gxOut,
        float[] gyOut)
    {
        if (!img.DangerousTryGetSinglePixelMemory(out var mem))
        {
            var nFallback = width * height;
            var r = new float[nFallback];
            var g = new float[nFallback];
            var b = new float[nFallback];
            img.ProcessPixelRows(acc =>
            {
                for (var y = 0; y < height; y++)
                {
                    var row = acc.GetRowSpan(y);
                    for (var x = 0; x < width; x++)
                    {
                        var p = row[x];
                        var i = y * width + x;
                        r[i] = p.R / 255f;
                        g[i] = p.G / 255f;
                        b[i] = p.B / 255f;
                    }
                }
            });
            var gxR = new float[nFallback];
            var gyR = new float[nFallback];
            var gxG = new float[nFallback];
            var gyG = new float[nFallback];
            var gxB = new float[nFallback];
            var gyB = new float[nFallback];
            ComputeGradients(r, width, height, kx, ky, radius, gxR, gyR);
            ComputeGradients(g, width, height, kx, ky, radius, gxG, gyG);
            ComputeGradients(b, width, height, kx, ky, radius, gxB, gyB);
            for (var i = 0; i < nFallback; i++)
            {
                var magR = MathF.Sqrt(gxR[i] * gxR[i] + gyR[i] * gyR[i]);
                var magG = MathF.Sqrt(gxG[i] * gxG[i] + gyG[i] * gyG[i]);
                var magB = MathF.Sqrt(gxB[i] * gxB[i] + gyB[i] * gyB[i]);
                if (magR >= magG && magR >= magB)
                {
                    gxOut[i] = gxR[i];
                    gyOut[i] = gyR[i];
                }
                else if (magG >= magB)
                {
                    gxOut[i] = gxG[i];
                    gyOut[i] = gyG[i];
                }
                else
                {
                    gxOut[i] = gxB[i];
                    gyOut[i] = gyB[i];
                }
            }

            return;
        }

        var span = mem.Span;
        const float inv255 = 1f / 255f;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                float sxR = 0, syR = 0;
                float sxG = 0, syG = 0;
                float sxB = 0, syB = 0;
                for (var oy = -radius; oy <= radius; oy++)
                {
                    for (var ox = -radius; ox <= radius; ox++)
                    {
                        var rx = Reflect(x + ox, width);
                        var ry = Reflect(y + oy, height);
                        var p = span[ry * width + rx];
                        var kxv = kx[oy + radius, ox + radius];
                        var kyv = ky[oy + radius, ox + radius];

                        var rv = p.R * inv255;
                        var gv = p.G * inv255;
                        var bv = p.B * inv255;

                        sxR += rv * kxv;
                        syR += rv * kyv;
                        sxG += gv * kxv;
                        syG += gv * kyv;
                        sxB += bv * kxv;
                        syB += bv * kyv;
                    }
                }

                var idx = y * width + x;
                var magR = MathF.Sqrt(sxR * sxR + syR * syR);
                var magG = MathF.Sqrt(sxG * sxG + syG * syG);
                var magB = MathF.Sqrt(sxB * sxB + syB * syB);
                if (magR >= magG && magR >= magB)
                {
                    gxOut[idx] = sxR;
                    gyOut[idx] = syR;
                }
                else if (magG >= magB)
                {
                    gxOut[idx] = sxG;
                    gyOut[idx] = syG;
                }
                else
                {
                    gxOut[idx] = sxB;
                    gyOut[idx] = syB;
                }
            }
        }
    }

    private static void CreateGradientKernels(
        NormalOperator op,
        NormalKernelSize size,
        out float[,] kx,
        out float[,] ky,
        out int radius)
    {
        var n = (int)size;
        if (op == NormalOperator.ScharrVc && n > 5)
        {
            n = 5;
        }

        if (n < 3)
        {
            n = 3;
        }

        if (n % 2 == 0)
        {
            n++;
        }

        radius = n / 2;

        if (n == 3)
        {
            if (op == NormalOperator.ScharrVc)
            {
                kx = new float[,]
                {
                    { -3, 0, 3 },
                    { -10, 0, 10 },
                    { -3, 0, 3 }
                };
                ky = new float[,]
                {
                    { -3, -10, -3 },
                    { 0, 0, 0 },
                    { 3, 10, 3 }
                };
            }
            else
            {
                kx = new float[,]
                {
                    { -1, 0, 1 },
                    { -2, 0, 2 },
                    { -1, 0, 1 }
                };
                ky = new float[,]
                {
                    { -1, -2, -1 },
                    { 0, 0, 0 },
                    { 1, 2, 1 }
                };
            }

            radius = 1;
            return;
        }

        var smooth = new float[n];
        smooth[0] = 1;
        for (var i = 1; i < n; i++)
        {
            smooth[i] = 1;
            for (var j = i - 1; j > 0; j--)
            {
                smooth[j] = smooth[j] + smooth[j - 1];
            }
        }

        var smoothSum = smooth.Sum();
        if (smoothSum > 0)
        {
            for (var i = 0; i < n; i++)
            {
                smooth[i] /= smoothSum;
            }
        }

        var center = radius;
        var deriv = new float[n];
        for (var i = 0; i < n; i++)
        {
            var pos = i - center;
            deriv[i] = pos * smooth[i];
        }

        if (op == NormalOperator.ScharrVc)
        {
            for (var i = 0; i < n; i++)
            {
                var pos = Math.Abs(i - center);
                var boost = pos == 0 ? 0.5f : pos == 1 ? 1.5f : 1f;
                deriv[i] *= boost;
            }
        }

        var derivSum = deriv.Sum(Math.Abs);
        if (derivSum > 0)
        {
            for (var i = 0; i < n; i++)
            {
                deriv[i] /= derivSum;
            }
        }

        kx = new float[n, n];
        ky = new float[n, n];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                kx[y, x] = smooth[y] * deriv[x];
                ky[y, x] = deriv[y] * smooth[x];
            }
        }
    }

    private static byte ToByte(float v)
    {
        var scaled = (v * 0.5f + 0.5f) * 255f;
        return (byte)Math.Clamp((int)MathF.Round(scaled), 0, 255);
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

    private static int Reflect(int i, int max)
    {
        if (i < 0)
        {
            return -i - 1;
        }

        if (i >= max)
        {
            return max - (i - max) - 1;
        }

        return i;
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

    private static float[] BuildVcCos()
    {
        var arr = new float[VcOrientationCount];
        var step = MathF.PI / VcOrientationCount;
        for (var i = 0; i < VcOrientationCount; i++)
        {
            arr[i] = MathF.Cos(i * step);
        }

        return arr;
    }

    private static float[] BuildVcSin()
    {
        var arr = new float[VcOrientationCount];
        var step = MathF.PI / VcOrientationCount;
        for (var i = 0; i < VcOrientationCount; i++)
        {
            arr[i] = MathF.Sin(i * step);
        }

        return arr;
    }

    private static string NormalizeFirstDeepBumpFailureReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "unknown ONNX Runtime error";
        }

        var cleaned = reason
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        while (cleaned.Contains("  ", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        }

        cleaned = cleaned.Trim();
        const int maxLen = 220;
        if (cleaned.Length > maxLen)
        {
            cleaned = $"{cleaned[..maxLen]}...";
        }

        return cleaned;
    }
}

