using System.Text.Json;

using Json.Schema;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>Phase 8: 1.21.11 geometry index manifest aligns with the model class batch list and on-disk shards.</summary>
public sealed class Phase8GeometryIndexHygieneTests
{
    private static readonly JsonSchema GeometryIndexSchema = JsonSchema.FromText(
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "docs",
                "generated",
                "schema",
                "geometry-index.schema.json")));

    [Fact]
    public void Geometry_index_1_21_11_matches_model_class_list_and_shard_files()
    {
        var repo = GeometryIrRepoPaths.FindRepoRoot();
        var indexPath = Path.Combine(GeometryIrRepoPaths.GeneratedRoot(repo), "geometry-index-1.21.11.json");
        Assert.True(File.Exists(indexPath));

        using var indexDoc = JsonDocument.Parse(File.ReadAllText(indexPath));
        var indexResult = GeometryIndexSchema.Evaluate(
            indexDoc.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.True(indexResult.IsValid, indexResult.ToString());

        Assert.Equal("1.21.11", indexDoc.RootElement.GetProperty("versionLabel").GetString());
        Assert.Equal("proguard", indexDoc.RootElement.GetProperty("mappingKind").GetString());

        var expected = GeometryIrRepoPaths.LoadFullModelClassList(repo);
        var entries = indexDoc.RootElement.GetProperty("entries");
        Assert.Equal(expected.Count, entries.GetArrayLength());

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray())
        {
            var jvm = entry.GetProperty("officialJvmName").GetString();
            Assert.False(string.IsNullOrEmpty(jvm));
            Assert.True(seen.Add(jvm), $"duplicate index row: {jvm}");

            var rel = entry.GetProperty("shardRelPath").GetString();
            var shardPath = Path.Combine(
                GeometryIrRepoPaths.GeneratedRoot(repo),
                rel!.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(shardPath), $"index points to missing shard: {rel}");

            var status = entry.GetProperty("extractionStatus").GetString();
            Assert.True(
                status is "ok" or "partial" or "heuristic" or "skipped",
                $"unexpected extractionStatus for {jvm}: {status}");
        }

        Assert.Equal(expected.Count, seen.Count);
        foreach (var jvm in expected)
        {
            Assert.Contains(jvm, seen);
        }
    }
}
