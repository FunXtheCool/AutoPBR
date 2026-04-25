using AutoPBR.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.Core;

/// <summary>
/// Structural mortar detection (multi-scale max white/black top-hat) and height post-processing for <c>brick</c>-tagged textures.
/// </summary>
internal static class BrickHeightPostProcessor
{
    /// <summary>Applies probe-driven rules; returns a new height buffer (copies input when skipping).</summary>
    public static byte[] Apply(
        ReadOnlySpan<byte> heightData,
        int width,
        int height,
        Image<Rgba32> diffuse,
        AutoPbrOptions options,
        out BrickHeightPostProcessResult result)
    {
        var n = width * height;
        if (heightData.Length != n)
        {
            throw new ArgumentException("Height buffer size must match width*height.", nameof(heightData));
        }

        var lum = ExtractLuminance01(diffuse, width, height, options);
        var hf = new float[n];
        for (var i = 0; i < n; i++)
        {
            hf[i] = heightData[i];
        }

        var minDim = Math.Min(width, height);
        var topHatRadii = BrickProbeResolution.GetTopHatRadii(minDim, options.BrickMortarTopHatMaxRadius);
        var brickFaceDilatePx = BrickProbeResolution.MortarBrickFaceDilateRadius(minDim);

        var combined = new float[n];
        var combinedWhite = new float[n];
        var combinedBlack = new float[n];

        foreach (var r in topHatRadii)
        {
            var opening = new float[n];
            var closing = new float[n];
            Opening(lum, opening, width, height, r);
            Closing(lum, closing, width, height, r);
            for (var i = 0; i < n; i++)
            {
                var wth = Math.Max(0f, lum[i] - opening[i]);
                var bth = Math.Max(0f, closing[i] - lum[i]);
                var t = Math.Max(wth, bth);
                combined[i] = Math.Max(combined[i], t);
                combinedWhite[i] = Math.Max(combinedWhite[i], wth);
                combinedBlack[i] = Math.Max(combinedBlack[i], bth);
            }
        }

        var hasAnyMask = NormalizeSoftMaskInPlace(combined, width, height);
        NormalizeSoftMaskInPlace(combinedWhite, width, height);
        NormalizeSoftMaskInPlace(combinedBlack, width, height);
        if (!hasAnyMask)
        {
            var noMaskDebug = options.BrickProbePreviewDebug
                ? "structural mask: none (no joint response; uniform or very flat diffuse)"
                : null;
            result = new BrickHeightPostProcessResult(0, 0, 0, 0, false, true, noMaskDebug);
            return heightData.ToArray();
        }

        float globalMeanLum = 0f;
        foreach (var lumValue in lum)
        {
            globalMeanLum += lumValue;
        }
        globalMeanLum /= Math.Max(1, lum.Length);

        var statsAny = BuildMaskStats(combined, hf, lum, width, height, brickFaceDilatePx, globalMeanLum);
        var statsWhite = BuildMaskStats(combinedWhite, hf, lum, width, height, brickFaceDilatePx, globalMeanLum);
        var statsBlack = BuildMaskStats(combinedBlack, hf, lum, width, height, brickFaceDilatePx, globalMeanLum);
        var (selected, maskLabel) = SelectBestMaskStats(statsAny, statsWhite, statsBlack);

        var meanS = selected.MeanS;
        var mM = selected.MeanMortarHeight;
        var mB = selected.MeanBrickHeight;
        var delta = selected.Delta;
        var lumGap = selected.LumGap;

        // Strong path: high mean mortar response + large height gap. The UI "min structure" slider raises the floor
        // when set above the default invert confidence (thin joints stay below ~0.04 mean S, so this path is rare).
        var strongFloor = Math.Max(options.BrickHeightInvertConfidenceFloor, options.BrickHeightMinStructuralConfidence);
        var invertStrong = meanS >= strongFloor && delta > options.BrickHeightInvertDeltaThreshold;

        // Light-grout path: diffuse height puts mortar above brick (Δ>0) but mean S stays very low for thin joints,
        // so we must not gate on meanS here. If albedo is lighter on mortar than on brick faces, global-invert.
        var lumMin = options.BrickLightGroutDiffuseDeltaMin;
        var lightGroutMeanSMin = MathF.Max(options.BrickHeightMinStructuralConfidence * 0.5f, 0.004f);
        var lumBeatMargin = MathF.Max(lumMin * 0.5f, 0.0015f);
        var absoluteMortarLumFloor = 0.18f;
        var relativeMortarLumFloor = MathF.Max(lumMin * 1.5f, 0.01f);

        // Require polarity agreement across signed masks so white-top-hat cannot trigger on random bright brick flecks.
        var polarityAgreement =
            statsWhite.LumGap > lumMin &&
            statsBlack.LumGap > MathF.Max(0.0008f, lumMin * 0.25f) &&
            statsAny.LumGap > MathF.Max(0.0008f, lumMin * 0.5f) &&
            statsWhite.LumGap > statsBlack.LumGap + lumBeatMargin;

        // Absolute/relative brightness guard: light grout should be genuinely light, not just "less dark".
        var isMortarBrightEnough =
            statsWhite.MeanLumMortar > absoluteMortarLumFloor &&
            (statsWhite.MeanLumMortar - statsWhite.GlobalMeanLum) > relativeMortarLumFloor;

        // Keep light-grout path permissive for thin joints, but not fully ungated.
        var hasLightGroutStructure =
            statsWhite.MeanS >= lightGroutMeanSMin &&
            statsWhite.SumMortarWeight > 2f;

        var invertLightGrout = delta > 0f && polarityAgreement && isMortarBrightEnough && hasLightGroutStructure;

        var invert = invertStrong || invertLightGrout;
        string FormatStatsLine(string label, ProbeMaskStats s)
            => $"{label}: meanS={s.MeanS:F5} sumS={s.SumMortarWeight:F3} mMortarH={s.MeanMortarHeight:F1} mBrickH={s.MeanBrickHeight:F1} delta={s.Delta:F2} lumGap={s.LumGap:F5} lumMortar={s.MeanLumMortar:F5} lumBrick={s.MeanLumBrick:F5}";

        string? FormatDebug(bool applied, string reasonTag)
        {
            if (!options.BrickProbePreviewDebug)
            {
                return null;
            }

            var rList = string.Join(",", topHatRadii);
            return string.Join(Environment.NewLine,
                $"mask={maskLabel}  minDim={minDim}  brickFaceDilatePx={brickFaceDilatePx}",
                $"topHatRadii=[{rList}]  userTopHatMax={options.BrickMortarTopHatMaxRadius}",
                FormatStatsLine("combined", statsAny),
                FormatStatsLine("white", statsWhite),
                FormatStatsLine("black", statsBlack),
                $"selected: meanS={meanS:F5} delta={delta:F2} lumGap={lumGap:F5}",
                $"strongFloor={strongFloor:F5}  lumMin={lumMin:F5}  lightGroutMeanSMin={lightGroutMeanSMin:F5}",
                $"lightGroutChecks: polarity={polarityAgreement}  bright={isMortarBrightEnough}  structure={hasLightGroutStructure}",
                $"path: strong={invertStrong}  lightGrout={invertLightGrout}  globalMeanLum={globalMeanLum:F5}",
                $"invert={applied}  ({reasonTag})");
        }

        if (!invert)
        {
            result = new BrickHeightPostProcessResult(mM, mB, delta, meanS, false, false,
                FormatDebug(false, "no rule matched"));
            return heightData.ToArray();
        }

        InvertHeightRangeInPlace(hf);
        var outBytes = new byte[n];
        for (var i = 0; i < n; i++)
        {
            outBytes[i] = (byte)Math.Clamp((int)MathF.Round(hf[i]), 0, 255);
        }

        var reason = invertStrong ? "strong" : "light_grout";
        result = new BrickHeightPostProcessResult(mM, mB, delta, meanS, true, false, FormatDebug(true, reason));
        return outBytes;
    }

