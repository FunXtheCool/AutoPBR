using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;

using Silk.NET.OpenGL;

using AutoPBR.PreviewGpuAssets;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private GlShaderProgram? _cloudProgram;
    private GlShaderProgram? _cloudUpsampleProgram;
    private uint _cloudQuadVao;
    private uint _cloudQuadVbo;
    private GlTexture3D? _cloudNoiseTex;
    private GlTexture3D? _cloudDetailTex;
    private GlTexture2D? _cloudCoverageTex;
    private GlColorRenderTarget? _cloudRenderTarget;
    private GlColorRenderTarget? _cloudHistoryTarget;
    private Matrix4x4 _cloudPrevViewProj = Matrix4x4.Identity;
    private bool _cloudHistoryValid;
    private float _cloudFramePhase;
    private int _cloudHistoryW;
    private int _cloudHistoryH;
    private bool _loggedCloudDraw;
    private int _cloudDeferredCompositeRetries;
    private int _loggedCloudDeferredCompositeMiss;
    private int _cloudTierReadyWarmupDraws;
    private int _loggedCloudDrawGlError;

    private void TryInitVolumetricClouds(GL gl, bool useOpenGlEs)
    {
        DestroyVolumetricCloudResources();
        _cloudProgram = CreatePreviewProgram("genesis_godrays.vert", "genesis_clouds.frag", out var err);
        if (_cloudProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Volumetric cloud shader: " + (err ?? "link failed"));
            _cloudProgram?.Dispose();
            _cloudProgram = null;
            return;
        }

        _cloudUniformLocs = ResolveCloudUniformLocs(_cloudProgram);

        // Depth-aware half-res upsample; non-fatal, falls back to the god-ray composite blit.
        _cloudUpsampleProgram = CreatePreviewProgram("genesis_godrays.vert", "genesis_clouds_upsample.frag",
            out var upErr, "clouds-upsample");
        if (_cloudUpsampleProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Cloud upsample shader: " + (upErr ?? "link failed"));
            _cloudUpsampleProgram?.Dispose();
            _cloudUpsampleProgram = null;
        }
        else
        {
            _cloudUpsampleUniformLocs = ResolveCloudUpsampleUniformLocs(_cloudUpsampleProgram);
        }

        if (_godRayCompositeProgram is { IsValid: true })
        {
            _cloudCompositeUniformLocs = ResolveCloudCompositeUniformLocs(_godRayCompositeProgram);
        }

        _cloudNoiseTex = new GlTexture3D(gl);
        if (PreviewCloudBakedAssetLoader.TryLoadShapeNoise(out var shapeRgba))
        {
            _cloudNoiseTex.UploadRgba(PreviewCloudNoiseTextureGenerator.Size, shapeRgba);
        }
        else
        {
            _cloudNoiseTex.UploadRgba(PreviewCloudNoiseTextureGenerator.Size,
                PreviewCloudNoiseTextureGenerator.GenerateRgba8());
        }

        _cloudDetailTex = new GlTexture3D(gl);
        if (PreviewCloudBakedAssetLoader.TryLoadDetailNoise(out var detailRgba))
        {
            _cloudDetailTex.UploadRgba(PreviewCloudNoiseTextureGenerator.DetailSize, detailRgba);
        }
        else
        {
            _cloudDetailTex.UploadRgba(PreviewCloudNoiseTextureGenerator.DetailSize,
                PreviewCloudNoiseTextureGenerator.GenerateDetailRgba8());
        }

        _cloudCoverageTex = new GlTexture2D(gl, nearestFilter: false);
        if (PreviewCloudBakedAssetLoader.TryLoadCoverageMap(out var coverageRgba))
        {
            _cloudCoverageTex.UploadRgba(PreviewCloudCoverageMapGenerator.Size, PreviewCloudCoverageMapGenerator.Size,
                coverageRgba, nearestFilter: false);
        }
        else
        {
            _cloudCoverageTex.UploadRgba(PreviewCloudCoverageMapGenerator.Size, PreviewCloudCoverageMapGenerator.Size,
                PreviewCloudCoverageMapGenerator.GenerateRgba8(), nearestFilter: false);
        }
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

    private void TryWarmCloudOffscreenTargets(int fullWidth, int fullHeight)
    {
        var w = Math.Max(1, fullWidth / 2);
        var h = Math.Max(1, fullHeight / 2);
        _cloudHistoryW = w;
        _cloudHistoryH = h;
        _cloudHistoryValid = false;
        if (_cloudRenderTarget is not null)
        {
            _ = _cloudRenderTarget.EnsureSize(w, h);
        }

        if (_cloudHistoryTarget is not null)
        {
            _ = _cloudHistoryTarget.EnsureSize(w, h);
        }
    }

    private void DestroyVolumetricCloudResources()
    {
        var gl = _gl;
        _cloudProgram?.Dispose();
        _cloudProgram = null;
        _cloudUpsampleProgram?.Dispose();
        _cloudUpsampleProgram = null;
        _cloudNoiseTex?.Dispose();
        _cloudNoiseTex = null;
        _cloudDetailTex?.Dispose();
        _cloudDetailTex = null;
        _cloudCoverageTex?.Dispose();
        _cloudCoverageTex = null;
        _cloudRenderTarget?.Dispose();
        _cloudRenderTarget = null;
        _cloudHistoryTarget?.Dispose();
        _cloudHistoryTarget = null;
        _cloudHistoryValid = false;
        _loggedCloudDraw = false;
        _cloudDeferredCompositeRetries = 0;
        _loggedCloudDeferredCompositeMiss = 0;
        _loggedCloudDrawGlError = 0;
        _cloudTierReadyWarmupDraws = 0;

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

    private bool CanDrawVolumetricClouds(in PreviewRenderSettingsSnapshot settings) =>
        settings.EnableVolumetricClouds &&
        _cloudProgram is { IsValid: true } &&
        _cloudQuadVao != 0;

    private bool DrawVolumetricClouds(
        ref GlRenderFrame frame,
        bool gateSkyDepth,
        bool deferComposite = false,
        bool? forceTemporal = null,
        bool updateHistory = true)
    {
        if (!CanDrawVolumetricClouds(frame.Settings))
        {
            return false;
        }

        BindDefaultFramebuffer(ref frame);
        return DrawVolumetricCloudsInternal(ref frame, gateSkyDepth, deferComposite, forceTemporal, updateHistory);
    }

    /// <summary>
    /// Cloud in-shader temporal uses slab-distance reprojection and is independent of scene depth gating.
    /// Disabled when god rays are active: god-ray history + preview TAA already stabilize the composited
    /// frame; stacking cloud history caused a visible frustum over the sky layer.
    /// </summary>
    private static bool ShouldUseCloudShaderTemporal(in PreviewRenderSettingsSnapshot settings, bool godRaysActive)
    {
        if (godRaysActive || settings.CloudDisableTemporal ||
            settings.CloudDebugView != PreviewCloudDebugView.Off)
        {
            return false;
        }

        var profile = PreviewVolumetricQuality.Resolve(settings.VolumetricQuality);
        return profile.CloudTemporalWeight > 0f;
    }

    private static bool CanUseCloudTemporalReproject(in PreviewRenderSettingsSnapshot settings)
    {
        return ShouldUseCloudShaderTemporal(settings, godRaysActive: false);
    }

    private bool DrawVolumetricCloudsInternal(
        ref GlRenderFrame frame,
        bool gateSkyDepth,
        bool deferComposite = false,
        bool? forceTemporal = null,
        bool updateHistory = true)
    {
        var settings = frame.Settings;
        var viewProj = frame.Proj * frame.View;
        if (!Matrix4x4.Invert(viewProj, out var invViewProj))
        {
            return false;
        }

        var gl = frame.Gl;
        var profile = PreviewVolumetricQuality.Resolve(settings.VolumetricQuality);
        var layerWorldY = PreviewStageConstants.CloudLayerBaseWorldY(settings.CloudLayerHeight);
        var useDepthGate = gateSkyDepth && _sceneCapture is { IsValid: true };
        var useTemporalReproject = forceTemporal ??
            (CanUseCloudTemporalReproject(settings) &&
             _cloudRenderTarget is not null && _cloudHistoryTarget is not null);
        var useOffscreen = deferComposite || useTemporalReproject;
        var wantedOffscreen = useOffscreen;

        if (useOffscreen)
        {
            var w = Math.Max(1, frame.Vw / 2);
            var h = Math.Max(1, frame.Vh / 2);
            if (_cloudHistoryW != w || _cloudHistoryH != h)
            {
                _cloudHistoryValid = false;
                _cloudHistoryW = w;
                _cloudHistoryH = h;
            }

            if (_cloudRenderTarget is null || !_cloudRenderTarget.EnsureSize(w, h))
            {
                if (deferComposite)
                {
                    if (_cloudDeferredCompositeRetries > 0)
                    {
                        _cloudDeferredCompositeRetries--;
                    }

                    return false;
                }

                useOffscreen = false;
                useTemporalReproject = false;
            }
            else if (deferComposite)
            {
                _cloudDeferredCompositeRetries = 0;
            }
        }

        if (useOffscreen)
        {
            _cloudRenderTarget!.BindDraw();
            // Transparent black, not the scene clear color: discarded pixels must stay
            // alpha 0 or the composite stamps opaque near-black over the sky between clouds.
            gl.ClearColor(0f, 0f, 0f, 0f);
            gl.Clear(ClearBufferMask.ColorBufferBit);
        }

        BindCloudShaderUniforms(frame, invViewProj, viewProj, layerWorldY, profile, useDepthGate, useTemporalReproject);

        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        var priorScissor = gl.IsEnabled(EnableCap.ScissorTest);
        var priorColorMask = new bool[4];
        gl.GetBoolean(GetPName.ColorWritemask, priorColorMask);
        if (!useOffscreen)
        {
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        gl.Disable(EnableCap.DepthTest);
        gl.DepthMask(false);
        gl.Disable(EnableCap.ScissorTest);
        gl.ColorMask(true, true, true, true);
        FlushPendingGlErrors(gl);
        gl.BindVertexArray(_cloudQuadVao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        var cloudDrawErr = gl.GetError();
        gl.BindVertexArray(0);
        gl.DepthMask(true);
        gl.Enable(EnableCap.DepthTest);
        gl.ColorMask(priorColorMask[0], priorColorMask[1], priorColorMask[2], priorColorMask[3]);
        if (priorScissor)
        {
            gl.Enable(EnableCap.ScissorTest);
        }

        if (cloudDrawErr != GLEnum.NoError && _loggedCloudDrawGlError == 0)
        {
            _loggedCloudDrawGlError = 1;
            EmitDiagnostic($"[3D preview] Volumetric cloud draw GL error: {cloudDrawErr}");
        }

        if (useOffscreen)
        {
            if (!deferComposite)
            {
                CompositeCloudRenderTargetToDefault(ref frame);
            }

            if (useTemporalReproject && updateHistory)
            {
                _cloudHistoryTarget!.CopyColorFrom(_cloudRenderTarget!);
                _cloudPrevViewProj = viewProj;
                _cloudHistoryValid = true;
                _cloudFramePhase += 0.071f;
                if (_cloudFramePhase > 1f)
                {
                    _cloudFramePhase -= 1f;
                }
            }
        }

        if (useOffscreen && deferComposite)
        {
            BindDefaultFramebuffer(ref frame);
        }

        if (!priorBlend && !useOffscreen)
        {
            gl.Disable(EnableCap.Blend);
        }

        if (!_loggedCloudDraw)
        {
            _loggedCloudDraw = true;
            var godRays = frame.GodRayCaptureActive && _sceneCapture is { IsValid: true };
            EmitDiagnostic($"[3D preview] Screen-space volumetric clouds active (depthGate={useDepthGate}, " +
                $"shaderTemporal={useTemporalReproject}, godRays={godRays}, " +
                $"previewTaa={frame.Settings.EnablePreviewTaa}, warmupDraws={_cloudTierReadyWarmupDraws}, " +
                $"noiseTex={_cloudNoiseTex is not null}, coverageMap={_cloudCoverageTex is not null}).");
        }

        return wantedOffscreen ? useOffscreen : true;
    }

    private void BindCloudShaderUniforms(
        GlRenderFrame frame,
        Matrix4x4 invViewProj,
        Matrix4x4 viewProj,
        float layerWorldY,
        PreviewVolumetricQuality.Profile profile,
        bool useDepthGate,
        bool useTemporalReproject)
    {
        var gl = frame.Gl;
        var settings = frame.Settings;
        var cu = _cloudUniformLocs;

        // GLES/ANGLE: sampler uniforms default to texture unit 0, and draw validation
        // rejects a program whose samplers of different types (sampler3D uCloudNoise vs
        // the sampler2D uniforms) reference the same unit — the whole cloud quad is then
        // silently dropped with GL_INVALID_OPERATION. On cold start with god rays active,
        // uPrevClouds (and uSceneDepth on the warmup path) were never assigned and sat on
        // unit 0 alongside uCloudNoise, so clouds never rendered until a god-ray retoggle
        // happened to route through the clouds-only path that assigns them. Pin every
        // sampler to its own unit unconditionally; the uHas*/uGateSkyDepth flags keep
        // unbound units from being sampled.
        SetIntOnProgramLoc(_cloudProgram, cu.CloudNoise, 0);
        SetIntOnProgramLoc(_cloudProgram, cu.CoverageMap, 1);
        SetIntOnProgramLoc(_cloudProgram, cu.SkyViewLut, 2);
        SetIntOnProgramLoc(_cloudProgram, cu.DetailNoise, 3);
        SetIntOnProgramLoc(_cloudProgram, cu.SceneDepth, 5);
        SetIntOnProgramLoc(_cloudProgram, cu.PrevClouds, 6);

        SetFloatOnProgramLoc(_cloudProgram, cu.SunIntensity, settings.AtmosphereSunIntensity);
        SetFloatOnProgramLoc(_cloudProgram, cu.SkyExposure, settings.AtmosphereSkyExposure);
        SetFloatOnProgramLoc(_cloudProgram, cu.LayerHeight, layerWorldY);
        SetFloatOnProgramLoc(_cloudProgram, cu.VolumeHeight, settings.CloudVolumeHeight);
        SetFloatOnProgramLoc(_cloudProgram, cu.Density, settings.CloudDensity);
        SetFloatOnProgramLoc(_cloudProgram, cu.CoverageScale, settings.CloudCoverageScale);
        SetFloatOnProgramLoc(_cloudProgram, cu.VolumeSize, settings.CloudVolumeSize);
        SetIntOnProgramLoc(_cloudProgram, cu.Quality, profile.CloudQuality);
        SetIntOnProgramLoc(_cloudProgram, cu.MarchSteps, Math.Clamp(settings.CloudMarchStepOverride, 0, 64));
        SetIntOnProgramLoc(_cloudProgram, cu.DebugView, (int)settings.CloudDebugView);
        SetMatrixOnProgramLoc(_cloudProgram, cu.InvViewProj, invViewProj);
        SetMatrixOnProgramLoc(_cloudProgram, cu.PrevViewProj, _cloudPrevViewProj);
        SetVec3OnProgramLoc(_cloudProgram, cu.CameraPos, frame.Eye);
        SetVec3OnProgramLoc(_cloudProgram, cu.SunDir, frame.LightDir);
        var windTime = settings.CloudFreezeWind ? 0.0 : frame.RenderTime;
        SetVec3OnProgramLoc(_cloudProgram, cu.WindOffset, ComputeCloudWindOffset(windTime, settings));
        SetFloatOnProgramLoc(_cloudProgram, cu.CirrusStrength, settings.CloudCirrusStrength);
        SetVec2OnProgramLoc(_cloudProgram, cu.CirrusWindOffset, ComputeCirrusWindOffset(windTime, settings));
        SetIntOnProgramLoc(_cloudProgram, cu.GateSkyDepth, useDepthGate ? 1 : 0);
        SetFloatOnProgramLoc(_cloudProgram, cu.TemporalWeight,
            useTemporalReproject
                ? PreviewVolumetricQuality.EffectivePassTemporalWeight(profile.CloudTemporalWeight, settings)
                : 0f);
        SetFloatOnProgramLoc(_cloudProgram, cu.FramePhase, _cloudFramePhase);
        SetIntOnProgramLoc(_cloudProgram, cu.HasCloudNoise, _cloudNoiseTex is not null ? 1 : 0);
        SetIntOnProgramLoc(_cloudProgram, cu.HasDetailNoise, _cloudDetailTex is not null ? 1 : 0);
        SetIntOnProgramLoc(_cloudProgram, cu.HasCoverageMap, _cloudCoverageTex is not null ? 1 : 0);
        SetIntOnProgramLoc(_cloudProgram, cu.HasSkyLut, _atmoLutsValid && _atmoSkyViewTex != 0 ? 1 : 0);
        SetIntOnProgramLoc(_cloudProgram, cu.HasPrevClouds,
            useTemporalReproject && _cloudHistoryValid ? 1 : 0);

        if (_cloudNoiseTex is not null)
        {
            _cloudNoiseTex.Bind(0);
        }

        if (_cloudCoverageTex is not null)
        {
            _cloudCoverageTex.Bind(1);
        }

        if (_cloudDetailTex is not null)
        {
            _cloudDetailTex.Bind(3);
        }

        if (_atmoLutsValid && _atmoSkyViewTex != 0)
        {
            gl.ActiveTexture(TextureUnit.Texture2);
            gl.BindTexture(TextureTarget.Texture2D, _atmoSkyViewTex);
        }

        if (useDepthGate && _sceneCapture is not null)
        {
            gl.ActiveTexture(TextureUnit.Texture5);
            gl.BindTexture(TextureTarget.Texture2D, _sceneCapture.DepthTextureHandle);
        }

        if (useTemporalReproject && _cloudHistoryTarget is not null && _cloudHistoryValid)
        {
            gl.ActiveTexture(TextureUnit.Texture6);
            gl.BindTexture(TextureTarget.Texture2D, _cloudHistoryTarget.ColorTextureHandle);
        }
    }

    /// <summary>
    /// World-space wind drift for the cloud field. Components wrap at the weather-map period
    /// (volumeSize * 4); the shape (×2) and detail (×1 in offset space) periods divide it evenly,
    /// so the wrap never produces a visible snap, and floats stay small over long sessions.
    /// </summary>
    private static Vector3 ComputeCloudWindOffset(double renderTime, in PreviewRenderSettingsSnapshot settings)
    {
        var period = Math.Max(settings.CloudVolumeSize, 8f) * 4f;
        var heading = settings.CloudWindHeadingDegrees * (MathF.PI / 180f);
        var travel = renderTime * settings.CloudWindSpeed;
        var wx = (float)((MathF.Cos(heading) * travel) % period);
        var wz = (float)((MathF.Sin(heading) * travel) % period);
        return new Vector3(wx, 0f, wz);
    }

    /// <summary>
    /// High-altitude wind for the cirrus sheet: faster than the cumulus layer and slightly
    /// veered, as real upper winds are. The cirrus noise is procedural (non-tiling), so the
    /// offset stays unwrapped; float precision is ample for multi-hour preview sessions.
    /// </summary>
    private static Vector2 ComputeCirrusWindOffset(double renderTime, in PreviewRenderSettingsSnapshot settings)
    {
        var heading = (settings.CloudWindHeadingDegrees + 18f) * (MathF.PI / 180f);
        var travel = (float)(renderTime * settings.CloudWindSpeed * 2.4);
        return new Vector2(MathF.Cos(heading) * travel, MathF.Sin(heading) * travel);
    }

    private void CompositeCloudRenderTargetToDefault(ref GlRenderFrame frame)
    {
        var useUpsample = _cloudUpsampleProgram is { IsValid: true };
        var program = useUpsample ? _cloudUpsampleProgram : _godRayCompositeProgram;
        if (_cloudRenderTarget is null || program is not { IsValid: true } || _godRayQuadVao == 0)
        {
            BindDefaultFramebuffer(ref frame);
            return;
        }

        var gl = frame.Gl;
        BindDefaultFramebuffer(ref frame);
        var priorBlend = gl.IsEnabled(EnableCap.Blend);
        var priorScissor = gl.IsEnabled(EnableCap.ScissorTest);
        var priorColorMask = new bool[4];
        gl.GetBoolean(GetPName.ColorWritemask, priorColorMask);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.ScissorTest);
        gl.ColorMask(true, true, true, true);
        FlushPendingGlErrors(gl);
        gl.BindVertexArray(_godRayQuadVao);
        program.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _cloudRenderTarget.ColorTextureHandle);
        if (useUpsample)
        {
            var upu = _cloudUpsampleUniformLocs;
            SetIntOnProgramLoc(program, upu.Clouds, 0);
            SetVec2OnProgramLoc(program, upu.CloudTexelSize, new Vector2(
                1f / Math.Max(_cloudRenderTarget.Width, 1),
                1f / Math.Max(_cloudRenderTarget.Height, 1)));
            var hasDepth = _sceneCapture is { IsValid: true };
            SetIntOnProgramLoc(program, upu.HasSceneDepth, hasDepth ? 1 : 0);
            if (hasDepth)
            {
                gl.ActiveTexture(TextureUnit.Texture1);
                gl.BindTexture(TextureTarget.Texture2D, _sceneCapture!.DepthTextureHandle);
                SetIntOnProgramLoc(program, upu.SceneDepth, 1);
            }
        }
        else
        {
            SetIntOnProgramLoc(program, _cloudCompositeUniformLocs.Rays, 0);
        }

        gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        gl.BindVertexArray(0);
        gl.Enable(EnableCap.DepthTest);
        gl.ColorMask(priorColorMask[0], priorColorMask[1], priorColorMask[2], priorColorMask[3]);
        if (priorScissor)
        {
            gl.Enable(EnableCap.ScissorTest);
        }

        if (!priorBlend)
        {
            gl.Disable(EnableCap.Blend);
        }
    }
}
