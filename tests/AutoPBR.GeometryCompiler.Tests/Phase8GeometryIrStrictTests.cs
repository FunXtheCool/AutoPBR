using System.Text.Json;
using System.Text.Json.Nodes;


using Json.Schema;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>Phase 8: committed 1.21.11 geometry IR strict-ok pilot shards — schema and structural lift completeness.</summary>
public sealed class Phase8GeometryIrStrictTests
{
    private static readonly JsonSchema GeometryIrSchema = JsonSchema.FromText(
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "docs",
                "generated",
                "schema",
                "geometry-ir.schema.json")));

    public static IEnumerable<object[]> Phase8Cases()
    {
        var repo = GeometryIrRepoPaths.FindRepoRoot();
        foreach (var jvm in GeometryIrRepoPaths.LoadPhase8StrictOkClassList(repo))
        {
            yield return [jvm];
        }
    }

    [Theory]
    [MemberData(nameof(Phase8Cases))]
    public void Committed_shard_passes_geometry_ir_schema_and_ok_status(string officialJvmName)
    {
        var repo = GeometryIrRepoPaths.FindRepoRoot();
        var path = GeometryIrRepoPaths.GeometryShardPath(
            repo,
            GeometryIrRepoPaths.VersionLabel12111,
            officialJvmName);
        Assert.True(File.Exists(path), $"missing shard: {path}");

        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (node["schemaVersion"]?.GetValue<int>() != 2 && node.ContainsKey("roots"))
        {
            GeometryIrV2Migration.ApplyToShard(node);
        }

        using var doc = JsonDocument.Parse(node.ToJsonString());
        var result = GeometryIrSchema.Evaluate(
            doc.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.True(result.IsValid, $"{officialJvmName}: {result}");

        Assert.Equal("ok", (string?)node["extractionStatus"]);
        Assert.NotNull(node["roots"]);
        Assert.NotEmpty(node["roots"]!.AsArray());

        var validation = GeometryIrStructuralValidator.ValidateShard(
            node,
            officialJvmName,
            new GeometryIrStructuralValidator.Options(Strict: true, RequireOkStatus: true));
        Assert.True(validation.IsValid, string.Join("; ", validation.Issues.Select(i => $"{i.Code}: {i.Message}")));
    }

    [Theory]
    [MemberData(nameof(Phase8Cases))]
    public void Geometry_index_row_is_ok_for_strict_pilot(string officialJvmName)
    {
        var repo = GeometryIrRepoPaths.FindRepoRoot();
        var indexPath = Path.Combine(GeometryIrRepoPaths.GeneratedRoot(repo), "geometry-index-1.21.11.json");
        using var indexDoc = JsonDocument.Parse(File.ReadAllText(indexPath));
        var entry = indexDoc.RootElement.GetProperty("entries").EnumerateArray()
            .FirstOrDefault(e => string.Equals(
                e.GetProperty("officialJvmName").GetString(),
                officialJvmName,
                StringComparison.Ordinal));
        Assert.True(entry.ValueKind != JsonValueKind.Undefined, $"index missing row: {officialJvmName}");
        Assert.Equal("ok", entry.GetProperty("extractionStatus").GetString());
        Assert.Equal("proguard_1.21.11", entry.GetProperty("profile").GetString());
    }
}
