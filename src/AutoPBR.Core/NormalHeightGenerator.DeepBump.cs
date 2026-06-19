using AutoPBR.Core.HeightFromNormals;
using AutoPBR.Core.Models;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.Core;

internal static partial class NormalHeightGenerator
{
    internal static DeepBumpNormalsGenerator? CreateDeepBumpGenerator(AutoPbrOptions options)
    {
        return options.UseDeepBumpNormals && !string.IsNullOrWhiteSpace(options.DeepBumpModelPath)
            ? DeepBumpNormalsGenerator.TryCreate(
                options.DeepBumpModelPath!,
                maxConcurrentRuns: Math.Max(1, ThreadingUtil.GetConversionParallelism(options)),
                preferOnnxTensorRtExecutionProvider: options.PreferOnnxTensorRtExecutionProvider)
            : null;
    }

    internal sealed class DeepBumpFallbackTracker(
        ConversionStage stage,
        int total,
        IProgress<ConversionProgress>? progress)
    {
        private int _activated;
        private int _textureCount;
        private string? _firstFailureReason;

        public bool IsActivated => Volatile.Read(ref _activated) != 0;

        public void Activate(string reason)
        {
            if (Interlocked.CompareExchange(ref _activated, 1, 0) != 0)
            {
                return;
            }

            var normalized = NormalizeFirstDeepBumpFailureReason(reason);
            Interlocked.CompareExchange(ref _firstFailureReason, normalized, null);
            progress?.Report(new ConversionProgress(
                ConversionStage.GeneratingNormals,
                0,
                total,
                null,
                "DeepBump failed during inference. Falling back to classic normal generation."));
        }

        public void IncrementTextureFallback()
        {
            Interlocked.Increment(ref _textureCount);
        }

        public void ReportSummary()
        {
            if (Volatile.Read(ref _textureCount) <= 0)
            {
                return;
            }

            var firstReason = Volatile.Read(ref _firstFailureReason);
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
                $"DeepBump fallback used on {Volatile.Read(ref _textureCount)} texture(s)."));
        }
    }

    private static bool TryGenerateDeepBumpNormal(
        Image<Rgba32> diffuse,
        TextureWorkItem t,
        AutoPbrOptions options,
        DeepBumpNormalsGenerator generatorForLoop,
        out Image<Rgba32> normal,
        out string failureReason)
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
                edgeGuidance = BuildDeepBumpEdgeGuidance(diffuse, diffuse.Width, diffuse.Height, options);
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
            failureReason = string.Empty;
            return true;
        }
        catch (OnnxRuntimeException ex)
        {
            normal = null!;
            failureReason = ex.Message;
            return false;
        }
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
