using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Java edition biome grass tint from <c>textures/colormap/grass.png</c> applied to grayscale block albedos.
/// </summary>
public static class PreviewGrassColormapTint
{
    public const string GrassColormapArchivePath = "assets/minecraft/textures/colormap/grass.png";

    public static bool IsGrassColormapTintIndexPath(string? archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            return false;
        }

        var norm = archivePath.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
        return norm.EndsWith("/grass_block_top.png", StringComparison.Ordinal) ||
               norm.EndsWith("/grass_block_side_overlay.png", StringComparison.Ordinal);
    }

    /// <summary>Snowy grass sides (<c>grass_block_snow</c>) sample the cold/snow region of <c>grass.png</c>.</summary>
    public static bool IsGrassSnowSideColormapTintIndexPath(string? archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            return false;
        }

        return archivePath.Replace('\\', '/').TrimStart('/').ToLowerInvariant()
            .EndsWith("/grass_block_snow.png", StringComparison.Ordinal);
    }

    public static bool NeedsGrassColormapTint(IReadOnlyList<string>? materialArchivePaths) =>
        materialArchivePaths is not null &&
        materialArchivePaths.Any(NeedsGrassColormapTint);

    public static bool NeedsGrassColormapTint(string? archivePath) =>
        IsGrassColormapTintIndexPath(archivePath) || IsGrassSnowSideColormapTintIndexPath(archivePath);

    /// <summary>Vanilla grass colormap lookup: triangular downfall, inverted axes.</summary>
    public static Rgba32 SampleGrassTint(PreviewColormapImage colormap, double temperature01, double downfall01)
    {
        var temperature = Math.Clamp(temperature01, 0.0, 1.0);
        var downfall = Math.Clamp(downfall01, 0.0, 1.0);
        downfall *= temperature;

        var x = (int)Math.Clamp(MathF.Round((float)((1.0 - temperature) * (colormap.Width - 1))), 0f, colormap.Width - 1);
        var y = (int)Math.Clamp(MathF.Round((float)((1.0 - downfall) * (colormap.Height - 1))), 0f, colormap.Height - 1);
        var i = (y * colormap.Width + x) * 4;
        var rgba = colormap.Rgba;
        return new Rgba32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
    }

    /// <summary>
    /// Snowy grass sides mirror temperature so the UI's warm/cold slider moves along the snow edge of <c>grass.png</c>.
    /// </summary>
    public static Rgba32 SampleSnowSideGrassTint(
        PreviewColormapImage colormap,
        double temperature01,
        double downfall01) =>
        SampleGrassTint(colormap, 1.0 - temperature01, downfall01);

    public static Rgba32 SampleTintForArchivePath(
        PreviewColormapImage colormap,
        string? archivePath,
        double temperature01,
        double downfall01) =>
        IsGrassSnowSideColormapTintIndexPath(archivePath)
            ? SampleSnowSideGrassTint(colormap, temperature01, downfall01)
            : SampleGrassTint(colormap, temperature01, downfall01);

    public static byte[] ApplyTintToDiffuse(ReadOnlySpan<byte> diffuseRgba, int width, int height, Rgba32 tint)
    {
        var px = width * height;
        var expected = px * 4;
        if (diffuseRgba.Length < expected || px == 0)
        {
            return diffuseRgba.ToArray();
        }

        var outBytes = new byte[expected];
        for (var i = 0; i < px; i++)
        {
            var o = i * 4;
            var p = diffuseRgba[o];
            if (LooksGrayscale(diffuseRgba[o], diffuseRgba[o + 1], diffuseRgba[o + 2]))
            {
                var lum = (p + diffuseRgba[o + 1] + diffuseRgba[o + 2]) / (3f * 255f);
                outBytes[o] = (byte)Math.Clamp(MathF.Round(lum * tint.R), 0f, 255f);
                outBytes[o + 1] = (byte)Math.Clamp(MathF.Round(lum * tint.G), 0f, 255f);
                outBytes[o + 2] = (byte)Math.Clamp(MathF.Round(lum * tint.B), 0f, 255f);
            }
            else
            {
                outBytes[o] = p;
                outBytes[o + 1] = diffuseRgba[o + 1];
                outBytes[o + 2] = diffuseRgba[o + 2];
            }

            outBytes[o + 3] = diffuseRgba[o + 3];
        }

        return outBytes;
    }

    public static PreviewTextureMaps WithGrassTint(
        PreviewTextureMaps maps,
        string? archivePath,
        PreviewColormapImage colormap,
        double temperature01,
        double downfall01)
    {
        if (!NeedsGrassColormapTint(archivePath))
        {
            return maps;
        }

        var tint = SampleTintForArchivePath(colormap, archivePath, temperature01, downfall01);
        return WithGrassTint(maps, archivePath, tint);
    }

    public static PreviewTextureMaps WithGrassTint(PreviewTextureMaps maps, string? archivePath, Rgba32 tint)
    {
        if (!NeedsGrassColormapTint(archivePath))
        {
            return maps;
        }

        var tinted = ApplyTintToDiffuse(maps.DiffuseRgba, maps.Width, maps.Height, tint);
        return new PreviewTextureMaps
        {
            Width = maps.Width,
            Height = maps.Height,
            DiffuseRgba = tinted,
            NormalRgba = maps.NormalRgba,
            SpecularRgba = maps.SpecularRgba,
            HeightRgba = maps.HeightRgba,
            IsPlantForNoHeight = maps.IsPlantForNoHeight,
            Sprite2DFoliageTarget = maps.Sprite2DFoliageTarget,
            IsItemTexturePath = maps.IsItemTexturePath,
        };
    }

    private static bool LooksGrayscale(byte r, byte g, byte b)
    {
        const int tol = 10;
        return Math.Abs(r - g) <= tol && Math.Abs(g - b) <= tol;
    }
}

/// <summary>RGBA8 grass (or foliage) colormap decoded for preview sampling.</summary>
public sealed class PreviewColormapImage
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required byte[] Rgba { get; init; }
}
