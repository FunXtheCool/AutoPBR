using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

internal static class SetupAnimDocumentLoader
{
    private static readonly ConcurrentDictionary<string, JsonObject?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string VersionLabel => EntityCleanRoomAnimationMap.VersionLabel;

    public static bool TryLoadOk(string officialJvmName, out JsonObject root)
    {
        root = null!;
        if (!TryLoad(officialJvmName, out var doc))
        {
            return false;
        }

        if (!IsEffectivelyOk(doc))
        {
            return false;
        }

        root = doc;
        return true;
    }

    public static bool IsEffectivelyOk(JsonObject doc)
    {
        if (string.Equals((string?)doc["extractionStatus"], "ok", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (doc["inheritsSetupAnimFrom"] is not JsonValue inh)
        {
            return false;
        }

        var parent = inh.GetValue<string>();
        return !string.IsNullOrEmpty(parent) &&
               TryLoad(parent, out var parentDoc) &&
               IsEffectivelyOk(parentDoc);
    }

    public static bool TryLoad(string officialJvmName, out JsonObject root)
    {
        root = null!;
        var key = $"{VersionLabel}|{officialJvmName}";
        if (Cache.TryGetValue(key, out var cached))
        {
            if (cached is null)
            {
                return false;
            }

            root = cached;
            return true;
        }

        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "minecraft-native",
            "setup-anim",
            VersionLabel,
            $"{officialJvmName}.json");
        if (!File.Exists(path))
        {
            Cache[key] = null;
            return false;
        }

        var parsed = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Cache[key] = parsed;
        root = parsed;
        return true;
    }
}
