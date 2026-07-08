using System.Text.Json;


namespace AutoPBR.Preview.GeometryIr;

/// <summary>
/// Loads packaged preview interpretation overlays from
/// <c>Data/minecraft-native/preview-deltas/&lt;versionLabel&gt;/</c>.
/// </summary>
internal static class GeometryIrPreviewDeltaDocument
{
    public static bool TryLoad(
        MinecraftNativeProfile? profile,
        string officialJvmName,
        out JsonElement root)
    {
        root = default;
        foreach (var ver in NativeIrVersionLabels.ForProfile(profile))
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "minecraft-native",
                "preview-deltas",
                ver,
                $"{officialJvmName}.json");
            if (!File.Exists(path))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            root = doc.RootElement.Clone();
            return true;
        }

        return false;
    }

    public static bool HasDeltaKind(JsonElement root, string kind) =>
        root.TryGetProperty("deltas", out var deltas) &&
        deltas.ValueKind == JsonValueKind.Array &&
        deltas.EnumerateArray().Any(d =>
            d.TryGetProperty("kind", out var k) &&
            string.Equals(k.GetString(), kind, StringComparison.Ordinal));

}
