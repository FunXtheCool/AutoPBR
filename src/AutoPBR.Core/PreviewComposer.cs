using AutoPBR.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.Core;

/// <summary>
/// Builds the 2x2 composite preview (diffuse, normal, specular, height) used by the UI.
/// </summary>
internal static class PreviewComposer
{
    public static byte[] ComposePreview(TextureWorkItem target)
    {
        using var diffuse = Image.Load<Rgba32>(target.DiffusePath);
        Image<Rgba32>? normal = null;
        Image<Rgba32>? spec = null;

        if (File.Exists(target.NormalPath))
        {
            normal = Image.Load<Rgba32>(target.NormalPath);
        }

        if (File.Exists(target.SpecularPath))
        {
            spec = Image.Load<Rgba32>(target.SpecularPath);
        }

        var tileWidth = diffuse.Width;
        var tileHeight = diffuse.Height;
        var finalWidth = tileWidth * 2;
        var finalHeight = tileHeight * 2;

        var output = new Image<Rgba32>(finalWidth, finalHeight);

        static void Blit(Image<Rgba32>? src, Image<Rgba32> dst, int offsetX, int offsetY, int tileWidth, int tileHeight)
        {
            if (src is null)
            {
                return;
            }

            var w = Math.Min(tileWidth, src.Width);
            var h = Math.Min(tileHeight, src.Height);

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    dst[offsetX + x, offsetY + y] = src[x, y];
                }
            }
        }

        Blit(diffuse, output, 0, 0, tileWidth, tileHeight);
        Blit(normal, output, tileWidth, 0, tileWidth, tileHeight);
        Blit(spec, output, 0, tileHeight, tileWidth, tileHeight);

        if (normal != null && !target.IsPlantForNoHeight)
        {
            using var height = new Image<Rgba32>(normal.Width, normal.Height);
            for (var y = 0; y < normal.Height; y++)
            {
                for (var x = 0; x < normal.Width; x++)
                {
                    var a = normal[x, y].A;
                    height[x, y] = new Rgba32(a, a, a, 255);
                }
            }

            Blit(height, output, tileWidth, tileHeight, tileWidth, tileHeight);
        }

        using var ms = new MemoryStream();
        output.SaveAsPng(ms);
        return ms.ToArray();
    }
}

