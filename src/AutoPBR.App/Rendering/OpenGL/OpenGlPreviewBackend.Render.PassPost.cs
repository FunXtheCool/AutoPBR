using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>OpenGL implementation of <see cref="IRenderPreviewBackend"/>; GPU entry points must run on the OpenGL thread (Avalonia <see cref="AutoPBR.App.Controls.GlPbrPreviewControl"/> callbacks).</summary>
public sealed partial class OpenGlPreviewBackend
{
    private void GlRenderPassPost(ref GlRenderFrame frame)
    {
        EnsurePostPassPerSettingsUniforms(ref frame);

        var cloudsActive = frame.Settings.EnableVolumetricClouds && CanDrawVolumetricClouds(frame.Settings);
        var godRaysActive = frame.Settings.EnableGodRays && frame.GodRayCaptureActive && _sceneCapture is { IsValid: true };
        var bothVolumetrics = cloudsActive && godRaysActive;
        var cloudWarmupDirect = bothVolumetrics && _cloudTierReadyWarmupDraws > 0;
        var useDeferredCloudComposite = bothVolumetrics && !cloudWarmupDirect;
        var cloudShaderTemporal = ShouldUseCloudShaderTemporal(frame.Settings, godRaysActive);
        var cloudRenderedOffscreen = false;

        if (cloudWarmupDirect)
        {
            DrawGodRayComposite(ref frame);
            var drewDirect = DrawVolumetricClouds(
                ref frame,
                gateSkyDepth: false,
                deferComposite: false,
                forceTemporal: false);
            NoteCloudTierWarmupDirectDrawCompleted(drewDirect);
        }
        else
        {
            if (cloudsActive)
            {
                cloudRenderedOffscreen = DrawVolumetricClouds(
                    ref frame,
                    gateSkyDepth: useDeferredCloudComposite,
                    deferComposite: useDeferredCloudComposite,
                    forceTemporal: cloudShaderTemporal ? null : false);
            }

            if (godRaysActive)
            {
                DrawGodRayComposite(ref frame);
            }

            if (useDeferredCloudComposite && cloudRenderedOffscreen)
            {
                CompositeCloudRenderTargetToDefault(ref frame);
            }
            else if (useDeferredCloudComposite && _loggedCloudDeferredCompositeMiss != frame.Vw + frame.Vh * 10000)
            {
                _loggedCloudDeferredCompositeMiss = frame.Vw + frame.Vh * 10000;
                EmitDiagnostic(
                    "[3D preview] Deferred cloud composite skipped (offscreen target not ready; " +
                    $"retriesLeft={_cloudDeferredCompositeRetries}).");
            }
        }

        DrawSunProjectionDebug(ref frame);

        if (frame.Settings.ShowCornerAxes && _lineProgram?.IsValid == true)
        {
            DrawCornerAxes(frame.Gl, frame.VpX, frame.VpY, frame.Vw, frame.Vh, frame.Proj, frame.View);
        }

        DrawPreviewTaa(ref frame);

        MaybeLogPreviewFingerprint(ref frame);
    }

    private void MaybeLogPreviewFingerprint(ref GlRenderFrame frame)
    {
        if (!frame.Settings.CapturePreviewFingerprint)
        {
            return;
        }

        var nowMs = frame.RenderTime * 1000.0;
        if (nowMs - _lastPreviewFingerprintLogMs < 2000.0)
        {
            return;
        }

        _lastPreviewFingerprintLogMs = nowMs;
        var readW = Math.Max(64, frame.Vw / 4);
        var readH = Math.Max(48, frame.Vh / 4);
        var readX = frame.VpX + Math.Max(0, (frame.Vw - readW) / 2);
        var readY = frame.VpY + Math.Max(0, (frame.Vh - readH) / 2);
        var pixels = GlFramebufferReadback.TryReadRgb8(frame.Gl, readX, readY, readW, readH);
        if (pixels is null)
        {
            return;
        }

        var fingerprint = PreviewFramebufferFingerprint.Compute(pixels, readW, readH);
        EmitDiagnostic($"[3D preview] Frame fingerprint {fingerprint:X8} ({readW}x{readH} center crop)");
    }
}
