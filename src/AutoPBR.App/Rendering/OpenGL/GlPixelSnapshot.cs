namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Immutable, top-left-origin RGBA8 capture used by opt-in live rendering regression tests.
/// </summary>
internal sealed class GlPixelSnapshot
{
    private readonly byte[] _rgba;

    public GlPixelSnapshot(string name, int width, int height, ReadOnlySpan<byte> topDownRgba)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var expectedLength = checked(width * height * 4);
        if (topDownRgba.Length != expectedLength)
        {
            throw new ArgumentException(
                $"RGBA8 capture requires exactly {expectedLength} bytes for {width}x{height} pixels.",
                nameof(topDownRgba));
        }

        Name = name;
        Width = width;
        Height = height;
        _rgba = topDownRgba.ToArray();
        Fingerprint = ComputeFingerprint(_rgba);
    }

    public string Name { get; }

    public int Width { get; }

    public int Height { get; }

    public int PixelCount => Width * Height;

    public uint Fingerprint { get; }

    public ReadOnlyMemory<byte> Rgba => _rgba;

    public static GlPixelSnapshot FromBottomUpRgba8(
        string name,
        int width,
        int height,
        ReadOnlySpan<byte> bottomUpRgba)
    {
        var rowBytes = checked(width * 4);
        var expectedLength = checked(rowBytes * height);
        if (width <= 0 || height <= 0 || bottomUpRgba.Length != expectedLength)
        {
            throw new ArgumentException("Bottom-up RGBA8 dimensions do not match the supplied pixel buffer.", nameof(bottomUpRgba));
        }

        var topDown = new byte[expectedLength];
        for (var y = 0; y < height; y++)
        {
            bottomUpRgba.Slice((height - 1 - y) * rowBytes, rowBytes)
                .CopyTo(topDown.AsSpan(y * rowBytes, rowBytes));
        }

        return new GlPixelSnapshot(name, width, height, topDown);
    }

    public int CountPixelsOutside(ReadOnlySpan<byte> rgba, byte tolerance = 0)
    {
        if (rgba.Length != 4)
        {
            throw new ArgumentException("Reference color must contain exactly four RGBA bytes.", nameof(rgba));
        }

        var count = 0;
        for (var i = 0; i < _rgba.Length; i += 4)
        {
            if (Math.Abs(_rgba[i] - rgba[0]) > tolerance ||
                Math.Abs(_rgba[i + 1] - rgba[1]) > tolerance ||
                Math.Abs(_rgba[i + 2] - rgba[2]) > tolerance ||
                Math.Abs(_rgba[i + 3] - rgba[3]) > tolerance)
            {
                count++;
            }
        }

        return count;
    }

    public GlPixelComparison CompareTo(
        GlPixelSnapshot actual,
        GlPixelComparisonOptions options = default) =>
        GlPixelComparison.Compare(this, actual, options);

    public byte[] CreateDifferenceRgba(GlPixelSnapshot actual, byte amplify = 4)
    {
        ArgumentNullException.ThrowIfNull(actual);
        EnsureSameSize(actual);

        var diff = new byte[_rgba.Length];
        var rhs = actual._rgba;
        for (var i = 0; i < diff.Length; i += 4)
        {
            var dr = Math.Abs(_rgba[i] - rhs[i]);
            var dg = Math.Abs(_rgba[i + 1] - rhs[i + 1]);
            var db = Math.Abs(_rgba[i + 2] - rhs[i + 2]);
            var da = Math.Abs(_rgba[i + 3] - rhs[i + 3]);
            diff[i] = (byte)Math.Min(255, dr * amplify);
            diff[i + 1] = (byte)Math.Min(255, dg * amplify);
            diff[i + 2] = (byte)Math.Min(255, db * amplify);
            diff[i + 3] = (byte)Math.Max(64, Math.Min(255, da * amplify));
        }

        return diff;
    }

    internal ReadOnlySpan<byte> GetRgbaSpan() => _rgba;

    internal void EnsureSameSize(GlPixelSnapshot other)
    {
        if (Width != other.Width || Height != other.Height)
        {
            throw new ArgumentException(
                $"Pixel captures must have the same dimensions; {Name} is {Width}x{Height}, " +
                $"{other.Name} is {other.Width}x{other.Height}.",
                nameof(other));
        }
    }

    private static uint ComputeFingerprint(ReadOnlySpan<byte> bytes)
    {
        const uint fnvPrime = 16777619;
        var hash = 2166136261u;
        foreach (var value in bytes)
        {
            hash ^= value;
            hash *= fnvPrime;
        }

        return hash;
    }
}
