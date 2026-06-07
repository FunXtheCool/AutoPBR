using System.Diagnostics;
using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private GlShaderProgram? _godRayUpsampleProgram;
    private GlShaderProgram? _godRayCompositeProgram;
    private GlSceneCaptureTarget? _sceneCapture;
    private GlColorRenderTarget? _godRayHalfResTarget;
    private GlColorRenderTarget? _godRayResolveTarget;
    private GlColorRenderTarget? _godRayHistoryTarget;
    private uint _godRayQuadVao;
    private uint _godRayQuadVbo;
    private int _godRayBlitFailLogged;
    private Matrix4x4 _godRayPrevViewProj = Matrix4x4.Identity;
    private bool _godRayHistoryValid;
    private int _godRayHistoryVw;
    private int _godRayHistoryVh;
    private int _volumePathFailLogged;
    private double _lastVolumetricTimingLogMs;

    private void TryInitGodRays(GL gl, bool useOpenGlEs)
    {
        DestroyGodRayResources();
        _godRayInitFailureDetail = null;
        _sceneCapture = new GlSceneCaptureTarget(gl, useOpenGlEs);
        _godRayHalfResTarget = new GlColorRenderTarget(gl, useOpenGlEs);
        _godRayResolveTarget = new GlColorRenderTarget(gl, useOpenGlEs);
        _godRayHistoryTarget = new GlColorRenderTarget(gl, useOpenGlEs);

        _godRayUpsampleProgram = new GlShaderProgram(gl, "genesis_godrays.vert", "genesis_godrays_upsample.frag", useOpenGlEs, out var upErr);
        if (_godRayUpsampleProgram is not { IsValid: true })
        {
            _godRayInitFailureDetail = "upsample: " + TrimShaderDiagnostic(upErr);
            EmitDiagnostic("[3D preview] God-ray upsample shader: " + TrimShaderDiagnostic(upErr));
            DestroyGodRayResources();
            return;
        }

        _godRayCompositeProgram = new GlShaderProgram(gl, "genesis_godrays.vert", "genesis_godrays_composite.frag", useOpenGlEs, out var compErr);
        if (_godRayCompositeProgram is not { IsValid: true })
        {
            _godRayInitFailureDetail = "composite: " + TrimShaderDiagnostic(compErr);
            EmitDiagnostic("[3D preview] God-ray composite shader: " + TrimShaderDiagnostic(compErr));
            DestroyGodRayResources();
            return;
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
        _godRayUpsampleProgram is { IsValid: true } &&
        _godRayCompositeProgram is { IsValid: true } &&
        _godRayHalfResTarget is not null &&
        _godRayResolveTarget is not null &&
        _godRayHistoryTarget is not null &&
        _godRayQuadVao != 0;

    private bool TryBeginGodRaySceneRender(ref GlRenderFrame frame)
    {
        if (!CanUseGodRayCapture(frame.Settings) || _sceneCapture is null)
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

        var blitOk = _useOpenGlEs
            ? TryCopySceneCaptureToDefault(ref frame)
            : _sceneCapture.BlitColorToDefault(frame.DefaultFbo, frame.VpX, frame.VpY, frame.Vw, frame.Vh);
        if (!blitOk)
        {
            blitOk = _useOpenGlEs
                ? _sceneCapture.BlitColorToDefault(frame.DefaultFbo, frame.VpX, frame.VpY, frame.Vw, frame.Vh)
                : TryCopySceneCaptureToDefault(ref frame);
            if (!blitOk)
            {
                var key = frame.Vw + frame.Vh * 10000;
                if (_godRayBlitFailLogged != key)
                {
                    _godRayBlitFailLogged = key;
                    EmitDiagnostic("[3D preview] God-ray blit to default FBO failed.");
                }
            }
        }

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

    private bool TryCopySceneCaptureToDefault(ref GlRenderFrame frame)
    {
        if (_sceneCapture is null || _godRayCompositeProgram is not { IsValid: true } || _godRayQuadVao == 0)
        {
            return false;
        }

        var gl = frame.Gl;
        if (frame.DefaultFbo != 0)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)frame.DefaultFbo);
        }
        else
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        gl.Viewport(frame.VpX, frame.VpY, (uint)frame.Vw, (uint)frame.Vh);
        var priorDepthTest = gl.IsEnabled(EnableCap.DepthTest);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.Blend);
        gl.BindVertexArray(_godRayQuadVao);
        _godRayCompositeProgram.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.ColorTextureHandle);
        SetIntOnProgram(_godRayCompositeProgram, "uRays", 0);
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
        if (!frame.GodRayCaptureActive || _sceneCapture is null || !_sceneCapture.IsValid ||
            _godRayUpsampleProgram is not { IsValid: true } ||
            _godRayCompositeProgram is not { IsValid: true } ||
            _godRayHalfResTarget is null || _godRayResolveTarget is null || _godRayHistoryTarget is null ||
            _godRayQuadVao == 0)
        {
            return;
        }

        var halfW = Math.Max(1, frame.Vw / 2);
        var halfH = Math.Max(1, frame.Vh / 2);
        if (!_godRayHalfResTarget.EnsureSize(halfW, halfH) ||
            !_godRayResolveTarget.EnsureSize(frame.Vw, frame.Vh) ||
            !_godRayHistoryTarget.EnsureSize(frame.Vw, frame.Vh))
        {
            return;
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

        frame.VolumeFroxelsReady = false;
        var volumeSw = frame.Settings.LogVolumetricTiming ? Stopwatch.StartNew() : null;
        var injectMs = 0.0;
        var integrateMs = 0.0;
        var canVolume = CanUseVolumeGodRays(frame.Settings);
        var usedVolumePath = canVolume && TryRunVolumeGodRayPass(ref frame, out injectMs, out integrateMs);
        if (volumeSw is not null)
        {
            volumeSw.Stop();
            MaybeLogVolumetricTiming(frame.Settings, injectMs, integrateMs);
        }

        frame.VolumeFroxelsReady = usedVolumePath;

        if (!usedVolumePath)
        {
            if (_volumePathFailLogged == 0)
            {
                _volumePathFailLogged = 1;
                EmitDiagnostic(!canVolume
                    ? DescribeVolumeGodRayUnavailableReason(frame.Settings)
                    : "[3D preview] Froxel god rays inject or integrate pass failed.");
            }

            gl.DepthMask(priorDepthMask);
            if (priorDepthTest)
            {
                gl.Enable(EnableCap.DepthTest);
            }

            if (priorCullFace)
            {
                gl.Enable(EnableCap.CullFace);
            }

            if (!priorBlend)
            {
                gl.Disable(EnableCap.Blend);
            }

            gl.BindVertexArray(0);
            return;
        }

        // Full-res bilateral upsample + temporal reprojection.
        _godRayResolveTarget.BindDraw();
        gl.Clear(ClearBufferMask.ColorBufferBit);
        _godRayUpsampleProgram.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _godRayHalfResTarget.ColorTextureHandle);
        SetIntOnProgram(_godRayUpsampleProgram, "uHalfResRays", 0);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.DepthTextureHandle);
        SetIntOnProgram(_godRayUpsampleProgram, "uSceneDepth", 1);
        gl.ActiveTexture(TextureUnit.Texture2);
        gl.BindTexture(TextureTarget.Texture2D, _godRayHistoryTarget.ColorTextureHandle);
        SetIntOnProgram(_godRayUpsampleProgram, "uHistory", 2);
        SetMatrixOnProgram(_godRayUpsampleProgram, "uInvViewProj", invViewProj);
        SetMatrixOnProgram(_godRayUpsampleProgram, "uPrevViewProj", _godRayPrevViewProj);
        SetVec2OnProgram(_godRayUpsampleProgram, "uHalfResTexelSize", new Vector2(1f / halfW, 1f / halfH));
        SetFloatOnProgram(_godRayUpsampleProgram, "uTemporalWeight", quality.UpsampleTemporalWeight);
        SetIntOnProgram(_godRayUpsampleProgram, "uHasHistory",
            _godRayHistoryValid && quality.UpsampleTemporalWeight > 0f ? 1 : 0);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        _godRayHistoryTarget.CopyColorFrom(_godRayResolveTarget);
        _godRayPrevViewProj = viewProj;
        _godRayHistoryValid = true;

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
        gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        _godRayCompositeProgram.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _godRayResolveTarget.ColorTextureHandle);
        SetIntOnProgram(_godRayCompositeProgram, "uRays", 0);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
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
