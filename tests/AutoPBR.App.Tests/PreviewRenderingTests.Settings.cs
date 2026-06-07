using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Tests;

public sealed partial class PreviewRenderingTests
{
    [Fact]
    public void RenderSettingsDefaultsAreUsable()
    {
        var s = new PreviewRenderSettings();
        Assert.Equal(1f, s.NormalStrength);
        Assert.True(s.EnableParallax);
        Assert.True(s.NearestTextureFilter);
        Assert.True(s.ShowBackgroundGrid);
        Assert.True(s.ShowGroundMesh);
        Assert.True(s.ShowCornerAxes);
        Assert.True(s.DrawPreviewSubject);
        Assert.Equal(PreviewEntityAlphaMode.Cutout, s.EntityAlphaMode);
        Assert.True(s.EnableEntityLabPbrShading);
        Assert.False(s.EnableEntityParallax);
    }

    [Fact]
    public void RenderSettingsGenesisDefaultsAreSensible()
    {
        var s = new PreviewRenderSettings();
        Assert.True(s.EnableSss);
        Assert.True(s.EnableParallaxShadow);
        Assert.True(s.EnableIbl);
        Assert.True(s.EnableAtmosphericSky);
        Assert.Equal(2.6f, s.AtmosphereTurbidity);
        Assert.Equal(10f, s.AtmosphereSunIntensity);
        Assert.Equal(1.35f, s.AtmosphereHorizonFalloff);
        Assert.Equal(1f, s.SssStrength);
        Assert.Equal(0.6f, s.IblStrength);
        Assert.Equal(1f, s.EmissionStrength);
    }

    [Fact]
    public void RenderSettingsShadowDefaultsAreSensible()
    {
        var s = new PreviewRenderSettings();
        Assert.True(s.EnableShadows);
        Assert.Equal(1024, s.ShadowMapResolution);
        Assert.Equal(0.0008f, s.ShadowMinBias);
        Assert.Equal(0.005f, s.ShadowMaxBias);
        // Phase 3 stub: persisted boolean only, defaults to false in Phase 2.
        Assert.False(s.EnableShadowCascades);
    }

    [Fact]
    public void RenderSettingsVolumetricDefaultsAreSensible()
    {
        var s = new PreviewRenderSettings();
        Assert.True(s.EnableGodRays);
        Assert.True(s.EnableVolumeGodRays);
        Assert.False(s.EnableVolumetricClouds);
        Assert.Equal(1, s.VolumetricQuality);
        Assert.Equal(0.45f, s.GodRayStrength);
        Assert.False(s.LogVolumetricTiming);
    }
}
