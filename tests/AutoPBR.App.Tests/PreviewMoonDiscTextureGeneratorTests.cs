using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class PreviewMoonDiscTextureGeneratorTests
{
    [Fact]
    public void GenerateRgba8_ProducesDiscWithTransparentCorners()
    {
        var rgba = PreviewMoonDiscTextureGenerator.GenerateRgba8();
        Assert.Equal(PreviewMoonDiscTextureGenerator.Size * PreviewMoonDiscTextureGenerator.Size * 4, rgba.Length);

        var corner = rgba[0];
        var centerIdx = ((PreviewMoonDiscTextureGenerator.Size / 2) * PreviewMoonDiscTextureGenerator.Size
            + PreviewMoonDiscTextureGenerator.Size / 2) * 4;

        Assert.Equal(0, corner);
        Assert.True(rgba[centerIdx] > 100);
        Assert.True(rgba[centerIdx + 3] > 200);
    }
}
