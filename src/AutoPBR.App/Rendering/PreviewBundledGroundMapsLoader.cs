using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.OpenGL;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

using Avalonia.Platform;

namespace AutoPBR.App.Rendering;

/// <summary>Bundled tinted grass albedo plus pre-generated LabPBR maps used when no pack/install source is available.</summary>
internal static class PreviewBundledGroundMapsLoader
{
    private const string AlbedoUri = "avares://AutoPBR.App/Assets/Preview/grass_block_top.png";
    private const string NormalUri = "avares://AutoPBR.App/Assets/Preview/grass_block_top_n.png";
    private const string SpecularUri = "avares://AutoPBR.App/Assets/Preview/grass_block_top_s.png";

    public static bool TryLoad(out PreviewMaterial material)
    {
        material = null!;
        try
        {
            if (!AssetLoader.Exists(new Uri(AlbedoUri)))
            {
                return false;
            }

            using var albedoStream = AssetLoader.Open(new Uri(AlbedoUri));
            if (!PreviewGrassTextureLoader.TryDecodeTinted(albedoStream, out var albedo, out var w, out var h) ||
                w < 1 || h < 1)
            {
                return false;
            }

            byte[]? normal = TryLoadRgbaAsset(NormalUri, w * h * 4);
            byte[]? spec = TryLoadRgbaAsset(SpecularUri, w * h * 4);
            byte[]? height = null;
            if (normal is not null)
            {
                height = ExtractHeightFromNormalAlpha(normal, w, h);
            }

            material = new PreviewMaterial
            {
                Width = w,
                Height = h,
                AlbedoRgba = albedo,
                NormalRgba = normal,
                SpecularRgba = spec,
                HeightRgba = height,
                GlUploadFlipRows = false,
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[]? TryLoadRgbaAsset(string uriText, int expectedBytes)
    {
        var uri = new Uri(uriText);
        if (!AssetLoader.Exists(uri))
        {
            return null;
        }

        using var stream = AssetLoader.Open(uri);
        if (!PreviewGrassTextureLoader.TryDecodeRgba(stream, out var rgba, out var w, out var h) || w < 1 || h < 1)
        {
            return null;
        }

        return rgba.Length >= expectedBytes ? rgba : null;
    }

    private static byte[] ExtractHeightFromNormalAlpha(ReadOnlySpan<byte> normalRgba, int width, int height)
    {
        var px = width * height;
        var heightBytes = new byte[px * 4];
        for (var i = 0; i < px; i++)
        {
            var a = normalRgba[i * 4 + 3];
            var o = i * 4;
            heightBytes[o] = a;
            heightBytes[o + 1] = a;
            heightBytes[o + 2] = a;
            heightBytes[o + 3] = 255;
        }

        return heightBytes;
    }
}
