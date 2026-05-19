using System.Text.Json;
using System.Text.Json.Nodes;


using Json.Schema;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class GeometryJsonSchemaTests
{
    private static string ContentPath(string relativeUnderOutput)
    {
        var repoPath = Path.Combine(Program.FindRepoRoot(), relativeUnderOutput);
        if (File.Exists(repoPath))
        {
            return repoPath;
        }

        return Path.Combine(AppContext.BaseDirectory, relativeUnderOutput);
    }

    private static JsonSchema LoadSchema(string relative) =>
        JsonSchema.FromText(File.ReadAllText(ContentPath(relative)));

    private static void AssertValid(JsonSchema schema, string relativeJson)
    {
        var path = ContentPath(relativeJson);
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (relativeJson.Contains($"{Path.DirectorySeparatorChar}geometry{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase) &&
            node["schemaVersion"]?.GetValue<int>() != 2 &&
            node.ContainsKey("roots"))
        {
            GeometryIrV2Migration.ApplyToShard(node);
        }

        using var doc = JsonDocument.Parse(node.ToJsonString());
        var result = schema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.True(result.IsValid, $"Schema validation failed for {path}: {result}");
    }

    [Fact]
    public void CowModel_geometry_ir_matches_schema()
    {
        var schema = LoadSchema(Path.Combine("docs", "generated", "schema", "geometry-ir.schema.json"));
        var path = Path.Combine("docs", "generated", "geometry", "26.1.2", "net.minecraft.client.model.animal.cow.CowModel.json");
        AssertValid(schema, path);
        AssertStructuralOk(path, strict: true);
    }

    private static void AssertStructuralOk(string relativeJson, bool strict)
    {
        var path = ContentPath(relativeJson);
        if (!File.Exists(path))
        {
            return;
        }

        var result = GeometryIrStructuralValidator.ValidateFile(path,
            new GeometryIrStructuralValidator.Options(Strict: strict, RequireOkStatus: strict));
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(i => $"{i.Code}: {i.Message}")));
    }

    [Fact]
    public void Animal_chicken_model_geometry_ir_matches_schema()
    {
        var schema = LoadSchema(Path.Combine("docs", "generated", "schema", "geometry-ir.schema.json"));
        AssertValid(schema,
            Path.Combine("docs", "generated", "geometry", "26.1.2", "net.minecraft.client.model.animal.chicken.ChickenModel.json"));
        AssertValid(schema,
            Path.Combine("docs", "generated", "geometry", "1.21.11", "net.minecraft.client.model.animal.chicken.ChickenModel.json"));
    }

    /// <summary>T0 schema only — does not require extractionStatus ok (see docs/test-guidance-geometry-animation-ir.md).</summary>
    [Fact]
    public void Promoted_pilot_geometry_shards_match_schema_regardless_of_lift_status()
    {
        var schema = LoadSchema(Path.Combine("docs", "generated", "schema", "geometry-ir.schema.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "geometry", "26.1.2", "net.minecraft.client.model.BlazeModel.json"));
        AssertValid(schema,
            Path.Combine("docs", "generated", "geometry", "1.21.11", "net.minecraft.client.model.animal.cow.CowModel.json"));
    }

    [Fact]
    public void Preview_delta_json_matches_schema()
    {
        var schema = LoadSchema(Path.Combine("docs", "generated", "schema", "preview-delta.schema.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "preview-deltas", "26.1.2", "net.minecraft.client.model.animal.cow.CowModel.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "preview-deltas", "26.1.2", "net.minecraft.client.model.animal.chicken.ChickenModel.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "preview-deltas", "26.1.2", "net.minecraft.client.model.BlazeModel.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "preview-deltas", "26.1.2", "net.minecraft.client.model.animal.fish.CodModel.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "preview-deltas", "26.1.2", "net.minecraft.client.model.animal.fish.SalmonModel.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "preview-deltas", "26.1.2", "net.minecraft.client.model.animal.pig.PigModel.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "preview-deltas", "26.1.2", "net.minecraft.client.model.ambient.BatModel.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "preview-deltas", "26.1.2", "net.minecraft.client.model.monster.creeper.CreeperModel.json"));
    }

    [Fact]
    public void Geometry_index_matches_schema()
    {
        var schema = LoadSchema(Path.Combine("docs", "generated", "schema", "geometry-index.schema.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "geometry-index-26.1.2.json"));
        AssertValid(schema, Path.Combine("docs", "generated", "geometry-index-1.21.11.json"));
    }
}
