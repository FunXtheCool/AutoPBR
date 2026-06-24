using System.Text.Json;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "UvAtlas")]
public sealed class GeometryIrUvAtlasQualityTests
{
    [Fact]
    public void ComputeUnfoldedUvMax_matches_vanilla_box_layout()
    {
        var (maxU, maxV) = GeometryIrUvAtlasQuality.ComputeUnfoldedUvMax(0, 0, 4, 8, 2);
        Assert.Equal(12, maxU);
        Assert.Equal(10, maxV);
    }

    [Fact]
    public void ComputeUnfoldedUvMax_zero_height_wing_with_negative_texOffs_fits_atlas()
    {
        var (maxU, maxV) = GeometryIrUvAtlasQuality.ComputeUnfoldedUvMax(-3, 9, 3, 0, 3, mirrorU: true);
        Assert.True(maxU <= 32 && maxV <= 32, $"footprint {maxU}x{maxV}");
    }

    [Fact]
    public void ResolveCuboidUvExtents_zero_thickness_axis_uses_logical_extent()
    {
        var cuboid = JsonDocument.Parse("""
            {"from":[0,1,0],"to":[0,6,8],"uvOrigin":[16,14]}
            """).RootElement;
        var (w, h, d) = GeometryIrUvAtlasQuality.ResolveCuboidUvExtents(
            cuboid, cuboid.GetProperty("from"), cuboid.GetProperty("to"));
        Assert.Equal(0, w);
        Assert.Equal(5, h);
        Assert.Equal(8, d);

        var (maxU, maxV) = GeometryIrUvAtlasQuality.ComputeUnfoldedUvMax(16, 14, w, h, d);
        Assert.Equal(32, maxU);
        Assert.Equal(27, maxV);
        Assert.True(maxU <= 32 && maxV <= 32);
    }

    [Fact]
    public void BuildUpDownTexCropFaceUvRects_single_up_mask_uses_java_down_slot_for_dragon_membranes()
    {
        var dragon = GeometryIrUvAtlasQuality.BuildUpDownTexCropFaceUvRects(
            -56, 88, 56, 56, ["up"]);
        Assert.Equal(
            EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.Down, -56, 88, 56, 0, 56),
            dragon.Down);
        Assert.Equal(
            EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.Up, -56, 88, 56, 0, 56),
            dragon.Up);
    }

    [Fact]
    public void BuildUpDownTexCropFaceUvRects_anchors_first_face_in_mask_order()
    {
        var upFirst = GeometryIrUvAtlasQuality.BuildUpDownTexCropFaceUvRects(
            0, 18, 9, 6, ["up", "down"]);
        Assert.Equal(new float[] { 0, 18, 9, 24 }, upFirst.Up);
        Assert.Equal(new float[] { 11, 18, 20, 24 }, upFirst.Down);

        var downFirst = GeometryIrUvAtlasQuality.BuildUpDownTexCropFaceUvRects(
            0, 18, 9, 6, ["down", "up"]);
        Assert.Equal(new float[] { 11, 18, 20, 24 }, downFirst.Up);
        Assert.Equal(new float[] { 0, 18, 9, 24 }, downFirst.Down);
    }

    [Fact]
    public void ResolveNorthSouthSheetUvFootprint_texCrop_anchor_duplicated_in_uvSpan_uses_geometry()
    {
        var cuboid = JsonDocument.Parse("""
            {
              "from": [-5, 0, 0],
              "to": [2, 2, 0],
              "uvOrigin": [26, 1],
              "faceMask": ["north", "south"],
              "uvSpan": [26, 1]
            }
            """).RootElement;
        var (w, h, d) = GeometryIrUvAtlasQuality.ResolveNorthSouthSheetUvFootprint(
            cuboid, 26, 1, 26, 1, 0, 7, 2, 0);
        Assert.Equal(7, w);
        Assert.Equal(2, h);
        Assert.Equal(0, d);
    }

    [Fact]
    public void ResolveNorthSouthSheetUvFootprint_geometry_uvSpan_kept_when_distinct_from_origin()
    {
        var cuboid = JsonDocument.Parse("""
            {
              "from": [3, -13, 0],
              "to": [12, 1, 0],
              "uvOrigin": [12, 40],
              "faceMask": ["north", "south"],
              "uvSpan": [9, 14]
            }
            """).RootElement;
        var (w, h, d) = GeometryIrUvAtlasQuality.ResolveNorthSouthSheetUvFootprint(
            cuboid, 12, 40, 9, 14, 0, 9, 14, 0);
        Assert.Equal(9, w);
        Assert.Equal(14, h);
        Assert.Equal(0, d);
    }

    [Fact]
    public void ComputeUnfoldedUvMaxForFaceMask_north_south_only_uses_subset_corners()
    {
        var (maxU, maxV) = GeometryIrUvAtlasQuality.ComputeUnfoldedUvMaxForFaceMask(
            0, 0, 4, 4, 4, ["north", "south"]);
        Assert.Equal(16, maxU);
        Assert.Equal(8, maxV);
    }

    [Fact]
    public void Creeper_ok_shard_uv_fits_committed_atlas()
    {
        const string jvmName = "net.minecraft.client.model.monster.creeper.CreeperModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvmName}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var uv = GeometryIrUvAtlasQuality.Evaluate(shard.RootElement);
        Assert.True(uv.UvWithinAtlasMatch, uv.Message);
    }

    [Fact]
    public void Breeze_ok_shard_layer_atlas_consistent_when_texture_keys_split()
    {
        const string jvmName = "net.minecraft.client.model.monster.breeze.BreezeModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvmName}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var uv = GeometryIrUvAtlasQuality.Evaluate(shard.RootElement);
        Assert.True(uv.UvWithinAtlasMatch, uv.Message);
        Assert.True(uv.LayerAtlasConsistent, uv.LayerAtlasMessage);
    }
}
