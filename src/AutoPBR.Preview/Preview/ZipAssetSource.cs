using System.IO.Compression;
using System.Text;

namespace AutoPBR.Preview;

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
