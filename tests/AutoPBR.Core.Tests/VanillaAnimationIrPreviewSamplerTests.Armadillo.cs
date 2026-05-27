namespace AutoPBR.Core.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Armadillo_walk_tail_pitch_deg_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleArmadilloWalkTailRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(-4.585f, euler.X, 2);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Baby_armadillo_walk_tail_pitch_deg_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyArmadilloWalkTailRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(-4.585f, euler.X, 2);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Armadillo_roll_up_body_y_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleArmadilloRollUpBodyPosition(TestProfile26, 0.25f, out var pos));
        Assert.Equal(6f, pos.Y, 3);
        Assert.Equal(-1f, pos.Z, 3);
    }


    [Fact]
    public void Armadillo_peek_head_pitch_at_point_four_seconds_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleArmadilloPeekHeadRotationDegrees(TestProfile26, 0.4f, out var euler));
        Assert.Equal(-50f, euler.X, 3);
    }


    [Fact]
    public void Armadillo_peek_right_front_leg_pitch_at_point_eight_three_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleArmadilloPeekRightFrontLegRotationDegrees(TestProfile26, 0.8333f, out var euler));
        Assert.Equal(-45f, euler.X, 3);
    }


    [Fact]
    public void Armadillo_peek_left_front_leg_pitch_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleArmadilloPeekLeftFrontLegRotationDegrees(TestProfile26, 0.8333f, out var euler));
        Assert.Equal(-45f, euler.X, 3);
    }


    [Fact]
    public void Armadillo_peek_right_hind_leg_offset_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleArmadilloPeekRightHindLegPosition(TestProfile26, 0f, out var pos));
        Assert.Equal(3f, pos.Y, 3);
        Assert.Equal(-2f, pos.Z, 3);
    }


    [Fact]
    public void Baby_armadillo_peek_head_pitch_at_point_four_seconds_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyArmadilloPeekHeadRotationDegrees(TestProfile26, 0.4f, out var euler));
        Assert.Equal(-50f, euler.X, 3);
    }


    [Fact]
    public void Baby_armadillo_roll_up_body_y_at_point_one_five_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyArmadilloRollUpBodyPosition(TestProfile26, 0.15f, out var pos));
        Assert.Equal(5f, pos.Y, 3);
    }


    [Fact]
    public void Armadillo_peek_head_y_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleArmadilloPeekHeadPosition(TestProfile26, 0.5f, out var pos));
        Assert.Equal(2.1f, pos.Y, 3);
        Assert.Equal(1.2f, pos.Z, 3);
    }


    [Fact]
    public void Armadillo_roll_out_head_pitch_at_tenth_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleArmadilloRollOutHeadRotationDegrees(TestProfile26, 0.1f, out var euler));
        Assert.Equal(-50f, euler.X, 3);
    }


    [Fact]
    public void Armadillo_roll_out_head_y_at_three_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleArmadilloRollOutHeadPosition(TestProfile26, 0.75f, out var pos));
        Assert.Equal(4.1f, pos.Y, 3);
        Assert.Equal(2.2f, pos.Z, 3);
    }

}

