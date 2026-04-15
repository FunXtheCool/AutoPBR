using AutoPBR.Core.Models;
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

        const int vcOrientationCount = 12;
        const float vcAngleStep = MathF.PI / vcOrientationCount;
        var edge = new float[width * height];
        for (var i = 0; i < gx.Length; i++)
        {
            var gxv = gx[i];
            var gyv = gy[i];
            float sum = 0;
            for (var k = 0; k < vcOrientationCount; k++)
            {
                var a = k * vcAngleStep;
                var r = gxv * MathF.Cos(a) + gyv * MathF.Sin(a);
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

            var rgbToLab = new ConverterBuilder()
                .FromRGB(RGBWorkingSpaces.sRGB)
                .ToLab(Illuminants.D65)
                .Build();

            var de2000 = new CIEDE2000ColorDifference();
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

                    var fast = t.Overrides.FastSpecular ?? options.FastSpecular;
                    var rules = t.Overrides.CustomSpecularRules
                                ?? options.SpecularData!.ByTextureName.GetValueOrDefault(t.Name)
                                ?? options.SpecularData.ByTextureName.GetValueOrDefault("*");

                    using var img = Image.Load<Rgba32>(t.DiffusePath);
                    using var cropped = CropToSquare(img, out var size);
                    var width = size;
                    var height = size;

                    // Ignore All: skip grass textures with significant transparency in diffuse (2D Sprite / foliage targets only)
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
                        int lowAlphaCount = 0;
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
                            var done = Interlocked.Increment(ref completed);
                            progress?.Report(new ConversionProgress(stage, done, total, t.Name));
                            return;
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
                    var porosityExtra = options.PorosityBias +
                                        (t.HasPlantMaterialTag ? options.PlantMaterialPorosityExtra : 0);
                    porosityExtra = Math.Clamp(porosityExtra, -512, 512);
                    var mlDriven = new bool[nPixels];

                    if (!useMlSpec && noHeuristic)
                    {
                        for (var idx = 0; idx < nPixels; idx++)
                        {
                            rBuf[idx] = 255;
                            gBuf[idx] = 0;
                            bBuf[idx] = 255;
                            aBuf[idx] = 255;
                            mlDriven[idx] = false;
                        }
                    }
                    else
                    {
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

                            if (!useMlSpec)
                            {
                                rBuf[idx] = (byte)Math.Clamp(hr, 0, 255);
                                gBuf[idx] = (byte)Math.Clamp((int)hg, 0, 255);
                                bBuf[idx] = (byte)Math.Clamp(hb, 0, 255);
                                aBuf[idx] = ha;
                                mlDriven[idx] = false;
                                continue;
                            }

                            var mix = useMlSpec ? blendSlider : 0f;
                            if (mix > 0f)
                            {
                                var mr = mlSpecR[idx];
                                var mg = mlSpecG[idx];
                                var mb = mlSpecB[idx];
                                var ma = mlSpecA[idx];
                                var mbAdj = Math.Clamp(mb + porosityExtra, 0, 255);

                                // Full: heuristic contributes to R/G/B/A — lerp every channel.
                                // Smoothness only: heuristic contributes only to R; G/B/A come from model.
                                // AI Metal & Emissive: like Smoothness only, but B (porosity) stays heuristic.
                                if (heuristicBlendMode == MlSpecularHeuristicBlendMode.SmoothnessOnly)
                                {
                                    var fr = BlendChannel(hr, mr, mix, blendMath);
                                    rBuf[idx] = (byte)Math.Clamp(Math.Round(fr), 0, 255);
                                    gBuf[idx] = mg;
                                    bBuf[idx] = (byte)mbAdj;
                                    aBuf[idx] = ma;
                                    mlDriven[idx] = mix >= 0.999f;
                                }
                                else if (heuristicBlendMode == MlSpecularHeuristicBlendMode.AiMetalAndEmissive)
                                {
                                    var fr = BlendChannel(hr, mr, mix, blendMath);
                                    rBuf[idx] = (byte)Math.Clamp(Math.Round(fr), 0, 255);
                                    gBuf[idx] = mg;
                                    bBuf[idx] = (byte)Math.Clamp(hb, 0, 255);
                                    aBuf[idx] = ma;
                                    mlDriven[idx] = mix >= 0.999f;
                                }
                                else
                                {
                                    var fr = BlendChannel(hr, mr, mix, blendMath);
                                    var fg = BlendChannel(hg, mg, mix, blendMath);
                                    var fb = BlendChannel(hb, mbAdj, mix, blendMath);
                                    var fa = BlendChannel(ha, ma, mix, blendMath);

                                    rBuf[idx] = (byte)Math.Clamp(Math.Round(fr), 0, 255);
                                    gBuf[idx] = (byte)Math.Clamp(Math.Round(fg), 0, 255);
                                    bBuf[idx] = (byte)Math.Clamp(Math.Round(fb), 0, 255);
                                    aBuf[idx] = (byte)Math.Clamp(Math.Round(fa), 0, 255);
                                    mlDriven[idx] = mix >= 0.999f;
                                }
                            }
                            else
                            {
                                rBuf[idx] = (byte)Math.Clamp(hr, 0, 255);
                                gBuf[idx] = (byte)Math.Clamp((int)hg, 0, 255);
                                bBuf[idx] = (byte)Math.Clamp(hb, 0, 255);
                                aBuf[idx] = ha;
                                mlDriven[idx] = false;
                            }
                        }
                    }

                    // Per-texture R normalization: remap to 10–200 when there is variation.
                    // With MlSpecularSkipSmoothnessRemap + ML, stats and remap apply only to non-ML pixels so model R stays faithful.
                    // Percentile remap is more robust than min/max on noisy low-res inputs.
                    var selectiveMlRemap = options.MlSpecularSkipSmoothnessRemap && useMlSpec;
                    byte minR;
                    byte maxR;
                    if (options.SpecularDebugSkipSpecularRemap)
                    {
                        minR = 0;
                        maxR = 0;
                    }
                    else if (selectiveMlRemap)
                    {
                        var nHeuristic = 0;
                        for (var i = 0; i < nPixels; i++)
                        {
                            if (!mlDriven[i])
                            {
                                nHeuristic++;
                            }
                        }

                        if (nHeuristic == 0)
                        {
                            minR = 0;
                            maxR = 0;
                        }
                        else if (nPixels > 0 && options.SpecularUsePercentileRemap)
                        {
                            var lowP = Math.Clamp(options.SpecularRemapLowPercentile, 0f, 1f);
                            var highP = Math.Clamp(options.SpecularRemapHighPercentile, 0f, 1f);
                            if (highP < lowP)
                            {
                                (lowP, highP) = (highP, lowP);
                            }

                            var sorted = new byte[nHeuristic];
                            var w = 0;
                            for (var i = 0; i < nPixels; i++)
                            {
                                if (!mlDriven[i])
                                {
                                    sorted[w++] = rBuf[i];
                                }
                            }

                            Array.Sort(sorted);
                            var last = nHeuristic - 1;
                            minR = sorted[(int)MathF.Round(lowP * last)];
                            maxR = sorted[(int)MathF.Round(highP * last)];
                        }
                        else
                        {
                            minR = 255;
                            maxR = 0;
                            for (var i = 0; i < nPixels; i++)
                            {
                                if (mlDriven[i])
                                {
                                    continue;
                                }

                                var v = rBuf[i];
                                if (v < minR)
                                {
                                    minR = v;
                                }

                                if (v > maxR)
                                {
                                    maxR = v;
                                }
                            }
                        }
                    }
                    else if (nPixels > 0 && options.SpecularUsePercentileRemap)
                    {
                        var lowP = Math.Clamp(options.SpecularRemapLowPercentile, 0f, 1f);
                        var highP = Math.Clamp(options.SpecularRemapHighPercentile, 0f, 1f);
                        if (highP < lowP)
                        {
                            (lowP, highP) = (highP, lowP);
                        }

                        var sorted = new byte[nPixels];
                        Array.Copy(rBuf, sorted, nPixels);
                        Array.Sort(sorted);
                        minR = sorted[(int)MathF.Round(lowP * (nPixels - 1))];
                        maxR = sorted[(int)MathF.Round(highP * (nPixels - 1))];
                    }
                    else
                    {
                        minR = 255;
                        maxR = 0;
                        for (var i = 0; i < nPixels; i++)
                        {
                            var v = rBuf[i];
                            if (v < minR)
                            {
                                minR = v;
                            }

                            if (v > maxR)
                            {
                                maxR = v;
                            }
                        }
                    }

                    if (!options.SpecularDebugSkipSpecularRemap && maxR > minR)
                    {
                        var denom = maxR - minR;
                        if (selectiveMlRemap)
                        {
                            for (var i = 0; i < nPixels; i++)
                            {
                                if (mlDriven[i])
                                {
                                    continue;
                                }

                                rBuf[i] = (byte)Math.Clamp(10 + (rBuf[i] - minR) * 190 / denom, 0, 255);
                            }
                        }
                        else
                        {
                            for (var i = 0; i < nPixels; i++)
                            {
                                rBuf[i] = (byte)Math.Clamp(10 + (rBuf[i] - minR) * 190 / denom, 0, 255);
                            }
                        }
                    }

                    if (t.Overrides.InvertSpecular)
                    {
                        for (var i = 0; i < nPixels; i++)
                        {
                            rBuf[i] = (byte)(255 - rBuf[i]);
                        }
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

                    var hasData = false;

                    using (var outImg = new Image<Rgba32>(width, height))
                    {
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

                        if (hasData)
                        {
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
                        useMlSpec,
                        mlSpecularDiagnostic);
                    var combinedLog = string.Join(" | ", new[] { specLog }.Where(s => !string.IsNullOrWhiteSpace(s)));

                    var n = Interlocked.Increment(ref completed);
                    progress?.Report(new ConversionProgress(stage, n, total, t.Name, string.IsNullOrWhiteSpace(combinedLog) ? null : combinedLog));
                });
        }, ct);
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

    private static Image<Rgba32> CropToSquare(Image<Rgba32> img, out int size)
    {
        var s = Math.Min(img.Width, img.Height);
        size = s;
        if (img.Width == s && img.Height == s)
        {
            return img.Clone();
        }

        var startX = (img.Width - s) / 2;
        var startY = (img.Height - s) / 2;
        return img.Clone(ctx => ctx.Crop(new Rectangle(startX, startY, s, s)));
    }
}

