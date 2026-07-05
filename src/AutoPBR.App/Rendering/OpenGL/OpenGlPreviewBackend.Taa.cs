using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private GlShaderProgram? _taaResolveProgram;
    private GlColorRenderTarget? _taaScratchTarget;
    private GlColorRenderTarget? _taaHistoryTarget;
    private Matrix4x4 _taaPrevViewProj = Matrix4x4.Identity;
    private bool _taaHistoryValid;
    private int _taaHistoryW;
    private int _taaHistoryH;
    private bool _prevEnablePreviewTaa = true;
    private bool _prevPreviewTaaActive;
    private int _taaFrameIndex;
    private Matrix4x4 _taaPrevSubjectModel = Matrix4x4.Identity;
    private bool _taaPrevSubjectModelValid;

    private void TryInitPreviewTaa(GL gl, bool useOpenGlEs)
    {
        DestroyPreviewTaaResources();
        _taaResolveProgram = CreatePreviewProgram("genesis_godrays.vert", "genesis_taa_resolve.frag",
            out var err, "preview-taa");
        if (_taaResolveProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Preview TAA shader: " + (err ?? "link failed"));
            _taaResolveProgram?.Dispose();
            _taaResolveProgram = null;
            return;
        }

        _taaScratchTarget = new GlColorRenderTarget(gl, useOpenGlEs);
        _taaHistoryTarget = new GlColorRenderTarget(gl, useOpenGlEs);
        if (!TryInitSceneCaptureCore(gl, useOpenGlEs, out var sceneErr))
        {
            EmitDiagnostic("[3D preview] Preview TAA scene capture: " + TrimShaderDiagnostic(sceneErr));
        }
    }

    private void DestroyPreviewTaaResources()
    {
        _taaResolveProgram?.Dispose();
        _taaResolveProgram = null;
        _taaScratchTarget?.Dispose();
        _taaScratchTarget = null;
        _taaHistoryTarget?.Dispose();
        _taaHistoryTarget = null;
        _taaHistoryValid = false;
    }

    private void SyncPreviewTaaToggleState(in PreviewRenderSettings settings)
    {
        var active = IsPreviewTaaActive(settings);
        if (_prevEnablePreviewTaa == settings.EnablePreviewTaa &&
            _prevPreviewTaaActive == active)
        {
            return;
        }

        _prevEnablePreviewTaa = settings.EnablePreviewTaa;
        _prevPreviewTaaActive = active;
        InvalidatePreviewTaaHistory();
        _taaFrameIndex = 0;
    }

    private bool IsPreviewTaaActive(in PreviewRenderSettings settings)
    {
        if (!settings.EnablePreviewTaa || _taaResolveProgram is not { IsValid: true } ||
            _taaScratchTarget is null || _taaHistoryTarget is null || _godRayQuadVao == 0)
        {
            return false;
        }

        var weight = PreviewVolumetricQuality.Resolve(settings.VolumetricQuality).PreviewTaaWeight;
        return weight > 0f;
    }

    private Vector2 CurrentPreviewTaaJitter(int width, int height)
    {
        var sample = (_taaFrameIndex & 7) + 1;
        var pixelJitter = new Vector2(Halton(sample, 2) - 0.5f, Halton(sample, 3) - 0.5f);
        return new Vector2(
            2f * pixelJitter.X / Math.Max(1, width),
            2f * pixelJitter.Y / Math.Max(1, height));
    }

    private static float Halton(int index, int radix)
    {
        var result = 0f;
        var fraction = 1f / radix;
        while (index > 0)
        {
            result += fraction * (index % radix);
            index /= radix;
            fraction /= radix;
        }

        return result;
    }

    private Matrix4x4 ResolvePreviewTaaPrevViewProj(Matrix4x4 currentViewProj) =>
        _taaHistoryValid ? _taaPrevViewProj : currentViewProj;

    private Matrix4x4 ResolvePreviewTaaPrevSubjectModel(Matrix4x4 currentModel) =>
        _taaHistoryValid && _taaPrevSubjectModelValid ? _taaPrevSubjectModel : currentModel;

    private void InvalidatePreviewTaaHistory()
    {
        _taaHistoryValid = false;
        _taaPrevSubjectModel = Matrix4x4.Identity;
        _taaPrevSubjectModelValid = false;
        InvalidatePreviousEntitySkinningBones();
    }

    private void DrawPreviewTaa(ref GlRenderFrame frame)
    {
        SyncPreviewTaaToggleState(frame.Settings);
        if (!IsPreviewTaaActive(frame.Settings))
        {
            return;
        }

        var gl = frame.Gl;
        var w = Math.Max(1, frame.Vw);
        var h = Math.Max(1, frame.Vh);
        if (_taaHistoryW != w || _taaHistoryH != h)
        {
            _taaHistoryValid = false;
            _taaPrevSubjectModelValid = false;
            _taaHistoryW = w;
            _taaHistoryH = h;
        }

        if (!_taaScratchTarget!.EnsureSize(w, h) || !_taaHistoryTarget!.EnsureSize(w, h))
        {
            return;
        }

        var readFbo = (uint)Math.Max(0, frame.DefaultFbo);
        if (!_taaScratchTarget.CopyColorFromFramebuffer(readFbo, w, h))
        {
            _taaHistoryValid = false;
            return;
        }

        var viewProj = frame.Proj * frame.View;
        if (!Matrix4x4.Invert(viewProj, out var invViewProj))
        {
            return;
        }

        var quality = PreviewVolumetricQuality.Resolve(frame.Settings.VolumetricQuality);
        var hasSceneDepth = frame.GodRayCaptureActive && _sceneCapture is { IsValid: true };
        var hasTaaSignal = hasSceneDepth && _sceneCapture!.TaaSignalTextureHandle != 0;

        BindDefaultFramebuffer(ref frame);
        var priorDepthTest = gl.IsEnabled(EnableCap.DepthTest);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.Blend);
        gl.BindVertexArray(_godRayQuadVao);
        _taaResolveProgram!.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _taaScratchTarget.ColorTextureHandle);
        SetIntOnProgram(_taaResolveProgram, "uCurrent", 0);
        gl.ActiveTexture(TextureUnit.Texture1);
        gl.BindTexture(TextureTarget.Texture2D, _taaHistoryTarget.ColorTextureHandle);
        SetIntOnProgram(_taaResolveProgram, "uHistory", 1);
        if (hasSceneDepth)
        {
            gl.ActiveTexture(TextureUnit.Texture2);
            gl.BindTexture(TextureTarget.Texture2D, _sceneCapture!.DepthTextureHandle);
            SetIntOnProgram(_taaResolveProgram, "uSceneDepth", 2);
        }

        if (hasTaaSignal)
        {
            gl.ActiveTexture(TextureUnit.Texture3);
            gl.BindTexture(TextureTarget.Texture2D, _sceneCapture!.TaaSignalTextureHandle);
            SetIntOnProgram(_taaResolveProgram, "uTaaSignal", 3);
        }

        SetIntOnProgram(_taaResolveProgram, "uHasSceneDepth", hasSceneDepth ? 1 : 0);
        SetIntOnProgram(_taaResolveProgram, "uHasTaaSignal", hasTaaSignal ? 1 : 0);
        SetIntOnProgram(_taaResolveProgram, "uHasHistory", _taaHistoryValid ? 1 : 0);
        SetMatrixOnProgram(_taaResolveProgram, "uInvViewProj", invViewProj);
        SetMatrixOnProgram(_taaResolveProgram, "uPrevViewProj", _taaPrevViewProj);
        SetVec2OnProgram(_taaResolveProgram, "uTexelSize", new Vector2(1f / w, 1f / h));
        SetFloatOnProgram(_taaResolveProgram, "uTemporalWeight", quality.PreviewTaaWeight);
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

        _taaHistoryTarget.CopyColorFromFramebuffer(readFbo, w, h);
        _taaPrevViewProj = viewProj;
        _taaPrevSubjectModel = frame.ModelMatrix;
        _taaPrevSubjectModelValid = true;
        CapturePreviousEntitySkinningBones();
        _taaHistoryValid = true;
        _taaFrameIndex++;
    }
}
