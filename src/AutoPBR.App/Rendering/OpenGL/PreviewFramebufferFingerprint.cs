namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>FNV-1a fingerprint of downsampled RGB8 preview pixels for GPU regression hooks.</summary>
internal static class PreviewFramebufferFingerprint
{
    public static int Compute(ReadOnlySpan<byte> rgb8, int width, int height, int downsample = 16)
    {
        if (rgb8.Length < width * height * 3 || width <= 0 || height <= 0)
        {
            return 0;
        }

        downsample = Math.Clamp(downsample, 4, Math.Min(width, height));
        var stepX = Math.Max(1, width / downsample);
        var stepY = Math.Max(1, height / downsample);
        unchecked
        {
            var hash = (int)2166136261;
            for (var y = 0; y < height; y += stepY)
            {
                for (var x = 0; x < width; x += stepX)
                {
                    var i = (y * width + x) * 3;
                    hash ^= rgb8[i];
                    hash *= 16777619;
                    hash ^= rgb8[i + 1];
                    hash *= 16777619;
                    hash ^= rgb8[i + 2];
                    hash *= 16777619;
                }
            }

            return hash;
        }
    }
}
