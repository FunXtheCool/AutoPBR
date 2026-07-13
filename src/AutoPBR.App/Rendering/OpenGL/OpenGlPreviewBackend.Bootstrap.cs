using AutoPBR.App.Rendering.Scene;

using AutoPBR.App.Lang;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private GpuBootstrapRunner? _gpuBootstrap;
    private bool _gpuBootstrapAborted;
    private bool _pendingShaderReload;
    private string _glVersionString = "(unknown)";

    public string? ActiveContextSummary { get; private set; }

    private sealed class GpuBootstrapRunner
    {
        private int _step;
        private const int StepCount = 8;

        public bool IsComplete => _step >= StepCount;

        public double Fraction => Math.Clamp((double)_step / StepCount, 0.0, 1.0);

        public string Phase => PreviewGpuInitPhases.BootstrapPhase(_step);

        public void Advance(OpenGlPreviewBackend backend, double maxMilliseconds)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_step < StepCount && sw.Elapsed.TotalMilliseconds < maxMilliseconds)
            {
                if (!backend.RunGpuBootstrapStep(_step))
                {
                    return;
                }

                _step++;
            }
        }
    }

    public void InvalidateShaderCachesAndReload()
    {
        GlProgramBinaryCache.ClearAll();
        PreviewShaderPrewarm.ClearAndRestart();
        lock (_sync)
        {
            if (_gl is null)
            {
                return;
            }

            _pendingShaderReload = true;
            _gpuInitStopwatch.Restart();
            RaiseGpuInitProgress(PreviewGpuInitPhases.ClearingShaderCache, _settings);
        }
    }

    private void HandlePendingShaderReloadLocked()
    {
        if (!_pendingShaderReload)
        {
            return;
        }

        ReleaseGpuResourceObjectsLocked();
        _gpuBootstrap = new GpuBootstrapRunner();
        _gpuBootstrapAborted = false;
        _pendingShaderReload = false;
    }

    private void ReleaseGpuResourceObjectsLocked()
    {
        _gpuAlive = false;
        _mesh?.Dispose();
        _mesh = null;
        _groundMesh?.Dispose();
        _groundMesh = null;
        _grassGroundAlbedo?.Dispose();
        _grassGroundAlbedo = null;
        _grassGroundNormal?.Dispose();
        _grassGroundNormal = null;
        _grassGroundSpec?.Dispose();
        _grassGroundSpec = null;
        _grassGroundHeight?.Dispose();
        _grassGroundHeight = null;
        _neutralNormal?.Dispose();
        _neutralNormal = null;
        _neutralSpec?.Dispose();
        _neutralSpec = null;
        _neutralHeight?.Dispose();
        _neutralHeight = null;
        _albedo?.Dispose();
        _albedo = null;
        _normal?.Dispose();
        _normal = null;
        _spec?.Dispose();
        _spec = null;
        _height?.Dispose();
        _height = null;
        _program?.Dispose();
        _program = null;
        _mainProgramUsesTessellation = false;
        _shadowProgram?.Dispose();
        _shadowProgram = null;
        _shadowTarget?.Dispose();
        _shadowTarget = null;
        _shadowTargetCascadeNear?.Dispose();
        _shadowTargetCascadeNear = null;
        _grassGroundReady = false;
        DestroyAtmosphereResources();
        DestroyGodRayResources();
        DestroyVolumeResources();
        DestroyVolumetricCloudResources();
        DestroyPreviewTaaResources();
        DestroyMoonBillboard();
        DestroyLineOverlay();
        DestroySunDebugOverlay();
        _proceduralSkyProgram?.Dispose();
        _proceduralSkyProgram = null;
        _shaderCtx = null;
        _gpuInitTier = PreviewGpuInitTier.None;
        _shadowAwareGodRayInitAttempted = false;
        _atmoLutsValid = false;
    }

    private bool RunGpuBootstrapStep(int step)
    {
        var gl = _gl!;
        switch (step)
        {
            case 0:
                InitShaderCompileContext(gl, _useOpenGlEs);
                EmitDiagnostic(_useOpenGlEs
                    ? $"[3D preview] Context: {_glVersionString} (Genesis shader path, GLSL ES 3.0)."
                    : $"[3D preview] Context: {_glVersionString} (Genesis shader path, GLSL 330 core).");
                RecordActiveContextSummary();
                _mainProgramUsesTessellation = false;
                string? err = null;
                var bootMask = GenesisShaderFeatureMaskBuilder.Build(_settings, entityEmulatedPreview: false);
                var bootDefines = GenesisShaderFeatureMaskBuilder.ToDefines(bootMask);
                if (!_useOpenGlEs)
                {
                    _program = CreatePreviewProgram(
                        "genesis.vert",
                        "genesis.tcs",
                        "genesis.tes",
                        "genesis.frag",
                        out err,
                        "genesis+tessellation",
                        bootDefines);
                    if (_program.IsValid)
                    {
                        _mainProgramUsesTessellation = true;
                        EmitDiagnostic("[3D preview] Genesis tessellation program ready (triangle patches).");
                    }
                    else
                    {
                        DisableGenesisTessellationCompile(err ?? "link failed");
                        _program.Dispose();
                        _program = null;
                    }
                }

                _program ??= CreatePreviewProgram("genesis.vert", "genesis.frag", out err, defines: bootDefines);
                if (!_program.IsValid)
                {
                    _lastError = err ?? "Shader link failed.";
                    EmitDiagnostic("[3D preview] " + _lastError);
                    _program.Dispose();
                    _program = null;
                    _gpuBootstrapAborted = true;
                    return false;
                }

                _mainEntityUniformLocs = ResolveEntitySkinningUniformLocs(_program);
                _mainUniformLocs = ResolveMainProgramUniformLocs(_program);
                _activeGenesisProgramKey = new GenesisProgramCacheKey(bootMask, _mainProgramUsesTessellation);
                _genesisPrograms[_activeGenesisProgramKey] = _program;
                _genesisProgramLru.AddFirst(_activeGenesisProgramKey);
                return true;

            case 1:
                _shadowProgram = CreatePreviewProgram("genesis_shadow.vert", "genesis_shadow.frag", out var shadowErr);
                if (!_shadowProgram.IsValid)
                {
                    EmitDiagnostic("[3D preview] Shadow program: " + (shadowErr ?? "link failed"));
                    _shadowProgram.Dispose();
                    _shadowProgram = null;
                }
                else
                {
                    _shadowEntityUniformLocs = ResolveEntitySkinningUniformLocs(_shadowProgram);
                    _shadowUniformLocs = ResolveShadowProgramUniformLocs(_shadowProgram);
                }

                InitEntitySkinningBoneUbo(gl);
                LogEntityShaderInitDiagnosticsOnce();
                return true;

            case 2:
                var shadowResolution = Math.Clamp(_settings.ShadowMapResolution, 256, 4096);
                try
                {
                    _shadowTarget = new GlShadowMapTarget(gl, shadowResolution, _useOpenGlEs);
                    _shadowTargetCascadeNear = new GlShadowMapTarget(gl, shadowResolution, _useOpenGlEs);
                    EmitDiagnostic(
                        $"[3D preview] Shadow map: {shadowResolution}x{shadowResolution} (near cascade ready)");
                }
                catch (Exception ex)
                {
                    _shadowTarget = null;
                    _shadowTargetCascadeNear = null;
                    EmitDiagnostic("[3D preview] Shadow target init failed: " + ex.Message);
                }

                return true;

            case 3:
                _albedo = new GlTexture2D(gl);
                _normal = new GlTexture2D(gl);
                _spec = new GlTexture2D(gl);
                _height = new GlTexture2D(gl);
                _mesh = new GlMeshBuffer(gl);
                _groundMesh = new GlMeshBuffer(gl);
                var groundGeom = PreviewMeshFactory.CreatePreviewGroundPlane();
                _groundMesh.Upload(groundGeom.InterleavedVertices, groundGeom.Indices);
                _neutralNormal = new GlTexture2D(gl);
                _neutralNormal.UploadRgba(1, 1, [128, 128, 255, 255]);
                _neutralSpec = new GlTexture2D(gl);
                _neutralSpec.UploadRgba(1, 1, [120, 60, 40, 255]);
                _neutralHeight = new GlTexture2D(gl);
                _neutralHeight.UploadRgba(1, 1, [128, 128, 128, 255]);
                _grassGroundAlbedo = new GlTexture2D(gl);
                _grassGroundNormal = new GlTexture2D(gl);
                _grassGroundSpec = new GlTexture2D(gl);
                _grassGroundHeight = new GlTexture2D(gl);
                _grassGroundReady = TryUploadBundledGroundFallback(gl);
                return true;

            case 4:
                TryInitLineOverlay(gl, _useOpenGlEs);
                return true;

            case 5:
                TryInitMoonBillboard(gl, _useOpenGlEs);
                return true;

            case 6:
                TryInitAtmosphere(gl);
                return true;

            case 7:
                PrewarmCommonGenesisProgramsOnGpu();
                _gpuInitTier = PreviewGpuInitTier.Core;
                EmitDiagnostic(
                    "[3D preview] Core GPU init: " +
                    $"{_gpuInitStopwatch.Elapsed.TotalMilliseconds:F0} ms, " +
                    $"sky={(_atmoSkyProgram is { IsValid: true } ? "lut" : "lazy-procedural")}, " +
                    $"atmoLut={(_atmoTransProgram is { IsValid: true } && _atmoSkyViewProgram is { IsValid: true } ? "yes" : "no")}.");
                _gpuAlive = true;
                _materialDirty = true;
                _meshDirty = true;
                if (_scene is not null)
                {
                    SyncOrbitFromSceneLocked(_scene);
                    _orbitSyncedKey = ResolveOrbitSyncKey(_scene, _blockModelSubject);
                }

                _loggedMeshReady = false;
                _loggedZeroIndex = false;
                return true;

            default:
                return true;
        }
    }

    private void RecordActiveContextSummary()
    {
        ActiveContextSummary = _desktopWglSidecar is not null
            ? _desktopWglSidecar.UsesDxInteropPresentation
                ? $"{_glVersionString} · GLSL 330 core (WGL sidecar · D3D11 interop)"
                : $"{_glVersionString} · GLSL 330 core (WGL sidecar)"
            : _useOpenGlEs
                ? $"{_glVersionString} · GLSL ES 3.0"
                : $"{_glVersionString} · GLSL 330 core";

        if (PreviewOpenGlSession.RequestedDesktopGl4 && _useOpenGlEs)
        {
            EmitDiagnostic("[3D preview] " + Resources.PreviewOpenGlFallbackWarning);
        }
    }
}
