using AutoPBR.App.Models;

using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Win32;

namespace AutoPBR.App.Rendering.OpenGL;

internal static class PreviewOpenGlPlatformConfigurator
{
    public static Win32PlatformOptions CreateWin32PlatformOptions(UserSettings settings)
    {
        PreviewOpenGlSession.RequestedDesktopGl4 = settings.PreviewUseOpenGl4;

        var useDesktop = settings.PreviewUseOpenGl4;
        return new Win32PlatformOptions
        {
            // Keep ANGLE for the Avalonia compositor (display refresh pacing). Desktop GL 4.x preview uses a WGL sidecar.
            RenderingMode =
            [
                Win32RenderingMode.AngleEgl,
            ],
            WglProfiles = useDesktop
                ?
                [
                    new GlVersion(GlProfileType.OpenGL, 4, 6),
                    new GlVersion(GlProfileType.OpenGL, 4, 0),
                    new GlVersion(GlProfileType.OpenGL, 3, 3),
                ]
                : new Win32PlatformOptions().WglProfiles,
            CompositionMode =
            [
                Win32CompositionMode.WinUIComposition,
                Win32CompositionMode.DirectComposition,
                Win32CompositionMode.RedirectionSurface,
            ],
        };
    }
}
