namespace AutoPBR.Preview.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Nautilus_swimming_upper_mouth_matches_ir_at_half_second()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleNautilusSwimmingUpperMouthRotationDegrees(TestProfile26, 0.5f, out var euler));
        Assert.Equal(30f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Nautilus_swimming_body_scale_z_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleNautilusSwimmingBodyScale(TestProfile26, 0.5f, out var scale));
        Assert.Equal(1f, scale.X, 3);
        Assert.Equal(1f, scale.Y, 3);
        Assert.Equal(1.2f, scale.Z, 3);
    }


    [Fact]
    public void Nautilus_swim_inner_mouth_scale_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleNautilusSwimmingInnerMouthScale(TestProfile26, 0.5f, out var scale));
        Assert.Equal(0.8f, scale.X, 3);
        Assert.Equal(0.8f, scale.Y, 3);
        Assert.Equal(1f, scale.Z, 3);
    }


    [Fact]
    public void Nautilus_swim_lower_mouth_scale_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleNautilusSwimmingLowerMouthScale(TestProfile26, 0.5f, out var scale));
        Assert.Equal(1f, scale.X, 3);
        Assert.Equal(1.4f, scale.Z, 3);
    }

}