    private static void Opening(float[] src, float[] dst, int w, int h, int r)
    {
        var eroded = new float[src.Length];
        var tmp = new float[src.Length];
        Erode(src, eroded, tmp, w, h, r);
        Dilate(eroded, dst, tmp, w, h, r);
    }

    private static void Closing(float[] src, float[] dst, int w, int h, int r)
    {
        var dilated = new float[src.Length];
        var tmp = new float[src.Length];
        Dilate(src, dilated, tmp, w, h, r);
        Erode(dilated, dst, tmp, w, h, r);
    }

    private static bool NormalizeSoftMaskInPlace(float[] mask, int width, int height)
    {
        var max = 0f;
        foreach (var v in mask)
        {
            if (v > max)
            {
                max = v;
            }
        }

        if (max < 1e-6f)
        {
            return false;
        }

        foreach (ref var v in mask.AsSpan())
        {
            v /= max;
        }

        PreprocessUtil.BoxBlurInPlace(mask, width, height, 2);

        var maxAfterBlur = 0f;
        foreach (var v in mask)
        {
            if (v > maxAfterBlur)
            {
                maxAfterBlur = v;
            }
        }

        if (maxAfterBlur < 1e-6f)
        {
            return false;
        }

        for (var i = 0; i < mask.Length; i++)
        {
            mask[i] /= maxAfterBlur;
        }

        return true;
    }

