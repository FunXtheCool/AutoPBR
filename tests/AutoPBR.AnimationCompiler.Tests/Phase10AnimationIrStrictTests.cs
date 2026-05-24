using System.Text.Json;
using System.Text.Json.Nodes;

using AutoPBR.Tools.AnimationCompiler;

using Json.Schema;

namespace AutoPBR.AnimationCompiler.Tests;

/// <summary>Phase 10: committed 1.21.11 animation IR shards — schema, index, and lift completeness.</summary>
public sealed class Phase10AnimationIrStrictTests
{
    private static readonly JsonSchema AnimationIrSchema = JsonSchema.FromText(
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "docs",
                "generated",
                "schema",
                "animation-ir.schema.json")));

    private static readonly JsonSchema AnimationIndexSchema = JsonSchema.FromText(
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "docs",
                "generated",
                "schema",
                "animation-index.schema.json")));

    public static IEnumerable<object[]> Phase10Cases()
    {
        var repo = AnimationIrRepoPaths.FindRepoRoot();
        foreach (var jvm in AnimationIrRepoPaths.LoadOfficialJvmNamesFromBatchList(
                     repo,
                     "minecraft_1.21.11_client_animation_definition_classes.txt"))
        {
            yield return [jvm];
        }
    }

    [Theory]
    [MemberData(nameof(Phase10Cases))]
    public void Committed_shard_passes_animation_ir_schema_and_ok_status(string officialJvmName)
    {
        var repo = AnimationIrRepoPaths.FindRepoRoot();
        var path = AnimationIrRepoPaths.AnimationShardPath(
            repo,
            officialJvmName,
            AnimationIrRepoPaths.VersionLabel12111);
        Assert.True(File.Exists(path), $"missing shard: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = AnimationIrSchema.Evaluate(
            doc.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.True(result.IsValid, $"{officialJvmName}: {result}");

        Assert.Equal("ok", doc.RootElement.GetProperty("extractionStatus").GetString());
        Assert.Equal("proguard_1.21.11", doc.RootElement.GetProperty("profile").GetString());

        var definitions = JsonNode.Parse(doc.RootElement.GetProperty("definitions").GetRawText())!.AsArray();
        Assert.NotEmpty(definitions);
        Assert.False(
            AnimationClinitLift.HasIncompleteChannels(definitions),
            $"{officialJvmName}: incomplete channels (empty channel rows or zero keyframes)");
    }

    [Fact]
    public void Animation_index_1_21_11_matches_batch_list_and_shard_files()
    {
        var repo = AnimationIrRepoPaths.FindRepoRoot();
        var indexPath = Path.Combine(
            AnimationIrRepoPaths.GeneratedRoot(repo),
            $"animation-index-{AnimationIrRepoPaths.VersionLabel12111}.json");
        Assert.True(File.Exists(indexPath));

        using var indexDoc = JsonDocument.Parse(File.ReadAllText(indexPath));
        var indexResult = AnimationIndexSchema.Evaluate(
            indexDoc.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.True(indexResult.IsValid, indexResult.ToString());

        Assert.Equal(AnimationIrRepoPaths.VersionLabel12111, indexDoc.RootElement.GetProperty("versionLabel").GetString());
        Assert.Equal("proguard", indexDoc.RootElement.GetProperty("mappingKind").GetString());

        var expected = AnimationIrRepoPaths.LoadOfficialJvmNamesFromBatchList(
            repo,
            "minecraft_1.21.11_client_animation_definition_classes.txt");
        var entries = indexDoc.RootElement.GetProperty("entries");
        Assert.Equal(expected.Count, entries.GetArrayLength());

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray())
        {
            var jvm = entry.GetProperty("officialJvmName").GetString();
            Assert.False(string.IsNullOrEmpty(jvm));
            Assert.True(seen.Add(jvm), $"duplicate index row: {jvm}");
            Assert.Equal("ok", entry.GetProperty("extractionStatus").GetString());
            Assert.Equal("proguard_1.21.11", entry.GetProperty("profile").GetString());

            var rel = entry.GetProperty("shardRelPath").GetString();
            var shardPath = Path.Combine(
                AnimationIrRepoPaths.GeneratedRoot(repo),
                rel!.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(shardPath), $"index points to missing shard: {rel}");
        }

        Assert.Equal(expected.Count, seen.Count);
        foreach (var jvm in expected)
        {
            Assert.Contains(jvm, seen);
        }
    }
}
