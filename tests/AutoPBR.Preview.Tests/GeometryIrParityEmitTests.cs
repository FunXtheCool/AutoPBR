using System.Numerics;
using System.Text.Json;
using AutoPBR.Preview;

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
        var ok = EntityModelRuntime.TryToEntityCuboidForTests(
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
        var ok = EntityModelRuntime.TryToEntityCuboidForTests(
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

    [Fact]
    public void DecoratedPot_cap_cuboid_resolves_texcrop_updown_footprint()
    {
        const string cap = """
            {
              "from": [0, 0, 0],
              "to": [14, 0, 14],
              "uvOrigin": [18, 13],
              "uvSpan": [14, 0, 14],
              "textureKey": "#base",
              "faceMask": ["down"],
              "liftKind": "exact"
            }
            """;

        using var doc = JsonDocument.Parse(cap);
        var ok = EntityModelRuntime.TryToEntityCuboidForTests(
            doc.RootElement,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                AtlasWidth = 32,
                AtlasHeight = 32,
                PreviewDegenerateAxisThickness = 0.08f,
            },
            out var cuboidOut,
            out var failure);

        Assert.True(ok, failure);
        Assert.Equal(14, cuboidOut.UvSizeW);
        Assert.Equal(0, cuboidOut.UvSizeH);
        Assert.Equal(14, cuboidOut.UvSizeD);
        Assert.NotNull(cuboidOut.FaceMask);
        Assert.Equal(["down"], cuboidOut.FaceMask);

        var b = new EntityModelRuntime.RigBuilder(32, 32);
        cuboidOut.Emit(b, Matrix4x4.Identity, 1f, "#base");
        var built = b.Build("entity/decorated_pot/decorated_pot_base");
        var capEl = Assert.Single(built.Elements);
        var down = capEl.Faces["down"];
        Assert.Equal(0f, down.Uv![0], 0.01f);
        Assert.Equal(13f, down.Uv![1], 0.01f);
        Assert.Equal(14f, down.Uv![2], 0.01f);
        Assert.Equal(27f, down.Uv![3], 0.01f);
    }

    [Fact]
    public void DecoratedPot_top_cap_cuboid_uses_java_up_exterior_slot()
    {
        const string cap = """
            {
              "from": [0, 0, 0],
              "to": [14, 0, 14],
              "uvOrigin": [-14, 13],
              "uvSpan": [14, 0, 14],
              "textureKey": "#base",
              "faceMask": ["up"],
              "liftKind": "exact"
            }
            """;

        using var doc = JsonDocument.Parse(cap);
        Assert.True(EntityModelRuntime.TryToEntityCuboidForTests(
            doc.RootElement,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                AtlasWidth = 32,
                AtlasHeight = 32,
            },
            out var cuboidOut,
            out var failure), failure);

        var b = new EntityModelRuntime.RigBuilder(32, 32);
        cuboidOut.Emit(b, Matrix4x4.Identity, 1f, "#base");
        var up = Assert.Single(b.Build("entity/decorated_pot/decorated_pot_base").Elements).Faces["up"];
        Assert.Equal(14f, up.Uv![0], 0.01f);
        Assert.Equal(27f, up.Uv![1], 0.01f);
        Assert.Equal(28f, up.Uv![2], 0.01f);
        Assert.Equal(13f, up.Uv![3], 0.01f);
    }

    [Fact]
    public void DecoratedPot_cap_cuboid_preserves_raw_negative_texOffs()
    {
        const string cap = """
            {
              "from": [0, 0, 0],
              "to": [14, 0, 14],
              "uvOrigin": [-14, 13],
              "uvSpan": [14, 0, 14],
              "textureKey": "#base",
              "faceMask": ["down"],
              "liftKind": "exact"
            }
            """;

        using var doc = JsonDocument.Parse(cap);
        var ok = EntityModelRuntime.TryToEntityCuboidForTests(
            doc.RootElement,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                AtlasWidth = 32,
                AtlasHeight = 32,
                PreviewDegenerateAxisThickness = 0.08f,
            },
            out var cuboidOut,
            out var failure);

        Assert.True(ok, failure);

        var b = new EntityModelRuntime.RigBuilder(32, 32);
        cuboidOut.Emit(b, Matrix4x4.Identity, 1f, "#base");
        var built = b.Build("entity/decorated_pot/decorated_pot_base");
        var capEl = Assert.Single(built.Elements);
        var down = capEl.Faces["down"];
        Assert.Equal(0f, down.Uv![0], 0.01f);
        Assert.Equal(13f, down.Uv![1], 0.01f);
        Assert.Equal(14f, down.Uv![2], 0.01f);
        Assert.Equal(27f, down.Uv![3], 0.01f);
    }

    [Fact]
    public void DecoratedPot_side_thicken_keeps_north_on_exterior_plane()
    {
        float x0 = 0f, y0 = 0f, z0 = 0f, x1 = 14f, y1 = 16f, z1 = 0f;
        EntityModelRuntime.ApplyDecoratedPotPreviewSheetThickness(
            ref x0, ref y0, ref z0, ref x1, ref y1, ref z1,
            EntityModelRuntime.DecoratedPotPreviewDegenerateAxisThickness,
            ["north"]);
        Assert.Equal(0f, z0, 3);
        Assert.Equal(EntityModelRuntime.DecoratedPotPreviewDegenerateAxisThickness, z1, 3);
        Assert.True(x1 - x0 >= 14f - 0.01f);
        Assert.True(x1 - x0 <= 14f + 0.01f);
    }

    [Fact]
    public void Bee_wing_sheet_uses_java_cube_unfold_not_texcrop_anchor()
    {
        const string wing = """
            {
              "from": [-9, 0, 0],
              "to": [0, 0, 6],
              "uvOrigin": [0, 18],
              "uvSpan": [9, 0, 6],
              "textureKey": "#skin",
              "faceMask": ["up", "down"],
              "liftKind": "exact"
            }
            """;

        using var doc = JsonDocument.Parse(wing);
        var ok = EntityModelRuntime.TryToEntityCuboidForTests(
            doc.RootElement,
            new GeometryIrMeshEmitOptions
            {
                Fidelity = GeometryIrEmitFidelity.Parity,
                AtlasWidth = 64,
                AtlasHeight = 64,
                PreviewDegenerateAxisThickness = 0.06f,
            },
            out var cuboidOut,
            out var failure);

        Assert.True(ok, failure);
        var b = new EntityModelRuntime.RigBuilder(64, 64);
        cuboidOut.Emit(b, Matrix4x4.Identity, 1f);
        var built = b.Build("entity/bee/bee");
        var el = Assert.Single(built.Elements);
        var up = el.Faces["up"].Uv!;
        Assert.Equal(15f, up[0], 0.01f);
        Assert.Equal(24f, up[1], 0.01f);
        Assert.Equal(24f, up[2], 0.01f);
        Assert.Equal(18f, up[3], 0.01f);
    }
}
