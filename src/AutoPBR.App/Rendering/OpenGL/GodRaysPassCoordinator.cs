using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Toggle-driven history invalidation for god-ray and volumetric passes.</summary>
internal readonly struct GodRaysPassInvalidation
{
    public bool GodRayHistory { get; init; }
    public bool VolumeFroxelHistory { get; init; }
    public bool VolumeIntegrateHistory { get; init; }
    public bool CloudHistory { get; init; }
    public bool TaaHistory { get; init; }
    public bool ResetGodRayLogs { get; init; }
    public bool ResetCloudDrawLog { get; init; }
}

/// <summary>Coordinates god-ray toggle sync, scene-capture sizing, and diagnostic throttling.</summary>
internal sealed class GodRaysPassCoordinator
{
    public const int PreviewTaaSsaaMaxDimension = 3072;

    private long _lastVolumetricTimingLogMs;
    private int _lastSceneCaptureAaLogKey;
    private bool _prevEnableGodRays;
    private bool _prevEnableVolumetricClouds;
    private bool _prevGodRayStabilizeDebug = true;
    private bool _prevCloudDisableTemporal;

    public GodRaysPassInvalidation SyncGodRayToggleState(in PreviewRenderSettings settings)
    {
        var godRaysChanged = _prevEnableGodRays != settings.EnableGodRays;
        var stabilizeChanged = _prevGodRayStabilizeDebug != settings.GodRayStabilizeDebug;
        if (!godRaysChanged && !stabilizeChanged)
        {
            return default;
        }

        _prevEnableGodRays = settings.EnableGodRays;
        _prevGodRayStabilizeDebug = settings.GodRayStabilizeDebug;
        return new GodRaysPassInvalidation
        {
            GodRayHistory = true,
            VolumeFroxelHistory = true,
            VolumeIntegrateHistory = true,
            CloudHistory = true,
            TaaHistory = true,
            ResetGodRayLogs = true,
            ResetCloudDrawLog = true,
        };
    }

    public GodRaysPassInvalidation SyncVolumetricToggleState(in PreviewRenderSettings settings)
    {
        var cloudsChanged = _prevEnableVolumetricClouds != settings.EnableVolumetricClouds;
        var temporalChanged = _prevCloudDisableTemporal != settings.CloudDisableTemporal;
        if (!cloudsChanged && !temporalChanged)
        {
            return default;
        }

        _prevEnableVolumetricClouds = settings.EnableVolumetricClouds;
        _prevCloudDisableTemporal = settings.CloudDisableTemporal;
        return new GodRaysPassInvalidation
        {
            GodRayHistory = true,
            VolumeFroxelHistory = true,
            CloudHistory = true,
            TaaHistory = true,
            ResetCloudDrawLog = true,
        };
    }

    public static float ResolveSceneCaptureScale(
        in PreviewRenderSettings settings,
        Func<PreviewRenderSettings, bool> isTaaActive,
        Func<PreviewRenderSettings, PreviewVolumetricQuality.TaaProfile> resolveEffectiveTaa)
    {
        if (!isTaaActive(settings))
        {
            return 1f;
        }

        var taa = resolveEffectiveTaa(settings);
        if (settings.PreviewTaaForceFxaa ||
            Math.Clamp(settings.PreviewTaaMode, 0, 4) == 2 ||
            taa.EdgeAaBlend >= 0.45f ||
            taa.FxaaEdgeStrength >= 0.70f)
        {
            return 2f;
        }

        if (taa.EdgeAaBlend > 0.05f || taa.FxaaEdgeStrength > 0.20f)
        {
            return 1.5f;
        }

        return 1f;
    }

    public static void ResolveSceneCaptureSize(
        in GlRenderFrame frame,
        Func<PreviewRenderSettings, bool> isTaaActive,
        Func<PreviewRenderSettings, PreviewVolumetricQuality.TaaProfile> resolveEffectiveTaa,
        out int captureW,
        out int captureH,
        out float captureScale)
    {
        captureScale = ResolveSceneCaptureScale(frame.Settings, isTaaActive, resolveEffectiveTaa);
        if (captureScale > 1f)
        {
            var maxOutputDimension = Math.Max(frame.Vw, frame.Vh);
            var maxAllowedScale = PreviewTaaSsaaMaxDimension / (float)Math.Max(1, maxOutputDimension);
            captureScale = Math.Clamp(captureScale, 1f, Math.Max(1f, maxAllowedScale));
        }

        captureW = Math.Max(1, (int)MathF.Ceiling(frame.Vw * captureScale));
        captureH = Math.Max(1, (int)MathF.Ceiling(frame.Vh * captureScale));
    }

    public bool TryLogSceneCaptureAaScale(
        in GlRenderFrame frame,
        Action<string> emitDiagnostic)
    {
        if (!frame.Settings.LogPreviewTaaDiagnostics || frame.SceneCaptureScale <= 1f)
        {
            return false;
        }

        var key = HashCode.Combine(
            frame.Vw,
            frame.Vh,
            frame.SceneCaptureW,
            frame.SceneCaptureH,
            MathF.Round(frame.SceneCaptureScale * 100f),
            Math.Clamp(frame.Settings.PreviewTaaMode, 0, 4),
            frame.Settings.PreviewTaaForceFxaa);
        if (_lastSceneCaptureAaLogKey == key)
        {
            return false;
        }

        _lastSceneCaptureAaLogKey = key;
        emitDiagnostic(
            $"[3D preview] Scene capture AA scale: {frame.SceneCaptureScale:0.##}x " +
            $"({frame.Vw}x{frame.Vh} -> {frame.SceneCaptureW}x{frame.SceneCaptureH}, " +
            $"taaMode={Math.Clamp(frame.Settings.PreviewTaaMode, 0, 4)} forceFxaa={frame.Settings.PreviewTaaForceFxaa}).");
        return true;
    }

    public bool TryLogVolumetricTiming(
        in PreviewRenderSettings settings,
        double injectMs,
        double integrateMs,
        Action<string> emitDiagnostic)
    {
        if (!settings.LogVolumetricTiming)
        {
            return false;
        }

        var totalMs = injectMs + integrateMs;
        if (totalMs < 2.5)
        {
            return false;
        }

        var now = Environment.TickCount64;
        if (now - _lastVolumetricTimingLogMs < 8000)
        {
            return false;
        }

        _lastVolumetricTimingLogMs = now;
        emitDiagnostic(
            $"[3D preview] Volumetric pass timing: inject {injectMs:F2} ms, integrate {integrateMs:F2} ms " +
            $"(budget ~2.5 ms @1080p; quality={settings.VolumetricQuality}).");
        return true;
    }

    public void SeedToggleBaseline(in PreviewRenderSettings settings)
    {
        _prevEnableGodRays = settings.EnableGodRays;
        _prevGodRayStabilizeDebug = settings.GodRayStabilizeDebug;
        _prevEnableVolumetricClouds = settings.EnableVolumetricClouds;
        _prevCloudDisableTemporal = settings.CloudDisableTemporal;
    }
}
