namespace AutoPBR.App.Rendering.OpenGL;

internal readonly record struct GlPixelComparisonOptions(
    byte PerChannelTolerance = 0,
    double MaxDifferentPixelRatio = 0,
    double MaxMeanAbsoluteError = 0,
    bool IncludeAlpha = false)
{
    public static GlPixelComparisonOptions Exact => new();

    internal void Validate()
    {
        if (!double.IsFinite(MaxDifferentPixelRatio) || MaxDifferentPixelRatio is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDifferentPixelRatio));
        }

        if (!double.IsFinite(MaxMeanAbsoluteError) || MaxMeanAbsoluteError is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxMeanAbsoluteError));
        }
    }
}

internal readonly record struct GlPixelMismatchBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left + 1;

    public int Height => Bottom - Top + 1;

    public override string ToString() => $"({Left},{Top})-({Right},{Bottom})";
}

internal sealed record GlPixelComparison(
    string ExpectedName,
    string ActualName,
    int Width,
    int Height,
    int DifferentPixels,
    int DifferentChannels,
    int MaximumChannelDifference,
    double MeanAbsoluteError,
    double RootMeanSquareError,
    GlPixelMismatchBounds? MismatchBounds,
    GlPixelComparisonOptions Options)
{
    public int PixelCount => Width * Height;

    public double DifferentPixelRatio => DifferentPixels / (double)PixelCount;

    public bool Passed =>
        DifferentPixelRatio <= Options.MaxDifferentPixelRatio &&
        MeanAbsoluteError <= Options.MaxMeanAbsoluteError;

    public string FormatDiagnostic() =>
        $"{ExpectedName} vs {ActualName}: {(Passed ? "pass" : "FAIL")}, " +
        $"differentPixels={DifferentPixels}/{PixelCount} ({DifferentPixelRatio:P4}), " +
        $"differentChannels={DifferentChannels}, maxDiff={MaximumChannelDifference}, " +
        $"mae={MeanAbsoluteError:0.####}, rmse={RootMeanSquareError:0.####}, " +
        $"bounds={(MismatchBounds?.ToString() ?? "none")}, tolerance={Options.PerChannelTolerance}";

    internal static GlPixelComparison Compare(
        GlPixelSnapshot expected,
        GlPixelSnapshot actual,
        GlPixelComparisonOptions options)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        options.Validate();
        expected.EnsureSameSize(actual);

        var lhs = expected.GetRgbaSpan();
        var rhs = actual.GetRgbaSpan();
        var channelsPerPixel = options.IncludeAlpha ? 4 : 3;
        var differentPixels = 0;
        var differentChannels = 0;
        var maxDifference = 0;
        long absoluteDifferenceSum = 0;
        long squaredDifferenceSum = 0;
        var left = expected.Width;
        var top = expected.Height;
        var right = -1;
        var bottom = -1;

        for (var pixel = 0; pixel < expected.PixelCount; pixel++)
        {
            var offset = pixel * 4;
            var pixelDifferent = false;
            for (var channel = 0; channel < channelsPerPixel; channel++)
            {
                var difference = Math.Abs(lhs[offset + channel] - rhs[offset + channel]);
                absoluteDifferenceSum += difference;
                squaredDifferenceSum += difference * difference;
                maxDifference = Math.Max(maxDifference, difference);
                if (difference > options.PerChannelTolerance)
                {
                    differentChannels++;
                    pixelDifferent = true;
                }
            }

            if (!pixelDifferent)
            {
                continue;
            }

            differentPixels++;
            var x = pixel % expected.Width;
            var y = pixel / expected.Width;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }

        var comparedChannelCount = checked(expected.PixelCount * channelsPerPixel);
        return new GlPixelComparison(
            expected.Name,
            actual.Name,
            expected.Width,
            expected.Height,
            differentPixels,
            differentChannels,
            maxDifference,
            absoluteDifferenceSum / (double)comparedChannelCount,
            Math.Sqrt(squaredDifferenceSum / (double)comparedChannelCount),
            differentPixels == 0 ? null : new GlPixelMismatchBounds(left, top, right, bottom),
            options);
    }
}
