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
