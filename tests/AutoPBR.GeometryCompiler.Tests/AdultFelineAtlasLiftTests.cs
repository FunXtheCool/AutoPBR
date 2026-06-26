using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class AdultFelineAtlasLiftTests
{
    private const string Jvm = "net.minecraft.client.model.animal.feline.AdultFelineModel";

    [Fact]
    public void Lifted_AdultFelineModel_documents_64x32_atlas_from_LayerDefinitions_registration()
    {
        var repo = Program.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{Jvm}.json");
        Assert.True(File.Exists(shardPath), $"Missing shard at {shardPath}");

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(shardPath));
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("textureWidth", out var tw) && tw.GetInt32() == 64);
        Assert.True(root.TryGetProperty("textureHeight", out var th) && th.GetInt32() == 32);
    }
}
