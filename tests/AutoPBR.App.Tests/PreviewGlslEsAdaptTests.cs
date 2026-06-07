using AutoPBR.App.Rendering.OpenGL;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Tests;

public class PreviewGlslEsAdaptTests
{
    [Theory]
    [InlineData("genesis_volume_inject.frag")]
    [InlineData("genesis_volume_integrate.frag")]
    [InlineData("atmo_sky.frag")]
    [InlineData("atmo_transmittance.frag")]
    [InlineData("atmo_skyview.frag")]
    public void EsAdaptedFragment_StartsWithEsVersionAndSamplerPrecisions(string fragmentFile)
    {
        var src = GlslIncludeResolver.Resolve(fragmentFile, LoadShader);
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.StartsWith("#version 300 es", adapted.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("#version 330 core", adapted, StringComparison.Ordinal);
        Assert.Contains("precision highp sampler2D;", adapted, StringComparison.Ordinal);
        if (adapted.Contains("sampler2DArray", StringComparison.Ordinal))
        {
            Assert.Contains("precision highp sampler2DArray;", adapted, StringComparison.Ordinal);
        }

        if (adapted.Contains("sampler2DShadow", StringComparison.Ordinal))
        {
            Assert.Contains("precision highp sampler2DShadow;", adapted, StringComparison.Ordinal);
        }
    }

    private static string LoadShader(string fileName)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "AutoPBR.App", "Rendering", "Shaders"));
        return File.ReadAllText(Path.Combine(root, fileName.Replace('/', Path.DirectorySeparatorChar)));
    }
}
