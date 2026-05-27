using System.Text.Json;
using System.Text.Json.Nodes;


namespace AutoPBR.Tools.GeometryCompiler;

internal sealed partial class GeometryCompilerHost
{
    private static JsonArray CreateSkippedRoots() =>
        new(new JsonObject
        {
            ["id"] = "root",
            ["pose"] = new JsonObject
            {
                ["translation"] = new JsonArray { 0, 0, 0 },
                ["rotationEulerRad"] = new JsonArray { 0, 0, 0 },
                ["eulerOrder"] = "XYZ"
            },
            ["cuboids"] = new JsonArray(),
            ["children"] = new JsonArray()
        });

    private static string? TryPickShardTemplateFile(string outputShardPath)
    {
        if (File.Exists(outputShardPath) && !IsPlaceholderOnlyShard(outputShardPath))
        {
            return outputShardPath;
        }

        return File.Exists(outputShardPath) ? outputShardPath : null;
    }

    private JsonObject CreateSyntheticShard(string officialJvmName, string mappingKind)
    {
        var profile = mappingKind == "named_jar" ? $"named_jar_{_versionLabel}" : $"proguard_{_versionLabel}";
        return new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = _versionLabel,
            ["officialJvmName"] = officialJvmName,
            ["profile"] = profile,
            ["extractionStatus"] = "partial",
            ["extractionNotes"] = new JsonArray(
                "Synthetic placeholder shard: client.jar metadata (SHA-256, javap float probe) only. Extend GeometryCompiler extraction profiles or re-run with a matching jar so javap mesh lift can emit cuboids."),
            ["factoryMethod"] = "createBodyLayer",
            ["roots"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "_placeholder",
                    ["cuboids"] = new JsonArray(),
                    ["children"] = new JsonArray()
                }
            }
        };
    }

    private static bool IsPlaceholderOnlyShard(string path)
    {
        try
        {
            var o = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            if (o["roots"] is not JsonArray roots || roots.Count != 1)
            {
                return false;
            }

            return roots[0] is JsonObject r && string.Equals((string?)r["id"], "_placeholder", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private string ReadShardStatus(string official)
    {
        var shardPath = Path.Combine(_outDir, "geometry", _versionLabel, $"{official}.json");
        if (!File.Exists(shardPath))
        {
            return "partial";
        }

        var o = JsonNode.Parse(File.ReadAllText(shardPath))!.AsObject();
        return (string?)o["extractionStatus"] ?? "partial";
    }

    private void TryAddShaFromShard(JsonObject entry, string official)
    {
        var shardPath = Path.Combine(_outDir, "geometry", _versionLabel, $"{official}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        var o = JsonNode.Parse(File.ReadAllText(shardPath))!.AsObject();
        if (o["classSha256Hex"] is JsonValue v)
        {
            entry["classSha256Hex"] = v.ToString();
        }

        if (o["jarPath"] is JsonValue jp)
        {
            entry["jarPath"] = jp.ToString();
        }
    }

    private void UpsertIndexEntry(string mappingKind, string official, string jarPath, string sha, string status)
    {
        var indexPath = Path.Combine(_outDir, $"geometry-index-{_versionLabel}.json");
        JsonObject root;
        if (File.Exists(indexPath))
        {
            root = JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject();
        }
        else
        {
            root = new JsonObject
            {
                ["schemaVersion"] = 2,
                ["versionLabel"] = _versionLabel,
                ["mappingKind"] = mappingKind,
                ["entries"] = new JsonArray()
            };
        }

        root["mappingKind"] = mappingKind;
        if (root["entries"] is not JsonArray arr)
        {
            arr = [];
            root["entries"] = arr;
        }

        JsonObject? existing = null;
        foreach (var n in arr)
        {
            if (n is JsonObject o &&
                string.Equals((string?)o["officialJvmName"], official, StringComparison.Ordinal))
            {
                existing = o;
                break;
            }
        }

        var entry = existing ?? new JsonObject();
        entry["officialJvmName"] = official;
        entry["jarPath"] = jarPath;
        entry["classSha256Hex"] = sha;
        entry["shardRelPath"] = $"geometry/{_versionLabel}/{official}.json";
        entry["profile"] = mappingKind == "named_jar" ? $"named_jar_{_versionLabel}" : $"proguard_{_versionLabel}";
        entry["extractionStatus"] = status;
        entry["modelIndexJvmName"] = official;
        if (existing is null)
        {
            arr.Add(entry);
        }

        File.WriteAllText(indexPath, root.ToJsonString(WriteIndentedJson));
    }
}
