using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
    [Fact]
    public void BannerStanding_resolves_flag_bar_and_pole_from_composite_shard()
    {
        const string path = "assets/minecraft/textures/entity/banner/stripe_top.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(3, model.Elements.Count);
    }

    [Fact]
    public void BannerStanding_flag_hangs_below_bar_in_preview_space()
    {
        const string path = "assets/minecraft/textures/entity/banner/stripe_top.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        ModelElement? flag = null;
        ModelElement? bar = null;
        foreach (var el in model.Elements)
        {
            var height = el.To[1] - el.From[1];
            if (height > 30f)
            {
                flag = el;
            }
            else if (height is >= 1.5f and <= 3f)
            {
                bar = el;
            }
        }

        Assert.NotNull(flag);
        Assert.NotNull(bar);
        TransformWorldCorners(flag!, out var flagMin, out _);
        TransformWorldCorners(bar!, out _, out var barMax);
        Assert.True(
            flagMin.Y < barMax.Y - 4f,
            $"banner cloth should hang below the bar; flagMinY={flagMin.Y:G3} barMaxY={barMax.Y:G3}");
    }

    [Fact]
    public void BannerWall_resolves_flag_and_bar_without_pole()
    {
        const string path = "assets/minecraft/textures/entity/banner/banner_base.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(2, model.Elements.Count);
    }

    [Fact]
    public void BannerWall_flag_hangs_below_bar_in_preview_space()
    {
        const string path = "assets/minecraft/textures/entity/banner/banner_base.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        ModelElement? flag = null;
        ModelElement? bar = null;
        foreach (var el in model.Elements)
        {
            var height = el.To[1] - el.From[1];
            if (height > 30f)
            {
                flag = el;
            }
            else if (height is >= 1.5f and <= 3f)
            {
                bar = el;
            }
        }

        Assert.NotNull(flag);
        Assert.NotNull(bar);
        TransformWorldCorners(flag!, out var flagMin, out _);
        TransformWorldCorners(bar!, out _, out var barMax);
        Assert.True(
            flagMin.Y < barMax.Y - 4f,
            $"wall banner cloth should hang below the bar; flagMinY={flagMin.Y:G3} barMaxY={barMax.Y:G3}");
    }
}
