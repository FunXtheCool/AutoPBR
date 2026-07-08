using AutoPBR.Core;

namespace AutoPBR.Preview;

internal static class AssetSourceMaterializer
{
    public static bool Materialize(IAssetSource source, string assetPath, string extractedRoot)
    {
        if (!source.TryReadBytes(assetPath, out var bytes))
        {
            return false;
        }

        if (!ArchivePathSafety.TryResolveExtractionPath(extractedRoot, assetPath, out var outPath))
        {
            return false;
        }

        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(outPath, bytes);
        return true;
    }
}
