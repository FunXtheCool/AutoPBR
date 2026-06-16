using System.Text.Json;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class GeometryIrParityEmitTests
{
    [Fact]
    public void Parity_emit_does_not_apply_cube_deformation_inflate()
    {
        const string cuboid = """
            {
              "from": [-4, -7, -10],
              "to": [4, 1, 2],
              "uvOrigin": [0, 0],
              "inflate": 0.3
            }
            """;

        using var doc = JsonDocument.Parse(cuboid);
        var ok = CleanRoomEntityModelRuntime.TryToEntityCuboidForTests(
            doc.RootElement,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                AtlasWidth = 64,
                AtlasHeight = 64,
            },
            out var cuboidOut,
            out _);

        Assert.True(ok);
        Assert.Equal(-4f, cuboidOut.X0);
        Assert.Equal(-7f, cuboidOut.Y0);
        Assert.Equal(-10f, cuboidOut.Z0);
        Assert.Equal(4f, cuboidOut.X1);
        Assert.Equal(1f, cuboidOut.Y1);
        Assert.Equal(2f, cuboidOut.Z1);
    }

    [Fact]
    public void Negative_inflate_emit_uses_pre_inflate_integer_uv_footprint_for_snow_golem()
    {
        const string snowGolemJvm = "net.minecraft.client.model.animal.golem.SnowGolemModel";
        const string cuboid = """
            {
              "from": [-4, -8, -4],
              "to": [4, 0, 4],
              "uvOrigin": [0, 0],
              "inflate": -0.5
            }
            """;

        using var doc = JsonDocument.Parse(cuboid);
        var ok = CleanRoomEntityModelRuntime.TryToEntityCuboidForTests(
            doc.RootElement,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                AtlasWidth = 64,
                AtlasHeight = 64,
                OfficialJvmName = snowGolemJvm,
                PreviewApplyCubeDeformationInflate = true,
            },
            out var cuboidOut,
            out _);

        Assert.True(ok);
        Assert.Equal(7f, cuboidOut.X1 - cuboidOut.X0, 3);
        Assert.Equal(8, cuboidOut.UvSizeW);
        Assert.Equal(8, cuboidOut.UvSizeH);
        Assert.Equal(8, cuboidOut.UvSizeD);
    }
}
