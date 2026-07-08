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
        Assert.Contains("shadowMapTexelDepth", adapted, StringComparison.Ordinal);
        Assert.Contains("texel * 1.75", adapted, StringComparison.Ordinal);
        Assert.Contains("uLightViewProj * vec4(vWorldPos, 1.0)", adapted, StringComparison.Ordinal);
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
        Assert.Contains("textureGrad(uAlbedo, vUv, dFdx(vUv), dFdy(vUv))", adapted, StringComparison.Ordinal);
        Assert.Contains("textureGrad(uNormal, uv, dx, dy)", adapted, StringComparison.Ordinal);
        Assert.Contains("textureGrad(uSpecular, uv, uvDx, uvDy)", adapted, StringComparison.Ordinal);
        Assert.Contains("pomTileUv(tileBase", adapted, StringComparison.Ordinal);
        Assert.Contains("tileBase + fract(localUv)", adapted, StringComparison.Ordinal);
        Assert.Contains("uParallaxTraceLayers", adapted, StringComparison.Ordinal);
        Assert.Contains("uParallaxRefineSteps", adapted, StringComparison.Ordinal);
        Assert.Contains("uParallaxShadowSamples", adapted, StringComparison.Ordinal);
        Assert.Contains("uParallaxShadowSoftness", adapted, StringComparison.Ordinal);
        Assert.Contains("uParallaxUvScale", adapted, StringComparison.Ordinal);
        Assert.Contains(
            "pomUvDisplacementScale(strength) * clamp(uParallaxUvScale, 0.02, 1.0)",
            adapted,
            StringComparison.Ordinal);
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
    public void GenesisTessellation_UsesTrianglePatchDisplacementContract()
    {
        var control = GlslIncludeResolver.Resolve("genesis.tcs", LoadShader);
        var evaluation = GlslIncludeResolver.Resolve("genesis.tes", LoadShader);

        Assert.Contains("#version 400 core", control, StringComparison.Ordinal);
        Assert.Contains("layout(vertices = 3) out;", control, StringComparison.Ordinal);
        Assert.Contains("gl_TessLevelOuter", control, StringComparison.Ordinal);
        Assert.Contains("uEnableTessellationDisplacement > 0 && uHasHeight > 0", control, StringComparison.Ordinal);
        Assert.Contains("clamp(uTessellationLevel, 1.0, 16.0)", control, StringComparison.Ordinal);

        Assert.Contains("#version 400 core", evaluation, StringComparison.Ordinal);
        Assert.Contains("layout(triangles, equal_spacing, ccw) in;", evaluation, StringComparison.Ordinal);
        Assert.Contains("textureLod(uHeight, uv, 0.0).r", evaluation, StringComparison.Ordinal);
        Assert.Contains("max(rawHeight - 0.5, 0.0) * 2.0", evaluation, StringComparison.Ordinal);
        Assert.Contains("uTessellationDisplacementStrength", evaluation, StringComparison.Ordinal);
        Assert.Contains("uLightViewProj * vec4(worldPos, 1.0)", evaluation, StringComparison.Ordinal);
    }

    [Fact]
    public void GenesisTaaResolve_UsesReactiveYcocgHistoryClip()
    {
        var src = GlslIncludeResolver.Resolve("genesis_taa_resolve.frag", LoadShader);
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.Contains("uTaaSignal", adapted, StringComparison.Ordinal);
        Assert.Contains("uHasTaaSignal", adapted, StringComparison.Ordinal);
        Assert.Contains("objectMotion", adapted, StringComparison.Ordinal);
        Assert.Contains("smoothstep(0.08, 0.75, objectMotion)", adapted, StringComparison.Ordinal);
        Assert.Contains("smoothstep(0.18, 0.90, reactivity)", adapted, StringComparison.Ordinal);
        Assert.Contains("trNeighborhoodMinMax3YCoCg", adapted, StringComparison.Ordinal);
        Assert.Contains("trClipHistoryToNeighborhoodYCoCg", adapted, StringComparison.Ordinal);
        Assert.Contains("trLuminanceReactiveWeight", adapted, StringComparison.Ordinal);
        Assert.Contains("trDepthEdgeWeight", adapted, StringComparison.Ordinal);
        Assert.Contains("stableTemporal", adapted, StringComparison.Ordinal);
        Assert.Contains("uStableTemporalBoost", adapted, StringComparison.Ordinal);
        Assert.Contains("uMaxStableTemporal", adapted, StringComparison.Ordinal);
        Assert.Contains("uTaaSharpenStrength", adapted, StringComparison.Ordinal);
        Assert.Contains("uDepthEdgeHistoryFloor", adapted, StringComparison.Ordinal);
        Assert.Contains("uEdgeAaBlend", adapted, StringComparison.Ordinal);
        Assert.Contains("uCurrentJitterPixels", adapted, StringComparison.Ordinal);
        Assert.Contains("uCaptureTexelSize", adapted, StringComparison.Ordinal);
        Assert.Contains("uSourceFilterStrength", adapted, StringComparison.Ordinal);
        Assert.Contains("uSilhouetteHistoryWeight", adapted, StringComparison.Ordinal);
        Assert.Contains("uFxaaEdgeStrength", adapted, StringComparison.Ordinal);
        Assert.Contains("uFxaaLumaEdgeStrength", adapted, StringComparison.Ordinal);
        Assert.Contains("uFxaaLumaThreshold", adapted, StringComparison.Ordinal);
        Assert.Contains("uForceFxaa", adapted, StringComparison.Ordinal);
        Assert.Contains("taaCurrentResolveFilter", adapted, StringComparison.Ordinal);
        Assert.Contains("taaFxaaLite", adapted, StringComparison.Ordinal);
        Assert.Contains("taaTentBlur3x3", adapted, StringComparison.Ordinal);
        Assert.Contains("taaMorphologicalEdgeBlend", adapted, StringComparison.Ordinal);
        Assert.Contains("taaClosestGeometryDepth3", adapted, StringComparison.Ordinal);
        Assert.Contains("taaLumaEdgeMask", adapted, StringComparison.Ordinal);
        Assert.Contains("depthGeometryW", adapted, StringComparison.Ordinal);
        Assert.Contains("signalGeometryW", adapted, StringComparison.Ordinal);
        Assert.Contains("silhouetteW", adapted, StringComparison.Ordinal);
        Assert.Contains("depthFxaaMask", adapted, StringComparison.Ordinal);
        Assert.Contains("lumaFxaaMask", adapted, StringComparison.Ordinal);
        Assert.Contains("geometryFxaaW", adapted, StringComparison.Ordinal);
        Assert.Contains("fxaaEdgeMask", adapted, StringComparison.Ordinal);
        Assert.Contains("edgeHistoryW", adapted, StringComparison.Ordinal);
        Assert.Contains("postFxaaW", adapted, StringComparison.Ordinal);
        Assert.Contains("acrossNormal", adapted, StringComparison.Ordinal);
        Assert.Contains("depthGrad", adapted, StringComparison.Ordinal);
        Assert.Contains("forceFullFrame", adapted, StringComparison.Ordinal);
        Assert.Contains("uForceFxaa > 0", adapted, StringComparison.Ordinal);
        Assert.Contains("thresholdLow", adapted, StringComparison.Ordinal);
        Assert.Contains("edgeMask * strength", adapted, StringComparison.Ordinal);
        Assert.Contains("coverageW", adapted, StringComparison.Ordinal);
        Assert.Contains("reprojectionDepth", adapted, StringComparison.Ordinal);
        Assert.Contains("rawDepthEdgeW", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void TemporalReproject_ProvidesYcocgAndReactiveHelpers()
    {
        var src = GlslIncludeResolver.Resolve("common/temporal_reproject.glsl", LoadShader);

        Assert.Contains("vec3 trRgbToYCoCg", src, StringComparison.Ordinal);
        Assert.Contains("vec3 trYCoCgToRgb", src, StringComparison.Ordinal);
        Assert.Contains("float trLuminanceReactiveWeight", src, StringComparison.Ordinal);
        Assert.Contains("float trDepthEdgeWeight", src, StringComparison.Ordinal);
    }

    [Fact]
    public void GenesisFragment_WritesTaaSignalForGeometryReactivity()
    {
        var src = GlslIncludeResolver.Resolve("genesis.frag", LoadShader);
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.Contains("layout(location = 1) out vec4 TaaSignal", adapted, StringComparison.Ordinal);
        Assert.Contains("alphaEdge", adapted, StringComparison.Ordinal);
        Assert.Contains("vPrevClip", adapted, StringComparison.Ordinal);
        Assert.Contains("motion = clamp", adapted, StringComparison.Ordinal);
        Assert.Contains("uEntityAlphaMode == 1", adapted, StringComparison.Ordinal);
        Assert.Contains("uEntityAlphaMode == 2", adapted, StringComparison.Ordinal);
        Assert.Contains("TaaSignal = vec4", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void GenesisFragment_GatesSpecularLobeAndDithersSrgbOutput()
    {
        var src = GlslIncludeResolver.Resolve("genesis.frag", LoadShader);
        var adapted = GlslSourceAdapter.Adapt(src, ShaderType.FragmentShader, useOpenGlEs: true);

        Assert.Contains("float specLobe = uEnableSpecularMap > 0 ? 1.0 : 0.0;", adapted, StringComparison.Ordinal);
        Assert.Contains("br.specular *= groundSpecFade * specLobe;", adapted, StringComparison.Ordinal);
        Assert.Contains("ditherSrgb8(linearToSrgb(mapped), gl_FragCoord.xy)", adapted, StringComparison.Ordinal);
    }

    [Fact]
    public void GenesisVertex_EmitsPreviousClipForTaaMotionSignal()
    {
        var src = GlslIncludeResolver.Resolve("genesis.vert", LoadShader);

        Assert.Contains("uniform mat4 uPrevModel", src, StringComparison.Ordinal);
        Assert.Contains("uniform mat4 uPrevViewProj", src, StringComparison.Ordinal);
        Assert.Contains("uniform mat4 uTaaCurrViewProj", src, StringComparison.Ordinal);
        Assert.Contains("EntityPrevSkinningBones", src, StringComparison.Ordinal);
        Assert.Contains("uEntityPrevBonePaletteValid", src, StringComparison.Ordinal);
        Assert.Contains("uniform vec2 uTextureAtlasScale", src, StringComparison.Ordinal);
        Assert.Contains("vUv = aUv * uTextureAtlasScale", src, StringComparison.Ordinal);
        Assert.Contains("prevEntityPos = prevBone * vec4(aPos, 1.0)", src, StringComparison.Ordinal);
        Assert.Contains("out vec4 vPrevClip", src, StringComparison.Ordinal);
        Assert.Contains("vCurrClip = uTaaCurrViewProj * wp", src, StringComparison.Ordinal);
        Assert.Contains("vPrevClip = uPrevViewProj * uPrevModel * prevEntityPos", src, StringComparison.Ordinal);
        Assert.Contains("gl_Position = clip", src, StringComparison.Ordinal);
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
