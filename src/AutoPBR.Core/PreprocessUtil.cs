namespace AutoPBR.Core;

internal static class PreprocessUtil
{
    private static readonly float[] SrgbToLinearLut = BuildSrgbToLinearLut();

    public static float SrgbToLinear(byte srgb) => SrgbToLinearLut[srgb];

    private static float[] BuildSrgbToLinearLut()
    {
        var lut = new float[256];
        for (var i = 0; i < 256; i++)
        {
            var c = i / 255f;
            // IEC 61966-2-1 sRGB
            lut[i] = c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        }
        return lut;
    }

    public static void BoxBlurInPlace(float[] data, int width, int height, int radius)
    {
        if (radius <= 0)
        {
            return;
        }

        var tmp = new float[data.Length];
        BoxBlur(data, tmp, width, height, radius);
        Array.Copy(tmp, data, data.Length);
    }

    // Separable box blur (two-pass). Edges clamp.
    private static void BoxBlur(float[] src, float[] dst, int width, int height, int radius)
    {
        var inv = 1f / (radius * 2 + 1);

        var tmp = new float[src.Length];

        // Horizontal into tmp
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            float sum = 0;
            for (var x = -radius; x <= radius; x++)
            {
                var ix = Math.Clamp(x, 0, width - 1);
                sum += src[row + ix];
            }

            for (var x = 0; x < width; x++)
            {
                tmp[row + x] = sum * inv;
                var xRemove = Math.Clamp(x - radius, 0, width - 1);
                var xAdd = Math.Clamp(x + radius + 1, 0, width - 1);
                sum += src[row + xAdd] - src[row + xRemove];
            }
        }

        // Vertical into dst
        for (var x = 0; x < width; x++)
        {
            float sum = 0;
            for (var y = -radius; y <= radius; y++)
            {
                var iy = Math.Clamp(y, 0, height - 1);
                sum += tmp[iy * width + x];
            }

            for (var y = 0; y < height; y++)
            {
                dst[y * width + x] = sum * inv;
                var yRemove = Math.Clamp(y - radius, 0, height - 1);
                var yAdd = Math.Clamp(y + radius + 1, 0, height - 1);
                sum += tmp[yAdd * width + x] - tmp[yRemove * width + x];
            }
        }
    }
}

