using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private GlShaderProgram? _cloudProgram;
    private uint _cloudQuadVao;
    private uint _cloudQuadVbo;
    private GlTexture3D? _cloudNoiseTex;
    private GlTexture2D? _cloudCoverageTex;
    private GlColorRenderTarget? _cloudRenderTarget;
    private GlColorRenderTarget? _cloudHistoryTarget;
    private Matrix4x4 _cloudPrevViewProj = Matrix4x4.Identity;
    private bool _cloudHistoryValid;
    private float _cloudFramePhase;
    private int _cloudHistoryW;
    private int _cloudHistoryH;
    private bool _loggedCloudDraw;

    private void TryInitVolumetricClouds(GL gl, bool useOpenGlEs)
    {
        DestroyVolumetricCloudResources();
        _cloudProgram = new GlShaderProgram(gl, "genesis_godrays.vert", "genesis_clouds.frag", useOpenGlEs, out var err);
        if (_cloudProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Volumetric cloud shader: " + (err ?? "link failed"));
            _cloudProgram?.Dispose();
            _cloudProgram = null;
            return;
        }

        _cloudNoiseTex = new GlTexture3D(gl);
        _cloudNoiseTex.UploadRgba(PreviewCloudNoiseTextureGenerator.Size, PreviewCloudNoiseTextureGenerator.GenerateRgba8());
        _cloudCoverageTex = new GlTexture2D(gl, nearestFilter: false);
        var coverage = PreviewCloudCoverageMapGenerator.GenerateR8();
        var covRgba = new byte[coverage.Length * 4];
        for (var i = 0; i < coverage.Length; i++)
        {
            covRgba[i * 4] = coverage[i];
            covRgba[i * 4 + 1] = coverage[i];
            covRgba[i * 4 + 2] = coverage[i];
            covRgba[i * 4 + 3] = 255;
        }

        _cloudCoverageTex.UploadRgba(PreviewCloudCoverageMapGenerator.Size, PreviewCloudCoverageMapGenerator.Size,
            covRgba, nearestFilter: false);
        _cloudRenderTarget = new GlColorRenderTarget(gl, useOpenGlEs);
        _cloudHistoryTarget = new GlColorRenderTarget(gl, useOpenGlEs);

        Span<float> quad =
        [
            -1f, -1f, 1f, -1f, 1f, 1f,
            -1f, -1f, 1f, 1f, -1f, 1f
        ];
        _cloudQuadVao = gl.GenVertexArray();
        _cloudQuadVbo = gl.GenBuffer();
        gl.BindVertexArray(_cloudQuadVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _cloudQuadVbo);
        gl.BufferData<float>(GLEnum.ArrayBuffer, quad, GLEnum.StaticDraw);
        unsafe
        {
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        }

        gl.BindVertexArray(0);
    }

    private void DestroyVolumetricCloudResources()
    {
        var gl = _gl;
        _cloudProgram?.Dispose();
        _cloudProgram = null;
        _cloudNoiseTex?.Dispose();
        _cloudNoiseTex = null;
        _cloudCoverageTex?.Dispose();
        _cloudCoverageTex = null;
        _cloudRenderTarget?.Dispose();
        _cloudRenderTarget = null;
        _cloudHistoryTarget?.Dispose();
        _cloudHistoryTarget = null;
        _cloudHistoryValid = false;
        _loggedCloudDraw = false;

        if (gl is null)
        {
            _cloudQuadVao = _cloudQuadVbo = 0;
            return;
        }

        if (_cloudQuadVbo != 0)
        {
            gl.DeleteBuffer(_cloudQuadVbo);
            _cloudQuadVbo = 0;
        }

        if (_cloudQuadVao != 0)
        {
            gl.DeleteVertexArray(_cloudQuadVao);
            _cloudQuadVao = 0;
        }
    }

    private bool CanDrawVolumetricClouds(in PreviewRenderSettings settings) =>
        settings.EnableVolumetricClouds &&
        _cloudProgram is { IsValid: true } &&
        _cloudQuadVao != 0;

    private void DrawVolumetricClouds(ref GlRenderFrame frame, bool gateSkyDepth)
    {
        if (!CanDrawVolumetricClouds(frame.Settings))
        {
            return;
        }

        BindDefaultFramebuffer(ref frame);
        DrawVolumetricCloudsInternal(ref frame, gateSkyDepth);
    }

    private void DrawVolumetricCloudsInternal(ref GlRenderFrame frame, bool gateSkyDepth)
    {
        var settings = frame.Settings;
        var viewProj = frame.Proj * frame.View;
        if (!Matrix4x4.Invert(viewProj, out var invViewProj))
        {
            return;
        }

        var gl = frame.Gl;
        var profile = PreviewVolumetricQuality.Resolve(settings.VolumetricQuality);
        var layerWorldY = PreviewStageConstants.CloudLayerBaseWorldY(settings.CloudLayerHeight);
        var useDepthGate = gateSkyDepth && _sceneCapture is { IsValid: true };
        var useTemporal = useDepthGate && profile.CloudTemporalWeight > 0f &&
                          _cloudRenderTarget is not null && _cloudHistoryTarget is not null;

        if (useTemporal)
        {
            var w = Math.Max(1, frame.Vw / 2);
            var h = Math.Max(1, frame.Vh / 2);
            if (_cloudHistoryW != w || _cloudHistoryH != h)
            {
                _cloudHistoryValid = false;
                _cloudHistoryW = w;
                _cloudHistoryH = h;
            }

            if (!_cloudRenderTarget!.EnsureSize(w, h))
            {
                useTemporal = false;
            }
        }

        if (useTemporal)
        {
            _cloudRenderTarget!.BindDraw();
            gl.Clear(ClearBufferMask.ColorBufferBit);
        }

        BindCloudShaderUniforms(frame, invViewProj, viewProj, layerWorldY, profile, useDepthGate, useTemporal);

        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        if (!useTemporal)
        {
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        gl.Disable(EnableCap.DepthTest);
        gl.DepthMask(false);
        gl.BindVertexArray(_cloudQuadVao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);
        gl.DepthMask(true);
        gl.Enable(EnableCap.DepthTest);

        if (useTemporal)
        {
            CompositeCloudRenderTargetToDefault(ref frame);
            _cloudHistoryTarget!.CopyColorFrom(_cloudRenderTarget!);
            _cloudPrevViewProj = viewProj;
            _cloudHistoryValid = true;
            _cloudFramePhase += 0.071f;
            if (_cloudFramePhase > 1f)
            {
                _cloudFramePhase -= 1f;
            }
        }

        if (!priorBlend && !useTemporal)
        {
            gl.Disable(EnableCap.Blend);
        }

        if (!_loggedCloudDraw)
        {
            _loggedCloudDraw = true;
            EmitDiagnostic("[3D preview] Screen-space volumetric clouds active (Beer–Powder + coverage map).");
        }
    }

    private void BindCloudShaderUniforms(
        GlRenderFrame frame,
        Matrix4x4 invViewProj,
        Matrix4x4 viewProj,
        float layerWorldY,
        PreviewVolumetricQuality.Profile profile,
        bool useDepthGate,
        bool useTemporal)
    {
        var gl = frame.Gl;
        var settings = frame.Settings;
        _cloudProgram!.Use();
        SetMatrixOnProgram(_cloudProgram, "uInvViewProj", invViewProj);
        SetMatrixOnProgram(_cloudProgram, "uPrevViewProj", _cloudPrevViewProj);
        SetVec3OnProgram(_cloudProgram, "uCameraPos", frame.Eye);
        SetVec3OnProgram(_cloudProgram, "uSunDir", frame.LightDir);
        SetVec3OnProgram(_cloudProgram, "uSunColor", frame.Scene.Light.Color);
        SetFloatOnProgram(_cloudProgram, "uLayerHeight", layerWorldY);
        SetFloatOnProgram(_cloudProgram, "uVolumeHeight", settings.CloudVolumeHeight);
        SetFloatOnProgram(_cloudProgram, "uDensity", settings.CloudDensity);
        SetFloatOnProgram(_cloudProgram, "uVolumeSize", settings.CloudVolumeSize);
        SetIntOnProgram(_cloudProgram, "uQuality", profile.CloudQuality);
        SetIntOnProgram(_cloudProgram, "uGateSkyDepth", useDepthGate ? 1 : 0);
        SetFloatOnProgram(_cloudProgram, "uTemporalWeight", useTemporal ? profile.CloudTemporalWeight : 0f);
        SetFloatOnProgram(_cloudProgram, "uFramePhase", _cloudFramePhase);
        SetIntOnProgram(_cloudProgram, "uHasCloudNoise", _cloudNoiseTex is not null ? 1 : 0);
        SetIntOnProgram(_cloudProgram, "uHasCoverageMap", _cloudCoverageTex is not null ? 1 : 0);
        SetIntOnProgram(_cloudProgram, "uHasSkyLut", _atmoLutsValid && _atmoSkyViewTex != 0 ? 1 : 0);
        SetIntOnProgram(_cloudProgram, "uHasPrevClouds",
            useTemporal && _cloudHistoryValid ? 1 : 0);

        if (_cloudNoiseTex is not null)
        {
            gl.ActiveTexture(TextureUnit.Texture0);
            _cloudNoiseTex.Bind(0);
            SetIntOnProgram(_cloudProgram, "uCloudNoise", 0);
        }

        if (_cloudCoverageTex is not null)
        {
            gl.ActiveTexture(TextureUnit.Texture1);
            _cloudCoverageTex.Bind(1);
            SetIntOnProgram(_cloudProgram, "uCoverageMap", 1);
        }

        if (_atmoLutsValid && _atmoSkyViewTex != 0)
        {
            gl.ActiveTexture(TextureUnit.Texture2);
            gl.BindTexture(TextureTarget.Texture2D, _atmoSkyViewTex);
            SetIntOnProgram(_cloudProgram, "uSkyViewLut", 2);
        }

        if (useDepthGate && _sceneCapture is not null)
        {
            gl.ActiveTexture(TextureUnit.Texture5);
            gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.DepthTextureHandle);
            SetIntOnProgram(_cloudProgram, "uSceneDepth", 5);
        }

        if (useTemporal && _cloudHistoryTarget is not null && _cloudHistoryValid)
        {
            gl.ActiveTexture(TextureUnit.Texture6);
            gl.BindTexture(TextureTarget.Texture2D, _cloudHistoryTarget.ColorTextureHandle);
            SetIntOnProgram(_cloudProgram, "uPrevClouds", 6);
        }
    }

    private void CompositeCloudRenderTargetToDefault(ref GlRenderFrame frame)
    {
        if (_cloudRenderTarget is null || _godRayCompositeProgram is not { IsValid: true } || _godRayQuadVao == 0)
        {
            BindDefaultFramebuffer(ref frame);
            return;
        }

        var gl = frame.Gl;
        BindDefaultFramebuffer(ref frame);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        gl.Disable(EnableCap.DepthTest);
        gl.BindVertexArray(_godRayQuadVao);
        _godRayCompositeProgram.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _cloudRenderTarget.ColorTextureHandle);
        SetIntOnProgram(_godRayCompositeProgram, "uRays", 0);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);
        gl.Enable(EnableCap.DepthTest);
        if (!priorBlend)
        {
            gl.Disable(EnableCap.Blend);
        }
    }
}
