using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;

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
        float discStrength, bool restoreSolidBackFaceCull)
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

        if (towardSun.Y < -0.03f)
        {
            return;
        }

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

        var strengthLoc = _sunProgram.GetUniformLocation("uDiscStrength");
        if (strengthLoc >= 0)
        {
            gl.Uniform1(strengthLoc, Math.Max(discStrength, 0f));
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
        EnsureAtmoQuadBuffer(gl);

        _atmoSkyProgram = new GlShaderProgram(gl, "atmo_sky.vert", "atmo_sky.frag", useOpenGlEs, out var skyErr);
        if (_atmoSkyProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Atmosphere sky shader: " + (skyErr ?? "link failed"));
            _atmoSkyProgram?.Dispose();
            _atmoSkyProgram = null;
            _proceduralSkyProgram = new GlProceduralSkyProgram(gl, useOpenGlEs, out var procErr);
            if (_proceduralSkyProgram is not { IsValid: true })
            {
                EmitDiagnostic("[3D preview] Procedural sky fallback: " + (procErr ?? "link failed"));
                _proceduralSkyProgram?.Dispose();
                _proceduralSkyProgram = null;
                return;
            }

            EmitDiagnostic("[3D preview] Using embedded procedural sky (LUT sky shader unavailable).");
        }
        else
        {
            _proceduralSkyProgram = new GlProceduralSkyProgram(gl, useOpenGlEs, out _);
        }

        _atmoTransProgram = new GlShaderProgram(gl, "atmo_lut.vert", "atmo_transmittance.frag", useOpenGlEs, out var transErr);
        if (_atmoTransProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Atmosphere transmittance shader: " + (transErr ?? "link failed"));
            _atmoTransProgram?.Dispose();
            _atmoTransProgram = null;
        }

        _atmoSkyViewProgram = new GlShaderProgram(gl, "atmo_lut.vert", "atmo_skyview.frag", useOpenGlEs, out var skyViewErr);
        if (_atmoSkyViewProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Atmosphere sky-view shader: " + (skyViewErr ?? "link failed"));
            _atmoSkyViewProgram?.Dispose();
            _atmoSkyViewProgram = null;
        }

        if (!TryCreateAtmosphereTargets(gl))
        {
            EmitDiagnostic("[3D preview] Atmosphere LUT targets unavailable; procedural sky only.");
        }

        _atmoLutsValid = false;
    }

    private void EnsureAtmoQuadBuffer(GL gl)
    {
        if (_atmoQuadVao != 0)
        {
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
    }

    private bool TryCreateAtmosphereTargets(GL gl)
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
                    PixelFormat.Rgba, PixelType.UnsignedByte, pTrans);
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
                    PixelFormat.Rgba, PixelType.UnsignedByte, pSky);
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
        ConfigureSingleColorDrawBuffer(gl);
        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            EmitDiagnostic("[3D preview] Atmosphere transmittance FBO incomplete; disabling atmospheric LUT path.");
            fboOk = false;
        }

        _atmoSkyViewFbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _atmoSkyViewFbo);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D,
            _atmoSkyViewTex, 0);
        ConfigureSingleColorDrawBuffer(gl);
        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            EmitDiagnostic("[3D preview] Atmosphere sky-view FBO incomplete; disabling atmospheric LUT path.");
            fboOk = false;
        }

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return fboOk;
    }

    private static float EffectiveAtmosphereSunIntensity(Vector3 worldLightDir, float userIntensity)
    {
        var towardSun = -worldLightDir;
        if (towardSun.LengthSquared() < 1e-12f)
        {
            return userIntensity * 0.08f;
        }

        towardSun = Vector3.Normalize(towardSun);
        var dayFactor = Math.Clamp((towardSun.Y + 0.04f) / 0.26f, 0f, 1f);
        return userIntensity * (0.08f + 0.92f * dayFactor);
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

        while (gl.GetError() != GLEnum.NoError)
        {
        }

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
        ConfigureSingleColorDrawBuffer(gl);
        gl.Viewport(0, 0, 256u, 64u);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        _atmoTransProgram!.Use();
        SetFloatOnProgram(_atmoTransProgram, "uTurbidity", settings.AtmosphereTurbidity);
        SetFloatOnProgram(_atmoTransProgram, "uHorizonFalloff", settings.AtmosphereHorizonFalloff);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _atmoSkyViewFbo);
        ConfigureSingleColorDrawBuffer(gl);
        gl.Viewport(0, 0, 192u, 108u);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        _atmoSkyViewProgram!.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _atmoTransmittanceTex);
        SetIntOnProgram(_atmoSkyViewProgram, "uTransmittanceLut", 0);
        SetVec3OnProgram(_atmoSkyViewProgram, "uSunDir", worldLightDir);
        SetFloatOnProgram(_atmoSkyViewProgram, "uTurbidity", settings.AtmosphereTurbidity);
        SetFloatOnProgram(_atmoSkyViewProgram, "uSunIntensity",
            EffectiveAtmosphereSunIntensity(worldLightDir, settings.AtmosphereSunIntensity));
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

    private void ConfigureSingleColorDrawBuffer(GL gl)
    {
        if (_useOpenGlEs)
        {
            unsafe
            {
                var buf = DrawBufferMode.ColorAttachment0;
                gl.DrawBuffers(1, &buf);
            }
        }
        else
        {
            gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
        }
    }

    private void DrawAtmosphereSky(GL gl, ref GlRenderFrame frame, bool lutSkyReady)
    {
        if (_atmoQuadVao == 0)
        {
            return;
        }

        var aspect = frame.Vw / (float)Math.Max(frame.Vh, 1);
        var viewProj = frame.Proj * frame.View;
        if (!Matrix4x4.Invert(viewProj, out var invViewProj))
        {
            invViewProj = Matrix4x4.Identity;
        }

        PreviewSunScreenProjection.Compute(frame.Eye, frame.LightDir, frame.View, frame.Proj, aspect,
            frame.Settings.GodRayConeScale, out _, out var sunDiscRadiusUv, out _, out var sunCosDiscEdge);
        PreviewSunScreenProjection.ComputeMoon(frame.Eye, frame.LightDir, frame.View, frame.Proj, aspect,
            out _, out _, out var moonCosDiscEdge);

        if (_atmoSkyProgram is { IsValid: true })
        {
            _atmoSkyProgram.Use();
            if (lutSkyReady && _atmoSkyViewTex != 0)
            {
                gl.ActiveTexture(TextureUnit.Texture0);
                gl.BindTexture(TextureTarget.Texture2D, _atmoSkyViewTex);
            }

            SetIntOnProgram(_atmoSkyProgram, "uSkyViewLut", 0);
            SetIntOnProgram(_atmoSkyProgram, "uLutValid", lutSkyReady && _atmoSkyViewTex != 0 ? 1 : 0);
            SetFloatOnProgram(_atmoSkyProgram, "uTurbidity", frame.Settings.AtmosphereTurbidity);
            SetFloatOnProgram(_atmoSkyProgram, "uHorizonFalloff", frame.Settings.AtmosphereHorizonFalloff);
            SetMatrixOnProgram(_atmoSkyProgram, "uInvViewProj", invViewProj);
            SetVec3OnProgram(_atmoSkyProgram, "uCameraPos", frame.Eye);
            SetVec3OnProgram(_atmoSkyProgram, "uLightDir", frame.LightDir);
            SetFloatOnProgram(_atmoSkyProgram, "uSunIntensity", frame.Settings.AtmosphereSunIntensity);
            SetFloatOnProgram(_atmoSkyProgram, "uHorizonFogStrength",
                frame.Settings.EnableAtmosphericSky ? frame.Settings.AerialFogStrength : 0f);
            SetFloatOnProgram(_atmoSkyProgram, "uSkyExposure", frame.Settings.AtmosphereSkyExposure);
            SetFloatOnProgram(_atmoSkyProgram, "uSunDiscStrength", frame.Settings.AtmosphereSunDiscStrength);
            SetFloatOnProgram(_atmoSkyProgram, "uSunCosDiscEdge", sunCosDiscEdge);
            SetFloatOnProgram(_atmoSkyProgram, "uMoonCosDiscEdge", moonCosDiscEdge);
            SetFloatOnProgram(_atmoSkyProgram, "uRenderTime", (float)frame.RenderTime);
            SetFloatOnProgram(_atmoSkyProgram, "uViewportAspect", aspect);
            SetFloatOnProgram(_atmoSkyProgram, "uSunDiscRadiusUv", sunDiscRadiusUv);
            gl.BindVertexArray(_atmoQuadVao);
            gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
            gl.BindVertexArray(0);
            var glErrAfterSky = gl.GetError();
            if (glErrAfterSky != GLEnum.NoError)
            {
                EmitDiagnostic($"[3D preview] Atmosphere sky pass error: {glErrAfterSky}. Atmosphere fallback engaged.");
                _atmoLutsValid = false;
            }

            return;
        }

        if (_proceduralSkyProgram is { IsValid: true })
        {
            _proceduralSkyProgram.Use();
            SetMatrixOnProgram(_proceduralSkyProgram, "uInvViewProj", invViewProj);
            SetVec3OnProgram(_proceduralSkyProgram, "uCameraPos", frame.Eye);
            SetVec3OnProgram(_proceduralSkyProgram, "uLightDir", frame.LightDir);
            SetFloatOnProgram(_proceduralSkyProgram, "uSunIntensity", frame.Settings.AtmosphereSunIntensity);
            SetFloatOnProgram(_proceduralSkyProgram, "uSkyExposure", frame.Settings.AtmosphereSkyExposure);
            SetFloatOnProgram(_proceduralSkyProgram, "uRenderTime", (float)frame.RenderTime);
            SetFloatOnProgram(_proceduralSkyProgram, "uTurbidity", frame.Settings.AtmosphereTurbidity);
            SetFloatOnProgram(_proceduralSkyProgram, "uHorizonFalloff", frame.Settings.AtmosphereHorizonFalloff);
            SetFloatOnProgram(_proceduralSkyProgram, "uHorizonFogStrength",
                frame.Settings.EnableAtmosphericSky ? frame.Settings.AerialFogStrength : 0f);
            SetFloatOnProgram(_proceduralSkyProgram, "uSunDiscStrength", frame.Settings.AtmosphereSunDiscStrength);
            SetFloatOnProgram(_proceduralSkyProgram, "uSunCosDiscEdge", sunCosDiscEdge);
            SetFloatOnProgram(_proceduralSkyProgram, "uMoonCosDiscEdge", moonCosDiscEdge);
            SetFloatOnProgram(_proceduralSkyProgram, "uViewportAspect", aspect);
            SetFloatOnProgram(_proceduralSkyProgram, "uSunDiscRadiusUv", sunDiscRadiusUv);
            gl.BindVertexArray(_atmoQuadVao);
            gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
            gl.BindVertexArray(0);
            return;
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
            _proceduralSkyProgram?.Dispose();
            _atmoTransProgram = null;
            _atmoSkyViewProgram = null;
            _atmoSkyProgram = null;
            _proceduralSkyProgram = null;
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
        _proceduralSkyProgram?.Dispose();
        _proceduralSkyProgram = null;
    }

}
