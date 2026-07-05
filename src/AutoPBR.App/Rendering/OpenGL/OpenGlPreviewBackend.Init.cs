using System.Diagnostics;
using System.Runtime.InteropServices;

using AutoPBR.App.Lang;
using AutoPBR.App.Rendering.Abstractions;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private GlShaderCompileContext? _shaderCtx;
    private PreviewGpuInitTier _gpuInitTier = PreviewGpuInitTier.None;
    private readonly Stopwatch _gpuInitStopwatch = new();
    private PreviewGpuInitProgress _gpuInitProgress = PreviewGpuInitProgress.Starting;
    private bool _shadowAwareGodRayInitAttempted;

    public PreviewGpuInitProgress GpuInitProgress
    {
        get
        {
            lock (_sync)
            {
                return _gpuInitProgress;
            }
        }
    }

    public event Action<PreviewGpuInitProgress>? GpuInitProgressChanged;

    private void RaiseGpuInitProgress(string phase, in PreviewRenderSettings settings)
    {
        var desired = ComputeDesiredGpuTier(settings);
        var bootstrapFrac = _gpuBootstrap?.Fraction ?? (_gpuAlive ? 1.0 : 0.0);
        var tierFrac = ComputeTierProgressFraction(desired);
        var prewarmFrac = PreviewShaderPrewarm.Fraction;
        var progressFraction = Math.Clamp(
            prewarmFrac * 0.18 + bootstrapFrac * 0.52 + tierFrac * 0.30,
            0.0,
            1.0);
        var fullyReady = PreviewShaderPrewarm.IsComplete &&
                         _gpuAlive &&
                         _gpuBootstrap is null &&
                         _gpuInitTier.HasAll(desired);
        var progress = new PreviewGpuInitProgress
        {
            ShaderSourcesReady = PreviewShaderPrewarm.IsComplete,
            CoreReady = _gpuInitTier.HasAll(PreviewGpuInitTier.Core),
            GodRaysReady = (desired & PreviewGpuInitTier.GodRays) == 0 ||
                           _gpuInitTier.HasAll(PreviewGpuInitTier.GodRays),
            CloudsReady = (desired & PreviewGpuInitTier.Clouds) == 0 ||
                          _gpuInitTier.HasAll(PreviewGpuInitTier.Clouds),
            PreviewTaaReady = (desired & PreviewGpuInitTier.PreviewTaa) == 0 ||
                              _gpuInitTier.HasAll(PreviewGpuInitTier.PreviewTaa),
            IsFullyReady = fullyReady,
            Phase = phase,
            ProgressFraction = fullyReady ? 1.0 : progressFraction,
            ElapsedMs = _gpuInitStopwatch.Elapsed.TotalMilliseconds,
        };
        _gpuInitProgress = progress;
        GpuInitProgressChanged?.Invoke(progress);
    }

    private double ComputeTierProgressFraction(PreviewGpuInitTier desired)
    {
        var total = 0;
        var ready = 0;
        if ((desired & PreviewGpuInitTier.Core) != 0)
        {
            total++;
            if (_gpuInitTier.HasAll(PreviewGpuInitTier.Core))
            {
                ready++;
            }
        }

        if ((desired & PreviewGpuInitTier.GodRays) != 0)
        {
            total++;
            if (_gpuInitTier.HasAll(PreviewGpuInitTier.GodRays))
            {
                ready++;
            }
        }

        if ((desired & PreviewGpuInitTier.Clouds) != 0)
        {
            total++;
            if (_gpuInitTier.HasAll(PreviewGpuInitTier.Clouds))
            {
                ready++;
            }
        }

        if ((desired & PreviewGpuInitTier.PreviewTaa) != 0)
        {
            total++;
            if (_gpuInitTier.HasAll(PreviewGpuInitTier.PreviewTaa))
            {
                ready++;
            }
        }

        return total == 0 ? 1.0 : (double)ready / total;
    }

    private static PreviewGpuInitTier ComputeDesiredGpuTier(in PreviewRenderSettings settings)
    {
        var tier = PreviewGpuInitTier.Core;
        if (settings.EnableGodRays)
        {
            tier |= PreviewGpuInitTier.GodRays;
        }

        if (settings.EnableVolumetricClouds)
        {
            tier |= PreviewGpuInitTier.Clouds;
        }

        if (settings.EnablePreviewTaa &&
            PreviewVolumetricQuality.Resolve(settings.VolumetricQuality).PreviewTaaWeight > 0f)
        {
            tier |= PreviewGpuInitTier.PreviewTaa;
        }

        return tier;
    }

    private void EnsureGpuTier(in PreviewRenderSettings settings)
    {
        if (_gl is null || _shaderCtx is null || !_gpuInitTier.HasAll(PreviewGpuInitTier.Core))
        {
            return;
        }

        var desired = ComputeDesiredGpuTier(settings);
        // God rays compile first; when clouds are also enabled, load them in the same frame so
        // the first combined draw never runs with capture ready but cloud shaders/textures missing.
        if ((desired & PreviewGpuInitTier.GodRays) != 0 && !_gpuInitTier.HasAll(PreviewGpuInitTier.GodRays))
        {
            RaiseGpuInitProgress(PreviewGpuInitPhases.LoadingGodRays, settings);
            TryInitGodRaysCore(_gl, _useOpenGlEs);
            TryInitVolume(_gl, _useOpenGlEs);
            _gpuInitTier |= PreviewGpuInitTier.GodRays;
            if ((desired & PreviewGpuInitTier.Clouds) != 0 && !_gpuInitTier.HasAll(PreviewGpuInitTier.Clouds))
            {
                TryInitCloudGpuTierIfNeeded(settings, _previewPixelWidth, _previewPixelHeight);
            }

            RaiseGpuInitProgress(_gpuInitTier.HasAll(desired) ? PreviewGpuInitPhases.Ready : PreviewGpuInitPhases.PreviewReady, settings);
            return;
        }

        if ((desired & PreviewGpuInitTier.Clouds) != 0 && !_gpuInitTier.HasAll(PreviewGpuInitTier.Clouds))
        {
            TryInitCloudGpuTierIfNeeded(settings, _previewPixelWidth, _previewPixelHeight);
            RaiseGpuInitProgress(_gpuInitTier.HasAll(desired) ? PreviewGpuInitPhases.Ready : PreviewGpuInitPhases.PreviewReady, settings);
            return;
        }

        if ((desired & PreviewGpuInitTier.PreviewTaa) != 0 && !_gpuInitTier.HasAll(PreviewGpuInitTier.PreviewTaa))
        {
            RaiseGpuInitProgress(PreviewGpuInitPhases.LoadingTaa, settings);
            TryInitPreviewTaa(_gl, _useOpenGlEs);
            _gpuInitTier |= PreviewGpuInitTier.PreviewTaa;
        }

        RaiseGpuInitProgress(_gpuInitTier.HasAll(desired) ? PreviewGpuInitPhases.Ready : PreviewGpuInitPhases.PreviewReady, settings);
    }

    private void InitShaderCompileContext(GL gl, bool useOpenGlEs)
    {
        unsafe
        {
            var vendorPtr = gl.GetString(StringName.Vendor);
            var rendererPtr = gl.GetString(StringName.Renderer);
            var vendor = vendorPtr is null ? "unknown" : Marshal.PtrToStringUTF8((nint)vendorPtr) ?? "unknown";
            var renderer = rendererPtr is null ? "unknown" : Marshal.PtrToStringUTF8((nint)rendererPtr) ?? "unknown";
            _shaderCtx = new GlShaderCompileContext(gl, useOpenGlEs, vendor, renderer);
        }
    }

    private GlShaderProgram CreatePreviewProgram(string vertexFile, string fragmentFile, out string? error,
        string? debugLabel = null) =>
        _shaderCtx!.CreateProgram(vertexFile, fragmentFile, out error, debugLabel);

    private GlShaderProgram CreatePreviewProgram(
        string vertexFile,
        string tessControlFile,
        string tessEvaluationFile,
        string fragmentFile,
        out string? error,
        string? debugLabel = null) =>
        _shaderCtx!.CreateProgram(vertexFile, tessControlFile, tessEvaluationFile, fragmentFile, out error, debugLabel);

    private bool TryEnsureProceduralSkyProgram()
    {
        if (_proceduralSkyProgram is { IsValid: true } || _gl is null)
        {
            return _proceduralSkyProgram is { IsValid: true };
        }

        _proceduralSkyProgram = new GlProceduralSkyProgram(_gl, _useOpenGlEs, out var procErr);
        if (_proceduralSkyProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Procedural sky fallback: " + (procErr ?? "link failed"));
            _proceduralSkyProgram?.Dispose();
            _proceduralSkyProgram = null;
            return false;
        }

        EmitDiagnostic("[3D preview] Using embedded procedural sky (LUT sky shader unavailable).");
        return true;
    }

    private void TryEnsureShadowAwareGodRayProgram()
    {
        if (_shadowAwareGodRayProgram is { IsValid: true } || _shaderCtx is null || _shadowAwareGodRayInitAttempted)
        {
            return;
        }

        _shadowAwareGodRayInitAttempted = true;
        _shadowAwareGodRayProgram = CreatePreviewProgram("genesis_godrays.vert", "genesis_godrays_shadow.frag",
            out var shErr);
        if (_shadowAwareGodRayProgram is not { IsValid: true })
        {
            EmitDiagnostic("[3D preview] Shadow-aware god-ray shader: " + TrimShaderDiagnostic(shErr));
            _shadowAwareGodRayProgram?.Dispose();
            _shadowAwareGodRayProgram = null;
        }
    }
}
