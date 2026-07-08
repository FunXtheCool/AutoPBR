using System.Text.Json;
using AutoPBR.Preview;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class PilotUvAtlasLiftTests
{
    public static TheoryData<string> UvAtlasPilotJvms { get; } = new()
    {
        "net.minecraft.client.model.animal.allay.AllayModel",
        "net.minecraft.client.model.animal.axolotl.BabyAxolotlModel",
        "net.minecraft.client.model.animal.camel.AdultCamelModel",
        "net.minecraft.client.model.animal.camel.CamelSaddleModel",
        "net.minecraft.client.model.animal.bee.BeeModel",
        "net.minecraft.client.model.animal.bee.BabyBeeModel",
        "net.minecraft.client.model.animal.bee.AdultBeeModel",
        "net.minecraft.client.model.animal.wolf.BabyWolfModel",
        "net.minecraft.client.model.monster.breeze.BreezeModel",
    };

    [Theory]
    [MemberData(nameof(UvAtlasPilotJvms))]
    public void Lifted_mesh_passes_uv_within_atlas_quality(string jvmName)
    {
        var jar = ResolveClientJar();
        var factoryMethod = jvmName.Contains("CamelSaddle", StringComparison.Ordinal)
            ? "createSaddleLayer"
            : "createBodyLayer";
        Assert.True(
            GeometryLiftPipeline.TryLiftWithJavapFallback(GeometryJavapLocator.FindJavap(), jar, null, jvmName,
                factoryMethod, preferAsm: true, out var attempt),
            string.Join("; ", attempt.Notes));

        var tw = 0;
        var th = 0;
        if (!string.IsNullOrEmpty(attempt.MeshConcat))
        {
            _ = LayerDefinitionRetainAtlasStamp.TryReadPrimaryRetainFactoryAtlas(attempt.MeshConcat, out tw, out th) ||
                LayerDefinitionAtlasSizeProbe.TryReadPrimaryIsland(attempt.MeshConcat, out tw, out th) ||
                LayerDefinitionAtlasSizeProbe.TryRead(attempt.MeshConcat, out tw, out th);
        }

        Assert.True(tw > 0 && th > 0, $"{jvmName}: could not resolve LayerDefinition atlas from mesh concat");

        var shardJson = new
        {
            textureWidth = tw,
            textureHeight = th,
            roots = attempt.Roots
        };
        var json = JsonSerializer.Serialize(shardJson);
        using var doc = JsonDocument.Parse(json);
        var uv = GeometryIrUvAtlasQuality.Evaluate(doc.RootElement);
        Assert.True(uv.UvWithinAtlasMatch, $"{jvmName}: {uv.Message}");
        Assert.True(uv.LayerAtlasConsistent, $"{jvmName}: {uv.LayerAtlasMessage}");
    }

    private static string ResolveClientJar()
    {
        var root = Program.FindRepoRoot();
        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        Assert.True(File.Exists(jar), $"Missing client.jar at {jar}");
        return jar;
    }
}
