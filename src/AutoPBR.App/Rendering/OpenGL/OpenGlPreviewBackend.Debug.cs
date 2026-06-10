using System.Numerics;

using AutoPBR.App.Rendering.Scene;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private const int SunDebugLineCount = 12;
    private const int SunDebugVertexCount = SunDebugLineCount * 2;
    private const int SunDebugFloatCount = SunDebugVertexCount * PreviewGridLinesFactory.FloatsPerVertex;

    private uint _sunDebugVao;
    private uint _sunDebugVbo;

    private void EnsureSunDebugOverlay(GL gl)
    {
        if (_sunDebugVao != 0)
        {
            return;
        }

        _sunDebugVao = gl.GenVertexArray();
        _sunDebugVbo = gl.GenBuffer();
        gl.BindVertexArray(_sunDebugVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _sunDebugVbo);
        Span<float> vboStub = stackalloc float[SunDebugFloatCount];
        gl.BufferData<float>(GLEnum.ArrayBuffer, vboStub, GLEnum.DynamicDraw);
        unsafe
        {
            var stride = PreviewGridLinesFactory.FloatsPerVertex * sizeof(float);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, (uint)stride, (void*)(3 * sizeof(float)));
        }

        gl.BindVertexArray(0);
    }

    private void DestroySunDebugOverlay()
    {
        var gl = _gl;
        if (gl is null)
        {
            _sunDebugVao = _sunDebugVbo = 0;
            return;
        }

        if (_sunDebugVbo != 0)
        {
            gl.DeleteBuffer(_sunDebugVbo);
            _sunDebugVbo = 0;
        }

        if (_sunDebugVao != 0)
        {
            gl.DeleteVertexArray(_sunDebugVao);
            _sunDebugVao = 0;
        }
    }

    private void DrawSunProjectionDebug(ref GlRenderFrame frame)
    {
        if (!frame.Settings.ShowSunProjectionDebug || _lineProgram is not { IsValid: true })
        {
            return;
        }

        var gl = frame.Gl;
        EnsureSunDebugOverlay(gl);
        if (_sunDebugVao == 0)
        {
            return;
        }

        var aspect = frame.Vw / (float)Math.Max(frame.Vh, 1);
        var coneScale = Math.Max(frame.Settings.GodRayConeScale, 0.05f);
        PreviewSunScreenProjection.Compute(frame.Eye, frame.LightDir, frame.View, frame.Proj, aspect, coneScale,
            frame.Settings.AtmosphereSunDiscSize, out var sunUv, out var sunDiscRadiusUv, out _, out _);
        PreviewSunScreenProjection.ComputeMoon(frame.Eye, frame.LightDir, frame.View, frame.Proj, aspect,
            out var moonUv, out var moonDiscRadiusUv, out _);

        Span<float> verts = stackalloc float[SunDebugFloatCount];
        var i = 0;
        AppendSunDebugDisc(ref i, verts, sunUv, sunDiscRadiusUv, frame.Vw, frame.Vh, 1f, 0.15f, 0.15f, 0.15f, 1f, 0.35f);
        AppendSunDebugDisc(ref i, verts, moonUv, moonDiscRadiusUv, frame.Vw, frame.Vh, 0.55f, 0.65f, 1f, 0.35f, 0.55f, 1f);

        var priorDepth = gl.IsEnabled(EnableCap.DepthTest);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        gl.Viewport(frame.VpX, frame.VpY, (uint)frame.Vw, (uint)frame.Vh);
        gl.Disable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        gl.BindVertexArray(_sunDebugVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _sunDebugVbo);
        gl.BufferSubData<float>(GLEnum.ArrayBuffer, 0, verts[..i]);
        _lineProgram.Use();
        SetLineProgramMvp(gl, Matrix4x4.Identity);
        gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(i / PreviewGridLinesFactory.FloatsPerVertex));
        gl.BindVertexArray(0);

        if (priorDepth)
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

    private static void AppendSunDebugDisc(ref int offset, Span<float> verts, Vector2 uv, float discRadiusUv, int vw, int vh,
        float crossR, float crossG, float crossB, float ringR, float ringG, float ringB)
    {
        var ndcX = uv.X * 2f - 1f;
        var ndcY = uv.Y * 2f - 1f;
        var minDim = Math.Min(vw, vh);
        var armPx = Math.Max(18f, discRadiusUv * minDim * 1.4f);
        var discPx = Math.Max(armPx * 1.15f, 22f);
        var armX = armPx / vw * 2f;
        var armY = armPx / vh * 2f;
        var discX = discPx / vw * 2f;
        var discY = discPx / vh * 2f;
        AddSunDebugLine(verts, ref offset, ndcX - armX, ndcY, ndcX + armX, ndcY, crossR, crossG, crossB, 1f);
        AddSunDebugLine(verts, ref offset, ndcX, ndcY - armY, ndcX, ndcY + armY, crossR, crossG, crossB, 1f);
        AddSunDebugLine(verts, ref offset, ndcX - discX, ndcY - discY, ndcX + discX, ndcY - discY, ringR, ringG, ringB, 1f);
        AddSunDebugLine(verts, ref offset, ndcX + discX, ndcY - discY, ndcX + discX, ndcY + discY, ringR, ringG, ringB, 1f);
        AddSunDebugLine(verts, ref offset, ndcX + discX, ndcY + discY, ndcX - discX, ndcY + discY, ringR, ringG, ringB, 1f);
        AddSunDebugLine(verts, ref offset, ndcX - discX, ndcY + discY, ndcX - discX, ndcY - discY, ringR, ringG, ringB, 1f);
    }

    private static void AddSunDebugLine(Span<float> verts, ref int offset, float x0, float y0, float x1, float y1,
        float r, float g, float b, float a)
    {
        WriteSunDebugVertex(verts, ref offset, x0, y0, r, g, b, a);
        WriteSunDebugVertex(verts, ref offset, x1, y1, r, g, b, a);
    }

    private static void WriteSunDebugVertex(Span<float> verts, ref int offset, float x, float y, float r, float g, float b, float a)
    {
        verts[offset++] = x;
        verts[offset++] = y;
        verts[offset++] = 0f;
        verts[offset++] = r;
        verts[offset++] = g;
        verts[offset++] = b;
        verts[offset++] = a;
    }
}
