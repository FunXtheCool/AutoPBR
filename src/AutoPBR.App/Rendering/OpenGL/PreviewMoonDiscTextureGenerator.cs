using Avalonia.Platform;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Packed RGBA full-moon albedo for the Genesis moon billboard.</summary>
internal static class PreviewMoonDiscTextureGenerator
{
    public const int Size = 1024;
    private const string LrocColorMapAsset = "avares://AutoPBR.App/Assets/Preview/lroc_color_2k.jpg";

    public static byte[] GenerateRgba8()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(LrocColorMapAsset));
            using var source = Image.Load<Rgba32>(stream);
            return GenerateFromLrocColorMap(source);
        }
        catch
        {
            return GenerateProceduralLunaRgba8();
        }
    }

    private static byte[] GenerateFromLrocColorMap(Image<Rgba32> source)
    {
        var rgba = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var u = (x + 0.5f) / Size;
                var v = (y + 0.5f) / Size;
                var nx = u * 2f - 1f;
                var ny = v * 2f - 1f;
                var r = MathF.Sqrt(nx * nx + ny * ny);
                if (r > 1f)
                {
                    WritePixel(rgba, x, y, 0, 0, 0, 0);
                    continue;
                }

                var sphereZ = MathF.Sqrt(Math.Max(1f - r * r, 0f));
                var lon = MathF.Atan2(nx, Math.Max(sphereZ, 1e-5f));
                var lat = MathF.Asin(Math.Clamp(ny, -1f, 1f));
                var srcU = 0.5f + lon / (2f * MathF.PI);
                var srcV = 0.5f - lat / MathF.PI;
                var (sr, sg, sb) = SampleBilinear(source, srcU, srcV);
                var lum = Math.Max(sr * 0.2126f + sg * 0.7152f + sb * 0.0722f, 1e-4f);

                // LROC color is physically useful but subtle; the preview disc needs stronger albedo
                // separation so maria and ray systems survive the atmosphere/glow compositing pass.
                var tone = Math.Clamp((lum - 0.52f) * 1.82f + 0.56f, 0.10f, 0.98f);
                var colorMix = 0.22f;
                var rf = tone * Lerp(1f, Math.Clamp(sr / lum, 0.72f, 1.28f), colorMix);
                var gf = tone * Lerp(1f, Math.Clamp(sg / lum, 0.72f, 1.28f), colorMix);
                var bf = tone * Lerp(1f, Math.Clamp(sb / lum, 0.72f, 1.28f), colorMix);
                var edgeAlpha = 1f - Smoothstep(0.992f, 1f, r);

                WritePixel(rgba, x, y,
                    ToByte(rf),
                    ToByte(gf),
                    ToByte(bf),
                    ToByte(edgeAlpha));
            }
        }

        return rgba;
    }

    private static byte[] GenerateProceduralLunaRgba8()
    {
        var rgba = new byte[Size * Size * 4];

        ReadOnlySpan<(float X, float Y, float Rx, float Ry, float Darkness)> maria =
        [
            (-0.46f, 0.05f, 0.30f, 0.48f, 0.48f), // Oceanus Procellarum
            (-0.17f, 0.34f, 0.25f, 0.18f, 0.46f), // Mare Imbrium
            (0.23f, 0.31f, 0.17f, 0.14f, 0.40f),  // Mare Serenitatis
            (0.33f, 0.09f, 0.20f, 0.15f, 0.42f),  // Mare Tranquillitatis
            (0.60f, 0.22f, 0.10f, 0.08f, 0.38f),  // Mare Crisium
            (0.45f, -0.17f, 0.15f, 0.21f, 0.34f), // Mare Fecunditatis
            (0.27f, -0.26f, 0.10f, 0.12f, 0.29f), // Mare Nectaris
            (-0.17f, -0.34f, 0.20f, 0.12f, 0.36f), // Mare Nubium
            (-0.39f, -0.30f, 0.12f, 0.10f, 0.34f), // Mare Humorum
            (0.02f, 0.52f, 0.44f, 0.06f, 0.25f)  // Mare Frigoris
        ];

        ReadOnlySpan<(float X, float Y, float Radius, float Depth, float RayStrength)> craters =
        [
            (-0.06f, -0.61f, 0.055f, 0.48f, 1.00f), // Tycho
            (-0.23f, -0.10f, 0.055f, 0.42f, 0.60f), // Copernicus
            (-0.42f, -0.05f, 0.036f, 0.34f, 0.42f), // Kepler
            (-0.50f, 0.19f, 0.034f, 0.34f, 0.36f),  // Aristarchus
            (-0.16f, 0.49f, 0.042f, 0.32f, 0.08f),  // Plato
            (-0.20f, -0.72f, 0.062f, 0.34f, 0.18f), // Clavius
            (0.51f, -0.22f, 0.036f, 0.28f, 0.18f),  // Langrenus
            (0.34f, -0.42f, 0.043f, 0.28f, 0.20f)   // Theophilus/Cyrillus area
        ];

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

                var nx = dx * 2f;
                var ny = dy * 2f;
                var sphereZ = MathF.Sqrt(Math.Max(1f - r * r, 0f));
                var lon = MathF.Atan2(nx, Math.Max(sphereZ, 1e-4f));
                var lat = MathF.Asin(Math.Clamp(ny, -1f, 1f));
                var sx = lon * 1.35f;
                var sy = lat * 1.35f;

                var macro = Fbm(sx * 1.25f + 1.8f, sy * 1.25f - 0.7f, 1.1f);
                var highland = Fbm(sx * 4.2f - 3.2f, sy * 4.2f + 2.6f, 2.7f);
                var fine = Fbm(sx * 9.0f + 5.1f, sy * 9.0f - 8.4f, 3.9f);

                var mareMask = Smoothstep(0.62f, 0.86f, macro) * 0.18f;
                foreach (var mare in maria)
                {
                    var ox = (nx - mare.X) / mare.Rx;
                    var oy = (ny - mare.Y) / mare.Ry;
                    var basin = 1f - Smoothstep(0.72f, 1.18f, ox * ox + oy * oy);
                    mareMask = Math.Clamp(mareMask + basin * mare.Darkness * 1.35f, 0f, 1f);
                }

                var tone = Lerp(0.88f, 0.42f, mareMask);
                tone += (highland - 0.5f) * 0.16f + (fine - 0.5f) * 0.035f;

                var craterTone = 0f;
                foreach (var crater in craters)
                {
                    var cd = MathF.Sqrt((nx - crater.X) * (nx - crater.X) + (ny - crater.Y) * (ny - crater.Y));
                    var ring = MathF.Exp(-MathF.Pow((cd - crater.Radius) / (crater.Radius * 0.18f), 2f));
                    var floor = 1f - Smoothstep(0.0f, crater.Radius * 0.78f, cd);
                    var ray = MathF.Exp(-cd / Math.Max(crater.Radius * 3.8f, 1e-3f)) *
                              Smoothstep(0.985f, 1.0f, HashAngle(nx - crater.X, ny - crater.Y, crater.Radius));
                    craterTone += ring * crater.Depth * 0.18f;
                    craterTone -= floor * crater.Depth * 0.16f;
                    craterTone += ray * crater.Depth * crater.RayStrength * 0.085f;
                }

                var microCraters = Smoothstep(0.996f, 0.9995f, ValueNoise(sx * 30f, sy * 30f + fine * 1.5f, 7.6f));
                craterTone += microCraters * 0.010f;

                tone = Math.Clamp(tone + craterTone, 0.34f, 0.98f);
                var warm = Lerp(0.96f, 1.03f, highland);
                var rf = tone * warm;
                var gf = tone * (0.99f + fine * 0.03f);
                var bf = tone * (1.04f + (1f - mareMask) * 0.03f);

                var edgeAlpha = 1f - Smoothstep(0.985f, 1f, r);
                WritePixel(rgba, x, y,
                    ToByte(rf),
                    ToByte(gf),
                    ToByte(bf),
                    ToByte(edgeAlpha));
            }
        }

        return rgba;
    }

    private static (float R, float G, float B) SampleBilinear(Image<Rgba32> image, float u, float v)
    {
        var w = image.Width;
        var h = image.Height;
        if (w <= 0 || h <= 0)
        {
            return (0.65f, 0.65f, 0.68f);
        }

        u -= MathF.Floor(u);
        v = Math.Clamp(v, 0f, 1f);
        var fx = u * (w - 1);
        var fy = v * (h - 1);
        var x0 = (int)MathF.Floor(fx);
        var y0 = (int)MathF.Floor(fy);
        var x1 = (x0 + 1) % w;
        var y1 = Math.Min(y0 + 1, h - 1);
        var tx = fx - x0;
        var ty = fy - y0;

        var c00 = image[x0, y0];
        var c10 = image[x1, y0];
        var c01 = image[x0, y1];
        var c11 = image[x1, y1];

        var r0 = Lerp(c00.R / 255f, c10.R / 255f, tx);
        var g0 = Lerp(c00.G / 255f, c10.G / 255f, tx);
        var b0 = Lerp(c00.B / 255f, c10.B / 255f, tx);
        var r1 = Lerp(c01.R / 255f, c11.R / 255f, tx);
        var g1 = Lerp(c01.G / 255f, c11.G / 255f, tx);
        var b1 = Lerp(c01.B / 255f, c11.B / 255f, tx);

        return (Lerp(r0, r1, ty), Lerp(g0, g1, ty), Lerp(b0, b1, ty));
    }

    private static byte ToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

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

    private static float Hash(float x, float y)
    {
        var s = MathF.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
        return s - MathF.Floor(s);
    }

    private static float ValueNoise(float x, float y, float seed)
    {
        var ix = MathF.Floor(x);
        var iy = MathF.Floor(y);
        var fx = Smooth01(x - ix);
        var fy = Smooth01(y - iy);
        var a = Hash(ix + seed * 13.1f, iy + seed * 7.7f);
        var b = Hash(ix + 1f + seed * 13.1f, iy + seed * 7.7f);
        var c = Hash(ix + seed * 13.1f, iy + 1f + seed * 7.7f);
        var d = Hash(ix + 1f + seed * 13.1f, iy + 1f + seed * 7.7f);
        return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
    }

    private static float HashAngle(float x, float y, float seed)
    {
        var angle = MathF.Atan2(y, x);
        return Hash(angle * 8.0f + seed * 19.0f, MathF.Sqrt(x * x + y * y) * 13.0f);
    }

    private static float Fbm(float x, float y, float z)
    {
        var sum = 0f;
        var amp = 0.55f;
        var freq = 1f;
        for (var i = 0; i < 5; i++)
        {
            sum += amp * ValueNoise(x * freq, y * freq, z + i * 17.0f);
            freq *= 2.03f;
            amp *= 0.5f;
        }

        return sum;
    }

    private static float Smooth01(float x) => x * x * (3f - 2f * x);
}
