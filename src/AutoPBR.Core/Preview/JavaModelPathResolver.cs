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
        return TryResolveModelJsonPathsFromTexture(source, textureArchivePath, out var paths, out defaultNamespace) &&
               paths.Count > 0 &&
               (modelJsonZipPath = paths[0]).Length > 0;
    }

    /// <summary>Resolve one or more model JSON paths (multipart blockstates may yield several).</summary>
    public static bool TryResolveModelJsonPathsFromTexture(
        IAssetSource source,
        string textureArchivePath,
        out List<string> modelJsonZipPaths,
        out string defaultNamespace)
    {
        modelJsonZipPaths = [];
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

        var mapped = MapTextureStemToModelStem(stem);
        var relMapped = MapTextureRelPathToModelRelPath(relStem);
        var candidates = new List<string>
        {
            $"assets/{defaultNamespace}/models/{modelsFolder}/{relMapped}.json",
            $"assets/{defaultNamespace}/models/{modelsFolder}/{relStem}.json",
            $"assets/{defaultNamespace}/models/{modelsFolder}/{mapped}.json",
            $"assets/{defaultNamespace}/models/{modelsFolder}/{stem}.json",
        };

        foreach (var c in candidates)
        {
            if (source.Exists(c))
            {
                modelJsonZipPaths.Add(c);
                BlockDoorPreviewPairing.TryAppendDoorPairModelPaths(source, stem, defaultNamespace, modelJsonZipPaths);
                return true;
            }
        }

        if (category.Equals("block", StringComparison.OrdinalIgnoreCase))
        {
            var bsStem = BlockstateStem(stem);
            var familyStem = BlockPreviewBlockstateDefaults.ResolveFamilyStem(stem);
            var bsPath = $"assets/{defaultNamespace}/blockstates/{bsStem}.json";
            if (source.Exists(bsPath) && source.TryReadText(bsPath, out var bsText) &&
                TryPickModelPathsFromBlockstate(bsText, familyStem, stem, out var fromBlockstate))
            {
                foreach (var notation in fromBlockstate)
                {
                    if (!ModelNotationToZipPath(notation, out var zipPath) || !source.Exists(zipPath))
                    {
                        continue;
                    }

                    if (!modelJsonZipPaths.Contains(zipPath, StringComparer.OrdinalIgnoreCase))
                    {
                        modelJsonZipPaths.Add(zipPath);
                    }
                }

                if (modelJsonZipPaths.Count > 0)
                {
                    BlockDoorPreviewPairing.TryAppendDoorPairModelPaths(source, stem, defaultNamespace, modelJsonZipPaths);
                    return true;
                }
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
        foreach (var suf in new[] { "_lower", "_upper", "_bottom", "_top", "_inner", "_side" })
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
        return TryPickModelPathsFromBlockstate(json, string.Empty, null, out var paths) &&
               paths.Count > 0 &&
               (modelNotation = paths[0]).Length > 0;
    }

    internal static bool TryPickModelPathsFromBlockstate(
        string json,
        string familyStem,
        string? textureStem,
        out List<string> modelNotations)
    {
        modelNotations = [];
        using var doc = JsonDocument.Parse(json,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = doc.RootElement;
        if (root.TryGetProperty("variants", out var variants) && variants.ValueKind == JsonValueKind.Object)
        {
            var keys = variants.EnumerateObject().Select(p => p.Name).ToList();
            if (BlockDoorPreviewPairing.IsDoorHalfTextureStem(familyStem) ||
                familyStem.EndsWith("_door", StringComparison.Ordinal))
            {
                return TryPickDoorPairVariantModels(variants, keys, modelNotations);
            }

            if (!BlockPreviewBlockstateDefaults.TryPickPreferredVariantKey(keys, familyStem, textureStem, out var bestKey) ||
                !TryGetVariantValue(variants, bestKey, out var vObj))
            {
                return false;
            }

            return TryAppendModelNotation(vObj, modelNotations);
        }

        if (root.TryGetProperty("multipart", out var mp) && mp.ValueKind == JsonValueKind.Array)
        {
            var collectAll = BlockPreviewBlockstateDefaults.ShouldCollectAllMultipartApplies(familyStem);
            foreach (var part in mp.EnumerateArray())
            {
                if (part.TryGetProperty("when", out _))
                {
                    if (!collectAll)
                    {
                        continue;
                    }
                }

                if (!part.TryGetProperty("apply", out var apply))
                {
                    continue;
                }

                if (apply.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in apply.EnumerateArray())
                    {
                        TryAppendModelNotation(entry, modelNotations);
                    }
                }
                else
                {
                    TryAppendModelNotation(apply, modelNotations);
                }
            }

            return modelNotations.Count > 0;
        }

        return false;
    }

    private static bool TryPickDoorPairVariantModels(
        JsonElement variants,
        IReadOnlyList<string> variantKeys,
        List<string> modelNotations)
    {
        string? lowerKey = null;
        string? upperKey = null;
        foreach (var key in variantKeys)
        {
            if (key.Contains("half=lower", StringComparison.OrdinalIgnoreCase) &&
                !key.Contains("half=upper", StringComparison.OrdinalIgnoreCase))
            {
                lowerKey ??= key;
            }

            if (key.Contains("half=upper", StringComparison.OrdinalIgnoreCase) &&
                !key.Contains("half=lower", StringComparison.OrdinalIgnoreCase))
            {
                upperKey ??= key;
            }
        }

        lowerKey ??= variantKeys.FirstOrDefault(k =>
            k.Contains("half=lower", StringComparison.OrdinalIgnoreCase));
        upperKey ??= variantKeys.FirstOrDefault(k =>
            k.Contains("half=upper", StringComparison.OrdinalIgnoreCase));

        var picked = false;
        if (!string.IsNullOrEmpty(lowerKey) &&
            TryGetVariantValue(variants, lowerKey, out var lowerObj) &&
            TryAppendModelNotation(lowerObj, modelNotations))
        {
            picked = true;
        }

        if (!string.IsNullOrEmpty(upperKey) &&
            TryGetVariantValue(variants, upperKey, out var upperObj) &&
            TryAppendModelNotation(upperObj, modelNotations))
        {
            picked = true;
        }

        return picked;
    }

    private static bool TryGetVariantValue(JsonElement variants, string? key, out JsonElement value)
    {
        value = default;
        if (key is null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(key) && variants.TryGetProperty(key, out value))
        {
            return true;
        }

        foreach (var prop in variants.EnumerateObject())
        {
            if (string.Equals(prop.Name, key, StringComparison.Ordinal))
            {
                value = prop.Value;
                return true;
            }
        }

        return false;
    }

    private static bool TryAppendModelNotation(JsonElement vObj, List<string> modelNotations)
    {
        if (!TryGetModelProperty(vObj, out var modelNotation) || string.IsNullOrEmpty(modelNotation))
        {
            return false;
        }

        if (!modelNotations.Contains(modelNotation, StringComparer.OrdinalIgnoreCase))
        {
            modelNotations.Add(modelNotation);
        }

        return true;
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
