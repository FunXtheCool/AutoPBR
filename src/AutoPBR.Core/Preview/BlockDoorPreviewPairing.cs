namespace AutoPBR.Core.Preview;

internal static class BlockDoorPreviewPairing
{
    internal static bool IsDoorHalfTextureStem(string textureStem)
    {
        var lower = textureStem.ToLowerInvariant();
        return lower.Contains("_door_", StringComparison.Ordinal) &&
               (lower.EndsWith("_bottom", StringComparison.Ordinal) ||
                lower.EndsWith("_top", StringComparison.Ordinal) ||
                lower.EndsWith("_lower", StringComparison.Ordinal) ||
                lower.EndsWith("_upper", StringComparison.Ordinal));
    }

    internal static string ResolveDoorBlockstateStem(string textureStem) =>
        JavaModelPathResolver.BlockstateStem(textureStem);

    internal static void TryAppendDoorPairModelPaths(
        IAssetSource source,
        string textureStem,
        string defaultNamespace,
        List<string> modelJsonPaths)
    {
        if (!IsDoorHalfTextureStem(textureStem))
        {
            return;
        }

        var doorStem = ResolveDoorBlockstateStem(textureStem);
        foreach (var half in new[] { "bottom", "top" })
        {
            var modelPath = $"assets/{defaultNamespace}/models/block/{doorStem}_{half}.json";
            if (!source.Exists(modelPath))
            {
                continue;
            }

            if (!modelJsonPaths.Contains(modelPath, StringComparer.OrdinalIgnoreCase))
            {
                modelJsonPaths.Add(modelPath);
            }
        }

        modelJsonPaths.Sort(static (a, b) =>
        {
            var aIsBottom = a.Contains("_bottom", StringComparison.OrdinalIgnoreCase);
            var bIsBottom = b.Contains("_bottom", StringComparison.OrdinalIgnoreCase);
            if (aIsBottom == bIsBottom)
            {
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }

            return aIsBottom ? -1 : 1;
        });
    }

    /// <summary>
    /// Vanilla door blockstate models (<c>door_*_left</c>) are each authored for a separate block space at full height.
    /// Rebake into stacked full-height panels with correct per-half textures (lower Y 0–16, upper Y 16–32).
    /// </summary>
    internal static bool TryNormalizeMergedDoorToPreviewPair(
        string textureArchivePath,
        string textureNamespace,
        ref MergedJavaBlockModel merged)
    {
        var stem = Path.GetFileNameWithoutExtension(textureArchivePath.Replace('\\', '/').Split('/')[^1]);
        if (!IsDoorHalfTextureStem(stem))
        {
            return false;
        }

        var doorStem = ResolveDoorBlockstateStem(stem);
        var pairTextures = new Dictionary<string, string>(StringComparer.Ordinal);
        if (TryResolveDoorTextureRef(merged.Textures, "bottom", doorStem, textureNamespace, out var bottomRef))
        {
            pairTextures["bottom"] = bottomRef;
        }
        else
        {
            pairTextures["bottom"] = $"{textureNamespace}:block/{doorStem}_bottom";
        }

        if (TryResolveDoorTextureRef(merged.Textures, "top", doorStem, textureNamespace, out var topRef))
        {
            pairTextures["top"] = topRef;
        }
        else
        {
            pairTextures["top"] = $"{textureNamespace}:block/{doorStem}_top";
        }

        merged = VanillaBlockDoorHalfBuilder.BuildPair(pairTextures);
        return true;
    }

    private static bool TryResolveDoorTextureRef(
        Dictionary<string, string> textures,
        string halfKey,
        string doorStem,
        string textureNamespace,
        out string textureRef)
    {
        textureRef = string.Empty;
        if (textures.TryGetValue(halfKey, out var direct) && !string.IsNullOrWhiteSpace(direct) && !direct.StartsWith('#'))
        {
            textureRef = direct;
            return true;
        }

        foreach (var (_, value) in textures)
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('#'))
            {
                continue;
            }

            if (value.Contains($"{doorStem}_{halfKey}", StringComparison.OrdinalIgnoreCase))
            {
                textureRef = value;
                return true;
            }
        }

        foreach (var face in mergedFaceTextureKeys(textures))
        {
            if (face.Contains($"{doorStem}_{halfKey}", StringComparison.OrdinalIgnoreCase))
            {
                textureRef = face;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> mergedFaceTextureKeys(Dictionary<string, string> textures) =>
        textures.Values.Where(v => !string.IsNullOrWhiteSpace(v) && !v.StartsWith('#'));
}
