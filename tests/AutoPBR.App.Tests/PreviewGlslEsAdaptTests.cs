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
        Assert.Contains("groundSpecularReceiverFade", adapted, StringComparison.Ordinal);
        Assert.Contains("uIsGroundPass", adapted, StringComparison.Ordinal);
        Assert.Contains("uLightDir, uLightColor, uAtmosphereSunIntensity", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("Sun / punctual highlights come from direct lighting only", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("iblPrefilteredSkyRadianceFallback", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void GenesisShadowSampling_UsesSoftPcfKernel()
    {
        var src = GlslIncludeResolver.Resolve("genesis.frag", LoadShader);
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.Contains("sampleShadowPcfSoft", adapted, StringComparison.Ordinal);
        Assert.Contains("uShadowSoftnessTexels", adapted, StringComparison.Ordinal);
        Assert.Contains("vec2[16]", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void GenesisParallax_UsesTileLocalPomWithoutPostClamp()
    {
        var src = GlslIncludeResolver.Resolve("genesis.frag", LoadShader);
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.Contains("textureGrad(heightTex", adapted, StringComparison.Ordinal);
        Assert.Contains("textureGrad(uAlbedo, uv, uvDx, uvDy)", adapted, StringComparison.Ordinal);
        Assert.Contains("textureGrad(uNormal, uv, dx, dy)", adapted, StringComparison.Ordinal);
        Assert.Contains("textureGrad(uSpecular, uv, uvDx, uvDy)", adapted, StringComparison.Ordinal);
        Assert.Contains("pomTileUv(tileBase", adapted, StringComparison.Ordinal);
        Assert.Contains("tileBase + fract(localUv)", adapted, StringComparison.Ordinal);
        Assert.Contains("uParallaxTraceLayers", adapted, StringComparison.Ordinal);
        Assert.Contains("uParallaxRefineSteps", adapted, StringComparison.Ordinal);
        Assert.Contains("uParallaxShadowSamples", adapted, StringComparison.Ordinal);
        Assert.Contains("uParallaxShadowSoftness", adapted, StringComparison.Ordinal);
        Assert.Contains("textureSize(heightTex, 0)", adapted, StringComparison.Ordinal);
        Assert.Contains("if (Vtan.z <= 0.0)", adapted, StringComparison.Ordinal);
        Assert.Contains("Vtan.xy / max(Vtan.z, GEN_POM_MIN_VIEW_Z)", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("if (Vtan.z < GEN_POM_MIN_VIEW_Z)", adapted, StringComparison.Ordinal);
        Assert.Contains("if (curLayer < sampleH)", adapted, StringComparison.Ordinal);
        Assert.Contains("float delta = curLayer - sampleH;", adapted, StringComparison.Ordinal);
        Assert.Contains("smoothstep(0.0, softWidth, delta)", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("uv = clamp(uvDisp", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("clamp(localUv", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("cavityAmt", adapted, StringComparison.Ordinal);
        Assert.DoesNotContain("sampleHeight01(sampler2D heightTex, vec2 uv)", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void EsAdapt_LeavesAsciiSourceUnchangedAfterHeaderSwap()
    {
        const string src = "#version 330 core\n// plain ascii\nout vec4 c;\nvoid main(){c=vec4(1.0);}\n";
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.Contains("// plain ascii", adapted, StringComparison.Ordinal);
    }

    private static string LoadShader(string fileName) => LoadShaderCore(fileName, ThisFilePath());

    private static string ThisFilePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") =>
        sourceFilePath;

    private static string LoadShaderCore(string fileName, string sourceFilePath)
    {
        var relative = fileName.Replace('/', Path.DirectorySeparatorChar);
        var sourceDir = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        foreach (var start in new[] { sourceDir, AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var root = Path.Combine(dir.FullName, "src", "AutoPBR.App", "Rendering", "Shaders");
                var path = Path.Combine(root, relative);
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }

                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException($"Could not locate shader source '{fileName}'.");
    }
}
