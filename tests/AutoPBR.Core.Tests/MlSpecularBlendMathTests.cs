using AutoPBR.Core.Models;
using Xunit;

namespace AutoPBR.Core.Tests;

public sealed class MlSpecularBlendMathTests
{
    [Theory]
    [InlineData(MlSpecularBlendMath.Linear)]
    [InlineData(MlSpecularBlendMath.SoftLight)]
    [InlineData(MlSpecularBlendMath.Overlay)]
    [InlineData(MlSpecularBlendMath.Screen)]
    [InlineData(MlSpecularBlendMath.BiasGain)]
    [InlineData(MlSpecularBlendMath.SigmoidCrossfade)]
    public void MixZero_ReturnsHeuristic(MlSpecularBlendMath math)
    {
        var h = 73.0;
        var ml = 220.0;
        var v = SpecularGenerator.BlendChannel(h, ml, 0f, math);
        Assert.Equal(h, v, 10);
    }

    [Theory]
    [InlineData(MlSpecularBlendMath.Linear)]
    [InlineData(MlSpecularBlendMath.SoftLight)]
    [InlineData(MlSpecularBlendMath.Overlay)]
    [InlineData(MlSpecularBlendMath.Screen)]
    [InlineData(MlSpecularBlendMath.BiasGain)]
    [InlineData(MlSpecularBlendMath.SigmoidCrossfade)]
    public void BlendOutput_IsAlwaysByteRange(MlSpecularBlendMath math)
    {
        var heuristics = new[] { 0.0, 16.0, 64.0, 128.0, 220.0, 255.0 };
        var mlVals = new[] { 0.0, 12.0, 80.0, 128.0, 240.0, 255.0 };
        var mixes = new[] { 0f, 0.1f, 0.35f, 0.6f, 1f };
        foreach (var h in heuristics)
        {
            foreach (var ml in mlVals)
            {
                foreach (var mix in mixes)
                {
                    var v = SpecularGenerator.BlendChannel(h, ml, mix, math);
                    Assert.InRange(v, 0.0, 255.0);
                }
            }
        }
    }

    [Fact]
    public void Linear_MixOne_EqualsModel()
    {
        var v = SpecularGenerator.BlendChannel(32.0, 214.0, 1f, MlSpecularBlendMath.Linear);
        Assert.Equal(214.0, v, 10);
    }

    [Theory]
    [InlineData(MlSpecularBlendMath.SoftLight)]
    [InlineData(MlSpecularBlendMath.Overlay)]
    [InlineData(MlSpecularBlendMath.Screen)]
    [InlineData(MlSpecularBlendMath.BiasGain)]
    [InlineData(MlSpecularBlendMath.SigmoidCrossfade)]
    public void CandidateModes_DifferFromLinear_OnReferenceVector(MlSpecularBlendMath math)
    {
        const double h = 80.0;
        const double ml = 220.0;
        const float mix = 0.6f;
        var linear = SpecularGenerator.BlendChannel(h, ml, mix, MlSpecularBlendMath.Linear);
        var candidate = SpecularGenerator.BlendChannel(h, ml, mix, math);
        Assert.NotEqual(Math.Round(linear, 6), Math.Round(candidate, 6));
    }

    [Fact]
    public void SigmoidCrossfade_StaysBetweenInputs_WhenMixInRange()
    {
        const double h = 40.0;
        const double ml = 210.0;
        var v = SpecularGenerator.BlendChannel(h, ml, 0.5f, MlSpecularBlendMath.SigmoidCrossfade);
        Assert.InRange(v, h, ml);
    }
}
