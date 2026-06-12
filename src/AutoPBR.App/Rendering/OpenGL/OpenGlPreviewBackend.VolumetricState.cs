using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private const int CloudTierReadyWarmupDraws = 3;

    /// <summary>
    /// Called when the lazy cloud GPU tier first becomes ready. God-ray toggles already
    /// invalidate histories; this covers the startup path where god rays ran for one or
    /// more frames before clouds finished loading.
    /// </summary>
    private void OnLazyCloudGpuTierReady(in PreviewRenderSettings settings, int viewportWidth, int viewportHeight)
    {
        InvalidateVolumetricTemporalHistories();
        _prevEnableGodRays = settings.EnableGodRays;
        _prevGodRayStabilizeDebug = settings.GodRayStabilizeDebug;
        _prevEnableVolumetricClouds = settings.EnableVolumetricClouds;
        _prevCloudDisableTemporal = settings.CloudDisableTemporal;
        _prevEnablePreviewTaa = settings.EnablePreviewTaa;
        _cloudTierReadyWarmupDraws = CloudTierReadyWarmupDraws;
        _cloudDeferredCompositeRetries = 4;
        _loggedCloudDraw = false;
        _loggedCloudDeferredCompositeMiss = 0;
        TryWarmCloudOffscreenTargets(viewportWidth, viewportHeight);
        EmitDiagnostic(
            "[3D preview] Volumetric cloud GPU tier ready; temporal histories invalidated " +
            $"(warmupDraws={CloudTierReadyWarmupDraws}).");
    }

    private void InvalidateVolumetricTemporalHistories()
    {
        _godRayHistoryValid = false;
        _volumeFroxelHistoryValid = false;
        _volumeIntegrateHistoryValid = false;
        _cloudHistoryValid = false;
        _taaHistoryValid = false;
        _godRayPrevViewProj = Matrix4x4.Identity;
        _cloudPrevViewProj = Matrix4x4.Identity;
        _taaPrevViewProj = Matrix4x4.Identity;
        _cloudFramePhase = 0f;
        _volumePathFailLogged = 0;
        _screenSpaceGodRayLogged = 0;
        _shadowAwareGodRayLogged = 0;
        _godRayBlitFailLogged = 0;
    }

    private void NoteCloudTierWarmupDirectDrawCompleted(bool drew)
    {
        if (_cloudTierReadyWarmupDraws <= 0 || !drew)
        {
            return;
        }

        _cloudTierReadyWarmupDraws--;
    }

    private void TryInitCloudGpuTierIfNeeded(in PreviewRenderSettings settings, int viewportWidth, int viewportHeight)
    {
        if (_gpuInitTier.HasAll(PreviewGpuInitTier.Clouds) || !settings.EnableVolumetricClouds || _gl is null)
        {
            return;
        }

        RaiseGpuInitProgress("Loading volumetric clouds…", settings);
        TryInitVolumetricClouds(_gl, _useOpenGlEs);
        if (CanDrawVolumetricClouds(settings))
        {
            OnLazyCloudGpuTierReady(settings, viewportWidth, viewportHeight);
        }

        _gpuInitTier |= PreviewGpuInitTier.Clouds;
    }
}
