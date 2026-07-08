namespace AutoPBR.Preview;

/// <summary>
/// Maps assembly pilot JVM names to checked-in javap snapshot filenames and factory methods.
/// </summary>
public static class GeometryAssemblyPilotJavapSnapshotIndex
{
    public sealed record PilotSnapshot(string SnapshotFile, string FactoryMethod);

    public static IReadOnlyDictionary<string, string> LoadSnapshotFiles(string repoRoot, string versionLabel) =>
        Load(repoRoot, versionLabel).ToDictionary(kv => kv.Key, kv => kv.Value.SnapshotFile, StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, PilotSnapshot> Load(string repoRoot, string versionLabel)
    {
        var path = Path.Combine(repoRoot, "docs", "generated", $"geometry-assembly-pilot-javap-snapshots-{versionLabel}.csv");
        if (!File.Exists(path))
        {
            return new Dictionary<string, PilotSnapshot>(StringComparer.Ordinal);
        }

        var map = new Dictionary<string, PilotSnapshot>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line.StartsWith("jvm,", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 6)
            {
                continue;
            }

            var jvm = parts[0].Trim();
            var factoryMethod = parts[1].Trim();
            var snapshotFile = parts[5].Trim();
            if (jvm.Length > 0 && snapshotFile.Length > 0)
            {
                map[jvm] = new PilotSnapshot(snapshotFile, factoryMethod);
            }
        }

        return map;
    }
}
