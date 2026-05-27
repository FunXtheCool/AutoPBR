namespace AutoPBR.Core.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Frog_croak_croaking_body_y_at_one_second_matches_ir_hold()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogCroakCroakingBodyPosition(TestProfile26, 1f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(1f, pos.Y, 3);
        Assert.Equal(0f, pos.Z, 3);
    }


    [Fact]
    public void Frog_walk_left_leg_rotation_at_zero_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftLegRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Frog_walk_right_leg_rotation_at_zero_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightLegRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(-33.75f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Frog_walk_left_arm_rotation_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftArmRotationDegrees(TestProfile26, 0.2917f, out var euler));
        Assert.Equal(7.5f, euler.X, 3);
        Assert.Equal(-2.67f, euler.Y, 2);
        Assert.Equal(-7.5f, euler.Z, 3);
    }


    [Fact]
    public void Frog_walk_right_arm_rotation_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightArmRotationDegrees(TestProfile26, 0.125f, out var euler));
        Assert.Equal(22.5f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Frog_walk_left_arm_position_first_keyframe_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftArmPosition(TestProfile26, 0f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(0.1f, pos.Y, 3);
        Assert.Equal(-2f, pos.Z, 3);
    }


    [Fact]
    public void Frog_walk_right_arm_position_first_keyframe_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightArmPosition(TestProfile26, 0f, out var pos));
        Assert.Equal(0.5f, pos.X, 3);
        Assert.Equal(0.1f, pos.Y, 3);
        Assert.Equal(2f, pos.Z, 3);
    }


    [Fact]
    public void Frog_walk_left_leg_position_first_keyframe_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftLegPosition(TestProfile26, 0f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(0.1f, pos.Y, 3);
        Assert.Equal(1.2f, pos.Z, 3);
    }


    [Fact]
    public void Frog_walk_right_leg_position_first_keyframe_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightLegPosition(TestProfile26, 0f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(1.14f, pos.Y, 2);
        Assert.Equal(0.11f, pos.Z, 2);
    }


    [Fact]
    public void Frog_tongue_rotation_at_hold_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogTongueTongueRotationDegrees(TestProfile26, 0.4167f, out var euler));
        Assert.Equal(-18f, euler.X, 3);
    }


    [Fact]
    public void Frog_croak_croaking_body_scale_y_at_pulse_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogCroakCroakingBodyScale(TestProfile26, 0.5417f, out var scale));
        Assert.Equal(2.1f, scale.Y, 3);
    }


    [Fact]
    public void Frog_walk_body_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogWalkBodyRotationDegrees(TestProfile26, 0.2917f, out var euler));
        Assert.Equal(-7.5f, euler.X, 3);
    }

}

