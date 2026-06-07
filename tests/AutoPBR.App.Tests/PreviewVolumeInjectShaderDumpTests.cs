using AutoPBR.App.Rendering.OpenGL;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class PreviewVolumeInjectShaderEsTests
{
    [Theory]
    [InlineData("genesis_volume_inject.frag")]
    [InlineData("genesis_volume_inject_lite.frag")]
    [InlineData("genesis_volume_integrate.frag")]
    [InlineData("genesis_volume_integrate_lite.frag")]
    [InlineData("genesis_clouds.frag")]
    public void EsAdaptedVolumeShaders_AvoidGlesIncompatibleOutParams(string fragmentFile)
    {
        var adapted = ResolveAndAdapt(fragmentFile);
        Assert.DoesNotContain("bool vcIntersectLayer", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain(", out float", adapted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("genesis_volume_inject.frag")]
    [InlineData("genesis_volume_inject_lite.frag")]
    public void EsAdaptedVolumeInject_PacksFroxelWithComponentAssignment(string fragmentFile)
    {
        var adapted = ResolveAndAdapt(fragmentFile);
        Assert.Contains("packed.r = density", adapted, StringComparison.Ordinal);
        Assert.Contains("packed.gba = sunLit", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void LiteInject_ExcludesRayMarchHelpers()
    {
        var adapted = ResolveAndAdapt("genesis_volume_inject_lite.frag");
        Assert.DoesNotContain("vcMarchClouds", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("vcIntersectLayerRange", adapted, StringComparison.Ordinal);
    }

    private static string ResolveAndAdapt(string fragmentFile)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "AutoPBR.App", "Rendering", "Shaders"));
        string Read(string name) =>
            File.ReadAllText(Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar)));

        return GlslSourceAdapter.Adapt(
            GlslIncludeResolver.Resolve(fragmentFile, Read),
            ShaderType.FragmentShader,
            useOpenGlEs: true);
    }
}
