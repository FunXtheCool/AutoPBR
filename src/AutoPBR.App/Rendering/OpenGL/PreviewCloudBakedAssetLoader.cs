using Avalonia.Platform;

using AutoPBR.PreviewGpuAssets;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Loads pre-baked cloud noise/coverage blobs from Assets/Preview when present.</summary>
internal static class PreviewCloudBakedAssetLoader
{
    private const string AssetRoot = "avares://AutoPBR.App/Assets/Preview/";

    public static bool TryLoadShapeNoise(out byte[] rgba)
    {
        rgba = Array.Empty<byte>();
        if (!TryLoadRaw("cloud_noise_shape_128.bin", out var data))
        {
            return false;
        }

        var expected = PreviewCloudNoiseTextureGenerator.Size *
                       PreviewCloudNoiseTextureGenerator.Size *
                       PreviewCloudNoiseTextureGenerator.Size * 4;
        if (data.Length != expected)
        {
            return false;
        }

        rgba = data;
        return true;
    }

    public static bool TryLoadDetailNoise(out byte[] rgba)
    {
        rgba = Array.Empty<byte>();
        if (!TryLoadRaw("cloud_noise_detail_32.bin", out var data))
        {
            return false;
        }

        var expected = PreviewCloudNoiseTextureGenerator.DetailSize *
                       PreviewCloudNoiseTextureGenerator.DetailSize *
                       PreviewCloudNoiseTextureGenerator.DetailSize * 4;
        if (data.Length != expected)
        {
            return false;
        }

        rgba = data;
        return true;
    }

    public static bool TryLoadCoverageMap(out byte[] rgba)
    {
        rgba = Array.Empty<byte>();
        if (!TryLoadRaw("cloud_coverage_256.bin", out var data))
        {
            return false;
        }

        var expected = PreviewCloudCoverageMapGenerator.Size * PreviewCloudCoverageMapGenerator.Size * 4;
        if (data.Length != expected)
        {
            return false;
        }

        rgba = data;
        return true;
    }

    private static bool TryLoadRaw(string fileName, out byte[] data)
    {
        data = Array.Empty<byte>();
        var uri = new Uri(AssetRoot + fileName);
        if (!AssetLoader.Exists(uri))
        {
            return false;
        }

        try
        {
            using var stream = AssetLoader.Open(uri);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            data = ms.ToArray();
            return data.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
