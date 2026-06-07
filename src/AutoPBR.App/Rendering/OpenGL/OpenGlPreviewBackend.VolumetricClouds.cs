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
    private bool _loggedCloudDraw;

    private void TryInitVolumetricClouds(GL gl, bool useOpenGlEs)
    {
        DestroyVolumetricCloudResources();
        _cloudProgram = new GlShaderProgram(gl, "genesis_clouds.vert", "genesis_clouds.frag", useOpenGlEs, out var err);
        if (_cloudProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Volumetric cloud shader: " + (err ?? "link failed"));
            _cloudProgram?.Dispose();
            _cloudProgram = null;
            return;
        }

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

    /// <summary>
    /// Composite clouds over sky pixels after opaque geometry (depth at far plane).
    /// </summary>
    private void DrawVolumetricClouds(
        GL gl,
        Vector3 eye,
        Matrix4x4 view,
        Matrix4x4 proj,
        Vector3 lightPropagationDir,
        Vector3 lightColor,
        PreviewRenderSettings settings)
    {
        if (!CanDrawVolumetricClouds(settings))
        {
            return;
        }

        var viewProj = proj * view;
        if (!Matrix4x4.Invert(viewProj, out var invViewProj))
        {
            return;
        }

        var layerWorldY = PreviewStageConstants.CloudLayerBaseWorldY(settings.CloudLayerHeight);
        var quality = PreviewVolumetricQuality.Resolve(settings.VolumetricQuality).CloudQuality;

        _cloudProgram!.Use();
        SetMatrixOnProgram(_cloudProgram, "uInvViewProj", invViewProj);
        SetVec3OnProgram(_cloudProgram, "uCameraPos", eye);
        SetVec3OnProgram(_cloudProgram, "uSunDir", lightPropagationDir);
        SetVec3OnProgram(_cloudProgram, "uLightColor", lightColor);
        SetFloatOnProgram(_cloudProgram, "uLayerHeight", layerWorldY);
        SetFloatOnProgram(_cloudProgram, "uVolumeHeight", settings.CloudVolumeHeight);
        SetFloatOnProgram(_cloudProgram, "uDensity", settings.CloudDensity);
        SetFloatOnProgram(_cloudProgram, "uVolumeSize", settings.CloudVolumeSize);
        SetIntOnProgram(_cloudProgram, "uQuality", quality);

        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        gl.Disable(EnableCap.DepthTest);
        gl.DepthMask(false);

        gl.BindVertexArray(_cloudQuadVao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);

        gl.DepthMask(true);
        gl.Enable(EnableCap.DepthTest);

        if (!_loggedCloudDraw)
        {
            _loggedCloudDraw = true;
            EmitDiagnostic("[3D preview] Screen-space volumetric clouds active.");
        }
    }
}
