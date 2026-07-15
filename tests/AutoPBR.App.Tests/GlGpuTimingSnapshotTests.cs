using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class GlGpuTimingSnapshotTests
{
    [Fact]
    public void FormatHudLine_DefaultsToTotalOnly()
    {
        var snapshot = new GlGpuTimingSnapshot(
            SetupMs: 0.125,
            ShadowMs: 0.25,
            SceneMs: 1.5,
            PostMs: 0.375,
            OverlayMs: 0.0625);

        Assert.Equal(2.3125, snapshot.TotalMs, precision: 6);
        Assert.Equal("GPU 2.3 ms", snapshot.FormatHudLine());
        Assert.DoesNotContain("|", snapshot.FormatHudLine(), StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHudLine_ExpandedIncludesPassScopes()
    {
        var snapshot = new GlGpuTimingSnapshot(
            SetupMs: 0.125,
            ShadowMs: 0.25,
            SceneMs: 1.5,
            PostMs: 0.375,
            OverlayMs: 0.0625);

        var hud = snapshot.FormatHudLine(expanded: true);
        Assert.Contains("GPU 2.3 ms", hud, StringComparison.Ordinal);
        Assert.Contains("set 0.1", hud, StringComparison.Ordinal);
        Assert.Contains("sh 0.3", hud, StringComparison.Ordinal);
        Assert.Contains("scn 1.5", hud, StringComparison.Ordinal);
        Assert.Contains("post 0.4", hud, StringComparison.Ordinal);
        Assert.Contains("ovl 0.1", hud, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatDiagnostic_IncludesFullPassNames()
    {
        var snapshot = new GlGpuTimingSnapshot(0.01, 0.02, 0.03, 0.04, 0.05);

        var diagnostic = snapshot.FormatDiagnostic();
        Assert.Contains("setup=0.01ms", diagnostic, StringComparison.Ordinal);
        Assert.Contains("shadow=0.02ms", diagnostic, StringComparison.Ordinal);
        Assert.Contains("scene=0.03ms", diagnostic, StringComparison.Ordinal);
        Assert.Contains("post=0.04ms", diagnostic, StringComparison.Ordinal);
        Assert.Contains("overlay=0.05ms", diagnostic, StringComparison.Ordinal);
        Assert.Contains("total=0.15ms", diagnostic, StringComparison.Ordinal);
    }
}
