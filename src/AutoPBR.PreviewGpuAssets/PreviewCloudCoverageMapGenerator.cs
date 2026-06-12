namespace AutoPBR.PreviewGpuAssets;

/// <summary>
/// Regional weather map (256×256 RGBA8). R = coverage (where clouds exist),
/// G = cloud type (0 = low sheet, 0.5 = mid puff, 1 = towering), B/A reserved.
/// Periodic FBM so the map tiles seamlessly under wind advection.
/// </summary>
public static class PreviewCloudCoverageMapGenerator
{
    public const int Size = 256;

    public static byte[] GenerateRgba8()
    {
        var data = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var u = (x + 0.5f) / Size;
                var v = (y + 0.5f) / Size;

                var warpU = PeriodicFbm(u, v, basePeriod: 5, octaves: 2, seed: 99);
                var warpV = PeriodicFbm(u, v, basePeriod: 5, octaves: 2, seed: 101);
                var wu = u + (warpU - 0.5f) * 0.22f;
                var wv = v + (warpV - 0.5f) * 0.22f;

                var large = PeriodicFbm(wu, wv, basePeriod: 3, octaves: 4, seed: 5);
                var medium = PeriodicFbm(wu + 0.31f, wv - 0.17f, basePeriod: 7, octaves: 3, seed: 23);
                var detail = PeriodicFbm(wu * 1.13f - wv * 0.09f, wv * 1.07f, basePeriod: 11, octaves: 2, seed: 47);
                var coverage = Math.Clamp(large * 0.58f + medium * 0.28f + detail * 0.14f, 0f, 1f);
                coverage = Smoothstep(0.44f, 0.86f, coverage);

                var cloudType = PeriodicFbm(wu, wv, basePeriod: 2, octaves: 3, seed: 71);
                cloudType = Smoothstep(0.3f, 0.7f, cloudType);

                var i = (y * Size + x) * 4;
                data[i] = (byte)(coverage * 255f);
                data[i + 1] = (byte)(cloudType * 255f);
                data[i + 2] = 0;
                data[i + 3] = 255;
            }
        }

        return data;
    }

    private static float PeriodicFbm(float u, float v, int basePeriod, int octaves, int seed)
    {
        var sum = 0f;
        var amp = 0.5f;
        var norm = 0f;
        for (var i = 0; i < octaves; i++)
        {
            var period = basePeriod << i;
            sum += amp * PeriodicValueNoise(u * period, v * period, period, seed + i * 13);
            norm += amp;
            amp *= 0.5f;
        }

        return sum / Math.Max(norm, 1e-5f);
    }

    private static float PeriodicValueNoise(float pu, float pv, int period, int seed)
    {
        var x0 = (int)MathF.Floor(pu);
        var y0 = (int)MathF.Floor(pv);
        var fx = Smooth(pu - x0);
        var fy = Smooth(pv - y0);

        float Corner(int dx, int dy) => Hash01(Wrap(x0 + dx, period), Wrap(y0 + dy, period), seed);

        var a = Corner(0, 0);
        var b = Corner(1, 0);
        var c = Corner(0, 1);
        var d = Corner(1, 1);
        return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
    }

    private static int Wrap(int v, int period)
    {
        var m = v % period;
        return m < 0 ? m + period : m;
    }

    private static float Hash01(int x, int y, int seed)
    {
        var h = unchecked((uint)(x * 374761393 + y * 668265263 + seed * 974711));
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / MathF.Max(edge1 - edge0, 1e-5f), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
