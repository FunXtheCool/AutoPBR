using System.Diagnostics;
using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private GlShaderProgram? _scenePresentProgram;
    private GlShaderProgram? _screenSpaceGodRayProgram;
    private GlShaderProgram? _shadowAwareGodRayProgram;
    private GlShaderProgram? _godRayUpsampleProgram;
    private GlShaderProgram? _godRayCompositeProgram;
    private GlSceneCaptureTarget? _sceneCapture;
    private GlColorRenderTarget? _godRayHalfResTarget;
    private GlColorRenderTarget? _godRayResolveTarget;
    private GlColorRenderTarget? _godRayHistoryTarget;
    private uint _godRayQuadVao;
    private uint _godRayQuadVbo;
    private int _godRayBlitFailLogged;
    private int _screenSpaceGodRayLogged;
    private int _shadowAwareGodRayLogged;
    private Matrix4x4 _godRayPrevViewProj = Matrix4x4.Identity;
    private bool _godRayHistoryValid;
    private int _godRayHistoryVw;
    private int _godRayHistoryVh;
    private int _volumePathFailLogged;
    private double _lastVolumetricTimingLogMs;
    private bool _prevEnableGodRays;
    private bool _prevEnableVolumetricClouds;
    private bool _prevGodRayStabilizeDebug = true;
    private bool _prevCloudDisableTemporal;

    private void TryInitGodRaysCore(GL gl, bool useOpenGlEs)
    {
        DestroyGodRayResources();
        _godRayInitFailureDetail = null;
        if (!TryInitSceneCaptureCore(gl, useOpenGlEs, out var sceneErr))
        {
            _godRayInitFailureDetail = "scene-capture: " + TrimShaderDiagnostic(sceneErr);
            EmitDiagnostic("[3D preview] Scene capture shader: " + TrimShaderDiagnostic(sceneErr));
            DestroyGodRayResources();
            return;
        }

        _godRayHalfResTarget = new GlColorRenderTarget(gl, useOpenGlEs);
        _godRayResolveTarget = new GlColorRenderTarget(gl, useOpenGlEs);
        _godRayHistoryTarget = new GlColorRenderTarget(gl, useOpenGlEs);

        _screenSpaceGodRayProgram = CreatePreviewProgram("genesis_godrays.vert", "genesis_godrays.frag", out var ssErr);
        if (_screenSpaceGodRayProgram is not { IsValid: true })
        {
            _godRayInitFailureDetail = "screen-space: " + TrimShaderDiagnostic(ssErr);
            EmitDiagnostic("[3D preview] Screen-space god-ray shader: " + TrimShaderDiagnostic(ssErr));
            DestroyGodRayResources();
            return;
        }

        _godRayUpsampleProgram = CreatePreviewProgram("genesis_godrays.vert", "genesis_godrays_upsample.frag", out var upErr);
        if (_godRayUpsampleProgram is not { IsValid: true })
        {
            _godRayInitFailureDetail = "upsample: " + TrimShaderDiagnostic(upErr);
            EmitDiagnostic("[3D preview] God-ray upsample shader: " + TrimShaderDiagnostic(upErr));
            DestroyGodRayResources();
            return;
        }

        _godRayCompositeProgram = CreatePreviewProgram("genesis_godrays.vert", "genesis_godrays_composite.frag", out var compErr);
        if (_godRayCompositeProgram is not { IsValid: true })
        {
            _godRayInitFailureDetail = "composite: " + TrimShaderDiagnostic(compErr);
            EmitDiagnostic("[3D preview] God-ray composite shader: " + TrimShaderDiagnostic(compErr));
            DestroyGodRayResources();
            return;
        }

    }

    private bool TryInitSceneCaptureCore(GL gl, bool useOpenGlEs, out string? error)
    {
        error = null;
        _sceneCapture ??= new GlSceneCaptureTarget(gl, useOpenGlEs);
        if (_scenePresentProgram is not { IsValid: true })
        {
            _scenePresentProgram?.Dispose();
            _scenePresentProgram = CreatePreviewProgram("genesis_godrays.vert", "genesis_scene_present.frag", out error);
            if (_scenePresentProgram is not { IsValid: true })
            {
                _scenePresentProgram?.Dispose();
                _scenePresentProgram = null;
                return false;
            }
        }

        if (_godRayQuadVao == 0 || _godRayQuadVbo == 0)
        {
            CreateSceneFullscreenQuad(gl);
        }

        return _sceneCapture is not null &&
               _scenePresentProgram is { IsValid: true } &&
               _godRayQuadVao != 0;
    }

    private void CreateSceneFullscreenQuad(GL gl)
    {
        if (_godRayQuadVbo != 0)
        {
            gl.DeleteBuffer(_godRayQuadVbo);
            _godRayQuadVbo = 0;
        }

        if (_godRayQuadVao != 0)
        {
            gl.DeleteVertexArray(_godRayQuadVao);
            _godRayQuadVao = 0;
        }

        Span<float> quad =
        [
            -1f, -1f, 1f, -1f, 1f, 1f,
            -1f, -1f, 1f, 1f, -1f, 1f
        ];
        _godRayQuadVao = gl.GenVertexArray();
        _godRayQuadVbo = gl.GenBuffer();
        gl.BindVertexArray(_godRayQuadVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _godRayQuadVbo);
        gl.BufferData<float>(GLEnum.ArrayBuffer, quad, GLEnum.StaticDraw);
        unsafe
        {
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        }

        gl.BindVertexArray(0);
    }

    private void DestroyGodRayResources()
    {
        var gl = _gl;
        _sceneCapture?.Dispose();
        _sceneCapture = null;
        _godRayHalfResTarget?.Dispose();
        _godRayHalfResTarget = null;
        _godRayResolveTarget?.Dispose();
        _godRayResolveTarget = null;
        _godRayHistoryTarget?.Dispose();
        _godRayHistoryTarget = null;
        _godRayUpsampleProgram?.Dispose();
        _godRayUpsampleProgram = null;
        _screenSpaceGodRayProgram?.Dispose();
        _screenSpaceGodRayProgram = null;
        _shadowAwareGodRayProgram?.Dispose();
        _shadowAwareGodRayProgram = null;
        _scenePresentProgram?.Dispose();
        _scenePresentProgram = null;
        _godRayCompositeProgram?.Dispose();
        _godRayCompositeProgram = null;
        _godRayHistoryValid = false;

        if (gl is null)
        {
            _godRayQuadVao = _godRayQuadVbo = 0;
            return;
        }

        if (_godRayQuadVbo != 0)
        {
            gl.DeleteBuffer(_godRayQuadVbo);
            _godRayQuadVbo = 0;
        }

        if (_godRayQuadVao != 0)
        {
            gl.DeleteVertexArray(_godRayQuadVao);
            _godRayQuadVao = 0;
        }
    }

    private bool CanUseGodRayCapture(in PreviewRenderSettings settings) =>
        settings.EnableGodRays &&
        _sceneCapture is not null &&
        _scenePresentProgram is { IsValid: true } &&
        _screenSpaceGodRayProgram is { IsValid: true } &&
        _godRayCompositeProgram is { IsValid: true } &&
        _godRayHalfResTarget is not null &&
        _godRayResolveTarget is not null &&
        _godRayHistoryTarget is not null &&
        _godRayQuadVao != 0;

    private bool CanUseTaaSceneCapture(in PreviewRenderSettings settings) =>
        IsPreviewTaaActive(settings) &&
        _sceneCapture is not null &&
        _scenePresentProgram is { IsValid: true } &&
        _godRayQuadVao != 0;

    private void SyncGodRayToggleState(in PreviewRenderSettings settings)
    {
        var godRaysChanged = _prevEnableGodRays != settings.EnableGodRays;
        var stabilizeChanged = _prevGodRayStabilizeDebug != settings.GodRayStabilizeDebug;
        if (!godRaysChanged && !stabilizeChanged)
        {
            return;
        }

        _prevEnableGodRays = settings.EnableGodRays;
        _prevGodRayStabilizeDebug = settings.GodRayStabilizeDebug;
        _godRayHistoryValid = false;
        _volumeFroxelHistoryValid = false;
        _volumeIntegrateHistoryValid = false;
        _volumePathFailLogged = 0;
        _screenSpaceGodRayLogged = 0;
        _shadowAwareGodRayLogged = 0;
        _godRayBlitFailLogged = 0;
        _loggedCloudDraw = false;
        _cloudHistoryValid = false;
        _taaHistoryValid = false;
    }

    private void SyncVolumetricToggleState(in PreviewRenderSettings settings)
    {
        var cloudsChanged = _prevEnableVolumetricClouds != settings.EnableVolumetricClouds;
        var temporalChanged = _prevCloudDisableTemporal != settings.CloudDisableTemporal;
        if (!cloudsChanged && !temporalChanged)
        {
            return;
        }

        _prevEnableVolumetricClouds = settings.EnableVolumetricClouds;
        _prevCloudDisableTemporal = settings.CloudDisableTemporal;
        _loggedCloudDraw = false;
        _godRayHistoryValid = false;
        _volumeFroxelHistoryValid = false;
        _cloudHistoryValid = false;
        _taaHistoryValid = false;
    }

    private bool TryBeginGodRaySceneRender(ref GlRenderFrame frame)
    {
        if ((!CanUseGodRayCapture(frame.Settings) && !CanUseTaaSceneCapture(frame.Settings)) || _sceneCapture is null)
        {
            return false;
        }

        if (!_sceneCapture.EnsureSize(frame.Vw, frame.Vh))
        {
            EmitDiagnostic("[3D preview] God-ray scene target incomplete; rendering directly to the default FBO.");
            return false;
        }

        _sceneCapture.BindDraw(frame.Vw, frame.Vh);
        return true;
    }

    private void FinishGodRaySceneRender(ref GlRenderFrame frame)
    {
        if (!frame.GodRayCaptureActive || _sceneCapture is null)
        {
            return;
        }

        if (!TryPresentSceneCaptureToDefault(ref frame))
        {
            TryPresentSceneCaptureToDefault(ref frame);
            var key = frame.Vw + frame.Vh * 10000;
            if (_godRayBlitFailLogged != key)
            {
                _godRayBlitFailLogged = key;
                EmitDiagnostic("[3D preview] God-ray scene present to default FBO failed.");
            }
        }

        BindDefaultFramebuffer(ref frame);
    }

    private static void BindDefaultFramebuffer(ref GlRenderFrame frame)
    {
        if (frame.DefaultFbo != 0)
        {
            frame.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)frame.DefaultFbo);
        }
        else
        {
            frame.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        frame.Gl.Viewport(frame.VpX, frame.VpY, (uint)frame.Vw, (uint)frame.Vh);
    }

    private bool TryPresentSceneCaptureToDefault(ref GlRenderFrame frame)
    {
        if (_sceneCapture is null || _scenePresentProgram is not { IsValid: true } || _godRayQuadVao == 0)
        {
            return false;
        }

        var gl = frame.Gl;
        BindDefaultFramebuffer(ref frame);

        var priorDepthTest = gl.IsEnabled(EnableCap.DepthTest);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.Blend);
        gl.BindVertexArray(_godRayQuadVao);
        _scenePresentProgram.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.ColorTextureHandle);
        SetIntOnProgram(_scenePresentProgram, "uSceneColor", 0);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);
        if (priorDepthTest)
        {
            gl.Enable(EnableCap.DepthTest);
        }

        if (priorBlend)
        {
            gl.Enable(EnableCap.Blend);
        }

        return gl.GetError() == GLEnum.NoError;
    }

    private void TryCompositeAdditiveRays(ref GlRenderFrame frame, uint raysTexture, uint cloudMaskTexture = 0)
    {
        if (_godRayCompositeProgram is not { IsValid: true } || _godRayQuadVao == 0)
        {
            return;
        }

        var gl = frame.Gl;
        BindDefaultFramebuffer(ref frame);
        var priorDepthTest = gl.IsEnabled(EnableCap.DepthTest);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        gl.BindVertexArray(_godRayQuadVao);
        _godRayCompositeProgram.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, raysTexture);
        SetIntOnProgram(_godRayCompositeProgram, "uRays", 0);
        var hasCloudMask = cloudMaskTexture != 0;
        SetIntOnProgram(_godRayCompositeProgram, "uHasCloudMask", hasCloudMask ? 1 : 0);
        if (hasCloudMask)
        {
            gl.ActiveTexture(TextureUnit.Texture1);
            gl.BindTexture(TextureTarget.Texture2D, cloudMaskTexture);
            SetIntOnProgram(_godRayCompositeProgram, "uCloudMask", 1);
        }

        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);
        if (priorDepthTest)
        {
            gl.Enable(EnableCap.DepthTest);
        }
        else
        {
            gl.Disable(EnableCap.DepthTest);
        }

        if (!priorBlend)
        {
            gl.Disable(EnableCap.Blend);
        }
    }

    private void TryRunScreenSpaceGodRays(ref GlRenderFrame frame)
    {
        if (_screenSpaceGodRayProgram is null || _sceneCapture is null || _godRayQuadVao == 0)
        {
            return;
        }

        var aspect = frame.Vw / (float)Math.Max(frame.Vh, 1);
        var coneScale = Math.Max(frame.Settings.GodRayConeScale, 0.05f);
        var towardSun = -frame.WorldLightDir;
        var tls = towardSun.LengthSquared();
        if (tls < 1e-12f)
        {
            return;
        }

        towardSun /= MathF.Sqrt(tls);

        Vector2 lightUv;
        float discRadiusUv;
        float coneRadiusUv;
        if (towardSun.Y < 0.04f)
        {
            PreviewSunScreenProjection.ComputeMoon(frame.Eye, frame.WorldLightDir, frame.View, frame.Proj, aspect,
                out lightUv, out discRadiusUv, out _);
            coneRadiusUv = Math.Max(discRadiusUv * PreviewSunScreenProjection.ShaftScale * coneScale,
                PreviewSunScreenProjection.MinShaftRadiusUv * coneScale);
        }
        else
        {
            PreviewSunScreenProjection.Compute(frame.Eye, frame.WorldLightDir, frame.View, frame.Proj, aspect, coneScale,
                frame.Settings.AtmosphereSunDiscSize, out lightUv, out discRadiusUv, out coneRadiusUv, out _);
        }

        var gl = frame.Gl;
        BindDefaultFramebuffer(ref frame);
        var priorDepthTest = gl.IsEnabled(EnableCap.DepthTest);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        gl.BindVertexArray(_godRayQuadVao);
        _screenSpaceGodRayProgram.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.DepthTextureHandle);
        SetIntOnProgram(_screenSpaceGodRayProgram, "uSceneDepth", 0);
        SetVec2OnProgram(_screenSpaceGodRayProgram, "uSunUv", lightUv);
        SetFloatOnProgram(_screenSpaceGodRayProgram, "uSunDiscRadius", discRadiusUv);
        SetFloatOnProgram(_screenSpaceGodRayProgram, "uSunConeRadius", coneRadiusUv);
        SetFloatOnProgram(_screenSpaceGodRayProgram, "uStrength", frame.Settings.GodRayStrength);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);
        if (priorDepthTest)
        {
            gl.Enable(EnableCap.DepthTest);
        }
        else
        {
            gl.Disable(EnableCap.DepthTest);
        }

        if (!priorBlend)
        {
            gl.Disable(EnableCap.Blend);
        }

        if (_screenSpaceGodRayLogged == 0)
        {
            _screenSpaceGodRayLogged = 1;
            EmitDiagnostic("[3D preview] Screen-space god-ray fallback active.");
        }
    }

    private bool TryRunShadowAwareGodRays(ref GlRenderFrame frame)
    {
        TryEnsureShadowAwareGodRayProgram();
        if (_shadowAwareGodRayProgram is null || _sceneCapture is null || _godRayQuadVao == 0 ||
            !frame.ShadowAvailable || _shadowTarget is null)
        {
            return false;
        }

        var aspect = frame.Vw / (float)Math.Max(frame.Vh, 1);
        var coneScale = Math.Max(frame.Settings.GodRayConeScale, 0.05f);
        var towardSun = -frame.WorldLightDir;
        var tls = towardSun.LengthSquared();
        if (tls < 1e-12f)
        {
            return false;
        }

        towardSun /= MathF.Sqrt(tls);

        Vector2 lightUv;
        float discRadiusUv;
        float coneRadiusUv;
        if (towardSun.Y < 0.04f)
        {
            PreviewSunScreenProjection.ComputeMoon(frame.Eye, frame.WorldLightDir, frame.View, frame.Proj, aspect,
                out lightUv, out discRadiusUv, out _);
            coneRadiusUv = Math.Max(discRadiusUv * PreviewSunScreenProjection.ShaftScale * coneScale,
                PreviewSunScreenProjection.MinShaftRadiusUv * coneScale);
        }
        else
        {
            PreviewSunScreenProjection.Compute(frame.Eye, frame.WorldLightDir, frame.View, frame.Proj, aspect, coneScale,
                frame.Settings.AtmosphereSunDiscSize, out lightUv, out discRadiusUv, out coneRadiusUv, out _);
        }

        var viewProj = frame.Proj * frame.View;
        if (!Matrix4x4.Invert(viewProj, out var invViewProj))
        {
            return false;
        }

        var gl = frame.Gl;
        var shadowRes = _shadowTarget.Resolution;
        var shadowTexelSize = new Vector2(1f / shadowRes, 1f / shadowRes);
        var layerWorldY = PreviewStageConstants.CloudLayerBaseWorldY(frame.Settings.CloudLayerHeight);
        var cascadesActive = frame.ShadowCascadesActive;

        BindDefaultFramebuffer(ref frame);
        var priorDepthTest = gl.IsEnabled(EnableCap.DepthTest);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        gl.BindVertexArray(_godRayQuadVao);
        _shadowAwareGodRayProgram.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.DepthTextureHandle);
        SetIntOnProgram(_shadowAwareGodRayProgram, "uSceneDepth", 0);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, _shadowTarget.DepthTextureHandle);
        SetIntOnProgram(_shadowAwareGodRayProgram, "uShadowMap", 1);
        gl.ActiveTexture(TextureUnit.Texture2);
        if (cascadesActive && _shadowTargetCascadeNear is not null)
        {
            gl.BindTexture(TextureTarget.Texture2D, _shadowTargetCascadeNear.DepthTextureHandle);
        }
        else
        {
            gl.BindTexture(TextureTarget.Texture2D, _shadowTarget.DepthTextureHandle);
        }

        SetIntOnProgram(_shadowAwareGodRayProgram, "uShadowMapNear", 2);
        SetMatrixOnProgram(_shadowAwareGodRayProgram, "uInvViewProj", invViewProj);
        SetMatrixOnProgram(_shadowAwareGodRayProgram, "uLightViewProj", frame.ShadowVp);
        SetMatrixOnProgram(_shadowAwareGodRayProgram, "uLightViewProjNear", frame.ShadowVpNear);
        SetVec3OnProgram(_shadowAwareGodRayProgram, "uCameraPos", frame.Eye);
        SetVec2OnProgram(_shadowAwareGodRayProgram, "uSunUv", lightUv);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uSunDiscRadius", discRadiusUv);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uSunConeRadius", coneRadiusUv);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uStrength", frame.Settings.GodRayStrength);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uLayerHeight", layerWorldY);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uVolumeHeight", frame.Settings.CloudVolumeHeight);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uCloudDensity", frame.Settings.CloudDensity);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uVolumeSize", frame.Settings.CloudVolumeSize);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uGroundWorldY", PreviewStageConstants.GroundPlaneWorldY);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uFogSlabHeight", PreviewStageConstants.GroundFogSlabHeight);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uHeightFogStrength",
            frame.Settings.EnableAtmosphericSky ? frame.Settings.AerialFogStrength * VolumeHeightFogScale : 0f);
        SetVec2OnProgram(_shadowAwareGodRayProgram, "uShadowTexelSize", shadowTexelSize);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uShadowMinBias", frame.Settings.ShadowMinBias);
        SetIntOnProgram(_shadowAwareGodRayProgram, "uEnableShadowMap", 1);
        SetIntOnProgram(_shadowAwareGodRayProgram, "uEnableShadowCascades", cascadesActive ? 1 : 0);
        SetFloatOnProgram(_shadowAwareGodRayProgram, "uCascadeSplitDistance", frame.CascadeSplitWorldDistance);
        SetIntOnProgram(_shadowAwareGodRayProgram, "uEnableCloudAttenuation",
            frame.Settings.EnableVolumetricClouds ? 1 : 0);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);
        if (priorDepthTest)
        {
            gl.Enable(EnableCap.DepthTest);
        }
        else
        {
            gl.Disable(EnableCap.DepthTest);
        }

        if (!priorBlend)
        {
            gl.Disable(EnableCap.Blend);
        }

        if (_shadowAwareGodRayLogged == 0)
        {
            _shadowAwareGodRayLogged = 1;
            EmitDiagnostic("[3D preview] Shadow-aware god-ray fallback active.");
        }

        return true;
    }

    private void MaybeLogVolumetricTiming(in PreviewRenderSettings settings, double injectMs, double integrateMs)
    {
        if (!settings.LogVolumetricTiming)
        {
            return;
        }

        var totalMs = injectMs + integrateMs;
        if (totalMs < 2.5)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - _lastVolumetricTimingLogMs < 8000)
        {
            return;
        }

        _lastVolumetricTimingLogMs = now;
        EmitDiagnostic(
            $"[3D preview] Volumetric pass timing: inject {injectMs:F2} ms, integrate {integrateMs:F2} ms " +
            $"(budget ~2.5 ms @1080p; quality={settings.VolumetricQuality}).");
    }

    private void DrawGodRayComposite(ref GlRenderFrame frame)
    {
        SyncGodRayToggleState(frame.Settings);

        if (!frame.GodRayCaptureActive || _sceneCapture is null || !_sceneCapture.IsValid ||
            _godRayCompositeProgram is not { IsValid: true } ||
            _screenSpaceGodRayProgram is not { IsValid: true } ||
            _godRayQuadVao == 0)
        {
            return;
        }

        var halfW = Math.Max(1, frame.Vw / 2);
        var halfH = Math.Max(1, frame.Vh / 2);
        var canVolume = CanUseVolumeGodRays(frame.Settings) &&
                        _godRayUpsampleProgram is { IsValid: true } &&
                        _godRayHalfResTarget is not null &&
                        _godRayResolveTarget is not null &&
                        _godRayHistoryTarget is not null;
        if (canVolume &&
            (!_godRayHalfResTarget!.EnsureSize(halfW, halfH) ||
             !_godRayResolveTarget!.EnsureSize(frame.Vw, frame.Vh) ||
             !_godRayHistoryTarget!.EnsureSize(frame.Vw, frame.Vh)))
        {
            canVolume = false;
        }

        if (_godRayHistoryVw != frame.Vw || _godRayHistoryVh != frame.Vh)
        {
            _godRayHistoryValid = false;
            _godRayHistoryVw = frame.Vw;
            _godRayHistoryVh = frame.Vh;
        }

        var gl = frame.Gl;
        var viewProj = frame.Proj * frame.View;
        if (!Matrix4x4.Invert(viewProj, out var invViewProj))
        {
            return;
        }

        var quality = PreviewVolumetricQuality.Resolve(frame.Settings.VolumetricQuality);
        var priorDepthTest = gl.IsEnabled(EnableCap.DepthTest);
        var priorCullFace = gl.IsEnabled(EnableCap.CullFace);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        var priorDepthMask = gl.GetBoolean(GetPName.DepthWritemask);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);
        gl.DepthMask(false);
        gl.BindVertexArray(_godRayQuadVao);

        var volumeSw = frame.Settings.LogVolumetricTiming ? Stopwatch.StartNew() : null;
        var injectMs = 0.0;
        var integrateMs = 0.0;
        var usedVolumePath = canVolume && TryRunVolumeGodRayPass(ref frame, out injectMs, out integrateMs);
        if (volumeSw is not null)
        {
            volumeSw.Stop();
            MaybeLogVolumetricTiming(frame.Settings, injectMs, integrateMs);
        }

        if (!usedVolumePath)
        {
            if (_volumePathFailLogged == 0)
            {
                _volumePathFailLogged = 1;
                EmitDiagnostic(!canVolume
                    ? DescribeVolumeGodRayUnavailableReason(frame.Settings)
                    : "[3D preview] Froxel god rays inject or integrate pass failed; using screen-space fallback.");
            }

            if (!TryRunShadowAwareGodRays(ref frame))
            {
                TryRunScreenSpaceGodRays(ref frame);
            }

            gl.DepthMask(priorDepthMask);
            if (priorDepthTest)
            {
                gl.Enable(EnableCap.DepthTest);
            }
            else
            {
                gl.Disable(EnableCap.DepthTest);
            }

            if (priorCullFace)
            {
                gl.Enable(EnableCap.CullFace);
            }
            else
            {
                gl.Disable(EnableCap.CullFace);
            }

            if (!priorBlend)
            {
                gl.Disable(EnableCap.Blend);
            }

            gl.BindVertexArray(0);
            return;
        }

        // Full-res bilateral upsample + temporal reprojection.
        _godRayResolveTarget!.BindDraw();
        gl.Clear(ClearBufferMask.ColorBufferBit);
        _godRayUpsampleProgram!.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _godRayHalfResTarget!.ColorTextureHandle);
        SetIntOnProgram(_godRayUpsampleProgram!, "uHalfResRays", 0);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, _sceneCapture!.DepthTextureHandle);
        SetIntOnProgram(_godRayUpsampleProgram!, "uSceneDepth", 1);
        gl.ActiveTexture(TextureUnit.Texture2);
        gl.BindTexture(TextureTarget.Texture2D, _godRayHistoryTarget!.ColorTextureHandle);
        SetIntOnProgram(_godRayUpsampleProgram!, "uHistory", 2);
        SetMatrixOnProgram(_godRayUpsampleProgram!, "uInvViewProj", invViewProj);
        SetMatrixOnProgram(_godRayUpsampleProgram!, "uPrevViewProj", _godRayPrevViewProj);
        SetVec2OnProgram(_godRayUpsampleProgram, "uHalfResTexelSize", new Vector2(1f / halfW, 1f / halfH));
        var upsampleTemporal = frame.Settings.GodRayStabilizeDebug
            ? 0f
            : PreviewVolumetricQuality.EffectivePassTemporalWeight(
                quality.UpsampleTemporalWeight, frame.Settings);
        SetFloatOnProgram(_godRayUpsampleProgram, "uTemporalWeight", upsampleTemporal);
        SetIntOnProgram(_godRayUpsampleProgram, "uHasHistory",
            !frame.Settings.GodRayStabilizeDebug &&
            _godRayHistoryValid && upsampleTemporal > 0f ? 1 : 0);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        if (!frame.Settings.GodRayStabilizeDebug)
        {
            _godRayHistoryTarget.CopyColorFrom(_godRayResolveTarget);
            _godRayPrevViewProj = viewProj;
            _godRayHistoryValid = true;
        }

        TryCompositeAdditiveRays(ref frame, _godRayResolveTarget.ColorTextureHandle);
        gl.BindVertexArray(0);

        gl.DepthMask(priorDepthMask);
        if (priorDepthTest)
        {
            gl.Enable(EnableCap.DepthTest);
        }
        else
        {
            gl.Disable(EnableCap.DepthTest);
        }

        if (priorCullFace)
        {
            gl.Enable(EnableCap.CullFace);
        }
        else
        {
            gl.Disable(EnableCap.CullFace);
        }

        if (!priorBlend)
        {
            gl.Disable(EnableCap.Blend);
        }
    }

    private void SetVec2OnProgram(GlShaderProgram program, string name, Vector2 v)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform2(loc, v.X, v.Y);
        }
    }
}
