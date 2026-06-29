namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// ImageSharp and on-disk PNG row 0 is the top of the image; OpenGL <c>TexImage2D</c> row 0 is the bottom (V=0).
/// </summary>
internal static class OpenGlRgbaUpload
{
    public static byte[] EnsureBottomRowFirst(ReadOnlySpan<byte> rgba, int width, int height)
    {
        var px = width * height;
        var expected = px * 4;
        if (rgba.Length < expected || width < 1 || height < 1)
        {
            return rgba.Length == expected ? rgba.ToArray() : rgba.ToArray();
        }

        if (height == 1)
        {
            return rgba[..expected].ToArray();
        }

        var rowBytes = width * 4;
        var outBytes = new byte[expected];
        for (var y = 0; y < height; y++)
        {
            var srcRow = (height - 1 - y) * rowBytes;
            var dstRow = y * rowBytes;
            rgba.Slice(srcRow, rowBytes).CopyTo(outBytes.AsSpan(dstRow, rowBytes));
        }

        return outBytes;
    }
}
