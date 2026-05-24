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

    public static bool IsEffectivelyOk(JsonObject doc) =>
        IsEffectivelyOk(doc, new HashSet<string>(StringComparer.Ordinal));

    private static bool IsEffectivelyOk(JsonObject doc, HashSet<string> visited)
    {
        var self = (string?)doc["officialJvmName"];
        if (!string.IsNullOrEmpty(self) && !visited.Add(self))
        {
            return false;
        }

        if (string.Equals((string?)doc["extractionStatus"], "ok", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (doc["inheritsSetupAnimFrom"] is not JsonValue inh)
        {
            return false;
        }

        var parent = inh.GetValue<string>();
        if (string.IsNullOrEmpty(parent) ||
            string.Equals(parent, self, StringComparison.Ordinal) ||
            visited.Contains(parent))
        {
            return false;
        }

        return TryLoad(parent, out var parentDoc) && IsEffectivelyOk(parentDoc, visited);
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
