namespace AutoPBR.Core.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Rabbit_idle_head_tilt_body_y_mid_rise_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleRabbitIdleHeadTiltBodyPosition(TestProfile26, 0.083333336f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(0.35f, pos.Y, 3);
        Assert.Equal(0f, pos.Z, 3);
    }


    [Fact]
    public void Rabbit_idle_head_tilt_head_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleRabbitIdleHeadTiltHeadRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(54.5f, euler.X, 2);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Rabbit_idle_head_tilt_head_z_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleRabbitIdleHeadTiltHeadPosition(TestProfile26, 0.25f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(0f, pos.Y, 3);
        Assert.Equal(-2.5f, pos.Z, 3);
    }


    [Fact]
    public void Baby_rabbit_hop_frontlegs_pitch_at_third_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyRabbitHopFrontLegsRotationDegrees(TestProfile26, 0.3333f, out var euler));
        Assert.Equal(-74.370003f, euler.X, 2);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Baby_rabbit_hop_frontlegs_position_at_third_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyRabbitHopFrontLegsPosition(TestProfile26, 0.3333f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(0.1f, pos.Y, 3);
        Assert.Equal(-0.1f, pos.Z, 3);
    }


    [Fact]
    public void Baby_rabbit_idle_head_tilt_body_y_mid_rise_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyRabbitIdleHeadTiltBodyPosition(TestProfile26, 0.083333336f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(0.35f, pos.Y, 3);
        Assert.Equal(0f, pos.Z, 3);
    }


    [Fact]
    public void Rabbit_hop_frontlegs_position_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleRabbitHopFrontLegsPosition(TestProfile26, 0.3333f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(0.5f, pos.Y, 3);
        Assert.Equal(0.6f, pos.Z, 3);
    }


    [Fact]
    public void Rabbit_hop_tail_pitch_at_third_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleRabbitHopTailRotationDegrees(TestProfile26, 0.3333f, out var euler));
        Assert.Equal(15f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Rabbit_hop_right_hind_leg_pitch_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleRabbitHopRightHindLegRotationDegrees(TestProfile26, 0.2083f, out var euler));
        Assert.Equal(47.5f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Rabbit_hop_body_pitch_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleRabbitHopBodyRotationDegrees(TestProfile26, 0.2917f, out var euler));
        Assert.Equal(32.5f, euler.X, 2);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Rabbit_hop_head_pitch_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleRabbitHopHeadRotationDegrees(TestProfile26, 0.2917f, out var euler));
        Assert.Equal(-32.169998f, euler.X, 2);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Baby_rabbit_hop_body_pitch_at_peak_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyRabbitHopBodyRotationDegrees(TestProfile26, 0.2917f, out var euler));
        Assert.Equal(32.5f, euler.X, 2);
    }


    [Fact]
    public void Baby_rabbit_hop_head_pitch_at_peak_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyRabbitHopHeadRotationDegrees(TestProfile26, 0.2917f, out var euler));
        Assert.Equal(-32.169998f, euler.X, 2);
    }


    [Fact]
    public void Baby_rabbit_hop_right_hind_leg_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyRabbitHopRightHindLegRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(-25f, euler.X, 3);
    }


    [Fact]
    public void Baby_rabbit_hop_tail_pitch_first_channel_at_third_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyRabbitHopTailRotationDegrees(TestProfile26, 0.3333f, out var euler));
        Assert.Equal(15f, euler.X, 3);
    }


    [Fact]
    public void Rabbit_hop_right_front_leg_roll_at_third_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleRabbitHopRightFrontLegRotationDegrees(TestProfile26, 0.3333f, out var euler));
        Assert.Equal(-17.5f, euler.Z, 3);
    }

}

