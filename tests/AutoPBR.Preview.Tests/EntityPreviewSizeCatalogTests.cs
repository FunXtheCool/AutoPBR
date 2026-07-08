using System.Numerics;

using AutoPBR.Preview;

namespace AutoPBR.Preview.Tests;

public sealed class EntityPreviewSizeCatalogTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/entity/slime/slime.png", "Slime", true)]
    [InlineData("assets/minecraft/textures/entity/slime/magmacube.png", "MagmaCube", true)]
    [InlineData("assets/minecraft/textures/entity/cow/cow.png", "Quadruped", false)]
    public void TryGetSizeOptions_only_slime_family(string path, string builderMethod, bool expectOptions)
    {
        var ok = EntityPreviewSizeCatalog.TryGetSizeOptions(path, builderMethod, out var options);
        Assert.Equal(expectOptions, ok);
        if (expectOptions)
        {
            Assert.Equal(8, options.Count);
            Assert.Contains(options, o => o.IsDefault);
        }
    }

    [Fact]
    public void ResolveDefaultSize_magma_cube_is_medium()
    {
        Assert.Equal(4, EntityPreviewSizeCatalog.ResolveDefaultSize("MagmaCube"));
        Assert.Equal(2, EntityPreviewSizeCatalog.ResolveDefaultSize("Slime"));
    }

    [Fact]
    public void SlimeRenderer_scale_at_rest_is_uniform_size()
    {
        var scale = SlimeFamilyPreviewScale.ComputeRendererScaleFactors(size: 4, squish: 0f);
        Assert.Equal(new Vector3(4f, 4f, 4f), scale);
    }

    [Fact]
    public void SlimeRenderer_scale_with_squish_stretches_y()
    {
        var scale = SlimeFamilyPreviewScale.ComputeRendererScaleFactors(size: 4, squish: 0.5f);
        Assert.True(scale.Y > scale.X);
        Assert.InRange(scale.X, scale.Z - 0.001f, scale.Z + 0.001f);
    }

    [Fact]
    public void ApplyRendererScale_scales_local_to_parent()
    {
        var model = new MergedJavaBlockModel
        {
            Elements =
            [
                new ModelElement
                {
                    From = [-4f, 16f, -4f],
                    To = [4f, 24f, 4f],
                    LocalToParent = Matrix4x4.Identity,
                    Faces = new Dictionary<string, ModelFace>(),
                },
            ],
            Textures = new Dictionary<string, string>(),
        };

        var scaled = SlimeFamilyPreviewScale.ApplyRendererScale(model, size: 4, squish: 0f);
        var m = scaled.Elements[0].LocalToParent;
        Assert.InRange(m.M11, 3.99f, 4.01f);
        Assert.InRange(m.M22, 3.99f, 4.01f);
        Assert.InRange(m.M33, 3.99f, 4.01f);
    }
}
