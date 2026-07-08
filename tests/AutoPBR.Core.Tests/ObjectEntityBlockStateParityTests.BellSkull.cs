using System.Numerics;

using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
    [Fact]
    public void Bell_resolves_two_part_tree_from_bytecode_shard()
    {
        const string path = "assets/minecraft/textures/entity/bell/bell_body.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
        TransformWorldCorners(model.Elements[0], out var bodyMin, out var bodyMax);
        Assert.Equal(5f, bodyMin.X, 0.2f);
        Assert.Equal(-13f, bodyMin.Y, 0.2f);
        Assert.Equal(5f, bodyMin.Z, 0.2f);
        Assert.True(bodyMax.Y > bodyMin.Y, "bell body should span positive height after object-entity Y-up correction");
    }

    [Fact]
    public void Bell_preview_dome_extends_below_mounting_flange()
    {
        const string path = "assets/minecraft/textures/entity/bell/bell_body.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        TransformWorldCorners(model.Elements[0], out var bodyMin, out var bodyMax);
        TransformWorldCorners(model.Elements[1], out var baseMin, out var baseMax);
        Assert.True(
            bodyMin.Y < baseMin.Y - 2f,
            $"bell dome should hang below the top flange; bodyMinY={bodyMin.Y:G3} baseMinY={baseMin.Y:G3}");
        Assert.True(
            bodyMax.Y <= baseMax.Y + 0.5f,
            $"mounting flange should cap the dome; bodyMaxY={bodyMax.Y:G3} baseMaxY={baseMax.Y:G3}");
    }

    [Fact]
    public void Skull_resolves_head_and_hat_layers_with_block_offset()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/skull_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
        TransformWorldCorners(model.Elements[0], out var headMin, out _);
        Assert.Equal(0f, headMin.Y, 0.2f);
    }
}
