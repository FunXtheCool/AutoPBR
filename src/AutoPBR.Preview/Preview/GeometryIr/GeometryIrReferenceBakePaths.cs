using System.Text.Json;

namespace AutoPBR.Preview.GeometryIr;

/// <summary>
/// Locates optional <c>reference_java</c> geometry bakes for preview-time parity repair.
/// </summary>
internal static class GeometryIrReferenceBakePaths
{
    private static readonly string[] ShippedVersionLabels = ["26.1.2", "1.21.11"];

    public static IEnumerable<string> Enumerate(string officialJvmName)
    {
        var fileName = $"{officialJvmName}.json";
        foreach (var label in ShippedVersionLabels)
        {
            yield return Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "minecraft-native",
                "reference-geometry",
                label,
                fileName);
        }

        var envRoot = Environment.GetEnvironmentVariable("AUTOPBR_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
        {
            yield return Path.Combine(envRoot, "tools", "MinecraftGeometryReference", "reference-output", fileName);
        }

        var dir = AppContext.BaseDirectory;
        for (var depth = 0; depth < 8 && !string.IsNullOrEmpty(dir); depth++)
        {
            yield return Path.Combine(dir, "tools", "MinecraftGeometryReference", "reference-output", fileName);
            dir = Directory.GetParent(dir)?.FullName;
        }
    }

    public static bool TryLoadReferenceRoot(string officialJvmName, out JsonElement root)
    {
        root = default;
        foreach (var path in Enumerate(officialJvmName))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("extractionStatus", out var statusEl) &&
                string.Equals(statusEl.GetString(), "reference_java", StringComparison.Ordinal))
            {
                root = doc.RootElement.Clone();
                return true;
            }
        }

        return false;
    }
}
