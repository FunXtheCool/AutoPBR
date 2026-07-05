using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.OpenGL;

using System.Numerics;

namespace AutoPBR.App.Tests;

public sealed partial class PreviewRenderingTests
{
    [Fact]
    public void RenderSettingsDefaultsAreUsable()
    {
        var s = new PreviewRenderSettings();
        Assert.Equal(1f, s.NormalStrength);
        Assert.True(s.EnableParallax);
        Assert.True(s.NearestTextureFilter);
        Assert.True(s.ShowBackgroundGrid);
        Assert.True(s.ShowGroundMesh);
        Assert.True(s.ShowCornerAxes);
        Assert.True(s.DrawPreviewSubject);
        Assert.Equal(PreviewEntityAlphaMode.Cutout, s.EntityAlphaMode);
        Assert.True(s.EnableEntityLabPbrShading);
        Assert.False(s.EnableEntityParallax);
    }

    [Fact]
    public void RenderSettingsGenesisDefaultsAreSensible()
    {
        var s = new PreviewRenderSettings();
        Assert.True(s.EnableSss);
        Assert.True(s.EnableParallaxShadow);
        Assert.Equal(64, s.ParallaxTraceLayers);
        Assert.Equal(5, s.ParallaxRefineSteps);
        Assert.Equal(24, s.ParallaxShadowSamples);
        Assert.Equal(1.25f, s.ParallaxShadowSoftness);
        Assert.Equal(0.45f, s.ParallaxMaxUvShift);
        Assert.True(s.EnableIbl);
        Assert.True(s.EnableAtmosphericSky);
        Assert.Equal(2.6f, s.AtmosphereTurbidity);
        Assert.Equal(10f, s.AtmosphereSunIntensity);
        Assert.Equal(1.35f, s.AtmosphereHorizonFalloff);
        Assert.Equal(0.35f, s.AtmosphereSunDiscStrength);
        Assert.Equal(1f, s.AtmosphereSunDiscBrightness);
        Assert.Equal(1.35f, s.AtmosphereMoonDiscStrength);
        Assert.Equal(1f, s.AtmosphereMoonDiscSize);
        Assert.Equal(0.7f, s.AtmosphereMoonGlowStrength);
        Assert.Equal(1.25f, s.AtmosphereMoonTextureSharpness);
        Assert.Equal(1f, s.MoonWorldLightIntensity);
        Assert.Equal(1f, s.SssStrength);
        Assert.Equal(0.6f, s.IblStrength);
        Assert.Equal(1f, s.EmissionStrength);
    }

    [Fact]
    public void RenderSettingsShadowDefaultsAreSensible()
    {
        var s = new PreviewRenderSettings();
        Assert.True(s.EnableShadows);
        Assert.Equal(1024, s.ShadowMapResolution);
        Assert.Equal(0.0008f, s.ShadowMinBias);
        Assert.Equal(0.005f, s.ShadowMaxBias);
        Assert.Equal(2.25f, s.ShadowSoftnessTexels);
        // Phase 3 stub: persisted boolean only, defaults to false in Phase 2.
        Assert.False(s.EnableShadowCascades);
    }

    [Fact]
    public void RenderSettingsVolumetricDefaultsAreSensible()
    {
        var s = new PreviewRenderSettings();
        Assert.True(s.EnableGodRays);
        Assert.True(s.EnableVolumeGodRays);
        Assert.False(s.EnableVolumetricClouds);
        Assert.Equal(1, s.VolumetricQuality);
        Assert.Equal(0.45f, s.GodRayStrength);
        Assert.False(s.LogVolumetricTiming);
    }

