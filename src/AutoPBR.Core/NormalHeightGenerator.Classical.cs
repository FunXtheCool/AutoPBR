using AutoPBR.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.Core;

internal static partial class NormalHeightGenerator
{
    private const int VcOrientationCount = 12;
    private static readonly float[] VcCos = BuildVcCos();
    private static readonly float[] VcSin = BuildVcSin();

    private static Image<Rgba32> GenerateNormalMap(
        Image<Rgba32> cropped,
        int width,
        int height,
        float normalIntensity,
        bool invertR,
        bool invertG,
        AutoPBROptions options)
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
