using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class GlPixelComparisonTests
{
    [Fact]
    public void FromBottomUpRgba8_FlipsRowsAndKeepsStableFingerprint()
    {
        byte[] bottomUp =
        [
            0, 0, 255, 255, 255, 255, 255, 255,
            255, 0, 0, 255, 0, 255, 0, 255,
        ];

        var first = GlPixelSnapshot.FromBottomUpRgba8("first", 2, 2, bottomUp);
        var second = GlPixelSnapshot.FromBottomUpRgba8("second", 2, 2, bottomUp);

        Assert.Equal(
            new byte[]
            {
                255, 0, 0, 255, 0, 255, 0, 255,
                0, 0, 255, 255, 255, 255, 255, 255,
            },
            first.Rgba.ToArray());
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void CompareTo_MismatchReportsMetricsAndTopLeftBounds()
    {
        var expected = Solid("expected", 3, 2, 10, 20, 30, 255);
        var actualBytes = expected.Rgba.ToArray();
        SetPixel(actualBytes, 3, x: 2, y: 0, 20, 20, 30, 255);
        SetPixel(actualBytes, 3, x: 1, y: 1, 10, 25, 37, 255);
        var actual = new GlPixelSnapshot("actual", 3, 2, actualBytes);

        var result = expected.CompareTo(actual);

        Assert.False(result.Passed);
        Assert.Equal(2, result.DifferentPixels);
        Assert.Equal(3, result.DifferentChannels);
        Assert.Equal(10, result.MaximumChannelDifference);
        Assert.Equal(new GlPixelMismatchBounds(1, 0, 2, 1), result.MismatchBounds);
        Assert.Contains("differentPixels=2/6", result.FormatDiagnostic(), StringComparison.Ordinal);
        Assert.Contains("bounds=(1,0)-(2,1)", result.FormatDiagnostic(), StringComparison.Ordinal);
    }

    [Fact]
    public void CompareTo_ToleranceRatioAndMaeCanAcceptSmallDriverNoise()
    {
        var expected = Solid("expected", 10, 1, 50, 60, 70, 255);
        var actualBytes = expected.Rgba.ToArray();
        SetPixel(actualBytes, 10, x: 0, y: 0, 52, 59, 72, 0);
        SetPixel(actualBytes, 10, x: 9, y: 0, 56, 60, 70, 255);
        var actual = new GlPixelSnapshot("actual", 10, 1, actualBytes);

        var result = expected.CompareTo(
            actual,
            new GlPixelComparisonOptions(
                PerChannelTolerance: 2,
                MaxDifferentPixelRatio: 0.1,
                MaxMeanAbsoluteError: 0.37));

        Assert.True(result.Passed, result.FormatDiagnostic());
        Assert.Equal(1, result.DifferentPixels);
        Assert.Equal(1, result.DifferentChannels);
        Assert.Equal(6, result.MaximumChannelDifference);
        Assert.Equal(0.1, result.DifferentPixelRatio, precision: 8);
        Assert.Equal(11d / 30d, result.MeanAbsoluteError, precision: 8);
    }

    [Fact]
    public void CompareTo_AlphaIsOptionalAndCanBeIncluded()
    {
        var expected = Solid("expected", 1, 1, 1, 2, 3, 255);
        var actual = Solid("actual", 1, 1, 1, 2, 3, 0);

        Assert.True(expected.CompareTo(actual).Passed);

        var withAlpha = expected.CompareTo(
            actual,
            new GlPixelComparisonOptions(IncludeAlpha: true));
        Assert.False(withAlpha.Passed);
        Assert.Equal(1, withAlpha.DifferentPixels);
        Assert.Equal(255, withAlpha.MaximumChannelDifference);
    }

    [Fact]
    public void CreateDifferenceRgba_AmplifiesRgbAndMarksAlpha()
    {
        var expected = Solid("expected", 1, 1, 10, 20, 30, 255);
        var actual = Solid("actual", 1, 1, 12, 16, 38, 250);

        Assert.Equal(new byte[] { 8, 16, 32, 64 }, expected.CreateDifferenceRgba(actual));
    }

    private static GlPixelSnapshot Solid(
        string name,
        int width,
        int height,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = red;
            rgba[i + 1] = green;
            rgba[i + 2] = blue;
            rgba[i + 3] = alpha;
        }

        return new GlPixelSnapshot(name, width, height, rgba);
    }

    private static void SetPixel(
        byte[] rgba,
        int width,
        int x,
        int y,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        var offset = (y * width + x) * 4;
        rgba[offset] = red;
        rgba[offset + 1] = green;
        rgba[offset + 2] = blue;
        rgba[offset + 3] = alpha;
    }
}
