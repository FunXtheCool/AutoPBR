using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutoPBR.App.Models;

/// <summary>Serializable snapshot of manual tag overrides for a pack (key → added/removed tag ids).</summary>
public sealed class TagOverridesSnapshot
{
    public string PackPath { get; set; } = "";
    public Dictionary<string, TagPathOverrides> Overrides { get; set; } = new();

    public sealed class TagPathOverrides
    {
        public List<string> Added { get; set; } = [];
        public List<string> Removed { get; set; } = [];
    }
}

/// <summary>Load/save manual tag overrides per pack under AppData/AutoPBR/tag_overrides.</summary>
internal static class TagOverridesPersistence
{
    private static readonly JsonSerializerOptions SerializeOptions = new() { WriteIndented = false };

    private static string DirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoPBR", "tag_overrides");

    private static string GetFilePath(string packPath)
    {
        var key = GetStableKey(packPath);
        return Path.Combine(DirectoryPath, key + ".json");
    }

    private static string GetStableKey(string packPath)
    {
        var normalized = Path.GetFullPath(packPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..24];
    }

    public static TagOverridesSnapshot? Load(string packPath)
    {
        if (string.IsNullOrWhiteSpace(packPath))
        {
            return null;
        }

        try
        {
            var path = GetFilePath(packPath);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            var snapshot = JsonSerializer.Deserialize<TagOverridesSnapshot>(json);
            return snapshot;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string packPath, IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)> overrides)
    {
        if (string.IsNullOrWhiteSpace(packPath))
        {
            return;
        }

        try
        {
            var path = GetFilePath(packPath);
            if (overrides.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            var dir = DirectoryPath;
            Directory.CreateDirectory(dir);

            var snapshot = new TagOverridesSnapshot
            {
                PackPath = packPath,
                Overrides = overrides.ToDictionary(
                    kv => kv.Key,
                    kv => new TagOverridesSnapshot.TagPathOverrides
                    {
                        Added = kv.Value.Added.ToList(),
                        Removed = kv.Value.Removed.ToList()
                    },
                    StringComparer.OrdinalIgnoreCase)
            };

            var json = JsonSerializer.Serialize(snapshot, SerializeOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // best-effort
        }
    }
}
