namespace AutoPBR.Core.Preview;

internal static class BlockTextureSlotResolver
{
    internal static bool TryResolveSlotZipPaths(
        BlockTextureParityRule rule,
        string selectedTextureArchivePath,
        string defaultNamespace,
        out Dictionary<string, string> slotToZipPath)
    {
        slotToZipPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rule.TextureSlots is null || rule.TextureSlots.Count == 0)
        {
            return false;
        }

        var selectedStem = Path.GetFileNameWithoutExtension(selectedTextureArchivePath).ToLowerInvariant();
        foreach (var (face, slotStem) in rule.TextureSlots)
        {
            if (string.Equals(slotStem, selectedStem, StringComparison.OrdinalIgnoreCase))
            {
                slotToZipPath[face] = NormalizeZipPath(selectedTextureArchivePath);
                continue;
            }

            slotToZipPath[face] = StemToBlockTextureZipPath(defaultNamespace, slotStem);
        }

        return slotToZipPath.Count > 0;
    }

    internal static Dictionary<string, string> BuildTextureDictionary(
        BlockTextureParityRule rule,
        IReadOnlyDictionary<string, string> slotToZipPath,
        string defaultNamespace)
    {
        var textures = new Dictionary<string, string>(StringComparer.Ordinal);
        if (rule.TextureSlots is null)
        {
            return textures;
        }

        foreach (var (face, slotStem) in rule.TextureSlots)
        {
            if (!slotToZipPath.TryGetValue(face, out var zipPath))
            {
                zipPath = StemToBlockTextureZipPath(defaultNamespace, slotStem);
            }

            textures[face] = ZipPathToModelTextureReference(zipPath, defaultNamespace);
        }

        return textures;
    }

    internal static List<string> CollectOrderedDistinctZipPaths(IReadOnlyDictionary<string, string> slotToZipPath)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var face in new[] { "north", "south", "west", "east", "up", "down" })
        {
            if (!slotToZipPath.TryGetValue(face, out var zipPath) || !seen.Add(zipPath))
            {
                continue;
            }

            ordered.Add(zipPath);
        }

        foreach (var zipPath in slotToZipPath.Values)
        {
            if (seen.Add(zipPath))
            {
                ordered.Add(zipPath);
            }
        }

        return ordered;
    }

    internal static string StemToBlockTextureZipPath(string defaultNamespace, string slotStem) =>
        $"assets/{defaultNamespace}/textures/block/{slotStem}.png";

    private static string NormalizeZipPath(string archivePath) =>
        archivePath.Replace('\\', '/').TrimStart('/');

    internal static string ZipPathToModelTextureReference(string zipPath, string defaultNamespace)
    {
        var norm = NormalizeZipPath(zipPath);
        const string prefix = "assets/";
        if (!norm.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return $"block/{Path.GetFileNameWithoutExtension(norm)}";
        }

        var parts = norm.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || !parts[0].Equals("assets", StringComparison.OrdinalIgnoreCase) ||
            !parts[2].Equals("textures", StringComparison.OrdinalIgnoreCase) ||
            !parts[3].Equals("block", StringComparison.OrdinalIgnoreCase))
        {
            return $"block/{Path.GetFileNameWithoutExtension(norm)}";
        }

        var ns = parts[1];
        var rel = string.Join('/', parts.Skip(4));
        if (rel.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            rel = rel[..^4];
        }

        return ns.Equals(defaultNamespace, StringComparison.OrdinalIgnoreCase)
            ? $"block/{rel}"
            : $"{ns}:block/{rel}";
    }
}
