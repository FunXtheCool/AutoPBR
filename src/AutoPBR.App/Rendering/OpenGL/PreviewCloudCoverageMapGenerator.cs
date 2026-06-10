namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Nubis-style regional coverage map (256×256 R8) for cloud type / density modulation.</summary>
internal static class PreviewCloudCoverageMapGenerator
{
    public const int Size = 256;

    public static byte[] GenerateR8()
    {
        var data = new byte[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var u = (x + 0.5f) / Size;
                var v = (y + 0.5f) / Size;
                var large = Fbm(u * 2.4f + 0.1f, v * 2.4f + 0.3f, 0);
                var medium = Fbm(u * 6.5f + 2.7f, v * 6.5f + 1.9f, 1);
                var coverage = Math.Clamp(large * 0.62f + medium * 0.38f, 0f, 1f);
                coverage = Smoothstep(0.28f, 0.72f, coverage);
                data[y * Size + x] = (byte)(coverage * 255f);
            }
        }

        return data;
    }

    private static float Fbm(float u, float v, int seed)
    {
        var sum = 0f;
        var amp = 0.5f;
        for (var i = 0; i < 4; i++)
        {
            sum += amp * ValueNoise(u, v, seed + i * 17);
            u = u * 2.03f + 1.7f;
            v = v * 2.03f + 2.3f;
            amp *= 0.5f;
        }

        return sum;
    }

    private static float ValueNoise(float u, float v, int seed)
    {
        var x0 = MathF.Floor(u);
        var y0 = MathF.Floor(v);
        var fx = u - x0;
        var fy = v - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        var a = Hash(x0, y0, seed);
        var b = Hash(x0 + 1f, y0, seed);
        var c = Hash(x0, y0 + 1f, seed);
        var d = Hash(x0 + 1f, y0 + 1f, seed);
        return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
    }

    private static float Hash(float x, float y, int seed)
    {
        var h = MathF.Sin(x * 127.1f + y * 311.7f + seed * 41.3f) * 43758.5453f;
        return h - MathF.Floor(h);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / Math.Max(edge1 - edge0, 1e-5f), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
