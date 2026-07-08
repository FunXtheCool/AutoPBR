using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutoPBR.Core.Embeddings;

internal sealed class MiniLmEmbeddingCacheEntry
{
    public string Text { get; set; } = "";
    public float[] Vector { get; set; } = [];
    public long LastAccessUtcTicks { get; set; }
}

internal sealed class MiniLmEmbeddingCacheSnapshot
{
    public string ModelSignature { get; set; } = "";
    public int VectorDimension { get; set; }
    public List<MiniLmEmbeddingCacheEntry> Entries { get; set; } = [];
}

internal static class MiniLmEmbeddingPersistentCache
{
    private static readonly JsonSerializerOptions SerializeOptions = new() { WriteIndented = false };

    private static string CacheDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoPBR", "embedding_cache");

    private static string CacheFilePath =>
        Path.Combine(CacheDirectoryPath, "mini_lm_embeddings.json");

    public static string ComputeModelSignature(string? modelPath, string? vocabPath)
    {
        var modelSig = BuildFileIdentity(modelPath);
        var vocabSig = BuildFileIdentity(vocabPath);
        var payload = $"{modelSig}|{vocabSig}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    public static MiniLmEmbeddingCacheSnapshot? Load(string expectedSignature)
    {
        if (string.IsNullOrWhiteSpace(expectedSignature))
        {
            return null;
        }

        try
        {
            if (!File.Exists(CacheFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(CacheFilePath);
            var snapshot = JsonSerializer.Deserialize<MiniLmEmbeddingCacheSnapshot>(json);
            if (snapshot is null)
            {
                return null;
            }

            if (!string.Equals(snapshot.ModelSignature, expectedSignature, StringComparison.Ordinal))
            {
                return null;
            }

            return snapshot;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(MiniLmEmbeddingCacheSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectoryPath);
            var json = JsonSerializer.Serialize(snapshot, SerializeOptions);
            File.WriteAllText(CacheFilePath, json);
        }
        catch
        {
            // best effort
        }
    }

    private static string BuildFileIdentity(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "(missing)";
        }

        try
        {
            var info = new FileInfo(path);
            return $"{Path.GetFullPath(path)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        }
        catch
        {
            return Path.GetFullPath(path);
        }
    }
}
