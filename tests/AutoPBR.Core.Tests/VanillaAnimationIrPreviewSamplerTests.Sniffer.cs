namespace AutoPBR.Core.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Sniffer_long_sniff_head_pitch_deg_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferLongSniffHeadRotationDegrees(TestProfile26, 0.5f, out var euler));
        Assert.Equal(-12.5f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Sniffer_walk_left_hind_leg_pitch_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftHindLegRotationDegrees(TestProfile26, 0.5833f, out var euler));
        Assert.Equal(-35f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Sniffer_walk_right_hind_leg_position_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkRightHindLegPosition(TestProfile26, 1.3333f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(4f, pos.Y, 3);
        Assert.Equal(-1f, pos.Z, 3);
    }


    [Fact]
    public void Sniffer_walk_body_rotation_first_keyframe_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkBodyRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(1f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(-2.5f, euler.Z, 3);
    }


    [Fact]
    public void Sniffer_walk_right_front_leg_rotation_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkRightFrontLegRotationDegrees(TestProfile26, 0.5833f, out var euler));
        Assert.Equal(35f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Sniffer_walk_left_front_leg_rotation_at_zero_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftFrontLegRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(-35f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }

    [Fact]
    public void Sniffer_walk_left_mid_leg_rotation_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftMidLegRotationDegrees(TestProfile26, 0.75f, out var euler));
        Assert.Equal(35f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Sniffer_walk_right_mid_leg_rotation_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkRightMidLegRotationDegrees(TestProfile26, 0.1667f, out var euler));
        Assert.Equal(-35f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Sniffer_walk_head_rotation_at_half_second_uses_catmullrom()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkHeadRotationDegrees(TestProfile26, 0.5f, out var euler));
        Assert.True(Math.Abs(euler.X) > 0.01f);
    }


    [Fact]
    public void Sniffer_walk_right_mid_leg_position_at_bind_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkRightMidLegPosition(TestProfile26, 0f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(2.67f, pos.Y, 3);
        Assert.Equal(-0.67f, pos.Z, 3);
    }


    [Fact]
    public void Sniffer_dig_body_pitch_at_one_third_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferDigBodyRotationDegrees(TestProfile26, 1.3333f, out var euler));
        Assert.Equal(-5f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Sniffer_walk_head_pitch_at_point_eight_seven_five_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkHeadRotationDegrees(TestProfile26, 0.875f, out var euler));
        Assert.Equal(-1f, euler.X, 3);
    }


    [Fact]
    public void Sniffer_dig_head_pitch_at_two_point_five_seconds_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferDigHeadRotationDegrees(TestProfile26, 2.5f, out var euler));
        Assert.Equal(47.5f, euler.X, 3);
    }


    [Fact]
    public void Sniffer_dig_body_y_at_one_point_five_seconds_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferDigBodyPosition(TestProfile26, 1.5f, out var pos));
        Assert.Equal(-7f, pos.Y, 3);
    }


    [Fact]
    public void Sniffer_stand_up_body_y_at_rise_start_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferStandUpBodyPosition(TestProfile26, 0.25f, out var pos));
        Assert.Equal(-7f, pos.Y, 3);
    }


    [Fact]
    public void Sniffer_stand_up_body_pitch_at_mid_rise_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferStandUpBodyRotationDegrees(TestProfile26, 0.75f, out var euler));
        Assert.Equal(2.5f, euler.X, 3);
    }


    [Fact]
    public void Sniffer_happy_head_pitch_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferHappyHeadRotationDegrees(TestProfile26, 0.5f, out var euler));
        Assert.Equal(-32.00206f, euler.X, 2);
        Assert.Equal(19.354601f, euler.Y, 2);
        Assert.Equal(-11.70092f, euler.Z, 2);
    }

}

