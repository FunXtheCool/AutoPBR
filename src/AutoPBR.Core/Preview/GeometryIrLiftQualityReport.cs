using System.Text.Json;

namespace AutoPBR.Core.Preview;

/// <summary>Metrics for geometry IR shards (lift quality baseline / regression).</summary>
public static partial class GeometryIrLiftQualityReport
{
    public sealed record Entry(
        string OfficialJvmName,
        string ExtractionStatus,
        int CuboidCount,
        int MaxTreeDepth,
        int RootChildCount,
        int SuspectedFlatNestedPartCount,
        IReadOnlyDictionary<string, int> LiftWarningCounts,
        bool? ReferenceCuboidsMatch,
        string? ReferenceCompareMessage,
        bool? ReferencePosesMatch,
        string? ReferencePoseCompareMessage,
        bool? ReferenceMeshMatch,
        string? ReferenceMeshCompareMessage,
        bool? ReferenceWorldPoseMatch,
        string? ReferenceWorldPoseCompareMessage,
        bool ReferenceHierarchyMatch,
        string? ReferenceHierarchyMessage,
        bool ExtractionBindingGap,
        bool AssemblyGatePass,
        bool? JavapPoseOracleMatch,
        string? JavapPoseOracleMessage)
    {
        public bool AllReferenceLocalsMatch =>
            ReferenceCuboidsMatch == true &&
            ReferencePosesMatch == true &&
            ReferenceMeshMatch == true;
    }

    /// <summary>Weighted score for keep/revert after pilot re-lift (4A/4B policy).</summary>
    public static int ComputeLiftDecisionScore(Entry entry)
    {
        var score = 0;
        if (entry.AssemblyGatePass)
        {
            score += 1000;
        }

        if (entry.ReferenceWorldPoseMatch == true)
        {
            score += 100;
        }

        if (entry.JavapPoseOracleMatch == true)
        {
            score += 100;
        }

        if (entry.ReferenceHierarchyMatch)
        {
            score += 50;
        }

        if (entry.ReferenceCuboidsMatch == true)
        {
            score += 50;
        }

        if (entry.ExtractionBindingGap)
        {
            score -= 200;
        }

        score -= Math.Min(40, entry.SuspectedFlatNestedPartCount * 5);
        return score;
    }

    public sealed record Document(
        string VersionLabel,
        DateTime GeneratedUtc,
        int OkEntryCount,
        IReadOnlyList<Entry> Entries,
        IReadOnlyList<string> PrioritizedBacklogJvmNames);

    private static readonly HashSet<(string ParentId, string ChildId)> KnownNestedPairs =
    [
        ("head", "beak"),
        ("head", "red_thing"),
        ("head", "hat"),
        ("head", "nose"),
        ("head", "mole"),
        ("head", "top_gills"),
        ("head", "left_gills"),
        ("head", "right_gills"),
        ("body", "rods"),
        ("tail1", "tail2"),
        ("body", "left_front_leg"),
        ("body", "right_front_leg"),
        ("body", "left_hind_leg"),
        ("body", "right_hind_leg"),
    ];

    private static readonly string[] BodyLegPartIds =
    [
        "left_front_leg",
        "right_front_leg",
        "left_hind_leg",
        "right_hind_leg",
    ];

    public static Document BuildForIndex(
        string repoRoot,
        string versionLabel,
        JsonElement indexRoot,
        IReadOnlySet<string>? referenceCompareJvmNames = null,
        GeometryJavapPoseOracle.Context? javapPoseOracle = null)
    {
        javapPoseOracle ??= GeometryJavapPoseOracle.Context.TryCreate(repoRoot, versionLabel);
        var entries = new List<Entry>();
        if (!indexRoot.TryGetProperty("entries", out var indexEntries) ||
            indexEntries.ValueKind != JsonValueKind.Array)
        {
            return new Document(versionLabel, DateTime.UtcNow, 0, entries, []);
        }

        foreach (var row in indexEntries.EnumerateArray())
        {
            var jvm = row.TryGetProperty("officialJvmName", out var jvmEl) ? jvmEl.GetString() : null;
            if (string.IsNullOrEmpty(jvm))
            {
                continue;
            }

            var status = row.TryGetProperty("extractionStatus", out var stEl) ? stEl.GetString() ?? "" : "";
            if (!string.Equals(status, "ok", StringComparison.Ordinal))
            {
                continue;
            }

            var rel = row.TryGetProperty("shardRelPath", out var relEl)
                ? relEl.GetString()?.Replace('/', Path.DirectorySeparatorChar)
                : null;
            var shardPath = string.IsNullOrEmpty(rel)
                ? null
                : Path.Combine(repoRoot, "docs", "generated", rel);
            if (shardPath is null || !File.Exists(shardPath))
            {
                continue;
            }

            using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
            entries.Add(AnalyzeShard(jvm, status, shard.RootElement, repoRoot, referenceCompareJvmNames, javapPoseOracle));
        }

        var backlog = entries
            .Where(e => e.SuspectedFlatNestedPartCount > 0 ||
                        !e.ReferenceHierarchyMatch ||
                        e.ExtractionBindingGap ||
                        !e.AssemblyGatePass ||
                        e.ReferenceCuboidsMatch == false ||
                        e.ReferencePosesMatch == false ||
                        e.ReferenceMeshMatch == false ||
                        e.ReferenceWorldPoseMatch == false ||
                        e.JavapPoseOracleMatch == false ||
                        (e.ReferenceCompareMessage?.Contains("count", StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(e => !e.ReferenceHierarchyMatch || e.ExtractionBindingGap ? 0 : 1)
            .ThenByDescending(e => e.SuspectedFlatNestedPartCount)
            .ThenBy(e => e.ReferenceCuboidsMatch == false ? 0 : 1)
            .ThenBy(e => e.ReferenceWorldPoseMatch == false ? 0 : 1)
            .ThenBy(e => e.ReferencePosesMatch == false ? 0 : 1)
            .ThenBy(e => e.ReferenceMeshMatch == false ? 0 : 1)
            .ThenBy(e => e.JavapPoseOracleMatch == false ? 0 : 1)
            .Select(e => e.OfficialJvmName)
            .ToList();

        return new Document(versionLabel, DateTime.UtcNow, entries.Count, entries, backlog);
    }
}
