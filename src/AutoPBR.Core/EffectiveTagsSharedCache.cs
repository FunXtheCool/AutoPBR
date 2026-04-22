using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

public sealed class SharedEffectiveTagsCacheSnapshot
{
    public string PackPath { get; set; } = "";
    public string Signature { get; set; } = "";
    public Dictionary<string, List<string>> EffectiveTagIdsByStorageKey { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class SharedEffectiveTagsCachePersistence
{
    private static readonly JsonSerializerOptions SerializeOptions = new() { WriteIndented = false };

    private static string DirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoPBR", "effective_tags_cache");

    public static SharedEffectiveTagsCacheSnapshot? Load(string packPath)
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
            return JsonSerializer.Deserialize<SharedEffectiveTagsCacheSnapshot>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(
        string packPath,
        string signature,
        IReadOnlyDictionary<string, IReadOnlyList<string>> effectiveTagIdsByStorageKey)
    {
        if (string.IsNullOrWhiteSpace(packPath))
        {
            return;
        }

        try
        {
            var path = GetFilePath(packPath);
            if (effectiveTagIdsByStorageKey.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            Directory.CreateDirectory(DirectoryPath);
            var snapshot = new SharedEffectiveTagsCacheSnapshot
            {
                PackPath = packPath,
                Signature = signature,
                EffectiveTagIdsByStorageKey = effectiveTagIdsByStorageKey.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ToList(),
                    StringComparer.OrdinalIgnoreCase)
            };
            var json = JsonSerializer.Serialize(snapshot, SerializeOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // best effort
        }
    }

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
}

public static class SharedEffectiveTagsCacheSignature
{
    public static string Compute(
        IReadOnlyList<TagRule> rules,
        MaterialTagSemanticOptions? sem,
        IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>? manualOverrides)
    {
        var sb = new StringBuilder();
        foreach (var rule in rules.OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(rule.Id).Append('|').Append(rule.DisplayName).Append('|').Append((int)rule.Kind).Append('|');
            foreach (var k in rule.Keywords.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append(k).Append(',');
            }

            sb.Append('|');
            foreach (var h in rule.SemanticHints.OrderBy(static h => h, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append(h).Append(',');
            }

            sb.AppendLine();
        }

        AppendSemanticOptions(sb, sem);
        AppendManualOverrides(sb, manualOverrides);

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static void AppendSemanticOptions(StringBuilder sb, MaterialTagSemanticOptions? sem)
    {
        if (sem is null)
        {
            return;
        }

        sb.Append("sem:")
            .Append(sem.Enabled).Append('|')
            .Append(sem.MinSimilarity).Append('|')
            .Append(sem.CertaintyThreshold).Append('|')
            .Append(sem.AdditionalTagMaxGapFromBest).Append('|')
            .Append(sem.MaxTags).Append('|')
            .Append(sem.DictionaryEvidenceEnabled).Append('|')
            .Append(sem.DictionaryEvidenceWeight).Append('|')
            .Append(sem.DictionaryMinEvidenceScore).Append('|')
            .Append(sem.DictionaryRequestTimeoutMs).Append('|')
            .Append(sem.DictionaryLanguageCode)
            .AppendLine();
    }

    private static void AppendManualOverrides(
        StringBuilder sb,
        IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>? manualOverrides)
    {
        if (manualOverrides is null || manualOverrides.Count == 0)
        {
            return;
        }

        foreach (var key in manualOverrides.Keys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))
        {
            var (added, removed) = manualOverrides[key];
            sb.Append("a:").Append(key).Append(':');
            foreach (var id in added.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append(id).Append(',');
            }

            sb.AppendLine();
            sb.Append("r:").Append(key).Append(':');
            foreach (var id in removed.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append(id).Append(',');
            }

            sb.AppendLine();
        }
    }
}
