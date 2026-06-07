using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>OpenGL implementation of <see cref="IRenderPreviewBackend"/>; GPU entry points must run on the OpenGL thread (Avalonia <see cref="AutoPBR.App.Controls.GlPbrPreviewControl"/> callbacks).</summary>
public sealed partial class OpenGlPreviewBackend
{
    private void GlRenderPassPost(ref GlRenderFrame frame)
    {
        DrawGodRayComposite(ref frame);

        if (frame.Settings.EnableVolumetricClouds)
        {
            if (CanUseUnifiedVolumetrics(frame.Settings) && frame.VolumeFroxelsReady)
            {
                var halfExtent = ResolveVolumeHalfExtent(ref frame);
                DrawVolumeCloudComposite(ref frame, halfExtent);
            }
            else
            {
                DrawVolumetricClouds(
                    frame.Gl,
                    frame.Eye,
                    frame.View,
                    frame.Proj,
                    frame.LightDir,
                    frame.Scene.Light.Color,
                    frame.Settings);
            }
        }

        DrawSunProjectionDebug(ref frame);

        if (frame.Settings.ShowCornerAxes && _lineProgram?.IsValid == true)
        {
            DrawCornerAxes(frame.Gl, frame.VpX, frame.VpY, frame.Vw, frame.Vh, frame.Proj, frame.View);
        }

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
