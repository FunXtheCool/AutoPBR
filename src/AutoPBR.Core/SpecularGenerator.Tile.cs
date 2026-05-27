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

        if (options.SpecularForceNoEmissive)
        {
            Array.Fill(aBuf, (byte)255);
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
}
