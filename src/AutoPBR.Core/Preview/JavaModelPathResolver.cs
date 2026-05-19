using System.Text.Json;

namespace AutoPBR.Core.Preview;

internal static class JavaModelPathResolver
{
    /// <summary>Resolve the principal block/item/entity model JSON path from a selected texture path.</summary>
    public static bool TryResolveModelJsonFromTexture(
        IAssetSource source,
        string textureArchivePath,
        out string modelJsonZipPath,
        out string defaultNamespace)
    {
        modelJsonZipPath = string.Empty;
        defaultNamespace = "minecraft";
        var norm = textureArchivePath.Replace('\\', '/').TrimStart('/');
        if (!norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = norm.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || !parts[0].Equals("assets", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        defaultNamespace = parts[1];
        if (!parts[2].Equals("textures", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var category = parts[3];
        if (!category.Equals("block", StringComparison.OrdinalIgnoreCase) &&
            !category.Equals("item", StringComparison.OrdinalIgnoreCase) &&
            !category.Equals("entity", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(parts[^1]);
        var relTexturePath = string.Join('/', parts.Skip(4));
        var relStem = relTexturePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? relTexturePath[..^4]
            : relTexturePath;
        var modelsFolder = category.Equals("item", StringComparison.OrdinalIgnoreCase)
            ? "item"
            : category.Equals("entity", StringComparison.OrdinalIgnoreCase)
                ? "entity"
                : "block";

        // 1) Heuristic stem (door lower -> bottom model name).
        var mapped = MapTextureStemToModelStem(stem);
        var relMapped = MapTextureRelPathToModelRelPath(relStem);
        var candidates = new List<string>
        {
            $"assets/{defaultNamespace}/models/{modelsFolder}/{relMapped}.json",
            $"assets/{defaultNamespace}/models/{modelsFolder}/{relStem}.json",
            $"assets/{defaultNamespace}/models/{modelsFolder}/{mapped}.json",
            $"assets/{defaultNamespace}/models/{modelsFolder}/{stem}.json"
        };

        foreach (var c in candidates)
        {
            if (source.Exists(c))
            {
                modelJsonZipPath = c;
                return true;
            }
        }

        // 2) Blockstates (blocks only): assets/ns/blockstates/<stem>.json
        if (category.Equals("block", StringComparison.OrdinalIgnoreCase))
        {
            var bsStem = BlockstateStem(stem);
            var bsPath = $"assets/{defaultNamespace}/blockstates/{bsStem}.json";
            if (source.Exists(bsPath) && source.TryReadText(bsPath, out var bsText) &&
                TryPickModelFromBlockstate(bsText, out var modelNotation) &&
                ModelNotationToZipPath(modelNotation, out modelJsonZipPath) &&
                source.Exists(modelJsonZipPath))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool ModelNotationToZipPath(string notation, out string zipPath)
    {
        zipPath = string.Empty;
        var ns = "minecraft";
        var r = notation.Replace('\\', '/').Trim();
        if (string.IsNullOrEmpty(r))
        {
            return false;
        }

        if (r.Contains(':', StringComparison.Ordinal))
        {
            var c = r.IndexOf(':');
            ns = r[..c];
            r = r[(c + 1)..].TrimStart('/');
        }

        if (!r.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            r += ".json";
        }

        zipPath = $"assets/{ns}/models/{r}";
        return true;
    }

    internal static string MapTextureStemToModelStem(string stemNoExt)
    {
        if (stemNoExt.EndsWith("_lower", StringComparison.OrdinalIgnoreCase))
        {
            return stemNoExt[..^6] + "_bottom";
        }

        if (stemNoExt.EndsWith("_upper", StringComparison.OrdinalIgnoreCase))
        {
            return stemNoExt[..^6] + "_top";
        }

        return stemNoExt;
    }

    internal static string MapTextureRelPathToModelRelPath(string relNoExt)
    {
        var n = relNoExt.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(n))
        {
            return n;
        }

        var dir = Path.GetDirectoryName(n)?.Replace('\\', '/');
        var file = Path.GetFileName(n);
        var mapped = MapTextureStemToModelStem(file);
        return string.IsNullOrEmpty(dir) ? mapped : $"{dir}/{mapped}";
    }

    internal static string BlockstateStem(string stemNoExt)
    {
        foreach (var suf in new[] { "_lower", "_upper", "_bottom", "_top", "_side" })
        {
            if (stemNoExt.EndsWith(suf, StringComparison.OrdinalIgnoreCase) && stemNoExt.Length > suf.Length)
            {
                return stemNoExt[..^suf.Length];
            }
        }

        return stemNoExt;
    }

    internal static bool TryPickModelFromBlockstate(string json, out string modelNotation)
    {
        modelNotation = string.Empty;
        using var doc = JsonDocument.Parse(json,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = doc.RootElement;
        if (root.TryGetProperty("variants", out var variants) && variants.ValueKind == JsonValueKind.Object)
        {
            string? bestKey = null;
            foreach (var prop in variants.EnumerateObject())
            {
                if (prop.Name.Contains("half=lower", StringComparison.OrdinalIgnoreCase) &&
                    !prop.Name.Contains("half=upper", StringComparison.OrdinalIgnoreCase))
                {
                    bestKey = prop.Name;
                    break;
                }
            }

            bestKey ??= variants.EnumerateObject().FirstOrDefault().Name;
            if (string.IsNullOrEmpty(bestKey) || !variants.TryGetProperty(bestKey, out var vObj))
            {
                return false;
            }

            return TryGetModelProperty(vObj, out modelNotation);
        }

        if (root.TryGetProperty("multipart", out var mp) && mp.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in mp.EnumerateArray())
            {
                if (!part.TryGetProperty("apply", out var apply))
                {
                    continue;
                }

                if (TryGetModelProperty(apply, out modelNotation))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetModelProperty(JsonElement vObj, out string modelNotation)
    {
        modelNotation = string.Empty;
        if (vObj.ValueKind == JsonValueKind.String)
        {
            modelNotation = vObj.GetString() ?? string.Empty;
            return !string.IsNullOrEmpty(modelNotation);
        }

        if (vObj.ValueKind == JsonValueKind.Object && vObj.TryGetProperty("model", out var m) &&
            m.ValueKind == JsonValueKind.String)
        {
            modelNotation = m.GetString() ?? string.Empty;
            return !string.IsNullOrEmpty(modelNotation);
        }

        return false;
    }

}
