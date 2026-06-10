using System.Numerics;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Procedural 64³ cloud noise atlas (R=worley-ish, G=perlin, B=detail).</summary>
internal static class PreviewCloudNoiseTextureGenerator
{
    public const int Size = 64;

    public static byte[] GenerateRgba8()
    {
        var rgba = new byte[Size * Size * Size * 4];

        for (var z = 0; z < Size; z++)
        {
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var px = (x + 0.5f) / Size;
                    var py = (y + 0.5f) / Size;
                    var pz = (z + 0.5f) / Size;
                    var p = new Vector3(px, py, pz) * 4f;

                    var worley = Worley(p);
                    var perlin = Fbm(p * 1.2f + new Vector3(3.1f, 7.7f, 2.3f));
                    var detail = Fbm(p * 3.4f + new Vector3(19f, 5f, 11f));

                    WriteVoxel(rgba, x, y, z,
                        (byte)(worley * 255f),
                        (byte)(perlin * 255f),
                        (byte)(detail * 255f),
                        255);
                }
            }
        }

        return rgba;
    }

    private static void WriteVoxel(byte[] rgba, int x, int y, int z, byte r, byte g, byte b, byte a)
    {
        var i = ((z * Size + y) * Size + x) * 4;
        rgba[i] = r;
        rgba[i + 1] = g;
        rgba[i + 2] = b;
        rgba[i + 3] = a;
    }

    private static float Worley(Vector3 p)
    {
        var cell = new Vector3(MathF.Floor(p.X), MathF.Floor(p.Y), MathF.Floor(p.Z));
        var minDist = 1f;
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var neighbor = cell + new Vector3(dx, dy, dz);
                    var point = neighbor + Hash31(neighbor);
                    var dist = Vector3.Distance(p, point);
                    minDist = MathF.Min(minDist, dist);
                }
            }
        }

        return 1f - MathF.Min(minDist, 1f);
    }

    private static Vector3 Hash31(Vector3 cell)
    {
        var h = cell.X * 127.1f + cell.Y * 311.7f + cell.Z * 74.7f;
        return new Vector3(Hash01(h), Hash01(h + 19.7f), Hash01(h + 43.3f));
    }

    private static float Fbm(Vector3 p)
    {
        var v = 0f;
        var a = 0.5f;
        for (var i = 0; i < 4; i++)
        {
            v += a * Noise3(p);
            p = p * 2.03f + new Vector3(1.7f, 2.3f, 0.9f);
            a *= 0.5f;
        }

        return v;
    }

    private static float Noise3(Vector3 p)
    {
        var i = new Vector3(MathF.Floor(p.X), MathF.Floor(p.Y), MathF.Floor(p.Z));
        var f = p - i;
        f = new Vector3(Smooth(f.X), Smooth(f.Y), Smooth(f.Z));

        var n000 = Hash01(i);
        var n100 = Hash01(i + Vector3.UnitX);
        var n010 = Hash01(i + Vector3.UnitY);
        var n110 = Hash01(i + Vector3.UnitX + Vector3.UnitY);
        var n001 = Hash01(i + Vector3.UnitZ);
        var n101 = Hash01(i + Vector3.UnitX + Vector3.UnitZ);
        var n011 = Hash01(i + Vector3.UnitY + Vector3.UnitZ);
        var n111 = Hash01(i + Vector3.UnitX + Vector3.UnitY + Vector3.UnitZ);

        var nx00 = Lerp(n000, n100, f.X);
        var nx10 = Lerp(n010, n110, f.X);
        var nx01 = Lerp(n001, n101, f.X);
        var nx11 = Lerp(n011, n111, f.X);
        var nxy0 = Lerp(nx00, nx10, f.Y);
        var nxy1 = Lerp(nx01, nx11, f.Y);
        return Lerp(nxy0, nxy1, f.Z);
    }

    private static float Hash01(Vector3 p) => Hash01(p.X * 17.3f + p.Y * 43.1f + p.Z * 91.7f);

    private static float Hash01(float seed)
    {
        var h = MathF.Sin(seed * 12.9898f) * 43758.5453f;
        return h - MathF.Floor(h);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
