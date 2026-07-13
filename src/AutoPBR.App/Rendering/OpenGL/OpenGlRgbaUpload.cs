namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// ImageSharp and on-disk PNG row 0 is the top of the image; OpenGL <c>TexImage2D</c> row 0 is the bottom (V=0).
/// </summary>
internal static class OpenGlRgbaUpload
{

    public static void CopyBottomRowFirst(ReadOnlySpan<byte> rgba, int width, int height, Span<byte> destination)
    {
        var px = width * height;
        var expected = px * 4;
        if (rgba.Length < expected || width < 1 || height < 1 || destination.Length < expected)
        {
            rgba[..Math.Min(rgba.Length, destination.Length)].CopyTo(destination);
            return;
        }

        if (height == 1)
        {
            rgba[..expected].CopyTo(destination[..expected]);
            return;
        }

        var rowBytes = width * 4;
        for (var y = 0; y < height; y++)
        {
            var srcRow = (height - 1 - y) * rowBytes;
            var dstRow = y * rowBytes;
            rgba.Slice(srcRow, rowBytes).CopyTo(destination.Slice(dstRow, rowBytes));
        }
    }

    public static byte[] EnsureBottomRowFirst(ReadOnlySpan<byte> rgba, int width, int height, byte[]? scratch = null)
    {
        var px = width * height;
        var expected = px * 4;
        if (rgba.Length < expected || width < 1 || height < 1)
        {
            if (rgba.Length == expected && height == 1)
            {
                return rgba.ToArray();
            }

            var fallback = new byte[Math.Min(rgba.Length, expected)];
            rgba[..fallback.Length].CopyTo(fallback);
            return fallback;
        }

        if (height == 1)
        {
            if (scratch is not null && scratch.Length >= expected)
            {
                rgba[..expected].CopyTo(scratch);
                return scratch;
            }

            return rgba[..expected].ToArray();
        }

        if (scratch is not null && scratch.Length >= expected)
        {
            CopyBottomRowFirst(rgba, width, height, scratch);
            return scratch;
        }

        var outBytes = new byte[expected];
        CopyBottomRowFirst(rgba, width, height, outBytes);
        return outBytes;
    }
}
