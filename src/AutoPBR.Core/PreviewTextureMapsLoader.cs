using AutoPBR.Core.Models;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.Core;

internal static class PreviewTextureMapsLoader
{
    public static PreviewTextureMaps Load(TextureWorkItem target)
    {
        using var diffuse = Image.Load<Rgba32>(target.DiffusePath);
        var w = diffuse.Width;
        var h = diffuse.Height;
        var diffuseBytes = new byte[w * h * 4];
        diffuse.CopyPixelDataTo(diffuseBytes);

        byte[]? normalBytes = null;
        byte[]? heightBytes = null;
        if (File.Exists(target.NormalPath))
        {
            using var normal = Image.Load<Rgba32>(target.NormalPath);
            normalBytes = new byte[normal.Width * normal.Height * 4];
            normal.CopyPixelDataTo(normalBytes);

            if (!target.IsPlantForNoHeight)
            {
                heightBytes = new byte[normal.Width * normal.Height * 4];
                for (var y = 0; y < normal.Height; y++)
                {
                    for (var x = 0; x < normal.Width; x++)
                    {
                        var a = normal[x, y].A;
                        var i = (y * normal.Width + x) * 4;
                        heightBytes[i] = a;
                        heightBytes[i + 1] = a;
                        heightBytes[i + 2] = a;
                        heightBytes[i + 3] = 255;
                    }
                }
            }
        }

        byte[]? specBytes = null;
        if (File.Exists(target.SpecularPath))
        {
            using var spec = Image.Load<Rgba32>(target.SpecularPath);
            specBytes = new byte[spec.Width * spec.Height * 4];
            spec.CopyPixelDataTo(specBytes);
        }

        return new PreviewTextureMaps
        {
            Width = w,
            Height = h,
            DiffuseRgba = diffuseBytes,
            NormalRgba = normalBytes,
            SpecularRgba = specBytes,
            HeightRgba = heightBytes,
            IsPlantForNoHeight = target.IsPlantForNoHeight,
            Sprite2DFoliageTarget = target.Sprite2DFoliageTarget
        };
    }
}
