using AutoPBR.App.Rendering.OpenGL;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Tests;

public class PreviewGlslEsAdaptTests
{
    [Theory]
    [InlineData("genesis.frag")]
    [InlineData("genesis_godrays.frag")]
    [InlineData("genesis_godrays_shadow.frag")]
    [InlineData("genesis_godrays_upsample.frag")]
    [InlineData("genesis_scene_present.frag")]
    [InlineData("genesis_clouds.frag")]
    [InlineData("genesis_clouds_upsample.frag")]
    [InlineData("genesis_taa_resolve.frag")]
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

        if (adapted.Contains("sampler3D", StringComparison.Ordinal))
        {
            Assert.Contains("precision highp sampler3D;", adapted, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EsAdapt_StripsNonAsciiCommentChars_PreservingLength()
    {
        const string src = "#version 330 core\n// Beer\u2013Lambert \u2014 mist\nout vec4 c;\nvoid main(){c=vec4(1.0);}\n";
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.DoesNotContain('\u2013', adapted);
        Assert.DoesNotContain('\u2014', adapted);
        foreach (var ch in adapted)
        {
            Assert.True(ch <= '\x7F');
        }
    }

    [Fact]
    public void EsAdaptedGenesis_IncludesSplitSumIblWithoutAtmosphereOnlySymbols()
    {
        var src = GlslIncludeResolver.Resolve("genesis.frag", LoadShader);
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.Contains("iblEnvBrdfFactor", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("ATM_PI", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void GenesisMetalIbl_RetainsPreviewBaseForLabPbrMetalIds()
    {
        var src = GlslIncludeResolver.Resolve("genesis.frag", LoadShader);
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.Contains("metalPreviewBaseVisibility", adapted, StringComparison.Ordinal);
        Assert.Contains("mat.metallic * metalPreviewBaseVisibility(mat.roughness)", adapted, StringComparison.Ordinal);
        Assert.Contains("metalPreviewIrradiance * albedoLinear * metalBaseVisibility", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "indirect += iblDiff * albedoLinear * (1.0 - mat.metallic) * uIblStrength;",
            adapted,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenesisIbl_UsesGeneratedWorldSpaceProbeAndOffscreenSun()
    {
        var src = GlslIncludeResolver.Resolve("genesis.frag", LoadShader);
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.Contains("previewEnvSunRadiance", adapted, StringComparison.Ordinal);
        Assert.Contains("previewEnvCubemapRadiance", adapted, StringComparison.Ordinal);
        Assert.Contains("previewAmbientProbeIrradiance", adapted, StringComparison.Ordinal);
        Assert.Contains("uLightDir, uLightColor, uAtmosphereSunIntensity", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("Sun / punctual highlights come from direct lighting only", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("iblPrefilteredSkyRadianceFallback", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void EsAdapt_LeavesAsciiSourceUnchangedAfterHeaderSwap()
    {
        const string src = "#version 330 core\n// plain ascii\nout vec4 c;\nvoid main(){c=vec4(1.0);}\n";
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.Contains("// plain ascii", adapted, StringComparison.Ordinal);
    }

    private static string LoadShader(string fileName)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "AutoPBR.App", "Rendering", "Shaders"));
        return File.ReadAllText(Path.Combine(root, fileName.Replace('/', Path.DirectorySeparatorChar)));
    }
}