    private static ProbeMaskStats BuildMaskStats(
        float[] mortarWeight,
        float[] hf,
        float[] lum,
        int width,
        int height,
        int brickFaceDilatePx,
        float globalMeanLum)
    {
        var n = width * height;
        var dilateR = brickFaceDilatePx;
        var dilatedMortar = new float[n];
        var tmpMorph = new float[n];
        Dilate(mortarWeight, dilatedMortar, tmpMorph, width, height, dilateR);
        var brickFaceWeight = new float[n];
        for (var i = 0; i < n; i++)
        {
            brickFaceWeight[i] = Math.Clamp(1f - dilatedMortar[i], 0f, 1f);
        }

        float sumS = 0f, sumB = 0f, hM = 0f, hB = 0f, lumM = 0f, lumB = 0f, meanS = 0f;
        for (var i = 0; i < n; i++)
        {
            var s = mortarWeight[i];
            var b = brickFaceWeight[i];
            meanS += s;
            sumS += s;
            sumB += b;
            hM += hf[i] * s;
            hB += hf[i] * b;
            lumM += lum[i] * s;
            lumB += lum[i] * b;
        }

        meanS /= n;
        var eps = 1e-4f;
        var mM = sumS > eps ? hM / sumS : 0f;
        var mB = sumB > eps ? hB / sumB : 0f;
        var wMeanLumMortar = sumS > eps ? lumM / sumS : 0f;
        var wMeanLumBrick = sumB > eps ? lumB / sumB : 0f;
        return new ProbeMaskStats
        {
            MeanS = meanS,
            SumMortarWeight = sumS,
            MeanMortarHeight = mM,
            MeanBrickHeight = mB,
            Delta = mM - mB,
            LumGap = wMeanLumMortar - wMeanLumBrick,
            MeanLumMortar = wMeanLumMortar,
            MeanLumBrick = wMeanLumBrick,
            GlobalMeanLum = globalMeanLum
        };
    }

    private static (ProbeMaskStats Stats, string MaskLabel) SelectBestMaskStats(
        ProbeMaskStats any,
        ProbeMaskStats white,
        ProbeMaskStats black)
    {
        // Polarity-aware selection: prefer the signed top-hat mask that better explains height ordering,
        // and fall back to combined if neither signed variant is clearly better.
        var whiteScore = Math.Abs(white.Delta) * (0.3f + white.MeanS);
        var blackScore = Math.Abs(black.Delta) * (0.3f + black.MeanS);
        var minSignedConfidence = any.MeanS * 0.65f;
        var whiteUsable = white.MeanS >= minSignedConfidence;
        var blackUsable = black.MeanS >= minSignedConfidence;

        ProbeMaskStats selected;
        string label;

        if (whiteUsable && whiteScore > blackScore * 1.08f)
        {
            selected = white;
            label = "white_top_hat";
        }
        else if (blackUsable && blackScore > whiteScore * 1.08f)
        {
            selected = black;
            label = "black_top_hat";
        }
        else
        {
            selected = any;
            label = "combined";
        }

        // Lum-consistency repair: black_top_hat often tracks dark brick texture / edges; light grout joints are
        // bright and show up in white_top_hat. If black wins on score but assigns mortar darker than brick
        // (LumGap < 0) while white or combined shows the expected light-joint signature, prefer that instead.
        const float lightMortarLumEvidence = 0.001f;
        const float lumBeatEps = 0.0004f;
        if (string.Equals(label, "black_top_hat", StringComparison.Ordinal) && black.LumGap < 0f)
        {
            var minWhiteS = MathF.Max(minSignedConfidence * 0.12f, 0.004f);
            if (white.LumGap > lightMortarLumEvidence && white.MeanS >= minWhiteS)
            {
                return (white, "white_top_hat");
            }

            if (any.LumGap > lightMortarLumEvidence && any.LumGap > black.LumGap + lumBeatEps)
            {
                return (any, "combined");
            }

            // White is closer to light-mortar albedo than black even if barely above threshold (noisy 16×16).
            if (white.LumGap > black.LumGap + 0.0025f && white.LumGap > -0.0005f && white.MeanS >= minWhiteS * 0.85f)
            {
                return (white, "white_top_hat");
            }
        }

        // Dark-grout: white_top_hat can lock onto bright brick flecks; if black explains dark joints much better in albedo, prefer it.
        if (string.Equals(label, "white_top_hat", StringComparison.Ordinal) && white.LumGap > lightMortarLumEvidence
            && black.LumGap < -lightMortarLumEvidence && black.MeanS >= minSignedConfidence * 0.18f
            && blackScore >= whiteScore * 0.92f)
        {
            return (black, "black_top_hat");
        }

        return (selected, label);
    }

