using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class LayerDefinitionAtlasSizeProbeTests
{
    private const string IslandMarker = JavapClassDisassembly.GeometryMeshIslandBoundaryMarker;

    [Fact]
    public void TryRead_returns_first_layer_definition_create_pair_javap_style()
    {
        var text = """
            bipush 32
            1: bipush 32
            2: invokestatic #1 // Method foo.create:(Lbar;)Lbaz/LayerDefinition;
            bipush 128
            3: bipush 128
            4: invokestatic #2 // Method foo.create:(Lbar;)Lbaz/LayerDefinition;
            """;

        Assert.True(LayerDefinitionAtlasSizeProbe.TryRead(text, out var w, out var h));
        Assert.Equal(32, w);
        Assert.Equal(32, h);
    }

    [Fact]
    public void TryRead_returns_first_layer_definition_create_pair_asm_style()
    {
        var text = """
             4: bipush 32
             6: bipush 32
             8: invokestatic #1 // Method foo.create:(Lbar;)Lbaz/LayerDefinition;
             9: bipush 128
            11: bipush 128
            13: invokestatic #2 // Method foo.create:(Lbar;)Lbaz/LayerDefinition;
            """;

        Assert.True(LayerDefinitionAtlasSizeProbe.TryRead(text, out var w, out var h));
        Assert.Equal(32, w);
        Assert.Equal(32, h);
    }

    [Fact]
    public void TryReadPrimaryIsland_ignores_later_islands()
    {
        var text = $"""
             4: bipush 32
             6: bipush 32
             8: invokestatic #1 // Method foo.create:(Lbar;)Lbaz/LayerDefinition;
            {IslandMarker}
             9: bipush 128
            11: bipush 128
            13: invokestatic #2 // Method foo.create:(Lbar;)Lbaz/LayerDefinition;
            """;

        Assert.True(LayerDefinitionAtlasSizeProbe.TryReadPrimaryIsland(text, out var w, out var h));
        Assert.Equal(32, w);
        Assert.Equal(32, h);
    }

    [Fact]
    public void TryRead_on_island_slice_reads_supplementary_atlas()
    {
        var text = $"""
            {IslandMarker}
             9: bipush 128
            11: bipush 128
            13: invokestatic #2 // Method foo.create:(Lbar;)Lbaz/LayerDefinition;
            """;

        Assert.True(LayerDefinitionAtlasSizeProbe.TryRead(text, out var w, out var h));
        Assert.Equal(128, w);
        Assert.Equal(128, h);
    }
}
