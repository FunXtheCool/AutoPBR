using System.Numerics;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Procedural 512×512 full-moon albedo for the Genesis moon billboard.</summary>
internal static class PreviewMoonDiscTextureGenerator
{
    public const int Size = 512;

    public static byte[] GenerateRgba8()
    {
        var rgba = new byte[Size * Size * 4];
        var rng = new Random(0x4D30_4E21); // stable "moon" seed

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var u = (x + 0.5f) / Size;
                var v = (y + 0.5f) / Size;
                var dx = u - 0.5f;
                var dy = v - 0.5f;
                var r = MathF.Sqrt(dx * dx + dy * dy) * 2f;
                if (r > 1f)
                {
                    WritePixel(rgba, x, y, 0, 0, 0, 0);
                    continue;
                }

                // Spherical UV for surface detail (equirectangular patch).
                var theta = MathF.Atan2(dy, dx);
                var phi = MathF.Acos(Math.Clamp(1f - r * r, -1f, 1f));
                var sx = MathF.Cos(theta) * phi * 3.4f;
                var sy = MathF.Sin(theta) * phi * 3.4f;

                var mare = Fbm(sx * 1.1f + 0.2f, sy * 1.1f, 1.7f, rng);
                var crater = Fbm(sx * 4.2f + 8.3f, sy * 4.2f - 2.1f, 2.4f, rng);
                var micro = Hash(sx * 9f, sy * 9f, rng);

                var mareMask = Smoothstep(0.38f, 0.62f, mare * 0.7f + micro * 0.3f);
                var craterMask = Smoothstep(0.78f, 0.92f, crater) * Smoothstep(0.2f, 0.55f, 1f - crater);

                var highR = 0.82f + micro * 0.04f;
                var highG = 0.84f + micro * 0.03f;
                var highB = 0.88f + micro * 0.02f;
                var lowR = 0.58f + mare * 0.06f;
                var lowG = 0.60f + mare * 0.05f;
                var lowB = 0.66f + mare * 0.04f;

                var rf = Lerp(highR, lowR, mareMask * 0.88f);
                var gf = Lerp(highG, lowG, mareMask * 0.88f);
                var bf = Lerp(highB, lowB, mareMask * 0.88f);
                rf = Lerp(rf, 0.46f, craterMask * 0.5f);
                gf = Lerp(gf, 0.48f, craterMask * 0.5f);
                bf = Lerp(bf, 0.52f, craterMask * 0.5f);

                // Limb darkening toward the disc edge.
                var limb = 1f - Smoothstep(0.55f, 1f, r) * 0.42f;
                rf *= limb;
                gf *= limb;
                bf *= limb;

                var edgeAlpha = 1f - Smoothstep(0.94f, 1f, r);
                WritePixel(rgba, x, y,
                    (byte)Math.Clamp((int)(rf * 255f), 0, 255),
                    (byte)Math.Clamp((int)(gf * 255f), 0, 255),
                    (byte)Math.Clamp((int)(bf * 255f), 0, 255),
                    (byte)Math.Clamp((int)(edgeAlpha * 255f), 0, 255));
            }
        }

        return rgba;
    }

    private static void WritePixel(byte[] rgba, int x, int y, byte r, byte g, byte b, byte a)
    {
        var i = (y * Size + x) * 4;
        rgba[i] = r;
        rgba[i + 1] = g;
        rgba[i + 2] = b;
        rgba[i + 3] = a;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / Math.Max(edge1 - edge0, 1e-6f), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Hash(float x, float y, Random rng)
    {
        _ = rng;
        var s = MathF.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
        return s - MathF.Floor(s);
    }

    private static float Fbm(float x, float y, float z, Random rng)
    {
        var sum = 0f;
        var amp = 0.55f;
        var freq = 1f;
        for (var i = 0; i < 5; i++)
        {
            sum += amp * Hash(x * freq, y * freq + z, rng);
            freq *= 2.03f;
            amp *= 0.5f;
        }

        return sum;
    }
}
