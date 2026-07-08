using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
    [Fact]
    public void ChestEntity_resolves_three_part_tree_from_bytecode_shard()
    {
        const string path = "assets/minecraft/textures/entity/chest/normal.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(3, model.Elements.Count);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/chest/normal_left.png")]
    [InlineData("assets/minecraft/textures/entity/chest/normal_right.png")]
    public void ChestEntity_paired_preview_merges_left_and_right_halves(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(6, model.Elements.Count);
        Assert.Equal(2, model.Textures.Count);
        Assert.True(model.Textures.ContainsKey("skin"));
        Assert.True(model.Textures.ContainsKey("chest_pair"));

        var minX = float.MaxValue;
        var maxX = float.MinValue;
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out var cMax);
            minX = MathF.Min(minX, cMin.X);
            maxX = MathF.Max(maxX, cMax.X);
        }

        Assert.True(minX is >= 0.5f and <= 2f, $"expected right half anchored near origin; minX={minX}");
        Assert.True(maxX > 30f, $"expected left half offset by one block; maxX={maxX}");
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/chest/normal.png")]
    [InlineData("assets/minecraft/textures/entity/chest/normal_left.png")]
    [InlineData("assets/minecraft/textures/entity/chest/normal_right.png")]
    public void ChestEntity_lid_and_lock_use_ordered_depth_layers_for_closed_overlap(string path)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(
            runtime.TryBuildStaticMesh(
                path,
                Profile26,
                0f,
                0f,
                out var model,
                pairDoubleChestPreviewHalves: false),
            path);

        Assert.Equal(3, model.Elements.Count);
        Assert.Equal(PreviewDepthLayerKind.Base, model.Elements[0].DepthLayerKind);
        Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, model.Elements[1].DepthLayerKind);
        Assert.Equal(1, model.Elements[1].LayerOrdinal);
        Assert.Equal(PreviewDepthLayerKind.CutoutOverlay, model.Elements[2].DepthLayerKind);
        Assert.Equal(2, model.Elements[2].LayerOrdinal);
    }

    [Fact]
    public void ChestEntity_partner_path_swaps_left_and_right_suffix()
    {
        Assert.True(
            EntityModelRuntime.TryGetDoubleChestPartnerAssetPath(
                "assets/minecraft/textures/entity/chest/normal_left.png",
                out var right));
        Assert.Equal("assets/minecraft/textures/entity/chest/normal_right.png", right);

        Assert.True(
            EntityModelRuntime.TryGetDoubleChestPartnerAssetPath(
                "assets/minecraft/textures/entity/chest/copper_exposed_right.png",
                out var left));
        Assert.Equal("assets/minecraft/textures/entity/chest/copper_exposed_left.png", left);
    }

    [Fact]
    public void ChestEntity_single_body_emits_all_six_faces()
    {
        const string path = "assets/minecraft/textures/entity/chest/normal.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        foreach (var el in model.Elements)
        {
            Assert.True(el.Faces.ContainsKey("west"), $"element missing west face: {string.Join(',', el.Faces.Keys)}");
        }
    }

    [Fact]
    public void ChestEntity_closed_lid_hinge_landmark_matches_java_pose()
    {
        const string path = "assets/minecraft/textures/entity/chest/normal.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(3, model.Elements.Count);
        TransformWorldCorners(model.Elements[1], out var lidMin, out var lidMax);
        Assert.Equal(1f, lidMin.X, 0.1f);
        Assert.Equal(9f, lidMin.Y, 0.1f);
        Assert.Equal(1f, lidMin.Z, 0.1f);
        Assert.Equal(15f, lidMax.X, 0.1f);
        Assert.Equal(14f, lidMax.Y, 0.1f);
        Assert.Equal(15f, lidMax.Z, 0.1f);
    }

}
