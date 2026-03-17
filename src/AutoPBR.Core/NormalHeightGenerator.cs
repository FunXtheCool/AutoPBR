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
            var toProcess = textures.Where(t => !t.SpecularOnly).ToList();
            var total = toProcess.Count;
            var completed = 0;
            DeepBumpNormalsGenerator? deepBumpGenerator = null;
            try
            {
                deepBumpGenerator =
                    options.UseDeepBumpNormals && !string.IsNullOrWhiteSpace(options.DeepBumpModelPath)
                        ? DeepBumpNormalsGenerator.TryCreate(options.DeepBumpModelPath!)
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
                            normal = generatorForLoop.Generate(diffuseForNormals, overlap);
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
                                options.NormalOperator,
                                options.NormalKernelSize,
                                options.NormalDerivative);
                        }

                        using (normal)
                        {
                            var heightIntensity = t.Overrides.HeightIntensity ?? options.HeightIntensity;
                            var brightness = t.Overrides.HeightBrightness ?? AutoPbrDefaults.DefaultHeightBrightness;
                            var heightMap = GenerateHeightMap(croppedDiffuse, width, height, heightIntensity, brightness,
                                t.Overrides.InvertHeight);

                            var skipHeightInAlpha = t.IsPlantForNoHeight;
                            if (!skipHeightInAlpha && options.FoliageMode == "No Height" &&
                                (t.Name.Contains("grass", StringComparison.OrdinalIgnoreCase) ||
                                 t.RelativeKey.Contains("grass", StringComparison.OrdinalIgnoreCase)))
                            {
                                skipHeightInAlpha = HasSignificantTransparency(croppedDiffuse);
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
                                    }
                                }
                            });

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

    private static Image<Rgba32> GenerateNormalMap(
        Image<Rgba32> cropped,
        int width,
        int height,
        float normalIntensity,
        bool invertR,
        bool invertG,
        NormalOperator normalOperator = NormalOperator.SobelVc,
        NormalKernelSize kernelSize = NormalKernelSize.K3,
        NormalDerivative derivativeMode = NormalDerivative.Luminance)
    {
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
                    grey[y * width + x] = (p.R * 0.3f + p.G * 0.6f + p.B * 0.1f) / 255f;
                }
            }
        });

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
        float brightness, bool invertHeight)
    {
        var grey = new byte[width * height];
        cropped.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    var p = row[x];
                    var v = (int)MathF.Round(p.R * 0.3f + p.G * 0.6f + p.B * 0.1f);
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

        return img.Clone(ctx => ctx.Crop(new Rectangle(0, 0, s, s)));
    }
}

