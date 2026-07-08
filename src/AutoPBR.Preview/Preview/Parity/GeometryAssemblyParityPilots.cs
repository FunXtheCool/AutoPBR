namespace AutoPBR.Preview.Parity;

/// <summary>
/// Loads assembly-parity pilot JVM names (Phase 0B / Agent 2C entity-wide scope).
/// </summary>
public static class GeometryAssemblyParityPilots
{
    public static IReadOnlySet<string> Load(string repoRoot, string versionLabel)
    {
        var path = Path.Combine(repoRoot, "docs", "generated", $"geometry-assembly-parity-pilots-{versionLabel}.txt");
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#'))
            {
                continue;
            }

            set.Add(t);
        }

        return set;
    }
}
