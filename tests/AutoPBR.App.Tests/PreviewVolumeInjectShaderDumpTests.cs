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
    public void EsAdaptedVolumeInject_UsesAngleSafePackHelper(string fragmentFile)
    {
        var adapted = ResolveAndAdapt(fragmentFile);
        Assert.Contains("FragColor = viPackFroxelInject(mediumRho, uLightColor", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("return;", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("vcFbm", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("vmMediumDensity", adapted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("genesis_volume_integrate.frag")]
    [InlineData("genesis_volume_integrate_lite.frag")]
    public void EsAdaptedVolumeIntegrate_UsesTexelFetchAndGodRayOutput(string fragmentFile)
    {
        var adapted = ResolveAndAdapt(fragmentFile);
        Assert.Contains("GENESIS_GLES_PACK rev29", adapted, StringComparison.Ordinal);
        Assert.Contains("atmosphereMiePhase", adapted, StringComparison.Ordinal);
        Assert.Contains("vmSegmentInscatterWeight", adapted, StringComparison.Ordinal);
        Assert.Contains("vmSegmentTransmittance", adapted, StringComparison.Ordinal);
        Assert.Contains("viSampleFroxel", adapted, StringComparison.Ordinal);
        Assert.Contains("grWorldRayDir", adapted, StringComparison.Ordinal);
        Assert.Contains("vfWorldToFroxelUv", adapted, StringComparison.Ordinal);
        Assert.Contains("vfFroxelEdgeWeight", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("texelFetch(", adapted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("genesis_volume_inject.frag")]
    [InlineData("genesis_volume_inject_lite.frag")]
    [InlineData("genesis_volume_integrate.frag")]
    [InlineData("genesis_volume_integrate_lite.frag")]
    [InlineData("genesis_clouds.frag")]
    public void EsAdaptedVolumeShaders_ContainNoNonAsciiBytes(string fragmentFile)
    {
        var adapted = ResolveAndAdapt(fragmentFile);
        foreach (var ch in adapted)
        {
            Assert.True(ch <= '\x7F',
                $"Non-ASCII char U+{(int)ch:X4} in adapted '{fragmentFile}' breaks ANGLE's GLES lexer.");
        }
    }

    [Fact]
    public void LiteIntegrate_ExcludesFbmCloudDensity()
    {
        var adapted = ResolveAndAdapt("genesis_volume_integrate_lite.frag");
        Assert.DoesNotContain("vcFbm", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("vmMediumDensity", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("sampleShadowPcf3x3", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void LiteInject_ExcludesRayMarchHelpers()
    {
        var adapted = ResolveAndAdapt("genesis_volume_inject_lite.frag");
        Assert.DoesNotContain("vcMarchClouds", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("vcIntersectLayerRange", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("vcCloudDensityEx", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("sampler3D cloudNoise", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("vfSampleFroxel", adapted, StringComparison.Ordinal);
        Assert.Contains("viInjectMediumDensity", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void GenesisClouds_DefinesDensityFunctionsBeforeUse()
    {
        // Regression: the flattened TU once referenced vcCloudDensityRaw from the light-march helper
        // before any density include appeared, so the cloud program silently failed to compile and
        // the clouds toggle had no visible effect.
        var adapted = ResolveAndAdapt("genesis_clouds.frag");
        var densityEx = adapted.IndexOf("float vcCloudDensityEx(", StringComparison.Ordinal);
        var lightMarch = adapted.IndexOf("float vcLightOpticalDepth(", StringComparison.Ordinal);
        var marchUse = adapted.IndexOf("vcLightOpticalDepth(worldPos", StringComparison.Ordinal);
        Assert.True(densityEx >= 0, "vcCloudDensityEx definition missing from flattened genesis_clouds.frag");
        Assert.True(lightMarch >= 0, "vcLightOpticalDepth definition missing from flattened genesis_clouds.frag");
        Assert.True(marchUse > lightMarch, "main() must call vcLightOpticalDepth after its definition");
        Assert.True(densityEx < lightMarch,
            "cloud density functions must be defined before the sun light march that samples them");
    }

    [Fact]
    public void DumpAdaptedVolumeShaders_ForAngleDebug()
    {
        var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "agent-tools"));
        Directory.CreateDirectory(outDir);
        foreach (var f in new[]
        {
            "genesis_volume_inject.frag",
            "genesis_volume_inject_lite.frag",
            "genesis_volume_integrate.frag",
            "genesis_volume_integrate_lite.frag"
        })
        {
            var adapted = ResolveAndAdapt(f);
            File.WriteAllText(Path.Combine(outDir, f + ".es.glsl"), adapted);
        }
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
