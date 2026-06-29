using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class CactusCakePackJsonPreviewTests
{
    private static readonly string ClientJar =
        @"C:\Users\John_Phoenix\AppData\Roaming\PrismLauncher\libraries\com\mojang\minecraft\26.1.2\minecraft-26.1.2-client.jar";

    [Fact]
    public void Blockstate_empty_variant_key_resolves_model()
    {
        const string json = """
            {
              "variants": {
                "": { "model": "minecraft:block/cactus" }
              }
            }
            """;
        Assert.True(JavaModelPathResolver.TryPickModelPathsFromBlockstate(json, "cactus", null, out var paths));
        Assert.Single(paths);
        Assert.Equal("minecraft:block/cactus", paths[0]);
    }

    [Fact]
    public void Cactus_side_resolves_pack_json_with_three_elements_when_jar_present()
    {
        if (!File.Exists(ClientJar))
        {
            return;
        }

        using var zip = System.IO.Compression.ZipFile.OpenRead(ClientJar);
        var source = new ZipAssetSource(zip);
        const string texturePath = "assets/minecraft/textures/block/cactus_side.png";
        Assert.True(JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
            source,
            texturePath,
            out var paths,
            out var ns));
        Assert.Contains(paths, p => p.Contains("cactus.json", StringComparison.OrdinalIgnoreCase));
        Assert.True(MinecraftModelMerger.TryMergeMany(source, paths, out var merged));
        Assert.Equal(3, merged.Elements.Count);
        Assert.True(MinecraftModelBaker.TryResolveTextureZipPath(
            "#side",
            merged.Textures,
            ns,
            out var sideTex));
        Assert.Contains("cactus_side", sideTex, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cake_side_resolves_bites_zero_model_when_jar_present()
    {
        if (!File.Exists(ClientJar))
        {
            return;
        }

        using var zip = System.IO.Compression.ZipFile.OpenRead(ClientJar);
        var source = new ZipAssetSource(zip);
        const string texturePath = "assets/minecraft/textures/block/cake_side.png";
        Assert.True(JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
            source,
            texturePath,
            out var paths,
            out var ns));
        Assert.Contains(paths, p => p.Contains("cake.json", StringComparison.OrdinalIgnoreCase));
        Assert.True(MinecraftModelMerger.TryMergeMany(source, paths, out var merged));
        Assert.Single(merged.Elements);
        Assert.Equal(8f, merged.Elements[0].To[1]);
    }

    [Fact]
    public void Cake_inner_resolves_bites_one_slice_model_from_fixture()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string texturePath = "assets/minecraft/textures/block/cake_inner.png";
        Assert.True(JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
            source,
            texturePath,
            out var paths,
            out var ns));
        Assert.Contains(paths, p => p.Contains("cake_slice1", StringComparison.OrdinalIgnoreCase));
        Assert.True(MinecraftModelMerger.TryMergeMany(source, paths, out var merged));
        Assert.Single(merged.Elements);
        Assert.Equal(3f, merged.Elements[0].From[0]);
        Assert.True(MinecraftModelBaker.TryResolveTextureZipPath(
            "#inside",
            merged.Textures,
            ns,
            out var insideTex));
        Assert.Contains("cake_inner", insideTex, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cake_inner_parity_synthesis_uses_slice_geometry()
    {
        const string path = "assets/minecraft/textures/block/cake_inner.png";
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(
            path,
            out var merged,
            out _,
            out var ordered,
            out _));
        Assert.Single(merged.Elements);
        Assert.Equal(3f, merged.Elements[0].From[0]);
        Assert.Contains("cake_inner", merged.Textures["inside"], StringComparison.OrdinalIgnoreCase);
        Assert.True(ordered.Count >= 4);
    }
}
