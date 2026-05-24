using System.Text.Json;
using System.Text.Json.Nodes;

using AutoPBR.Tools.AnimationCompiler;

using Json.Schema;

namespace AutoPBR.AnimationCompiler.Tests;

public sealed class AnimationJsonSchemaTests
{
    private static string ContentPath(string relativeUnderOutput) =>
        Path.Combine(AppContext.BaseDirectory, relativeUnderOutput);

    private static JsonSchema LoadSchema(string relative) =>
        JsonSchema.FromText(File.ReadAllText(ContentPath(relative)));

    private static void AssertValid(JsonSchema schema, JsonNode doc)
    {
        using var jdoc = JsonDocument.Parse(doc.ToJsonString());
        var result = schema.Evaluate(jdoc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        Assert.True(result.IsValid, $"Schema validation failed: {result}");
    }

    [Fact]
    public void Lifted_Armadillo_shard_shape_matches_animation_ir_schema()
    {
        var javapPath = ContentPath(
            Path.Combine(
                "docs",
                "generated",
                "minecraft-client-model-index-26.1.2-animation-init",
                "net_minecraft_client_animation_definitions_ArmadilloAnimation.javapc.txt"));
        var javap = File.ReadAllText(javapPath);
        Assert.True(AnimationClinitLift.TryLift(javap, out var definitions, out _), "TryLift failed");

        var shard = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["versionLabel"] = "26.1.2",
            ["officialJvmName"] = "net.minecraft.client.animation.definitions.ArmadilloAnimation",
            ["profile"] = "named_jar_26.1.2",
            ["jarPath"] = "net/minecraft/client/animation/definitions/ArmadilloAnimation.class",
            ["classSha256Hex"] = new string('a', 64),
            ["extractionStatus"] = "ok",
            ["definitions"] = definitions
        };

        var schema = LoadSchema(Path.Combine("docs", "generated", "schema", "animation-ir.schema.json"));
        AssertValid(schema, shard);
    }
}