    [Fact]
    public void ScenePass_GroundParallaxUsesGlobalToggleNotEntityGate()
    {
        var source = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Render.PassScene.cs");

        Assert.Contains("var groundParallax = frame.Settings.EnableParallax && _grassGroundHasHeight;", source, StringComparison.Ordinal);
        Assert.Contains("SetInt(\"uEnableParallaxAo\", groundParallax && frame.Settings.EnableParallaxAo ? 1 : 0);", source, StringComparison.Ordinal);
        Assert.Contains("SetInt(\"uEnableParallaxShadow\", groundParallax && frame.Settings.EnableParallaxShadow ? 1 : 0);", source, StringComparison.Ordinal);
        Assert.Contains("SetInt(\"uEnableParallax\", frame.EnableParallaxEff ? 1 : 0);", source, StringComparison.Ordinal);
        Assert.Contains("SetInt(\"uEnableParallaxAo\", frame.EnableParallaxAoEff ? 1 : 0);", source, StringComparison.Ordinal);
        Assert.Contains("SetInt(\"uEnableParallaxShadow\", frame.EnableParallaxShadowEff ? 1 : 0);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ScenePass_EntityParallaxCanBeDisabledPerBatchWithoutAffectingBlocks()
    {
        var source = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Render.PassScene.cs");

        Assert.Contains("var batchAllowsParallax = !frame.EntityEmulatedPreview || batch.EnableParallax;", source, StringComparison.Ordinal);
        Assert.Contains("var batchParallax = frame.EnableParallaxEff && batchAllowsParallax && bHasH;", source, StringComparison.Ordinal);
        Assert.Contains("SetInt(\"uEnableParallax\", batchParallax ? 1 : 0);", source, StringComparison.Ordinal);
        Assert.Contains("? EntityParallaxUvScale(slot)", source, StringComparison.Ordinal);
        Assert.Contains("return Math.Clamp(16f / atlasMax, 0.02f, 1f);", source, StringComparison.Ordinal);
        Assert.Contains("batchAllowsParallax &&", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectionJitter_ShiftsClipSpaceBySubpixelNdc()
    {
        var projection = PreviewGlMatrices.CreatePerspectiveFieldOfViewOpenGl(
            60f * (MathF.PI / 180f),
            16f / 9f,
            0.1f,
            100f);
        var jittered = PreviewGlMatrices.ApplyProjectionJitter(projection, new Vector2(0.002f, -0.003f));
        var viewPos = new Vector4(0f, 0f, -5f, 1f);
        var baseClip = Vector4.Transform(viewPos, Matrix4x4.Transpose(projection));
        var jitteredClip = Vector4.Transform(viewPos, Matrix4x4.Transpose(jittered));

        Assert.Equal(baseClip.W, jitteredClip.W, 0.0001f);
        Assert.Equal(baseClip.X + 0.002f * baseClip.W, jitteredClip.X, 0.0001f);
        Assert.Equal(baseClip.Y - 0.003f * baseClip.W, jitteredClip.Y, 0.0001f);
    }

    [Fact]
    public void ScenePass_AppliesProjectionJitterOnlyWhenPreviewTaaActive()
    {
        var source = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Render.PassScene.cs");

        Assert.Contains("SyncPreviewTaaToggleState(frame.Settings);", source, StringComparison.Ordinal);
        Assert.Contains("if (IsPreviewTaaActive(frame.Settings))", source, StringComparison.Ordinal);
        Assert.Contains("PreviewGlMatrices.ApplyProjectionJitter", source, StringComparison.Ordinal);
        Assert.Contains("CurrentPreviewTaaJitter(frame.Vw, frame.Vh)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PostPass_AppliesPreviewTaaAfterFullSceneComposite()
    {
        var source = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Render.PassPost.cs");

        var godRays = source.IndexOf("DrawGodRayComposite(ref frame)", StringComparison.Ordinal);
        var clouds = source.IndexOf("CompositeCloudRenderTargetToDefault(ref frame)", StringComparison.Ordinal);
        var axes = source.IndexOf("DrawCornerAxes(", StringComparison.Ordinal);
        var taa = source.IndexOf("DrawPreviewTaa(ref frame);", StringComparison.Ordinal);
        var fingerprint = source.IndexOf("MaybeLogPreviewFingerprint(ref frame);", StringComparison.Ordinal);

        Assert.True(godRays >= 0);
        Assert.True(clouds >= 0);
        Assert.True(axes >= 0);
        Assert.True(taa > godRays);
        Assert.True(taa > clouds);
        Assert.True(taa > axes);
        Assert.True(fingerprint > taa);
    }

    [Fact]
    public void ColorRenderTarget_DefaultFramebufferCopyReadsBackBuffer()
    {
        var source = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "GlColorRenderTarget.cs");

        Assert.Contains(
            "gl.ReadBuffer(readFramebuffer == 0 ? ReadBufferMode.Back : ReadBufferMode.ColorAttachment0);",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SceneCapture_ProvidesTaaSignalAttachment()
    {
        var source = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "GlSceneCaptureTarget.cs");

        Assert.Contains("TaaSignalTextureHandle", source, StringComparison.Ordinal);
        Assert.Contains("FramebufferAttachment.ColorAttachment1", source, StringComparison.Ordinal);
        Assert.Contains("DrawBufferMode.ColorAttachment1", source, StringComparison.Ordinal);
        Assert.Contains("InternalFormat.Rgba8", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewTaa_BindsSceneCaptureSignalWhenAvailable()
    {
        var source = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Taa.cs");

        Assert.Contains("var hasTaaSignal", source, StringComparison.Ordinal);
        Assert.Contains("TextureUnit.Texture3", source, StringComparison.Ordinal);
        Assert.Contains("TaaSignalTextureHandle", source, StringComparison.Ordinal);
        Assert.Contains("\"uHasTaaSignal\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewTaa_CapturesPreviousEntityBonePaletteForSkinnedMotion()
    {
        var backend = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.cs");
        var lifecycle = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Lifecycle.cs");
        var setup = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Render.PassSetup.cs");
        var taa = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Taa.cs");

        Assert.Contains("EntityPrevSkinningUboBindingPoint = 3", backend, StringComparison.Ordinal);
        Assert.Contains("EntityPrevSkinningBones", lifecycle, StringComparison.Ordinal);
        Assert.Contains("uEntityPrevBonePaletteValid", lifecycle, StringComparison.Ordinal);
        Assert.Contains("UploadPreviousEntitySkinningBoneMatrices(frame.Gl);", setup, StringComparison.Ordinal);
        Assert.Contains("CapturePreviousEntitySkinningBones();", taa, StringComparison.Ordinal);
        Assert.Contains("InvalidatePreviousEntitySkinningBones();", taa, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewTaa_InitializesSceneCaptureWithoutRequiringGodRays()
    {
        var taa = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Taa.cs");
        var godRays = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.GodRays.cs");
        var post = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Render.PassPost.cs");

        Assert.Contains("TryInitSceneCaptureCore(gl, useOpenGlEs", taa, StringComparison.Ordinal);
        Assert.Contains("CanUseTaaSceneCapture", godRays, StringComparison.Ordinal);
        Assert.Contains("!CanUseGodRayCapture(frame.Settings) && !CanUseTaaSceneCapture(frame.Settings)", godRays, StringComparison.Ordinal);
        Assert.Contains("frame.Settings.EnableGodRays && frame.GodRayCaptureActive", post, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewTaa_InvalidatesHistoryWhenSceneInputsChange()
    {
        var backend = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.cs");
        var taa = LoadSource(ThisFilePath(),
            "src",
            "AutoPBR.App",
            "Rendering",
            "OpenGL",
            "OpenGlPreviewBackend.Taa.cs");

        Assert.Contains("private void InvalidatePreviewTaaHistory()", taa, StringComparison.Ordinal);
        Assert.Contains("SetScene(IRenderPreviewScene scene)", backend, StringComparison.Ordinal);
        Assert.Contains("SetMaterial(PreviewMaterial? material)", backend, StringComparison.Ordinal);
        Assert.Contains("SetBlockModelPreview(PreviewModelSubject? subject", backend, StringComparison.Ordinal);
        Assert.True(backend.Split("InvalidatePreviewTaaHistory()", StringSplitOptions.None).Length >= 5);
    }

    private static string ThisFilePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") =>
        sourceFilePath;

    private static string LoadSource(string sourceFilePath, params string[] pathParts)
    {
        var sourceDir = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        foreach (var start in new[] { sourceDir, AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var path = Path.Combine([dir.FullName, .. pathParts]);
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }

                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException($"Could not locate source file '{Path.Combine(pathParts)}'.");
    }
}
