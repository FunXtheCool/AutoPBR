namespace AutoPBR.App.Rendering.OpenGL;

internal sealed class GlLuminanceHistogramSnapshot
{
    public const int BinCount = 64;
    public const int DwordCount = BinCount + 2;
    public const int DefaultSampleCapacity = 65_536;

    public GlLuminanceHistogramSnapshot(uint[] bins, uint sampleCount, uint overflowCount)
    {
        if (bins.Length != BinCount)
        {
            throw new ArgumentException($"Luminance histogram requires {BinCount} bins.", nameof(bins));
        }

        Bins = bins;
        SampleCount = sampleCount;
        OverflowCount = overflowCount;
    }

    public uint[] Bins { get; }
    public uint SampleCount { get; }
    public uint OverflowCount { get; }
    public uint BinnedSampleCount => Bins.Aggregate(0u, static (sum, value) => sum + value);
    public bool IsConsistent => BinnedSampleCount == SampleCount;

    public static GlLuminanceHistogramSnapshot FromDwords(ReadOnlySpan<uint> dwords)
    {
        if (dwords.Length < DwordCount)
        {
            throw new ArgumentException($"Luminance histogram requires {DwordCount} uints.", nameof(dwords));
        }

        return new GlLuminanceHistogramSnapshot(
            dwords[..BinCount].ToArray(),
            dwords[BinCount],
            dwords[BinCount + 1]);
    }

    public static GlLuminanceHistogramSnapshot FromRgb8(
        ReadOnlySpan<byte> rgb,
        int width,
        int height,
        int sampleCapacity = DefaultSampleCapacity)
    {
        if (width <= 0 || height <= 0 || rgb.Length < checked(width * height * 3))
        {
            throw new ArgumentException("RGB data does not match the requested histogram dimensions.", nameof(rgb));
        }

        var stride = ResolveSampleStride(width, height, sampleCapacity);
        var bins = new uint[BinCount];
        uint sampleCount = 0;
        uint overflowCount = 0;
        for (var y = 0; y < height; y += stride)
        {
            for (var x = 0; x < width; x += stride)
            {
                if (sampleCount >= sampleCapacity)
                {
                    overflowCount++;
                    continue;
                }

                var offset = (y * width + x) * 3;
                bins[ResolveBin(rgb[offset], rgb[offset + 1], rgb[offset + 2])]++;
                sampleCount++;
            }
        }

        return new GlLuminanceHistogramSnapshot(bins, sampleCount, overflowCount);
    }

    public static int ResolveSampleStride(int width, int height, int sampleCapacity = DefaultSampleCapacity)
    {
        if (width <= 0 || height <= 0 || sampleCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCapacity));
        }

        var stride = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((double)width * height / sampleCapacity)));
        while ((long)((width + stride - 1) / stride) * ((height + stride - 1) / stride) > sampleCapacity)
        {
            stride++;
        }

        return stride;
    }

    public string FormatDiagnostic()
    {
        var dark = Bins.AsSpan(0, 16).ToArray().Aggregate(0u, static (sum, value) => sum + value);
        var bright = Bins.AsSpan(48, 16).ToArray().Aggregate(0u, static (sum, value) => sum + value);
        return $"samples={SampleCount}, dark={dark}, bright={bright}, overflow={OverflowCount}";
    }

    private static int ResolveBin(byte r, byte g, byte b)
    {
        var luminance = (54u * r + 183u * g + 19u * b + 128u) >> 8;
        return (int)Math.Min(63u, (luminance * 64u) >> 8);
    }
}
