using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Avalonia.OpenGL;
using Avalonia.Platform;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>OpenGL implementation of <see cref="IRenderPreviewBackend"/>; GPU entry points must run on the OpenGL thread (Avalonia <see cref="AutoPBR.App.Controls.GlPbrPreviewControl"/> callbacks).</summary>
public sealed partial class OpenGlPreviewBackend
{
    private void TryInitSunBillboard(GL gl, bool useOpenGlEs)
    {
        DestroySunBillboard();
        _sunProgram = new GlSunBillboardProgram(gl, useOpenGlEs, out var sunErr);
        if (_sunProgram is not { IsValid: true })
        {
            _sunProgram?.Dispose();
            _sunProgram = null;
            if (!string.IsNullOrWhiteSpace(sunErr))
            {
                EmitDiagnostic("[3D preview] Sun billboard shader: " + sunErr);
            }

            return;
        }

        Span<float> quad =
        [
            -1f, -1f, 1f, -1f, 1f, 1f,
            -1f, -1f, -1f, 1f, 1f, 1f
        ];
        _sunVao = gl.GenVertexArray();
        _sunVbo = gl.GenBuffer();
        gl.BindVertexArray(_sunVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _sunVbo);
        gl.BufferData<float>(GLEnum.ArrayBuffer, quad, GLEnum.StaticDraw);
        unsafe
        {
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        }

        gl.BindVertexArray(0);
    }

    private void DestroySunBillboard()
    {
        var gl = _gl;
        if (gl is null)
        {
            _sunProgram?.Dispose();
            _sunProgram = null;
            _sunVao = _sunVbo = 0;
            return;
        }

        if (_sunVbo != 0)
        {
            gl.DeleteBuffer(_sunVbo);
            _sunVbo = 0;
        }

        if (_sunVao != 0)
        {
            gl.DeleteVertexArray(_sunVao);
            _sunVao = 0;
        }

        _sunProgram?.Dispose();
        _sunProgram = null;
    }

