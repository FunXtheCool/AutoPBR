using AutoPBR.Core.HeightFromNormals;
using AutoPBR.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AutoPBR.Core;

/// <summary>
/// Generates normal maps and encodes height information into the alpha channel.
/// </summary>
internal static class NormalHeightGenerator
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
                Parallel.ForEach(
                    toProcess,
                    new ParallelOptions
                    { MaxDegreeOfParallelism = ThreadingUtil.GetConversionParallelism(options), CancellationToken = ct },
                    t =>
                    {
                        ThreadingUtil.SetThreadName("AutoPBR.Normals");
                        ct.ThrowIfCancellationRequested();

                        using var diffuseImg = Image.Load<Rgba32>(t.DiffusePath);
                        using var croppedDiffuse = CropToSquare(diffuseImg, out var size);
                        var width = size;
                        var height = size;

                        Image<Rgba32> normal;
                        if (generatorForLoop != null)
                        {
                            var overlap = options.DeepBumpOverlap switch
                            {
                                "Small" => DeepBumpNormalsGenerator.Overlap.Small,
                                "Medium" => DeepBumpNormalsGenerator.Overlap.Medium,
                                _ => DeepBumpNormalsGenerator.Overlap.Large
                            };

                            Image<Rgba32> diffuseForNormals = croppedDiffuse;
                            normal = generatorForLoop.Generate(
                                diffuseForNormals,
                                overlap,
                                options.DeepBumpInputMode,
                                options.DeepBumpForceBlue255);
                            var deepBumpIntensity = t.Overrides.NormalIntensity ?? options.DeepBumpNormalIntensity;
                            DeepBumpEdgeGuidance? edgeGuidance = null;
                            if (options.DeepBumpEdgeGuidedEnhance)
                            {
                                edgeGuidance = BuildDeepBumpEdgeGuidance(croppedDiffuse, width, height, options);
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
                        else
                        {
                            var normalIntensity = t.Overrides.NormalIntensity ?? options.NormalIntensity;
                            normal = GenerateNormalMap(
                                croppedDiffuse,
                                width,
                                height,
                                normalIntensity,
                                t.Overrides.InvertNormalRed,
                                t.Overrides.InvertNormalGreen,
                                options);
                        }

                        using (normal)
                        {
                            var heightIntensity = t.Overrides.HeightIntensity ?? options.HeightIntensity;
                            var brightness = t.Overrides.HeightBrightness ?? AutoPbrDefaults.DefaultHeightBrightness;
                            var heightMap = GenerateHeightMap(croppedDiffuse, width, height, heightIntensity, brightness,
                                t.Overrides.InvertHeight, options);

                            var skipHeightInAlpha = t.IsPlantForNoHeight;
                            if (!skipHeightInAlpha && FoliageModeResolver.IsNoHeight(options.FoliageMode) &&
                                t.Sprite2DFoliageTarget &&
                                (t.Name.Contains("grass", StringComparison.OrdinalIgnoreCase) ||
                                 t.RelativeKey.Contains("grass", StringComparison.OrdinalIgnoreCase)))
                            {
                                skipHeightInAlpha = HasSignificantTransparency(croppedDiffuse);
                            }

                            // LabPBR: normal blue channel = AO (0 = 100% occlusion, 255 = 0% occlusion).
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

                                        // LabPBR: B channel = ambient occlusion when enabled; else 255 (no occlusion).
                                        var i = y * heightMap.Width + x;
                                        row[x].B = aoChannel != null ? aoChannel[i] : (byte)255;
                                    }
                                }
                            });

                            if (options.NormalHeightZeroTransparentPixels)
                            {
                                ApplyTransparentZeroClamp(
                                    normal,
                                    croppedDiffuse,
                                    Math.Clamp(options.NormalHeightTransparentAlphaClampMax, 0, 255));
                            }

                            normal.Save(t.NormalPath);
                        }

                        var n = Interlocked.Increment(ref completed);
                        progress?.Report(new ConversionProgress(stage, n, total, t.Name));
                    });
            }
            finally
            {
                deepBumpGenerator?.Dispose();
            }
        }, ct);
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

        const int vcOrientationCount = 12;
        const float vcAngleStep = MathF.PI / vcOrientationCount;
        var vcMag = new float[width * height];
        for (var i = 0; i < gx.Length; i++)
        {
            var gxv = gx[i];
            var gyv = gy[i];
            float sum = 0;
            for (var k = 0; k < vcOrientationCount; k++)
            {
                var a = k * vcAngleStep;
                sum += MathF.Abs(gxv * MathF.Cos(a) + gyv * MathF.Sin(a));
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

        var maxGradMag = gradMag.Max();
        var maxVcMag = vcMag.Max();
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
        var n = width * height;
        var r = new float[n];
        var g = new float[n];
        var b = new float[n];
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
        var gxR = new float[n];
        var gyR = new float[n];
        var gxG = new float[n];
        var gyG = new float[n];
        var gxB = new float[n];
        var gyB = new float[n];
        ComputeGradients(r, width, height, kx, ky, radius, gxR, gyR);
        ComputeGradients(g, width, height, kx, ky, radius, gxG, gyG);
        ComputeGradients(b, width, height, kx, ky, radius, gxB, gyB);
        for (var i = 0; i < n; i++)
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
            var lowest = outData.Min();
            var highest = outData.Max();
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

