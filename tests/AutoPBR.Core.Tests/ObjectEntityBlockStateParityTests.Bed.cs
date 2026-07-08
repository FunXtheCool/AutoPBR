using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
    [Fact]
    public void Bed_resolves_six_cuboids_from_preview_composite_shard()
    {
        const string path = "assets/minecraft/textures/entity/bed/red.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(6, model.Elements.Count);
    }

    [Fact]
    public void Bed_preview_mattress_sits_above_legs_in_world_space()
    {
        const string path = "assets/minecraft/textures/entity/bed/black.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var mattressYs = new List<float>();
        var legMaxY = float.MinValue;
        foreach (var el in model.Elements)
        {
            var width = el.To[0] - el.From[0];
            var depth = el.To[1] - el.From[1];
            var isMattress = width > 10f && depth > 10f;
            TransformWorldCorners(el, out var min, out var max);
            if (isMattress)
            {
                mattressYs.Add((min.Y + max.Y) * 0.5f);
            }
            else
            {
                legMaxY = MathF.Max(legMaxY, max.Y);
            }
        }

        Assert.Equal(2, mattressYs.Count);
        foreach (var mattressCenterY in mattressYs)
        {
            Assert.True(
                mattressCenterY > legMaxY - 0.5f,
                $"mattress should sit above legs; mattressCenterY={mattressCenterY:G3} legMaxY={legMaxY:G3}");
        }
    }

    [Fact]
    public void Bed_preview_mattress_slabs_lie_flat_and_connect()
    {
        const string path = "assets/minecraft/textures/entity/bed/red.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(6, model.Elements.Count);

        var mains = model.Elements
            .Where(e => e.From[1] == 0f && e.To[1] == 16f && e.From[2] == 0f && e.To[2] == 6f)
            .ToList();
        Assert.Equal(2, mains.Count);

        foreach (var slab in mains)
        {
            TransformWorldCorners(slab, out var min, out var max);
            Assert.InRange(max.Y - min.Y, 5f, 7f);
            Assert.True(max.Y <= 16f, $"mattress slab should sit near ground after preview facing: yMax={max.Y}");
        }

        TransformWorldCorners(mains[0], out var headMin, out var headMax);
        TransformWorldCorners(mains[1], out var footMin, out var footMax);
        var headCenterZ = (headMin.Z + headMax.Z) * 0.5f;
        var footCenterZ = (footMin.Z + footMax.Z) * 0.5f;
        Assert.InRange(MathF.Abs(headCenterZ - footCenterZ), 14f, 18f);
    }
}
