using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class BlockModelTextureResolveTests
{
    [Fact]
    public void TryResolveTextureZipPath_follows_alias_chain_for_log_column_faces()
    {
        var textures = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["end"] = "minecraft:block/acacia_log_top",
            ["side"] = "minecraft:block/acacia_log",
            ["down"] = "#end",
            ["up"] = "#end",
            ["north"] = "#side",
        };

        Assert.True(MinecraftModelBaker.TryResolveTextureZipPath(
            "#down",
            textures,
            "minecraft",
            out var topZip));
        Assert.Equal("assets/minecraft/textures/block/acacia_log_top.png", topZip);

        Assert.True(MinecraftModelBaker.TryResolveTextureZipPath(
            "#north",
            textures,
            "minecraft",
            out var sideZip));
        Assert.Equal("assets/minecraft/textures/block/acacia_log.png", sideZip);
    }

    [Fact]
    public void CollectOrderedTextureZipPaths_resolves_log_column_sibling_textures()
    {
        var merged = VanillaBlockCubeBuilder.Build(
            VanillaBlockCubeBuilder.BuildFaceTextureKeys(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["north"] = "x",
                ["south"] = "x",
                ["west"] = "x",
                ["east"] = "x",
                ["up"] = "x",
                ["down"] = "x",
            }),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["north"] = "#side",
                ["south"] = "#side",
                ["west"] = "#side",
                ["east"] = "#side",
                ["up"] = "#end",
                ["down"] = "#end",
                ["end"] = "minecraft:block/acacia_log_top",
                ["side"] = "minecraft:block/acacia_log",
            });

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        Assert.Equal(2, ordered.Count);
        Assert.Contains("assets/minecraft/textures/block/acacia_log.png", ordered);
        Assert.Contains("assets/minecraft/textures/block/acacia_log_top.png", ordered);
    }
}
