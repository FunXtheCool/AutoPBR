using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Tests;

/// <summary>
/// Manual visual QA matrix for runtime entity previews (documented; run in app): humanoid, quadruped,
/// phantom (multi-slot + eyes), bee/bat wings, equipment overlay. Watch mirrored UV / tangent issues.
/// MikkTSpace tangent rebake (Core) is deferred until manual QA reports systematic mismatch.
/// </summary>
public class PreviewEntityEmulatedShaderGatingTests
{
    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, true, false)]
    public void EffectiveNormalMap_matches_entity_labpbr_toggle(
        bool enableNormalMap,
        bool entityEmulated,
        bool enableEntityLabPbrShading,
        bool expected)
    {
        Assert.Equal(
            expected,
            PreviewEntityEmulatedShaderGating.EffectiveNormalMap(
                enableNormalMap, entityEmulated, enableEntityLabPbrShading));
    }

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void EffectiveParallax_requires_entity_parallax_when_emulated(
        bool enableParallax,
        bool entityEmulated,
        bool enableEntityParallax,
        bool expected)
    {
        Assert.Equal(
            expected,
            PreviewEntityEmulatedShaderGating.EffectiveParallax(
                enableParallax, entityEmulated, enableEntityParallax));
    }

    [Fact]
    public void RenderSettings_defaults_enable_entity_labpbr_and_disable_entity_parallax()
    {
        var s = new PreviewRenderSettings();
        Assert.True(s.EnableEntityLabPbrShading);
        Assert.False(s.EnableEntityParallax);
    }
}
