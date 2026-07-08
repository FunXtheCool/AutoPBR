using System.Collections.Concurrent;
using System.Text.Json;


namespace AutoPBR.Preview.GeometryIr;

/// <summary>
/// Loads bytecode-lifted geometry IR shards packaged under
/// <c>Data/minecraft-native/geometry/&lt;versionLabel&gt;/</c>.
/// </summary>
internal static class GeometryIrDocumentLoader
{
    private static readonly ConcurrentDictionary<string, JsonDocument?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryLoad(MinecraftNativeProfile? profile, string officialJvmName, out JsonElement root) =>
        TryLoad(profile, officialJvmName, requireExtractionOk: false, out root);

    /// <summary>
    /// Parity-catalog path: only shards with <c>extractionStatus</c> <c>ok</c> (full bytecode lift).
    /// </summary>
    public static bool TryLoadLiftedOkForParity(
        MinecraftNativeProfile? profile,
        string officialJvmName,
        out JsonElement root) =>
        TryLoad(profile, officialJvmName, requireExtractionOk: true, out root);

    /// <summary>
    /// Parity-catalog mesh driver: prefers <c>ok</c> shards, then <c>partial</c> when
    /// <see cref="GeometryIrLiftPolicy"/> still allows emit (avoids silent hand-<c>Build*</c> fallback).
    /// </summary>
    public static bool TryLoadLiftedForParityCatalog(
        MinecraftNativeProfile? profile,
        string officialJvmName,
        out JsonElement root)
    {
        root = default;
        foreach (var ver in NativeIrVersionLabels.ForGeometryIrShardLookup(profile))
        {
            if (!TryLoadShard(ver, officialJvmName, requireExtractionOk: false, out var candidate) ||
                GeometryIrLiftPolicy.EvaluateDocument(candidate) == GeometryIrLiftPolicyDecision.RejectForParity)
            {
                continue;
            }

            root = candidate;
            return true;
        }

        return false;
    }

    private static bool TryLoad(
        MinecraftNativeProfile? profile,
        string officialJvmName,
        bool requireExtractionOk,
        out JsonElement root)
    {
        root = default;
        foreach (var ver in NativeIrVersionLabels.ForGeometryIrShardLookup(profile))
        {
            if (TryLoadShard(ver, officialJvmName, requireExtractionOk, out root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryLoadShard(
        string versionLabel,
        string officialJvmName,
        bool requireExtractionOk,
        out JsonElement root)
    {
        root = default;
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "minecraft-native",
            "geometry",
            versionLabel,
            $"{officialJvmName}.json");
        if (!File.Exists(path))
        {
            return false;
        }

        var doc = Cache.GetOrAdd(path, static p =>
        {
            try
            {
                return JsonDocument.Parse(File.ReadAllText(p));
            }
            catch
            {
                return null;
            }
        });

        if (doc is null)
        {
            return false;
        }

        root = doc.RootElement;
        if (!root.TryGetProperty("extractionStatus", out var status))
        {
            return false;
        }

        var statusStr = status.GetString();
        return requireExtractionOk
            ? string.Equals(statusStr, "ok", StringComparison.Ordinal)
            : statusStr is "ok" or "partial";
    }
}
