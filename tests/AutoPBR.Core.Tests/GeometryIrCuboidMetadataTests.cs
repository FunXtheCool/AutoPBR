using System.Text.Json;


namespace AutoPBR.Core.Tests;

public sealed class GeometryIrCuboidMetadataTests
{
    [Fact]
    public void GetMirrorCuboidUv_true_when_mirrorU_true()
    {
        using var doc = JsonDocument.Parse("""{"from":[0,0,0],"to":[1,1,1],"uvOrigin":[0,0],"mirrorU":true}""");
        Assert.True(GeometryIrCuboidMetadata.GetMirrorCuboidUv(doc.RootElement));
    }

    [Fact]
    public void GetMirrorCuboidUv_false_when_mirrorU_absent_or_false()
    {
        using var doc = JsonDocument.Parse("""{"from":[0,0,0],"to":[1,1,1],"uvOrigin":[0,0]}""");
        Assert.False(GeometryIrCuboidMetadata.GetMirrorCuboidUv(doc.RootElement));

        using var doc2 = JsonDocument.Parse("""{"from":[0,0,0],"to":[1,1,1],"uvOrigin":[0,0],"mirrorU":false}""");
        Assert.False(GeometryIrCuboidMetadata.GetMirrorCuboidUv(doc2.RootElement));
    }

    [Fact]
    public void TryGetInflate_reads_numeric_inflate()
    {
        using var doc = JsonDocument.Parse("""{"from":[0,0,0],"to":[1,1,1],"uvOrigin":[0,0],"inflate":0.5}""");
        Assert.True(GeometryIrCuboidMetadata.TryGetInflate(doc.RootElement, out var inf));
        Assert.Equal(0.5f, inf, 5);

        using var doc2 = JsonDocument.Parse("""{"from":[0,0,0],"to":[1,1,1],"uvOrigin":[0,0]}""");
        Assert.False(GeometryIrCuboidMetadata.TryGetInflate(doc2.RootElement, out _));
    }
}
