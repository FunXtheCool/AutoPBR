using AutoPBR.Core;

namespace AutoPBR.Preview;

internal sealed class DirectoryAssetSource(string rootDirectory) : IAssetSource
{
    public bool Exists(string assetPath) =>
        TryToDiskPath(assetPath, out var path) && File.Exists(path);

    public bool TryReadBytes(string assetPath, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!TryToDiskPath(assetPath, out var p))
        {
            return false;
        }

        if (!File.Exists(p))
        {
            return false;
        }

        bytes = File.ReadAllBytes(p);
        return true;
    }

    public bool TryReadText(string assetPath, out string text)
    {
        text = string.Empty;
        if (!TryToDiskPath(assetPath, out var p))
        {
            return false;
        }

        if (!File.Exists(p))
        {
            return false;
        }

        text = File.ReadAllText(p);
        return true;
    }

    private bool TryToDiskPath(string assetPath, out string path) =>
        ArchivePathSafety.TryResolveExtractionPath(rootDirectory, assetPath, out path);
}
