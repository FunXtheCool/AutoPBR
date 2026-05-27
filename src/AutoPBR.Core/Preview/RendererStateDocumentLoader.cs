using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

internal static class RendererStateDocumentLoader
{
    private static readonly ConcurrentDictionary<string, JsonObject?> RendererCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, JsonObject?> ModelCache = new(StringComparer.OrdinalIgnoreCase);
    private static volatile bool _modelIndexBuilt;

    public static string VersionLabel => EntityCleanRoomAnimationMap.VersionLabel;

    public static bool TryLoadByRenderer(string officialRendererJvmName, out JsonObject root)
    {
        root = null!;
        if (string.IsNullOrWhiteSpace(officialRendererJvmName))
        {
            return false;
        }

        if (RendererCache.TryGetValue(officialRendererJvmName, out var cached))
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
            "renderer-state",
            VersionLabel,
            $"{officialRendererJvmName}.json");
        if (!File.Exists(path))
        {
            RendererCache[officialRendererJvmName] = null;
            return false;
        }

        var parsed = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        RendererCache[officialRendererJvmName] = parsed;
        EnsureModelIndex();
        root = parsed;
        return true;
    }

    public static bool TryLoadForModel(string officialModelJvmName, out JsonObject root)
    {
        root = null!;
        if (string.IsNullOrWhiteSpace(officialModelJvmName))
        {
            return false;
        }

        EnsureModelIndex();
        if (ModelCache.TryGetValue(officialModelJvmName, out var cached))
        {
            if (cached is null)
            {
                return false;
            }

            root = cached;
            return true;
        }

        ModelCache[officialModelJvmName] = null;
        return false;
    }

    private static void EnsureModelIndex()
    {
        if (_modelIndexBuilt)
        {
            return;
        }

        var dir = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "minecraft-native",
            "renderer-state",
            VersionLabel);
        if (!Directory.Exists(dir))
        {
            _modelIndexBuilt = true;
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            JsonObject doc;
            try
            {
                doc = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            }
            catch
            {
                continue;
            }

            var renderer = (string?)doc["officialJvmName"];
            if (string.IsNullOrWhiteSpace(renderer))
            {
                continue;
            }

            RendererCache[renderer] = doc;
            if (doc["modelJvmNames"] is not JsonArray models)
            {
                continue;
            }

            foreach (var modelNode in models)
            {
                var model = modelNode?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(model))
                {
                    ModelCache[model] = doc;
                }
            }
        }

        _modelIndexBuilt = true;
    }
}
