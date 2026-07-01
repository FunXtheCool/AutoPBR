using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using System.Numerics;

namespace AutoPBR.Core.Tests;

public sealed class BlockTextureParityMeshTests
{
    [Fact]
    public void Grass_block_side_maps_top_side_and_dirt_bottom()
    {
        const string path = "assets/minecraft/textures/block/grass_block_side.png";
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out var provenance, out _, out _));
        Assert.Equal(PreviewMeshDriverKind.VanillaBlockParity, provenance.Kind);
        Assert.Equal(BlockTextureParityPreviewShape.CubeDirectional, BlockTextureParityCatalog.ResolveRule(path)!.PreviewShape);
        Assert.Contains("grass_block_side", merged.Textures["north"]);
        Assert.Contains("grass_block_top", merged.Textures["up"]);
        Assert.Contains("dirt", merged.Textures["down"]);
    }

    [Fact]
    public void Grass_block_top_keeps_selected_texture_on_up_face()
    {
        const string path = "assets/minecraft/textures/block/grass_block_top.png";
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out var ordered, out _));
        Assert.Contains("grass_block_top", merged.Textures["up"]);
        Assert.Contains("grass_block_side", merged.Textures["north"]);
        Assert.Contains("dirt", merged.Textures["down"]);
        Assert.Equal(2, merged.Elements.Count);
        Assert.Contains(ordered, p => p.Contains("grass_block_side_overlay", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Grass_block_side_overlay_synthesizes_paired_grass_cube()
    {
        const string path = "assets/minecraft/textures/block/grass_block_side_overlay.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.CubeDirectional, rule!.PreviewShape);
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out var ordered, out _));
        Assert.Equal(2, merged.Elements.Count);
        Assert.Contains(ordered, p => p.Contains("grass_block_top", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ordered, p => p.Contains("grass_block_side_overlay", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Grass_block_snow_keeps_pack_json_top_and_appends_snow_cap()
    {
        const string path = "assets/minecraft/textures/block/grass_block_snow.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.CubeDirectional, rule!.PreviewShape);
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out var provenance, out var ordered, out _));
        Assert.Equal(2, merged.Elements.Count);
        Assert.Contains("grass_block_top", merged.Textures["up"]);
        Assert.Contains("dirt", merged.Textures["down"]);
        Assert.Contains("grass_block_snow", merged.Textures["north"]);
        Assert.Contains("snow-cap", provenance.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ordered, p => p.Contains("snow.png", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ordered, p => p.Contains("grass_block_snow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Oak_log_uses_column_end_grain_on_up_and_down()
    {
        const string path = "assets/minecraft/textures/block/oak_log.png";
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out _, out _));
        Assert.Equal(BlockTextureParityPreviewShape.CubeColumnY, BlockTextureParityCatalog.ResolveRule(path)!.PreviewShape);
        Assert.Contains("oak_log", merged.Textures["north"]);
        Assert.Contains("oak_log_top", merged.Textures["up"]);
        Assert.Contains("oak_log_top", merged.Textures["down"]);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/block/stone.png")]
    [InlineData("assets/minecraft/textures/block/sand.png")]
    public void Uniform_cube_repeats_selected_texture_on_all_faces(string selectedPath)
    {
        var stem = Path.GetFileNameWithoutExtension(selectedPath);
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(selectedPath, out var merged, out _, out _, out _));
        Assert.Equal(BlockTextureParityPreviewShape.UniformCube, BlockTextureParityCatalog.ResolveRule(selectedPath)!.PreviewShape);
        foreach (var face in merged.Elements[0].Faces.Values)
        {
            Assert.Contains(stem, merged.Textures[face.TextureKey.TrimStart('#')]);
        }
    }

    [Fact]
    public void Oak_trapdoor_synthesizes_thin_plate()
    {
        const string path = "assets/minecraft/textures/block/oak_trapdoor.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.ThinPlate, rule!.PreviewShape);
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out _, out _));
        Assert.Single(merged.Elements);
        Assert.Equal(3f, merged.Elements[0].To[2]);
    }

    [Fact]
    public void Oak_door_bottom_synthesizes_paired_top_and_bottom_halves()
    {
        const string path = "assets/minecraft/textures/block/oak_door_bottom.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.DoorHalf, rule!.PreviewShape);
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out _, out _));
        Assert.Equal(2, merged.Elements.Count);
        Assert.Contains("oak_door_bottom", merged.Textures["bottom"]);
        Assert.Contains("oak_door_top", merged.Textures["top"]);
        Assert.Equal(16f, merged.Elements[0].To[1]);
        Assert.Equal(0f, merged.Elements[1].From[1]);
    }

    [Fact]
    public void Oak_door_top_also_synthesizes_paired_halves()
    {
        const string path = "assets/minecraft/textures/block/oak_door_top.png";
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out _, out _));
        Assert.Equal(2, merged.Elements.Count);
    }

    [Fact]
    public void Cake_side_synthesizes_wedge_with_multiple_elements()
    {
        const string path = "assets/minecraft/textures/block/cake_side.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.CakeWedge, rule!.PreviewShape);
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out _, out _));
        Assert.Single(merged.Elements);
        Assert.Equal(8f, merged.Elements[0].To[1]);
    }

    [Fact]
    public void Cactus_side_synthesizes_inset_cross_geometry()
    {
        const string path = "assets/minecraft/textures/block/cactus_side.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.CactusCross, rule!.PreviewShape);
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out _, out _));
        Assert.Equal(3, merged.Elements.Count);
    }

    [Fact]
    public void PackModelJsonOnly_rule_does_not_synthesize()
    {
        const string path = "assets/minecraft/textures/block/activator_rail_on.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.PackModelJsonOnly, rule!.PreviewShape);
        Assert.False(rule.CanSynthesizePreview());
        Assert.False(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(
            path,
            out _,
            out _,
            out _,
            out _));
    }

    [Fact]
    public void Powered_rail_synthesizes_rail_track()
    {
        const string path = "assets/minecraft/textures/block/powered_rail.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.RailTrack, rule!.PreviewShape);
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out _, out _));
        Assert.NotEmpty(merged.Elements);
    }

    [Fact]
    public void Short_grass_synthesizes_cross_sprite()
    {
        const string path = "assets/minecraft/textures/block/short_grass.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.CrossSprite, rule!.PreviewShape);
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out _, out _));
        Assert.Equal(2, merged.Elements.Count);
    }

    [Fact]
    public void Bamboo_fence_synthesizes_post_with_north_link()
    {
        const string path = "assets/minecraft/textures/block/bamboo_fence.png";
        var rule = BlockTextureParityCatalog.ResolveRule(path);
        Assert.NotNull(rule);
        Assert.Equal(BlockTextureParityPreviewShape.FenceWithLink, rule!.PreviewShape);
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out _, out _));
        Assert.True(merged.Elements.Count > 1);
    }

    [Fact]
    public void Block_texture_path_is_not_entity_texture_path()
    {
        const string path = "assets/minecraft/textures/block/grass_block_side.png";
        Assert.True(VanillaBlockPreviewRuntime.IsBlockTextureArchivePath(path));
        Assert.DoesNotContain("/textures/entity/", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Synthetic_grass_block_bake_winds_faces_outward_for_gl_cull()
    {
        const string path = "assets/minecraft/textures/block/grass_block_side.png";
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out var ordered, out var ns));
        var pathToIdx = ordered.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i, StringComparer.OrdinalIgnoreCase);
        var texSizes = ordered.ToDictionary(p => p, _ => (16, 16), StringComparer.OrdinalIgnoreCase);
        Assert.True(MinecraftModelBaker.TryBake(merged, ns, pathToIdx, texSizes, out var verts, out var indices, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        for (var tri = 0; tri < indices.Length / 3; tri++)
        {
            var i0 = (int)indices[tri * 3];
            var i1 = (int)indices[tri * 3 + 1];
            var i2 = (int)indices[tri * 3 + 2];
            var p0 = ReadPosition(verts, i0, stride);
            var p1 = ReadPosition(verts, i1, stride);
            var p2 = ReadPosition(verts, i2, stride);
            var geometricNormal = Vector3.Normalize(Vector3.Cross(p1 - p0, p2 - p0));
            var center = (p0 + p1 + p2) / 3f;
            Assert.True(Vector3.Dot(geometricNormal, center) > 0f,
                $"Triangle {tri} at {center} has inward normal {geometricNormal}");
        }
    }

    [Fact]
    public void Synthetic_grass_block_north_face_maps_grass_fringe_to_high_block_y()
    {
        const string path = "assets/minecraft/textures/block/grass_block_side.png";
        Assert.True(VanillaBlockPreviewRuntime.TryBuildSyntheticMesh(path, out var merged, out _, out var ordered, out var ns));
        var pathToIdx = ordered.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i, StringComparer.OrdinalIgnoreCase);
        var texSizes = ordered.ToDictionary(p => p, _ => (16, 16), StringComparer.OrdinalIgnoreCase);
        Assert.True(MinecraftModelBaker.TryBake(merged, ns, pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var northLowY = new List<float>();
        var northHighY = new List<float>();
        for (var i = 0; i < verts.Length / stride; i++)
        {
            var o = i * stride;
            var pos = new Vector3(verts[o], verts[o + 1], verts[o + 2]);
            if (MathF.Abs(pos.Z + 0.5f) > 0.01f)
            {
                continue;
            }

            var v = verts[o + 7];
            if (pos.Y < 0f)
            {
                northLowY.Add(v);
            }
            else
            {
                northHighY.Add(v);
            }
        }

        Assert.NotEmpty(northLowY);
        Assert.NotEmpty(northHighY);
        Assert.True(northHighY.Average() > northLowY.Average(),
            $"North face grass fringe should map to higher V (high Y avg={northHighY.Average():F3}, low Y avg={northLowY.Average():F3})");
    }

    private static Vector3 ReadPosition(float[] verts, int vertexIndex, int stride)
    {
        var o = vertexIndex * stride;
        return new Vector3(verts[o], verts[o + 1], verts[o + 2]);
    }
}
