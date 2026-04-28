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
internal static class SpecularGenerator
{
    /// <summary>LabPBR: G channel &lt;= this value is F0 (dielectric); 230+ = metal.</summary>
    private const byte LabPbrF0CapDielectric = 229;
    private const int VcOrientationCount = 12;
    private static readonly float[] VcCos = BuildVcCos();
    private static readonly float[] VcSin = BuildVcSin();

    private static string? BuildSpecularModelLogLine(
        string textureName,
        AutoPbrOptions options,
        bool useMlSpecular,
        string? mlDiagnostic)
    {
        if (!options.UseMlSpecularPredictor)
        {
            return null;
        }

        string? core;
        if (!useMlSpecular)
        {
            core = $"[{textureName}] Specular: heuristic/tag path only (ML specular did not produce a tensor — see diagnostic below if present).";
        }
        else
        {
            var blend = Math.Clamp(options.MlSpecularHeuristicBlend, 0f, 1f);
            var mode = options.MlSpecularHeuristicBlendMode;
            core = $"[{textureName}] Specular: ML+heuristic mix (blend {blend:0.##}, mode {mode}).";
        }

        var parts = new List<string> { core };
        if (options.SpecularDebugDisableHeuristicSpecular)
        {
            parts.Add("debugNoHeuristic=magenta fallback");
        }

        if (options.SpecularDebugSkipSpecularRemap)
        {
            parts.Add("debugSkipRRemap");
        }

        if (!string.IsNullOrWhiteSpace(mlDiagnostic))
        {
            parts.Add(mlDiagnostic);
        }

        return string.Join(" | ", parts);
    }

