using System.Text.Json;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class GeometryIrParityAtlasTests
{
    [Theory]
    [InlineData("Horse", 64, 64)]
    [InlineData("Wolf", 64, 32)]
    [InlineData("HumanoidVillager", 64, 64)]
    [InlineData("PlayerWide", 64, 64)]
    [InlineData("Blaze", 64, 32)]
    public void Parity_atlas_defaults_match_cleanroom_builder_sizes(string builder, int w, int h)
    {
        Assert.True(GeometryIrParityAtlasDefaults.TryGetForBuilderMethod(builder, out var aw, out var ah));
        Assert.Equal(w, aw);
        Assert.Equal(h, ah);
    }

    [Fact]
    public void Chicken_geometry_shard_documents_texture_dims_matching_manifest_row()
    {
        var shardPath = Path.Combine(
            GeometryIrTestTierSupport.FindRepoRoot(),
            "docs",
            "generated",
            "geometry",
            "26.1.2",
            "net.minecraft.client.model.animal.chicken.ChickenModel.json");
        Assert.True(File.Exists(shardPath), $"Missing {shardPath}");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        Assert.Equal(64, shard.RootElement.GetProperty("textureWidth").GetInt32());
        Assert.Equal(32, shard.RootElement.GetProperty("textureHeight").GetInt32());

        var rule = EntityTextureParityCatalog.ResolveRule(
            "assets/minecraft/textures/entity/chicken/chicken_temperate.png",
            "chicken_temperate");
        Assert.NotNull(rule);
        Assert.Equal(64, rule.GeometryIrTextureWidth.GetValueOrDefault());
        Assert.Equal(32, rule.GeometryIrTextureHeight.GetValueOrDefault());
    }
}
