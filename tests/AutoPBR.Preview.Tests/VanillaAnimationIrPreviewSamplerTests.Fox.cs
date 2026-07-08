namespace AutoPBR.Preview.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Fox_baby_walk_left_front_leg_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkLeftFrontLegRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(35f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Fox_baby_walk_left_hind_leg_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkLeftHindLegRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(-35f, euler.X, 3);
    }


    [Fact]
    public void Fox_baby_walk_right_front_leg_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkRightFrontLegRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(-35f, euler.X, 3);
    }


    [Fact]
    public void Fox_baby_walk_head_y_offset_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkHeadPosition(TestProfile26, 0f, out var pos));
        Assert.Equal(-1.025f, pos.Y, 3);
    }

}

