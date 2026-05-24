using System.IO.Compression;

namespace AutoPBR.Tools.AnimationCompiler;

internal static class ClientJarClassBytes
{
    public static bool TryReadClass(string jarPath, string officialJvmName, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        try
        {
            using var zip = ZipFile.OpenRead(jarPath);
            var slash = officialJvmName.Replace('.', '/') + ".class";
            var e = zip.GetEntry(slash);
            if (e is null)
            {
                return false;
            }

            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            bytes = ms.ToArray();
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static string ComputeSha256Hex(ReadOnlySpan<byte> data)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
