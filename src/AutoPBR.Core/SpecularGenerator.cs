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

    /// <summary>
    /// Treat texture as metal if name or relative key contains these substrings (case-insensitive).
    /// Includes vanilla and common mod metals.
    /// </summary>
    private static readonly string[] MetalSubstrings =
    [
        "iron", "gold", "copper", "diamond", "netherite", "armor", "helmet",
        "adamantite", "mythril", "quadrillum", "silver", "aquarium", "prometheum", "osmium", "bronze", "steel",
        "durasteel", "hallowed", "celestium", "metallurgium", "palladium", "carmot", "starrite", "platinum",
        "orichalcum", "manganese", "cobalt", "ardite", "manyullyn", "zinc", "brass", "tin", "lead",
        "aluminum", "aluminium", "nickel", "invar", "electrum", "chrome", "titanium", "tungsten",
        "bismuth", "antimony", "cadmium", "iridium", "signalum", "lumium", "enderium", "constantan"
    ];

    private static bool IsMetalTexture(string name, string relativeKey)
    {
        var combined = name + "\0" + relativeKey;
        foreach (var sub in MetalSubstrings)
        {
            if (combined.Contains(sub, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static (float[] luminance, float[] edgeMagnitude, float meanLuminance) BuildLuminanceAndEdge(
        Image<Rgba32> cropped, int width, int height)
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
                    lum[y * width + x] = (p.R * 0.3f + p.G * 0.6f + p.B * 0.1f) / 255f;
                }
            }
        });
        var sumLum = 0.0;
        foreach (var value in lum)
            sumLum += value;
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
        for (var x = 0; x < width; x++)
        {
            float sx = 0, sy = 0;
            for (var oy = -1; oy <= 1; oy++)
            for (var ox = -1; ox <= 1; ox++)
            {
                var rx = Reflect(x + ox, width);
                var ry = Reflect(y + oy, height);
                var v = lum[ry * width + rx];
                sx += v * kx[oy + 1, ox + 1];
                sy += v * ky[oy + 1, ox + 1];
            }

            gx[y * width + x] = sx;
            gy[y * width + x] = sy;
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
            if (e > maxEdge)
                maxEdge = e;
        if (maxEdge > 0f)
        {
            for (var i = 0; i < edge.Length; i++)
                edge[i] = Math.Clamp(edge[i] / maxEdge, 0f, 1f);
        }

        return (lum, edge, meanLum);
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
                    var fast = t.Overrides.FastSpecular ?? options.FastSpecular;
                    var rules = t.Overrides.CustomSpecularRules
                                ?? options.SpecularData!.ByTextureName.GetValueOrDefault(t.Name)
                                ?? options.SpecularData.ByTextureName.GetValueOrDefault("*");

                    using var img = Image.Load<Rgba32>(t.DiffusePath);
                    using var cropped = CropToSquare(img, out var size);
                    var width = size;
                    var height = size;

                    // Ignore All: skip grass textures with significant transparency in diffuse
                    if (options.FoliageMode == "Ignore All" &&
                        (t.Name.Contains("grass", StringComparison.OrdinalIgnoreCase) ||
                         t.RelativeKey.Contains("grass", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!cropped.DangerousTryGetSinglePixelMemory(out var alphaCheckMem))
                            throw new InvalidOperationException("Expected contiguous pixel memory.");
                        var alphaSpan = alphaCheckMem.Span;
                        long sumA = 0;
                        int lowAlphaCount = 0;
                        var pixelCount = width * height;
                        for (var i = 0; i < pixelCount; i++)
                        {
                            var a = alphaSpan[i].A;
                            sumA += a;
                            if (a < 128) lowAlphaCount++;
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

                    var (luminance, edgeMagnitude, meanLuminance) = BuildLuminanceAndEdge(cropped, width, height);
                    var isMetal = IsMetalTexture(t.Name, t.RelativeKey);
                    var nPixels = width * height;
                    var rBuf = new byte[nPixels];
                    var gBuf = new byte[nPixels];
                    var bBuf = new byte[nPixels];
                    var aBuf = new byte[nPixels];
                    if (!cropped.DangerousTryGetSinglePixelMemory(out var inMem))
                        throw new InvalidOperationException("Expected contiguous pixel memory.");
                    var inSpan = inMem.Span;

                    for (var idx = 0; idx < nPixels; idx++)
                    {
                        var p = inSpan[idx];
                        var spec = GetSpecularRgba(p, rules, rulesLab, fast, rgbToLab, de2000);
                        var lum = luminance[idx];
                        var edge = edgeMagnitude[idx];

                        int rr, gg, bb;
                        if (isMetal)
                        {
                            rr = (int)Math.Min(255, spec.r * options.MetallicBoost);
                            gg = 255;
                            bb = 0;
                        }
                        else
                        {
                            gg = Math.Min(spec.g, LabPbrF0CapDielectric);
                            rr = (int)Math.Min(255, spec.r * options.SmoothnessScale);
                            rr = (int)(rr * (1f - 0.2f * edge));
                            if (lum > 0.92f && meanLuminance < 0.25f)
                                rr = Math.Min(rr, 220);
                            bb = Math.Clamp(spec.b + options.PorosityBias, 0, 255);
                        }

                        rBuf[idx] = (byte)Math.Clamp(rr, 0, 255);
                        gBuf[idx] = (byte)Math.Clamp(gg, 0, 255);
                        bBuf[idx] = (byte)bb;
                        aBuf[idx] = spec.a;
                    }

                    // Per-texture R normalization: remap to 10–200 when there is variation
                    byte minR = 255, maxR = 0;
                    for (var i = 0; i < nPixels; i++)
                    {
                        var v = rBuf[i];
                        if (v < minR) minR = v;
                        if (v > maxR) maxR = v;
                    }

                    if (maxR > minR)
                    {
                        for (var i = 0; i < nPixels; i++)
                            rBuf[i] = (byte)Math.Clamp(10 + (rBuf[i] - minR) * 190 / (maxR - minR), 0, 255);
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
                                        hasData = true;

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

                    var n = Interlocked.Increment(ref completed);
                    progress?.Report(new ConversionProgress(stage, n, total, t.Name));
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
            return (0, 0, 0, 255); // LabPBR: alpha 255 = no emission

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
            return (0, 0, 0, 255);

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
        if (i < 0) return -i - 1;
        if (i >= max) return max - (i - max) - 1;
        return i;
    }

    private static Image<Rgba32> CropToSquare(Image<Rgba32> img, out int size)
    {
        var s = Math.Min(img.Width, img.Height);
        size = s;
        if (img.Width == s && img.Height == s)
            return img.Clone();

        return img.Clone(ctx => ctx.Crop(new Rectangle(0, 0, s, s)));
    }
}

