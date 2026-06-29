using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class BlockVanillaCustomGeometryTests
{
    [Fact]
    public void Cactus_side_from_fixture_resolves_three_element_pack_model()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string texturePath = "assets/minecraft/textures/block/cactus_side.png";
        Assert.True(JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
            source,
            texturePath,
            out var paths,
            out _));
        Assert.Contains(paths, p => p.Contains("cactus.json", StringComparison.OrdinalIgnoreCase));
        Assert.True(MinecraftModelMerger.TryMergeMany(source, paths, out var merged));
        Assert.Equal(3, merged.Elements.Count);
    }

    [Fact]
    public void Cake_side_from_fixture_resolves_single_element_slab()
    {
        var source = VanillaJsonFixture.OpenSource();
        const string texturePath = "assets/minecraft/textures/block/cake_side.png";
        Assert.True(JavaModelPathResolver.TryResolveModelJsonPathsFromTexture(
            source,
            texturePath,
            out var paths,
            out _));
        Assert.Contains(paths, p => p.Contains("cake.json", StringComparison.OrdinalIgnoreCase));
        Assert.True(MinecraftModelMerger.TryMergeMany(source, paths, out var merged));
        Assert.Single(merged.Elements);
        Assert.Equal(8f, merged.Elements[0].To[1]);
    }
}
