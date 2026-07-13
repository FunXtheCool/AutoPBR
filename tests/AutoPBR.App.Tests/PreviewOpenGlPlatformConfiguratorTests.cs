using AutoPBR.App.Models;
using AutoPBR.App.Rendering.OpenGL;

using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Win32;

namespace AutoPBR.App.Tests;

public sealed class PreviewOpenGlPlatformConfiguratorTests
{
    [Fact]
    public void CreateWin32PlatformOptions_DefaultsToAngleOnly()
    {
        PreviewOpenGlSession.RequestedDesktopGl4 = false;

        var options = PreviewOpenGlPlatformConfigurator.CreateWin32PlatformOptions(new UserSettings());

        Assert.False(PreviewOpenGlSession.RequestedDesktopGl4);
        Assert.Equal([Win32RenderingMode.AngleEgl], options.RenderingMode);
    }

    [Fact]
    public void CreateWin32PlatformOptions_DesktopKeepsAngleCompositorWithWglProfilesForSidecar()
    {
        var options = PreviewOpenGlPlatformConfigurator.CreateWin32PlatformOptions(new UserSettings
        {
            PreviewUseOpenGl4 = true,
        });

        Assert.True(PreviewOpenGlSession.RequestedDesktopGl4);
        Assert.Equal([Win32RenderingMode.AngleEgl], options.RenderingMode);
        Assert.Equal(GlProfileType.OpenGL, options.WglProfiles[0].Type);
        Assert.Equal(4, options.WglProfiles[0].Major);
        Assert.Equal(6, options.WglProfiles[0].Minor);
        Assert.Equal(4, options.WglProfiles[1].Major);
        Assert.Equal(0, options.WglProfiles[1].Minor);
        Assert.Equal(3, options.WglProfiles[2].Major);
        Assert.Equal(3, options.WglProfiles[2].Minor);
    }
}
