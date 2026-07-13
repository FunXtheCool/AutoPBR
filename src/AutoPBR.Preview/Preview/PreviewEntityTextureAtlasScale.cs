using System.Numerics;

namespace AutoPBR.Preview;

/// <summary>
/// Maps baked entity UVs (normalized against the geometry-IR logical atlas) onto uploaded PNGs.
/// Vanilla entity sheets are upscaled integer multiples of the model atlas (e.g. ghast 64×32 → 128×64),
/// not empty padding — normalized UVs match the same texel coordinates on the larger upload.
/// </summary>
public static class PreviewEntityTextureAtlasScale
{
    public static Vector2 Resolve(int physicalWidth, int physicalHeight, int bakeAtlasWidth, int bakeAtlasHeight)
    {
        if (physicalWidth <= 0 || physicalHeight <= 0)
        {
            return Vector2.One;
        }

        var bakeW = bakeAtlasWidth > 0 ? bakeAtlasWidth : physicalWidth;
        var bakeH = bakeAtlasHeight > 0 ? bakeAtlasHeight : physicalHeight;
        if (bakeW <= 0 || bakeH <= 0)
        {
            return Vector2.One;
        }

        if (physicalWidth >= bakeW &&
            physicalHeight >= bakeH &&
            physicalWidth % bakeW == 0 &&
            physicalHeight % bakeH == 0 &&
            physicalWidth / bakeW == physicalHeight / bakeH)
        {
            return Vector2.One;
        }

        return new Vector2(
            Math.Min(1f, (float)bakeW / physicalWidth),
            Math.Min(1f, (float)bakeH / physicalHeight));
    }
}
