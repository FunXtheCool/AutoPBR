namespace AutoPBR.Preview.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Camel_baby_walk_head_z_halfway_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelBabyWalkHeadPosition(TestProfile26, 0.22915f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(0f, pos.Y, 3);
        Assert.Equal(0.05f, pos.Z, 3);
    }


    [Fact]
    public void Camel_walk_root_roll_at_half_second_uses_catmullrom()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelWalkRootRotationDegrees(TestProfile26, 0.5f, out var euler));
        Assert.InRange(euler.Z, -1.5f, 1.5f);
    }


    [Fact]
    public void Camel_idle_tail_rotation_at_one_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelIdleTailRotationDegrees(TestProfile26, 1f, out var euler));
        Assert.Equal(4.98107f, euler.X, 3);
        Assert.Equal(0.43523f, euler.Y, 3);
        Assert.Equal(-4.98107f, euler.Z, 3);
    }


    [Fact]
    public void Camel_dash_head_pitch_at_start_uses_catmullrom_keyframe()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelDashHeadRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(10f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Camel_dash_head_pitch_at_eighth_second_uses_catmullrom()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelDashHeadRotationDegrees(TestProfile26, 0.125f, out var euler));
        Assert.Equal(0f, euler.X, 3);
    }


    [Fact]
    public void Camel_standup_body_pitch_at_point_seven_seconds_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelStandupBodyRotationDegrees(TestProfile26, 0.7f, out var euler));
        Assert.Equal(-17.5f, euler.X, 3);
    }


    [Fact]
    public void Camel_sit_body_pitch_at_one_point_three_seconds_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelSitBodyRotationDegrees(TestProfile26, 1.3f, out var euler));
        Assert.Equal(30f, euler.X, 3);
    }


    [Fact]
    public void Camel_baby_walk_right_front_leg_pitch_at_mid_cycle_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelBabyWalkRightFrontLegRotationDegrees(TestProfile26, 0.75f, out var euler));
        Assert.Equal(22.5f, euler.X, 3);
    }


    [Fact]
    public void Camel_baby_walk_left_hind_leg_pitch_at_mid_stride_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelBabyWalkLeftHindLegRotationDegrees(TestProfile26, 0.5833f, out var euler));
        Assert.Equal(-17.5f, euler.X, 3);
    }


    [Fact]
    public void Camel_baby_walk_right_hind_leg_pitch_at_three_quarter_cycle_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelBabyWalkRightHindLegRotationDegrees(TestProfile26, 0.75f, out var euler));
        Assert.Equal(22.5f, euler.X, 3);
    }

}

