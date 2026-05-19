using System.IO.Compression;
using System.Text;

namespace AutoPBR.Core.Preview;

internal interface IAssetSource
{
    bool Exists(string assetPath);
    bool TryReadBytes(string assetPath, out byte[] bytes);
    bool TryReadText(string assetPath, out string text);
}

internal sealed class ZipAssetSource(ZipArchive zip) : IAssetSource
{
    public bool Exists(string assetPath)
    {
        var p = Normalize(assetPath);
        return zip.GetEntry(p) is not null;
    }

    public bool TryReadBytes(string assetPath, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var p = Normalize(assetPath);
        var e = zip.GetEntry(p);
        if (e is null || string.IsNullOrEmpty(e.Name))
        {
            return false;
        }

        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        bytes = ms.ToArray();
        return true;
    }

    public bool TryReadText(string assetPath, out string text)
    {
        text = string.Empty;
        if (!TryReadBytes(assetPath, out var bytes))
        {
            return false;
        }

        text = Encoding.UTF8.GetString(bytes);
        return true;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}

internal sealed class DirectoryAssetSource(string rootDirectory) : IAssetSource
{
    public bool Exists(string assetPath) => File.Exists(ToDiskPath(assetPath));

    public bool TryReadBytes(string assetPath, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var p = ToDiskPath(assetPath);
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
        var p = ToDiskPath(assetPath);
        if (!File.Exists(p))
        {
            return false;
        }

        text = File.ReadAllText(p);
        return true;
    }

    private string ToDiskPath(string assetPath)
    {
        var rel = assetPath.Replace('\\', '/').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(rootDirectory, rel);
    }
}

internal sealed class CompositeAssetSource(params IAssetSource[] sources) : IAssetSource
{
    public bool Exists(string assetPath)
    {
        foreach (var s in sources)
        {
            if (s.Exists(assetPath))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryReadBytes(string assetPath, out byte[] bytes)
    {
        foreach (var s in sources)
        {
            if (s.TryReadBytes(assetPath, out bytes))
            {
                return true;
            }
        }

        bytes = Array.Empty<byte>();
        return false;
    }

    public bool TryReadText(string assetPath, out string text)
    {
        foreach (var s in sources)
        {
            if (s.TryReadText(assetPath, out text))
            {
                return true;
            }
        }

        text = string.Empty;
        return false;
    }
}

internal static class AssetSourceMaterializer
{
    public static bool Materialize(IAssetSource source, string assetPath, string extractedRoot)
    {
        if (!source.TryReadBytes(assetPath, out var bytes))
        {
            return false;
        }

        var rel = assetPath.Replace('\\', '/').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var outPath = Path.Combine(extractedRoot, rel);
        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(outPath, bytes);
        return true;
    }
}