    private sealed class ProbeMaskStats
    {
        public required float MeanS { get; init; }
        public required float SumMortarWeight { get; init; }
        public required float MeanMortarHeight { get; init; }
        public required float MeanBrickHeight { get; init; }
        public required float Delta { get; init; }
        public required float LumGap { get; init; }
        public required float MeanLumMortar { get; init; }
        public required float MeanLumBrick { get; init; }
        public required float GlobalMeanLum { get; init; }
    }

    private static void InvertHeightRangeInPlace(float[] hf)
    {
        var min = hf.Min();
        var max = hf.Max();
        for (var i = 0; i < hf.Length; i++)
        {
            hf[i] = max - hf[i] + min;
        }
    }

    private static float[] ExtractLuminance01(Image<Rgba32> cropped, int width, int height, AutoPbrOptions options)
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
                    float v;
                    if (options.PreprocessLinearize)
                    {
                        var r = PreprocessUtil.SrgbToLinear(p.R);
                        var g = PreprocessUtil.SrgbToLinear(p.G);
                        var b = PreprocessUtil.SrgbToLinear(p.B);
                        v = r * 0.2126f + g * 0.7152f + b * 0.0722f;
                    }
                    else
                    {
                        v = p.R * (0.3f / 255f) + p.G * (0.6f / 255f) + p.B * (0.1f / 255f);
                    }

                    lum[y * width + x] = Math.Clamp(v, 0f, 1f);
                }
            }
        });
        return lum;
    }

    private static void Dilate(float[] src, float[] dst, float[] tmp, int w, int h, int r)
    {
        MaxFilterHorizontal(src, tmp, w, h, r);
        MaxFilterVertical(tmp, dst, w, h, r);
    }

    private static void Erode(float[] src, float[] dst, float[] tmp, int w, int h, int r)
    {
        MinFilterHorizontal(src, tmp, w, h, r);
        MinFilterVertical(tmp, dst, w, h, r);
    }

    private static void MaxFilterHorizontal(float[] src, float[] dst, int w, int h, int r)
    {
        for (var y = 0; y < h; y++)
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                var x0 = Math.Max(0, x - r);
                var x1 = Math.Min(w - 1, x + r);
                var m = src[row + x0];
                for (var xx = x0 + 1; xx <= x1; xx++)
                {
                    var v = src[row + xx];
                    if (v > m)
                    {
                        m = v;
                    }
                }

                dst[row + x] = m;
            }
        }
    }

    private static void MaxFilterVertical(float[] src, float[] dst, int w, int h, int r)
    {
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                var y0 = Math.Max(0, y - r);
                var y1 = Math.Min(h - 1, y + r);
                var m = src[y0 * w + x];
                for (var yy = y0 + 1; yy <= y1; yy++)
                {
                    var v = src[yy * w + x];
                    if (v > m)
                    {
                        m = v;
                    }
                }

                dst[y * w + x] = m;
            }
        }
    }

    private static void MinFilterHorizontal(float[] src, float[] dst, int w, int h, int r)
    {
        for (var y = 0; y < h; y++)
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                var x0 = Math.Max(0, x - r);
                var x1 = Math.Min(w - 1, x + r);
                var m = src[row + x0];
                for (var xx = x0 + 1; xx <= x1; xx++)
                {
                    var v = src[row + xx];
                    if (v < m)
                    {
                        m = v;
                    }
                }

                dst[row + x] = m;
            }
        }
    }

    private static void MinFilterVertical(float[] src, float[] dst, int w, int h, int r)
    {
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                var y0 = Math.Max(0, y - r);
                var y1 = Math.Min(h - 1, y + r);
                var m = src[y0 * w + x];
                for (var yy = y0 + 1; yy <= y1; yy++)
                {
                    var v = src[yy * w + x];
                    if (v < m)
                    {
                        m = v;
                    }
                }

                dst[y * w + x] = m;
            }
        }
    }
}