    /// <summary>
    /// Visual sun along <c>-lightPropagationDir</c> (matches <see cref="PreviewLightMath"/> / Genesis <c>uLightDir</c>).
    /// </summary>
    private void DrawSunBillboard(GL gl, Matrix4x4 proj, Matrix4x4 view, Vector3 eye, Vector3 lightPropagationDir,
        bool restoreSolidBackFaceCull)
    {
        if (_sunProgram is null || !_sunProgram.IsValid || _sunVao == 0)
        {
            return;
        }

        var towardSun = -lightPropagationDir;
        var tls = towardSun.LengthSquared();
        if (tls < 1e-12f)
        {
            return;
        }

        towardSun /= MathF.Sqrt(tls);

        const float sunDist = 85f;
        const float sunRadius = 5.5f;
        var sunCenter = eye + towardSun * sunDist;

        var worldUp = Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(worldUp, towardSun));
        if (right.LengthSquared() < 1e-10f)
        {
            right = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, towardSun));
        }

        var sunBillboardUp = Vector3.Normalize(Vector3.Cross(towardSun, right));

        var viewProj = proj * view;
        _sunProgram.Use();
        var vpLoc = _sunProgram.GetUniformLocation("uViewProj");
        if (vpLoc >= 0)
        {
            var vpT = Matrix4x4.Transpose(viewProj);
            gl.UniformMatrix4(vpLoc, 1, false, in vpT.M11);
        }

        SetSunVec3(gl, "uSunCenter", sunCenter);
        SetSunVec3(gl, "uSunRight", right);
        SetSunVec3(gl, "uSunUp", sunBillboardUp);
        var rLoc = _sunProgram.GetUniformLocation("uRadius");
        if (rLoc >= 0)
        {
            gl.Uniform1(rLoc, sunRadius);
        }

        var blendWasEnabled = gl.IsEnabled(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(GLEnum.Lequal);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        gl.DepthMask(false);
        gl.Disable(EnableCap.CullFace);

        gl.BindVertexArray(_sunVao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);

        gl.DepthMask(true);
        if (!blendWasEnabled)
        {
            gl.Disable(EnableCap.Blend);
        }

        // Keep depth test enabled for the opaque mesh pass that follows.

        if (restoreSolidBackFaceCull)
        {
            gl.Enable(EnableCap.CullFace);
            gl.CullFace(GLEnum.Back);
            gl.FrontFace(GLEnum.Ccw);
        }
    }

    private void SetSunVec3(GL gl, string name, Vector3 v)
    {
        if (_sunProgram is null || !_sunProgram.IsValid)
        {
            return;
        }

        var loc = _sunProgram.GetUniformLocation(name);
        if (loc >= 0)
        {
            gl.Uniform3(loc, v.X, v.Y, v.Z);
        }
    }

    private void TryInitAtmosphere(GL gl, bool useOpenGlEs)
    {
        DestroyAtmosphereResources();
        _atmoTransProgram = new GlShaderProgram(gl, "atmo_lut.vert", "atmo_transmittance.frag", useOpenGlEs, out var transErr);
        if (_atmoTransProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Atmosphere transmittance shader: " + (transErr ?? "link failed"));
            _atmoTransProgram?.Dispose();
            _atmoTransProgram = null;
            return;
        }

        _atmoSkyViewProgram = new GlShaderProgram(gl, "atmo_lut.vert", "atmo_skyview.frag", useOpenGlEs, out var skyViewErr);
        if (_atmoSkyViewProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Atmosphere sky-view shader: " + (skyViewErr ?? "link failed"));
            _atmoSkyViewProgram?.Dispose();
            _atmoSkyViewProgram = null;
            return;
        }

        _atmoSkyProgram = new GlShaderProgram(gl, "atmo_sky.vert", "atmo_sky.frag", useOpenGlEs, out var skyErr);
        if (_atmoSkyProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Atmosphere sky shader: " + (skyErr ?? "link failed"));
            _atmoSkyProgram?.Dispose();
            _atmoSkyProgram = null;
            return;
        }

        Span<float> quad =
        [
            -1f, -1f, 1f, -1f, 1f, 1f,
            -1f, -1f, 1f, 1f, -1f, 1f
        ];
        _atmoQuadVao = gl.GenVertexArray();
        _atmoQuadVbo = gl.GenBuffer();
        gl.BindVertexArray(_atmoQuadVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _atmoQuadVbo);
        gl.BufferData<float>(GLEnum.ArrayBuffer, quad, GLEnum.StaticDraw);
        unsafe
        {
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        }

        gl.BindVertexArray(0);
        CreateAtmosphereTargets(gl);
        _atmoLutsValid = false;
    }

    private void CreateAtmosphereTargets(GL gl)
    {
        const int transW = 256;
        const int transH = 64;
        const int skyW = 192;
        const int skyH = 108;

        _atmoTransmittanceTex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _atmoTransmittanceTex);
        var transPixels = new byte[transW * transH * 4];
        unsafe
        {
            fixed (byte* pTrans = transPixels)
            {
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, transW, transH, 0,
                    Silk.NET.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, pTrans);
            }
        }
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _atmoSkyViewTex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _atmoSkyViewTex);
        var skyPixels = new byte[skyW * skyH * 4];
        unsafe
        {
            fixed (byte* pSky = skyPixels)
            {
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, skyW, skyH, 0,
                    Silk.NET.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, pSky);
            }
        }
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        var fboOk = true;
        _atmoTransmittanceFbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _atmoTransmittanceFbo);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D,
            _atmoTransmittanceTex, 0);
        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            EmitDiagnostic("[3D preview] Atmosphere transmittance FBO incomplete; disabling atmospheric LUT path.");
            fboOk = false;
        }

        _atmoSkyViewFbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _atmoSkyViewFbo);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D,
            _atmoSkyViewTex, 0);
        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            EmitDiagnostic("[3D preview] Atmosphere sky-view FBO incomplete; disabling atmospheric LUT path.");
            fboOk = false;
        }

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (!fboOk)
        {
            DestroyAtmosphereResources();
        }
    }

    private void EnsureAtmosphereLuts(GL gl, Vector3 worldLightDir, PreviewRenderSettings settings)
    {
        if (_atmoTransProgram is not { IsValid: true } || _atmoSkyViewProgram is not { IsValid: true } ||
            _atmoTransmittanceFbo == 0 || _atmoSkyViewFbo == 0 || _atmoQuadVao == 0)
        {
            return;
        }

        var sameSun = Vector3.DistanceSquared(worldLightDir, _lastAtmoSunDir) < 1e-6f;
        var sameParams = MathF.Abs(settings.AtmosphereTurbidity - _lastAtmoTurbidity) < 1e-4f &&
                         MathF.Abs(settings.AtmosphereSunIntensity - _lastAtmoSunIntensity) < 1e-4f &&
                         MathF.Abs(settings.AtmosphereHorizonFalloff - _lastAtmoHorizonFalloff) < 1e-4f;
        if (_atmoLutsValid && sameSun && sameParams)
        {
            return;
        }

        _lastAtmoSunDir = worldLightDir;
        _lastAtmoTurbidity = settings.AtmosphereTurbidity;
        _lastAtmoSunIntensity = settings.AtmosphereSunIntensity;
        _lastAtmoHorizonFalloff = settings.AtmosphereHorizonFalloff;

        var priorDrawFramebuffer = gl.GetInteger(GetPName.DrawFramebufferBinding);
        var priorViewport = new int[4];
        gl.GetInteger(GetPName.Viewport, priorViewport);
        var priorDepthTest = gl.IsEnabled(EnableCap.DepthTest);
        var priorCullFace = gl.IsEnabled(EnableCap.CullFace);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        var priorDepthMask = gl.GetBoolean(GetPName.DepthWritemask);
        var priorActiveTexture = gl.GetInteger(GetPName.ActiveTexture);

        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.Blend);
        gl.DepthMask(false);
        gl.BindVertexArray(_atmoQuadVao);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _atmoTransmittanceFbo);
        gl.Viewport(0, 0, 256u, 64u);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        _atmoTransProgram!.Use();
        SetFloatOnProgram(_atmoTransProgram, "uTurbidity", settings.AtmosphereTurbidity);
        SetFloatOnProgram(_atmoTransProgram, "uHorizonFalloff", settings.AtmosphereHorizonFalloff);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _atmoSkyViewFbo);
        gl.Viewport(0, 0, 192u, 108u);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        _atmoSkyViewProgram!.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _atmoTransmittanceTex);
        SetIntOnProgram(_atmoSkyViewProgram, "uTransmittanceLut", 0);
        SetVec3OnProgram(_atmoSkyViewProgram, "uSunDir", worldLightDir);
        SetFloatOnProgram(_atmoSkyViewProgram, "uTurbidity", settings.AtmosphereTurbidity);
        SetFloatOnProgram(_atmoSkyViewProgram, "uSunIntensity", settings.AtmosphereSunIntensity);
        SetFloatOnProgram(_atmoSkyViewProgram, "uHorizonFalloff", settings.AtmosphereHorizonFalloff);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        gl.BindVertexArray(0);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)Math.Max(0, priorDrawFramebuffer));
        gl.Viewport(priorViewport[0], priorViewport[1], (uint)priorViewport[2], (uint)priorViewport[3]);
        gl.ActiveTexture((TextureUnit)priorActiveTexture);
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

        if (priorBlend)
        {
            gl.Enable(EnableCap.Blend);
        }
        else
        {
            gl.Disable(EnableCap.Blend);
        }

        var glErr = gl.GetError();
        if (glErr != GLEnum.NoError)
        {
            EmitDiagnostic($"[3D preview] Atmosphere LUT pass error: {glErr}. Atmosphere fallback engaged.");
            _atmoLutsValid = false;
            return;
        }

        _atmoLutsValid = true;
    }

    private void DrawAtmosphereSky(GL gl, Vector3 worldLightDir, PreviewRenderSettings settings)
    {
        if (_atmoSkyProgram is not { IsValid: true } || _atmoQuadVao == 0 || _atmoSkyViewTex == 0)
        {
            return;
        }

        _atmoSkyProgram.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _atmoSkyViewTex);
        SetIntOnProgram(_atmoSkyProgram, "uSkyViewLut", 0);
        SetVec3OnProgram(_atmoSkyProgram, "uSunDir", worldLightDir);
        SetFloatOnProgram(_atmoSkyProgram, "uSunIntensity", settings.AtmosphereSunIntensity);
        gl.BindVertexArray(_atmoQuadVao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);
        var glErrAfterSky = gl.GetError();
        if (glErrAfterSky != GLEnum.NoError)
        {
            EmitDiagnostic($"[3D preview] Atmosphere sky pass error: {glErrAfterSky}. Atmosphere fallback engaged.");
            _atmoLutsValid = false;
        }
    }

    private void DestroyAtmosphereResources()
    {
        var gl = _gl;
        _atmoLutsValid = false;
        _lastAtmoTurbidity = _lastAtmoSunIntensity = _lastAtmoHorizonFalloff = -1f;

        if (gl is null)
        {
            _atmoTransProgram?.Dispose();
            _atmoSkyViewProgram?.Dispose();
            _atmoSkyProgram?.Dispose();
            _atmoTransProgram = null;
            _atmoSkyViewProgram = null;
            _atmoSkyProgram = null;
            _atmoQuadVao = _atmoQuadVbo = 0;
            _atmoTransmittanceTex = _atmoSkyViewTex = 0;
            _atmoTransmittanceFbo = _atmoSkyViewFbo = 0;
            return;
        }

        if (_atmoTransmittanceFbo != 0)
        {
            gl.DeleteFramebuffer(_atmoTransmittanceFbo);
            _atmoTransmittanceFbo = 0;
        }

        if (_atmoSkyViewFbo != 0)
        {
            gl.DeleteFramebuffer(_atmoSkyViewFbo);
            _atmoSkyViewFbo = 0;
        }

        if (_atmoTransmittanceTex != 0)
        {
            gl.DeleteTexture(_atmoTransmittanceTex);
            _atmoTransmittanceTex = 0;
        }

        if (_atmoSkyViewTex != 0)
        {
            gl.DeleteTexture(_atmoSkyViewTex);
            _atmoSkyViewTex = 0;
        }

        if (_atmoQuadVbo != 0)
        {
            gl.DeleteBuffer(_atmoQuadVbo);
            _atmoQuadVbo = 0;
        }

        if (_atmoQuadVao != 0)
        {
            gl.DeleteVertexArray(_atmoQuadVao);
            _atmoQuadVao = 0;
        }

        _atmoTransProgram?.Dispose();
        _atmoTransProgram = null;
        _atmoSkyViewProgram?.Dispose();
        _atmoSkyViewProgram = null;
        _atmoSkyProgram?.Dispose();
        _atmoSkyProgram = null;
    }

}
