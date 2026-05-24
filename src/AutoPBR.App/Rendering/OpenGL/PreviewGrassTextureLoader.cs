using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Loads vanilla-style grass_block_top (often grayscale for biome tint in JE) into RGBA for the preview ground.
/// Source PNG is bundled under Assets/Preview (see repository attribution in PR / packaging notes).
/// </summary>
internal static class PreviewGrassTextureLoader
{
    /// <summary>Approximate plains biome grass tint applied when the texture reads as grayscale.</summary>
    private static readonly Rgba32 PlainsGrassTint = new(91, 139, 54, 255);

    public static bool TryDecodeTinted(Stream pngStream, out byte[] rgba, out int width, out int height)
    {
        rgba = [];
        width = height = 0;
        try
        {
            using var image = Image.Load<Rgba32>(pngStream);
            width = image.Width;
            height = image.Height;
            if (width < 1 || height < 1)
            {
                return false;
            }

            rgba = new byte[width * height * 4];
            var o = 0;
            // OpenGL texture upload assumes bottom row first for this preview path;
            // walk source rows bottom->top to keep the ground texture upright in-world.
            for (var y = height - 1; y >= 0; y--)
            {
                for (var x = 0; x < width; x++)
                {
                    var p = image[x, y];
                    byte rr, gg, bb;
                    if (LooksGrayscale(p))
                    {
                        var lum = (p.R + p.G + p.B) / (3f * 255f);
                        rr = (byte)Math.Clamp(MathF.Round(lum * PlainsGrassTint.R), 0f, 255f);
                        gg = (byte)Math.Clamp(MathF.Round(lum * PlainsGrassTint.G), 0f, 255f);
                        bb = (byte)Math.Clamp(MathF.Round(lum * PlainsGrassTint.B), 0f, 255f);
                    }
                    else
                    {
                        rr = p.R;
                        gg = p.G;
                        bb = p.B;
                    }

                    rgba[o++] = rr;
                    rgba[o++] = gg;
                    rgba[o++] = bb;
                    rgba[o++] = p.A;
                }
            }

            return true;
        }
        catch
        {
            rgba = [];
            width = height = 0;
            return false;
        }
    }

    private static bool LooksGrayscale(Rgba32 p)
    {
        const int tol = 10;
        return Math.Abs(p.R - p.G) <= tol && Math.Abs(p.G - p.B) <= tol;
    }
}
