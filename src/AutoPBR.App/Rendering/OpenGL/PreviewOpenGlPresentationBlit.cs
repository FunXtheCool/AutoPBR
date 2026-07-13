using Avalonia;
using Avalonia.OpenGL;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>GPU blit from ANGLE export texture into the composition FBO (no CPU readback).</summary>
internal static class PreviewOpenGlPresentationBlit
{
    private static uint _readFbo;
    private static int _readFboTextureId;
    private static GL? _cachedEsGl;

    public static bool BlitExportToFramebuffer(
        GlInterface esGlInterface,
        int exportTextureId,
        int destFbo,
        int width,
        int height,
        bool drainBeforeReturn = false)
    {
        if (exportTextureId == 0 || destFbo == 0 || width <= 0 || height <= 0)
        {
            return false;
        }

        var esGl = _cachedEsGl ??= GL.GetApi(esGlInterface.GetProcAddress);
        EnsureReadFbo(esGl, exportTextureId);

        esGl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _readFbo);
        esGl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, (uint)destFbo);
        esGl.BlitFramebuffer(
            0,
            0,
            width,
            height,
            0,
            0,
            width,
            height,
            ClearBufferMask.ColorBufferBit,
            GLEnum.Nearest);
        esGl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)destFbo);

        // Timed drain only — never glFinish (can wedge forever across NV_DX/ANGLE).
        if (drainBeforeReturn)
        {
            return PreviewGlCommandDrain.Drain(esGl);
        }

        return true;
    }

    public static void Reset()
    {
        if (_cachedEsGl is { } gl && _readFbo != 0)
        {
            gl.DeleteFramebuffer(_readFbo);
        }

        _readFbo = 0;
        _readFboTextureId = 0;
        _cachedEsGl = null;
    }

    private static void EnsureReadFbo(GL esGl, int exportTextureId)
    {
        if (_readFbo != 0 && _readFboTextureId == exportTextureId)
        {
            return;
        }

        if (_readFbo != 0)
        {
            esGl.DeleteFramebuffer(_readFbo);
            _readFbo = 0;
        }

        _readFboTextureId = exportTextureId;
        _readFbo = esGl.GenFramebuffer();
        esGl.BindFramebuffer(FramebufferTarget.Framebuffer, _readFbo);
        esGl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            (uint)exportTextureId,
            0);
        esGl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
}
