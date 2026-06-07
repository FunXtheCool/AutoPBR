using System.Diagnostics;
using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private GlShaderProgram? _volumeInjectProgram;
    private GlShaderProgram? _volumeIntegrateProgram;
    private bool _volumeUseLiteShaders;
    private string? _volumeInitFailureDetail;
    private string? _godRayInitFailureDetail;
    private GlVolumeFroxelTarget? _volumeFroxelTarget;
    private GlColorRenderTarget? _volumeCloudTarget;
    private GlColorRenderTarget? _volumeIntegrateHistory;
    private float _volumeJitter;
    private Matrix4x4 _volumePrevViewProj = Matrix4x4.Identity;
    private bool _volumeIntegrateHistoryValid;
    private int _volumeHistoryHalfW;
    private int _volumeHistoryHalfH;

    private const float VolumeHeightFogScale = 0.42f;

    private static float ResolveCloudLayerWorldBase(in PreviewRenderSettings settings) =>
        PreviewStageConstants.CloudLayerBaseWorldY(settings.CloudLayerHeight);

    private void TryInitVolume(GL gl, bool useOpenGlEs)
    {
        DestroyVolumeResources();
        _volumeUseLiteShaders = false;
        _volumeInitFailureDetail = null;
        if (TryLoadVolumePrograms(gl, useOpenGlEs, lite: false))
        {
            EmitDiagnostic("[3D preview] Volume shaders ready (gles-pack rev 4, full path).");
            return;
        }

        EmitDiagnostic("[3D preview] Full volume shaders failed; trying lite god-ray path.");
        DestroyVolumeResources();
        _volumeUseLiteShaders = true;
        if (TryLoadVolumePrograms(gl, useOpenGlEs, lite: true))
        {
            EmitDiagnostic("[3D preview] Volume lite god-ray path ready (gles-pack rev 4).");
            return;
        }

        EmitDiagnostic("[3D preview] Volume lite shaders failed; froxel god rays disabled.");
        if (!string.IsNullOrWhiteSpace(_volumeInitFailureDetail))
        {
            EmitDiagnostic("[3D preview] Volume shader init detail: " + _volumeInitFailureDetail);
        }

        DestroyVolumeResources();
        _volumeUseLiteShaders = false;
    }

    private static string TrimShaderDiagnostic(string? error, int maxLen = 360)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "link failed";
        }

        var oneLine = error.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (oneLine.Contains("  ", StringComparison.Ordinal))
        {
            oneLine = oneLine.Replace("  ", " ", StringComparison.Ordinal);
        }

        return oneLine.Length <= maxLen ? oneLine : oneLine[..maxLen] + "...";
    }

    private void RecordVolumeShaderFailure(bool lite, string stage, string? error)
    {
        var label = lite ? "lite" : "full";
        var entry = $"{label} {stage}: {TrimShaderDiagnostic(error)}";
        _volumeInitFailureDetail = string.IsNullOrEmpty(_volumeInitFailureDetail)
            ? entry
            : _volumeInitFailureDetail + " | " + entry;
    }

    private bool TryLoadVolumePrograms(GL gl, bool useOpenGlEs, bool lite)
    {
        _volumeFroxelTarget = new GlVolumeFroxelTarget(gl, useOpenGlEs);
        _volumeCloudTarget = new GlColorRenderTarget(gl, useOpenGlEs);
        _volumeIntegrateHistory = new GlColorRenderTarget(gl, useOpenGlEs);

        var injectFile = lite ? "genesis_volume_inject_lite.frag" : "genesis_volume_inject.frag";
        var integrateFile = lite ? "genesis_volume_integrate_lite.frag" : "genesis_volume_integrate.frag";

        _volumeInjectProgram = new GlShaderProgram(gl, "genesis_godrays.vert", injectFile, useOpenGlEs, out var injectErr);
        if (_volumeInjectProgram is not { IsValid: true })
        {
            RecordVolumeShaderFailure(lite, "inject", injectErr);
            EmitDiagnostic("[3D preview] Volume inject shader: " + TrimShaderDiagnostic(injectErr));
            return false;
        }

        _volumeIntegrateProgram = new GlShaderProgram(gl, "genesis_godrays.vert", integrateFile, useOpenGlEs, out var intErr);
        if (_volumeIntegrateProgram is not { IsValid: true })
        {
            RecordVolumeShaderFailure(lite, "integrate", intErr);
            EmitDiagnostic("[3D preview] Volume integrate shader: " + TrimShaderDiagnostic(intErr));
            return false;
        }

        return true;
    }

    private void DestroyVolumeResources()
    {
        _volumeFroxelTarget?.Dispose();
        _volumeFroxelTarget = null;
        _volumeCloudTarget?.Dispose();
        _volumeCloudTarget = null;
        _volumeIntegrateHistory?.Dispose();
        _volumeIntegrateHistory = null;
        _volumeInjectProgram?.Dispose();
        _volumeInjectProgram = null;
        _volumeIntegrateProgram?.Dispose();
        _volumeIntegrateProgram = null;
        _volumeIntegrateHistoryValid = false;
        _volumeUseLiteShaders = false;
    }

    private bool CanUseVolumeGodRays(in PreviewRenderSettings settings) =>
        settings is { EnableVolumeGodRays: true, EnableGodRays: true } &&
        _volumeFroxelTarget is not null &&
        _volumeInjectProgram is { IsValid: true } &&
        _volumeIntegrateProgram is { IsValid: true } &&
        _sceneCapture is { IsValid: true } &&
        _godRayQuadVao != 0;

    private bool CanUseUnifiedVolumetrics(in PreviewRenderSettings settings) =>
        CanUseVolumeGodRays(settings) && settings.EnableVolumetricClouds && !_volumeUseLiteShaders;

    private static Vector3 ComputeFroxelHalfExtent(float fovRadians, float aspect, float forwardDistance)
    {
        var halfY = MathF.Tan(fovRadians * 0.5f) * forwardDistance * 0.52f;
        var halfX = halfY * aspect * 0.62f;
        return new Vector3(halfX, halfY, forwardDistance * 0.5f);
    }

    private static void ComputeCameraBasis(Vector3 eye, Vector3 lookTarget, out Vector3 right, out Vector3 up, out Vector3 forward)
    {
        forward = lookTarget - eye;
        if (forward.LengthSquared() < 1e-12f)
        {
            forward = -Vector3.UnitZ;
        }
        else
        {
            forward = Vector3.Normalize(forward);
        }

        right = Vector3.Cross(forward, Vector3.UnitY);
        if (right.LengthSquared() < 1e-10f)
        {
            right = Vector3.Cross(forward, Vector3.UnitZ);
        }

        right = Vector3.Normalize(right);
        up = Vector3.Normalize(Vector3.Cross(right, forward));
    }

    private static Vector3 ResolveVolumeHalfExtent(ref GlRenderFrame frame)
    {
        var cam = frame.Scene.Camera;
        var fovRad = cam.FieldOfViewDegrees * (MathF.PI / 180f);
        var aspect = frame.Vw / (float)Math.Max(frame.Vh, 1);
        var layerBase = ResolveCloudLayerWorldBase(frame.Settings);
        ComputeCameraBasis(frame.Eye, frame.LookTarget, out _, out _, out var camForward);

        // World-anchor froxel depth to the cloud slab instead of a fixed camera-relative distance.
        var cloudTop = layerBase + frame.Settings.CloudVolumeHeight;
        var verticalSpan = Math.Max(cloudTop - frame.Eye.Y, 12f);
        var forwardDist = Math.Clamp(verticalSpan / Math.Max(camForward.Y, 0.12f), 28f, 96f);
        return ComputeFroxelHalfExtent(fovRad, aspect, forwardDist);
    }

    private static float ResolveVolumeHeightFogStrength(in PreviewRenderSettings settings) =>
        settings.EnableAtmosphericSky ? settings.AerialFogStrength * VolumeHeightFogScale : 0f;

    private bool InjectVolumeFroxels(ref GlRenderFrame frame, Vector3 halfExtent, int froxelW, int froxelH, int froxelSlices)
    {
        if (_volumeFroxelTarget is null || _volumeInjectProgram is null)
        {
            return false;
        }

        if (!_volumeFroxelTarget.EnsureSize(froxelW, froxelH, froxelSlices))
        {
            return false;
        }

        var gl = frame.Gl;
        while (gl.GetError() != GLEnum.NoError)
        {
        }

        var injectSw = frame.Settings.LogVolumetricTiming ? Stopwatch.StartNew() : null;
        ComputeCameraBasis(frame.Eye, frame.LookTarget, out var camRight, out var camUp, out var camForward);

        var shadowAvailable = frame.ShadowAvailable && _shadowTarget is not null;
        var shadowRes = _shadowTarget?.Resolution ?? Math.Clamp(frame.Settings.ShadowMapResolution, 256, 4096);
        var shadowTexelSize = new Vector2(1f / shadowRes, 1f / shadowRes);
        var layerWorldY = ResolveCloudLayerWorldBase(frame.Settings);

        _volumeInjectProgram.Use();
        SetVec3OnProgram(_volumeInjectProgram, "uCameraPos", frame.Eye);
        SetVec3OnProgram(_volumeInjectProgram, "uCamRight", camRight);
        SetVec3OnProgram(_volumeInjectProgram, "uCamUp", camUp);
        SetVec3OnProgram(_volumeInjectProgram, "uCamForward", camForward);
        SetVec3OnProgram(_volumeInjectProgram, "uLightDir", frame.LightDir);
        SetVec3OnProgram(_volumeInjectProgram, "uLightColor", frame.Scene.Light.Color);
        SetVec3OnProgram(_volumeInjectProgram, "uHalfExtent", halfExtent);
        SetIntOnProgram(_volumeInjectProgram, "uSliceCount", _volumeFroxelTarget.Slices);
        SetFloatOnProgram(_volumeInjectProgram, "uLayerHeight", layerWorldY);
        SetFloatOnProgram(_volumeInjectProgram, "uVolumeHeight", frame.Settings.CloudVolumeHeight);
        SetFloatOnProgram(_volumeInjectProgram, "uCloudDensity", frame.Settings.CloudDensity);
        SetFloatOnProgram(_volumeInjectProgram, "uVolumeSize", frame.Settings.CloudVolumeSize);
        SetFloatOnProgram(_volumeInjectProgram, "uGroundWorldY", PreviewStageConstants.GroundPlaneWorldY);
        SetFloatOnProgram(_volumeInjectProgram, "uFogSlabHeight", PreviewStageConstants.GroundFogSlabHeight);
        SetFloatOnProgram(_volumeInjectProgram, "uHeightFogStrength", ResolveVolumeHeightFogStrength(frame.Settings));
        if (!_volumeUseLiteShaders)
        {
            SetMatrixOnProgram(_volumeInjectProgram, "uLightViewProj", frame.ShadowVp);
            SetMatrixOnProgram(_volumeInjectProgram, "uLightViewProjNear", frame.ShadowVpNear);
            SetVec2OnProgram(_volumeInjectProgram, "uShadowTexelSize", shadowTexelSize);
            SetFloatOnProgram(_volumeInjectProgram, "uShadowMinBias", frame.Settings.ShadowMinBias);
            var cascadesActive = shadowAvailable && frame.ShadowCascadesActive;
            SetIntOnProgram(_volumeInjectProgram, "uEnableShadowMap", shadowAvailable ? 1 : 0);
            SetIntOnProgram(_volumeInjectProgram, "uEnableShadowCascades", cascadesActive ? 1 : 0);
            SetFloatOnProgram(_volumeInjectProgram, "uCascadeSplitDistance", frame.CascadeSplitWorldDistance);
            gl.ActiveTexture(TextureUnit.Texture0);
            if (_shadowTarget is not null)
            {
                gl.BindTexture(TextureTarget.Texture2D, _shadowTarget.DepthTextureHandle);
            }
            else if (_sceneCapture is not null)
            {
                gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.DepthTextureHandle);
            }

            SetIntOnProgram(_volumeInjectProgram, "uShadowMap", 0);
            gl.ActiveTexture(TextureUnit.Texture1);
            if (cascadesActive && _shadowTargetCascadeNear is not null)
            {
                gl.BindTexture(TextureTarget.Texture2D, _shadowTargetCascadeNear.DepthTextureHandle);
            }
            else if (_shadowTarget is not null)
            {
                gl.BindTexture(TextureTarget.Texture2D, _shadowTarget.DepthTextureHandle);
            }
            else if (_sceneCapture is not null)
            {
                gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.DepthTextureHandle);
            }

            SetIntOnProgram(_volumeInjectProgram, "uShadowMapNear", 1);
        }

        gl.BindVertexArray(_godRayQuadVao);
        for (var layer = 0; layer < _volumeFroxelTarget.Slices; layer++)
        {
            if (!_volumeFroxelTarget.BindDrawLayer(layer))
            {
                _volumeFroxelTarget.Unbind();
                return false;
            }

            gl.Clear(ClearBufferMask.ColorBufferBit);
            SetIntOnProgram(_volumeInjectProgram, "uSliceIndex", layer);
            gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }

        _volumeFroxelTarget.Unbind();
        injectSw?.Stop();
        if (injectSw is not null)
        {
            frame.LastVolumeInjectMs = injectSw.Elapsed.TotalMilliseconds;
        }

        return true;
    }

    private string DescribeVolumeGodRayUnavailableReason(in PreviewRenderSettings settings)
    {
        if (!settings.EnableGodRays)
        {
            return "[3D preview] Froxel god rays disabled in settings.";
        }

        if (!settings.EnableVolumeGodRays)
        {
            return "[3D preview] Volume god-ray path disabled in settings.";
        }

        if (_volumeFroxelTarget is null || _volumeInjectProgram is not { IsValid: true } ||
            _volumeIntegrateProgram is not { IsValid: true })
        {
            var msg = "[3D preview] Froxel god rays unavailable; volume shaders or froxel target failed to init.";
            if (!string.IsNullOrWhiteSpace(_volumeInitFailureDetail))
            {
                msg += " " + _volumeInitFailureDetail;
            }

            return msg;
        }

        if (_sceneCapture is not { IsValid: true })
        {
            var msg = "[3D preview] Froxel god rays unavailable; scene capture depth target is invalid.";
            if (!string.IsNullOrWhiteSpace(_godRayInitFailureDetail))
            {
                msg += " " + _godRayInitFailureDetail;
            }

            return msg;
        }

        if (_godRayQuadVao == 0)
        {
            return "[3D preview] Froxel god rays unavailable; god-ray fullscreen quad missing.";
        }

        return _useOpenGlEs
            ? "[3D preview] Froxel god rays unavailable (OpenGL ES); check volume inject/integrate init."
            : "[3D preview] Froxel god rays unavailable; enable volume path or check GPU init.";
    }

    private void BindVolumeIntegrateUniforms(
        GlRenderFrame frame,
        Vector3 halfExtent,
        int outputClouds,
        float strength,
        float jitter)
    {
        if (_volumeIntegrateProgram is null || _volumeFroxelTarget is null || _sceneCapture is null)
        {
            return;
        }

        var gl = frame.Gl;
        var viewProj = frame.Proj * frame.View;
        Matrix4x4.Invert(viewProj, out var invViewProj);
        ComputeCameraBasis(frame.Eye, frame.LookTarget, out var camRight, out var camUp, out var camForward);

        if (_volumeUseLiteShaders)
        {
            if (outputClouds != 0)
            {
                return;
            }

            _volumeIntegrateProgram.Use();
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2DArray, _volumeFroxelTarget.ArrayTextureHandle);
            SetIntOnProgram(_volumeIntegrateProgram, "uFroxelVolume", 0);
            gl.ActiveTexture(TextureUnit.Texture1);
            gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.DepthTextureHandle);
            SetIntOnProgram(_volumeIntegrateProgram, "uSceneDepth", 1);
            SetMatrixOnProgram(_volumeIntegrateProgram, "uInvViewProj", invViewProj);
            SetVec3OnProgram(_volumeIntegrateProgram, "uCameraPos", frame.Eye);
            SetVec3OnProgram(_volumeIntegrateProgram, "uCamRight", camRight);
            SetVec3OnProgram(_volumeIntegrateProgram, "uCamUp", camUp);
            SetVec3OnProgram(_volumeIntegrateProgram, "uCamForward", camForward);
            SetVec3OnProgram(_volumeIntegrateProgram, "uLightDir", frame.LightDir);
            SetVec3OnProgram(_volumeIntegrateProgram, "uHalfExtent", halfExtent);
            SetIntOnProgram(_volumeIntegrateProgram, "uSliceCount", _volumeFroxelTarget.Slices);
            SetVec2OnProgram(_volumeIntegrateProgram, "uFroxelTexelSize",
                new Vector2(1f / _volumeFroxelTarget.Width, 1f / _volumeFroxelTarget.Height));
            SetFloatOnProgram(_volumeIntegrateProgram, "uStrength", strength);
            SetFloatOnProgram(_volumeIntegrateProgram, "uJitter", jitter);
            return;
        }

        var halfW = Math.Max(1, frame.Vw / 2);
        var halfH = Math.Max(1, frame.Vh / 2);
        if (_volumeHistoryHalfW != halfW || _volumeHistoryHalfH != halfH)
        {
            _volumeIntegrateHistoryValid = false;
            _volumeHistoryHalfW = halfW;
            _volumeHistoryHalfH = halfH;
        }

        _volumeIntegrateProgram.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2DArray, _volumeFroxelTarget.ArrayTextureHandle);
        SetIntOnProgram(_volumeIntegrateProgram, "uFroxelVolume", 0);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.DepthTextureHandle);
        SetIntOnProgram(_volumeIntegrateProgram, "uSceneDepth", 1);
        gl.ActiveTexture(TextureUnit.Texture2);
        if (_volumeIntegrateHistory is not null && _volumeIntegrateHistoryValid && outputClouds == 0)
        {
            gl.BindTexture(TextureTarget.Texture2D, _volumeIntegrateHistory.ColorTextureHandle);
            SetIntOnProgram(_volumeIntegrateProgram, "uPrevIntegrate", 2);
            SetIntOnProgram(_volumeIntegrateProgram, "uHasPrevIntegrate", 1);
        }
        else
        {
            gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.DepthTextureHandle);
            SetIntOnProgram(_volumeIntegrateProgram, "uPrevIntegrate", 2);
            SetIntOnProgram(_volumeIntegrateProgram, "uHasPrevIntegrate", 0);
        }

        SetMatrixOnProgram(_volumeIntegrateProgram, "uInvViewProj", invViewProj);
        SetMatrixOnProgram(_volumeIntegrateProgram, "uPrevViewProj", _volumePrevViewProj);
        SetVec3OnProgram(_volumeIntegrateProgram, "uCameraPos", frame.Eye);
        SetVec3OnProgram(_volumeIntegrateProgram, "uCamRight", camRight);
        SetVec3OnProgram(_volumeIntegrateProgram, "uCamUp", camUp);
        SetVec3OnProgram(_volumeIntegrateProgram, "uCamForward", camForward);
        SetVec3OnProgram(_volumeIntegrateProgram, "uLightDir", frame.LightDir);
        SetVec3OnProgram(_volumeIntegrateProgram, "uHalfExtent", halfExtent);
        SetIntOnProgram(_volumeIntegrateProgram, "uSliceCount", _volumeFroxelTarget.Slices);
        SetVec2OnProgram(_volumeIntegrateProgram, "uFroxelTexelSize",
            new Vector2(1f / _volumeFroxelTarget.Width, 1f / _volumeFroxelTarget.Height));
        SetFloatOnProgram(_volumeIntegrateProgram, "uStrength", strength);
        SetFloatOnProgram(_volumeIntegrateProgram, "uJitter", jitter);
        var quality = PreviewVolumetricQuality.Resolve(frame.Settings.VolumetricQuality);
        SetFloatOnProgram(_volumeIntegrateProgram, "uTemporalWeight",
            outputClouds == 0 ? quality.VolumeIntegrateTemporalWeight : 0f);
        SetIntOnProgram(_volumeIntegrateProgram, "uOutputClouds", outputClouds);
    }

    private void DrawVolumeCloudComposite(ref GlRenderFrame frame, Vector3 halfExtent)
    {
        if (!frame.Settings.EnableVolumetricClouds || _volumeCloudTarget is null)
        {
            return;
        }

        var halfW = Math.Max(1, frame.Vw / 2);
        var halfH = Math.Max(1, frame.Vh / 2);
        if (!_volumeCloudTarget.EnsureSize(halfW, halfH))
        {
            return;
        }

        var gl = frame.Gl;
        _volumeCloudTarget.BindDraw();
        gl.Clear(ClearBufferMask.ColorBufferBit);
        BindVolumeIntegrateUniforms(frame, halfExtent, outputClouds: 1, strength: 0.85f, jitter: _volumeJitter);

        gl.BindVertexArray(_godRayQuadVao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        if (frame.DefaultFbo != 0)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)frame.DefaultFbo);
        }
        else
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        gl.Viewport(frame.VpX, frame.VpY, (uint)frame.Vw, (uint)frame.Vh);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _godRayCompositeProgram!.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _volumeCloudTarget.ColorTextureHandle);
        SetIntOnProgram(_godRayCompositeProgram, "uRays", 0);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);
    }

    private bool TryIntegrateVolumeGodRaysToHalfRes(ref GlRenderFrame frame, Vector3 halfExtent)
    {
        if (_godRayHalfResTarget is null || _volumeIntegrateHistory is null ||
            _sceneCapture is not { IsValid: true } || _volumeIntegrateProgram is not { IsValid: true })
        {
            return false;
        }

        var halfW = Math.Max(1, frame.Vw / 2);
        var halfH = Math.Max(1, frame.Vh / 2);
        if (!_volumeIntegrateHistory.EnsureSize(halfW, halfH))
        {
            return false;
        }

        _godRayHalfResTarget.BindDraw();
        frame.Gl.Clear(ClearBufferMask.ColorBufferBit);
        var integrateSw = frame.Settings.LogVolumetricTiming ? Stopwatch.StartNew() : null;
        BindVolumeIntegrateUniforms(frame, halfExtent, outputClouds: 0, frame.Settings.GodRayStrength, _volumeJitter);
        frame.Gl.BindVertexArray(_godRayQuadVao);
        frame.Gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        integrateSw?.Stop();
        if (integrateSw is not null)
        {
            frame.LastVolumeIntegrateMs = integrateSw.Elapsed.TotalMilliseconds;
        }

        _volumeIntegrateHistory.CopyColorFrom(_godRayHalfResTarget);
        _volumePrevViewProj = frame.Proj * frame.View;
        _volumeIntegrateHistoryValid = true;
        return _godRayHalfResTarget.IsValid;
    }

    private bool TryRunVolumeGodRayPass(
        ref GlRenderFrame frame,
        out double injectMs,
        out double integrateMs)
    {
        injectMs = 0;
        integrateMs = 0;
        var halfExtent = ResolveVolumeHalfExtent(ref frame);
        var quality = PreviewVolumetricQuality.Resolve(frame.Settings.VolumetricQuality);
        var froxelW = quality.ResolveFroxelWidth(frame.Vw);
        var froxelH = quality.ResolveFroxelHeight(frame.Vh);
        if (!InjectVolumeFroxels(ref frame, halfExtent, froxelW, froxelH, quality.FroxelSlices))
        {
            return false;
        }

        injectMs = frame.LastVolumeInjectMs;
        _volumeJitter = (_volumeJitter + 0.0618f) % 1f;
        if (!TryIntegrateVolumeGodRaysToHalfRes(ref frame, halfExtent))
        {
            return false;
        }

        integrateMs = frame.LastVolumeIntegrateMs;
        return true;
    }
}