    private static (float[] luminance, float[] edgeMagnitude, float meanLuminance) BuildLuminanceAndEdge(
        Image<Rgba32> cropped, int width, int height, AutoPbrOptions options)
    {
        var lum = new float[width * height];
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
                        lum[y * width + x] = r * 0.2126f + g * 0.7152f + b * 0.0722f;
                    }
                    else
                    {
                        lum[y * width + x] = (p.R * 0.3f + p.G * 0.6f + p.B * 0.1f) / 255f;
                    }
                }
            }
        });

        if (options.PreprocessDenoiseRadius > 0)
        {
            var denoised = new float[lum.Length];
            Array.Copy(lum, denoised, lum.Length);
            PreprocessUtil.BoxBlurInPlace(denoised, width, height, options.PreprocessDenoiseRadius);
            var blend = Math.Clamp(options.PreprocessDenoiseBlend, 0f, 1f);
            var invBlend = 1f - blend;
            for (var i = 0; i < lum.Length; i++)
            {
                lum[i] = lum[i] * invBlend + denoised[i] * blend;
            }
        }

        var sumLum = 0.0;
        foreach (var value in lum)
        {
            sumLum += value;
        }

        var meanLum = (float)(sumLum / lum.Length);

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
        var gx = new float[width * height];
        var gy = new float[width * height];
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
                        var v = lum[ry * width + rx];
                        sx += v * kx[oy + 1, ox + 1];
                        sy += v * ky[oy + 1, ox + 1];
                    }
                }

                gx[y * width + x] = sx;
                gy[y * width + x] = sy;
            }
        }

        var edge = new float[width * height];
        for (var i = 0; i < gx.Length; i++)
        {
            var gxv = gx[i];
            var gyv = gy[i];
            float sum = 0;
            for (var k = 0; k < VcOrientationCount; k++)
            {
                var r = gxv * VcCos[k] + gyv * VcSin[k];
                sum += MathF.Abs(r);
            }

            edge[i] = sum;
        }

        var maxEdge = 0f;
        foreach (var e in edge)
        {
            if (e > maxEdge)
            {
                maxEdge = e;
            }
        }

        if (maxEdge > 0f)
        {
            for (var i = 0; i < edge.Length; i++)
            {
                edge[i] = Math.Clamp(edge[i] / maxEdge, 0f, 1f);
            }
        }

        return (lum, edge, meanLum);
    }

    internal static double BlendChannel(double heuristic, double ml, float mix, MlSpecularBlendMath math)
    {
        var h = Math.Clamp(heuristic / 255.0, 0.0, 1.0);
        var m = Math.Clamp(ml / 255.0, 0.0, 1.0);
        var t = Math.Clamp(mix, 0f, 1f);
        if (t <= 0f)
        {
            return heuristic;
        }

        static double Lerp(double a, double b, double u) => a + (b - a) * u;
        static double ToByte(double unit) => Math.Clamp(unit * 255.0, 0.0, 255.0);
        static double SoftLight(double baseVal, double blendVal)
        {
            if (blendVal <= 0.5)
            {
                return baseVal - (1.0 - 2.0 * blendVal) * baseVal * (1.0 - baseVal);
            }

            var d = baseVal <= 0.25
                ? ((16.0 * baseVal - 12.0) * baseVal + 4.0) * baseVal
                : Math.Sqrt(baseVal);
            return baseVal + (2.0 * blendVal - 1.0) * (d - baseVal);
        }

        static double Overlay(double baseVal, double blendVal) =>
            baseVal < 0.5 ? 2.0 * baseVal * blendVal : 1.0 - 2.0 * (1.0 - baseVal) * (1.0 - blendVal);
        static double Screen(double baseVal, double blendVal) => 1.0 - (1.0 - baseVal) * (1.0 - blendVal);
        static double Bias(double x, double b)
        {
            var bb = Math.Clamp(b, 0.001, 0.999);
            return x / ((((1.0 / bb) - 2.0) * (1.0 - x)) + 1.0);
        }

        static double Gain(double x, double g)
        {
            if (x < 0.5)
            {
                return 0.5 * Bias(2.0 * x, g);
            }

            return 1.0 - 0.5 * Bias(2.0 - 2.0 * x, g);
        }

        static double Logit(double x)
        {
            var xx = Math.Clamp(x, 0.001, 0.999);
            return Math.Log(xx / (1.0 - xx));
        }

        static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));

        var blended = math switch
        {
            MlSpecularBlendMath.SoftLight => Lerp(h, SoftLight(h, m), t),
            MlSpecularBlendMath.Overlay => Lerp(h, Overlay(h, m), t),
            MlSpecularBlendMath.Screen => Lerp(h, Screen(h, m), t),
            MlSpecularBlendMath.BiasGain => Lerp(h, Gain(h, m), t),
            MlSpecularBlendMath.SigmoidCrossfade => Sigmoid(Lerp(Logit(h), Logit(m), t)),
            _ => Lerp(h, m, t)
        };

        return ToByte(blended);
    }

    public static Task GenerateAsync(
        IReadOnlyList<TextureWorkItem> textures,
        AutoPbrOptions options,
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
                        var tileWorkerCount = total <= 1
                            ? Math.Min(Math.Max(1, ThreadingUtil.GetConversionParallelism(options)), Math.Max(1, tiles.Count))
                            : 1;
                        if (tileWorkerCount <= 1)
                        {
                            foreach (var tile in tiles)
                            {
                                ct.ThrowIfCancellationRequested();
                                var tileRgbToLab = rgbToLabByThread.Value ??
                                                   throw new InvalidOperationException("Thread-local Lab converter not available.");
                                var tileDe2000 = de2000ByThread.Value ??
                                                 throw new InvalidOperationException("Thread-local CIEDE2000 not available.");
                                using var tileDiffuse = AtlasTiling.ExtractTile(img, tile);
                                var tileResult = GenerateSpecularForDiffuseTile(
                                    tileDiffuse,
                                    t,
                                    options,
                                    tileRgbToLab,
                                    tileDe2000);
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
                                        var tileRgbToLab = rgbToLabByThread.Value ??
                                                           throw new InvalidOperationException("Thread-local Lab converter not available.");
                                        var tileDe2000 = de2000ByThread.Value ??
                                                         throw new InvalidOperationException("Thread-local CIEDE2000 not available.");
                                        var tileDiffuse = tileDiffuses[i] ?? throw new InvalidOperationException("Atlas tile extraction failed.");
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

                    var specLog = BuildSpecularModelLogLine(
                        t.Name,
                        options,
                        anyUseMlSpec,
                        mlDiagnostic);
                    var combinedLog = string.Join(" | ", new[] { specLog }.Where(s => !string.IsNullOrWhiteSpace(s)));

                    var n = Interlocked.Increment(ref completed);
                    progress?.Report(new ConversionProgress(stage, n, total, t.Name, string.IsNullOrWhiteSpace(combinedLog) ? null : combinedLog));
                });
        }, ct);
    }

    private sealed class SpecularTileResult
    {
        public required bool HasData { get; init; }
        public required bool UseMlSpec { get; init; }
        public required string? MlDiagnostic { get; init; }
        public Image<Rgba32>? Image { get; init; }
    }

    private static SpecularTileResult GenerateSpecularForDiffuseTile(
        Image<Rgba32> cropped,
        TextureWorkItem t,
        AutoPbrOptions options,
        IColorConverter<RGBColor, LabColor> rgbToLab,
        CIEDE2000ColorDifference de2000)
    {
        var fast = t.Overrides.FastSpecular ?? options.FastSpecular;
        var rules = t.Overrides.CustomSpecularRules
                    ?? options.SpecularData!.ByTextureName.GetValueOrDefault(t.Name)
                    ?? options.SpecularData.ByTextureName.GetValueOrDefault("*");

        var width = cropped.Width;
        var height = cropped.Height;
        if (FoliageModeResolver.IsIgnoreAll(options.FoliageMode) &&
            t.Sprite2DFoliageTarget &&
            (t.Name.Contains("grass", StringComparison.OrdinalIgnoreCase) ||
             t.RelativeKey.Contains("grass", StringComparison.OrdinalIgnoreCase)))
        {
            if (!cropped.DangerousTryGetSinglePixelMemory(out var alphaCheckMem))
            {
                throw new InvalidOperationException("Expected contiguous pixel memory.");
            }

            var alphaSpan = alphaCheckMem.Span;
            long sumA = 0;
            var lowAlphaCount = 0;
            var pixelCount = width * height;
            for (var i = 0; i < pixelCount; i++)
            {
                var a = alphaSpan[i].A;
                sumA += a;
                if (a < 128)
                {
                    lowAlphaCount++;
                }
            }

            var meanAlpha = (int)(sumA / pixelCount);
            if (meanAlpha < 200 || lowAlphaCount > 0.3 * pixelCount)
            {
                return new SpecularTileResult { HasData = false, UseMlSpec = false, MlDiagnostic = null, Image = null };
            }
        }

        List<(SpecularRule Rule, LabColor Lab)>? rulesLab = null;
        if (!fast && rules is not null)
        {
            rulesLab = new List<(SpecularRule, LabColor)>(rules.Count);
            foreach (var r in rules)
            {
                var rgb = RGBColor.FromRGB8Bit(r.ColorR, r.ColorG, r.ColorB);
                rulesLab.Add((r, rgbToLab.Convert(rgb)));
            }
        }

        var (luminance, edgeMagnitude, meanLuminance) = BuildLuminanceAndEdge(cropped, width, height, options);
        var useMlSpec = MlSpecularInference.TryPredictSpecular(
            cropped,
            edgeMagnitude,
            options,
            out var mlSpecR,
            out var mlSpecG,
            out var mlSpecB,
            out var mlSpecA,
            out var mlSpecularDiagnostic);
        var nPixels = width * height;
        var rBuf = new byte[nPixels];
        var gBuf = new byte[nPixels];
        var bBuf = new byte[nPixels];
        var aBuf = new byte[nPixels];
        if (!cropped.DangerousTryGetSinglePixelMemory(out var inMem))
        {
            throw new InvalidOperationException("Expected contiguous pixel memory.");
        }

        var inSpan = inMem.Span;
        var noHeuristic = options is { SpecularDebugDisableHeuristicSpecular: true, UseMlSpecularPredictor: true };
        var blendSlider = Math.Clamp(options.MlSpecularHeuristicBlend, 0f, 1f);
        var heuristicBlendMode = options.MlSpecularHeuristicBlendMode;
        var blendMath = options.MlSpecularBlendMath;
        var porosityExtra = options.PorosityBias + (t.HasPlantMaterialTag ? options.PlantMaterialPorosityExtra : 0);
        porosityExtra = Math.Clamp(porosityExtra, -512, 512);
        var mlDriven = new bool[nPixels];

        for (var idx = 0; idx < nPixels; idx++)
        {
            var p = inSpan[idx];
            var spec = GetSpecularRgba(p, rules, rulesLab, fast, rgbToLab, de2000);
            var lum = luminance[idx];
            var edge = edgeMagnitude[idx];
            var hg = Math.Min(spec.g, LabPbrF0CapDielectric);
            var smoothMul = options.SmoothnessScale;
            var hr = (int)Math.Min(255, spec.r * smoothMul);
            hr = (int)(hr * (1f - 0.2f * edge));
            if (lum > 0.92f && meanLuminance < 0.25f)
            {
                hr = Math.Min(hr, 220);
            }

            var hb = Math.Clamp(spec.b + porosityExtra, 0, 255);
            var ha = spec.a;

            if (noHeuristic && useMlSpec)
            {
                rBuf[idx] = mlSpecR[idx];
                gBuf[idx] = mlSpecG[idx];
                bBuf[idx] = (byte)Math.Clamp(mlSpecB[idx] + porosityExtra, 0, 255);
                aBuf[idx] = mlSpecA[idx];
                mlDriven[idx] = true;
                continue;
            }

            if (!useMlSpec || blendSlider <= 0f)
            {
                rBuf[idx] = (byte)Math.Clamp(hr, 0, 255);
                gBuf[idx] = (byte)Math.Clamp((int)hg, 0, 255);
                bBuf[idx] = (byte)Math.Clamp(hb, 0, 255);
                aBuf[idx] = ha;
                mlDriven[idx] = false;
                continue;
            }

            var mr = mlSpecR[idx];
            var mg = mlSpecG[idx];
            var mb = mlSpecB[idx];
            var ma = mlSpecA[idx];
            var mbAdj = Math.Clamp(mb + porosityExtra, 0, 255);

            if (heuristicBlendMode == MlSpecularHeuristicBlendMode.SmoothnessOnly)
            {
                var fr = BlendChannel(hr, mr, blendSlider, blendMath);
                rBuf[idx] = (byte)Math.Clamp(Math.Round(fr), 0, 255);
                gBuf[idx] = mg;
                bBuf[idx] = (byte)mbAdj;
                aBuf[idx] = ma;
                mlDriven[idx] = blendSlider >= 0.999f;
            }
            else if (heuristicBlendMode == MlSpecularHeuristicBlendMode.AiMetalAndEmissive)
            {
                var fr = BlendChannel(hr, mr, blendSlider, blendMath);
                rBuf[idx] = (byte)Math.Clamp(Math.Round(fr), 0, 255);
                gBuf[idx] = mg;
                bBuf[idx] = (byte)Math.Clamp(hb, 0, 255);
                aBuf[idx] = ma;
                mlDriven[idx] = blendSlider >= 0.999f;
            }
            else
            {
                var fr = BlendChannel(hr, mr, blendSlider, blendMath);
                var fg = BlendChannel(hg, mg, blendSlider, blendMath);
                var fb = BlendChannel(hb, mbAdj, blendSlider, blendMath);
                var fa = BlendChannel(ha, ma, blendSlider, blendMath);
                rBuf[idx] = (byte)Math.Clamp(Math.Round(fr), 0, 255);
                gBuf[idx] = (byte)Math.Clamp(Math.Round(fg), 0, 255);
                bBuf[idx] = (byte)Math.Clamp(Math.Round(fb), 0, 255);
                aBuf[idx] = (byte)Math.Clamp(Math.Round(fa), 0, 255);
                mlDriven[idx] = blendSlider >= 0.999f;
            }
        }

        byte minR = rBuf.Min();
        byte maxR = rBuf.Max();
        if (!options.SpecularDebugSkipSpecularRemap && maxR > minR)
        {
            var denom = maxR - minR;
            for (var i = 0; i < nPixels; i++)
            {
                if (options.MlSpecularSkipSmoothnessRemap && useMlSpec && mlDriven[i])
                {
                    continue;
                }

                rBuf[i] = (byte)Math.Clamp(10 + (rBuf[i] - minR) * 190 / denom, 0, 255);
            }
        }

        var invertSpecularR = t.Overrides.InvertSpecular;
        var brickProbeGlobalInvert = t.Overrides.BrickProbeAppliedGlobalInvert;
        if (t.HasBrickMaterialTag && options is { BrickSpecularAlignWithHeightProbe: true, BrickHeightMapPostProcessEnabled: true } && brickProbeGlobalInvert.HasValue)
        {
            invertSpecularR = brickProbeGlobalInvert.Value;
        }

        if (invertSpecularR)
        {
            for (var i = 0; i < nPixels; i++)
            {
                rBuf[i] = (byte)(255 - rBuf[i]);
            }
        }

        // Guardrail: particles and organic/plant-like textures should never become metallic.
        if (t.SpecularOnly || t.HasPlantMaterialTag)
        {
            Array.Fill(gBuf, (byte)0);
        }

        if (options.MlSpecularZeroTransparentPixels)
        {
            var alphaClampMax = Math.Clamp(options.MlSpecularTransparentAlphaClampMax, 0, 255);
            for (var i = 0; i < nPixels; i++)
            {
                if (inSpan[i].A <= alphaClampMax)
                {
                    rBuf[i] = 0;
                    gBuf[i] = 0;
                    bBuf[i] = 0;
                    aBuf[i] = 0;
                }
            }
        }

        var outImg = new Image<Rgba32>(width, height);
        var hasData = false;
        outImg.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    var idx = y * width + x;
                    var r = rBuf[idx];
                    var g = gBuf[idx];
                    var b = bBuf[idx];
                    var a = aBuf[idx];
                    if (r != 0 || g != 0 || b != 0 || a != 255)
                    {
                        hasData = true;
                    }

                    row[x] = new Rgba32(r, g, b, a);
                }
            }
        });

        if (!hasData)
        {
            outImg.Dispose();
        }

        return new SpecularTileResult
        {
            HasData = hasData,
            UseMlSpec = useMlSpec,
            MlDiagnostic = mlSpecularDiagnostic,
            Image = hasData ? outImg : null
        };
    }

    private static (byte r, byte g, byte b, byte a) GetSpecularRgba(
        Rgba32 pixel,
        IReadOnlyList<SpecularRule>? rules,
        List<(SpecularRule Rule, LabColor Lab)>? rulesLab,
        bool fast,
        IColorConverter<RGBColor, LabColor> rgbToLab,
        CIEDE2000ColorDifference de2000)
    {
        if (rules is null || rules.Count == 0)
        {
            return (0, 0, 0, 255); // LabPBR: alpha 255 = no emission
        }

        var pr = pixel.R;
        var pg = pixel.G;
        var pb = pixel.B;

        var bestIdx = -1;
        double best = double.MaxValue;

        if (fast)
        {
            for (var i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                var d = FastDistance(pr, pg, pb, r.ColorR, r.ColorG, r.ColorB);
                if (d < best)
                {
                    best = d;
                    bestIdx = i;
                }
            }

            var bestRule = rules[bestIdx];
            return (bestRule.SpecR, bestRule.SpecG, bestRule.SpecB, bestRule.SpecA);
        }

        var pixLab = rgbToLab.Convert(RGBColor.FromRGB8Bit(pr, pg, pb));
        if (rulesLab is null)
        {
            return (0, 0, 0, 255);
        }

        for (var i = 0; i < rulesLab.Count; i++)
        {
            var d = de2000.ComputeDifference(pixLab, rulesLab[i].Lab);
            if (d < best)
            {
                best = d;
                bestIdx = i;
            }
        }

        var rule2 = rulesLab[bestIdx].Rule;
        return (rule2.SpecR, rule2.SpecG, rule2.SpecB, rule2.SpecA);
    }

    private static double FastDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var cR = r1 - r2;
        var cG = g1 - g2;
        var cB = b1 - b2;
        var uR = r1 + r2;
        return cR * cR * (2 + uR / 256.0) + cG * cG * 4 + cB * cB * (2 + (255 - uR) / 256.0);
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
}

