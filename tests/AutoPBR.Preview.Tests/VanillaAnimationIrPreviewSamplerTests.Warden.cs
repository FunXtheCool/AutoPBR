namespace AutoPBR.Preview.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Warden_sniff_head_yaw_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenSniffHeadRotationDegrees(TestProfile26, 0.68f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(40f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Warden_emerge_body_roll_at_point_five_two_seconds_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenEmergeBodyRotationDegrees(TestProfile26, 0.52f, out var euler));
        Assert.Equal(-22.5f, euler.Z, 3);
    }


    [Fact]
    public void Warden_emerge_head_pitch_at_one_point_one_six_seconds_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenEmergeHeadRotationDegrees(TestProfile26, 1.16f, out var euler));
        Assert.Equal(-67.5f, euler.X, 3);
    }


    [Fact]
    public void Warden_roar_body_pitch_at_one_point_six_seconds_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenRoarBodyRotationDegrees(TestProfile26, 1.6f, out var euler));
        Assert.Equal(32.5f, euler.X, 3);
    }


    [Fact]
    public void Warden_roar_head_roll_at_one_point_six_seconds_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenRoarHeadRotationDegrees(TestProfile26, 1.6f, out var euler));
        Assert.Equal(-27.5f, euler.Z, 3);
    }


    [Fact]
    public void Warden_attack_body_pitch_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenAttackBodyRotationDegrees(TestProfile26, 0.2083f, out var euler));
        Assert.Equal(22.5f, euler.X, 3);
    }


    [Fact]
    public void Warden_attack_head_pitch_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenAttackHeadRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(-30.174931f, euler.X, 2);
    }


    [Fact]
    public void Warden_sonic_boom_body_pitch_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenSonicBoomBodyRotationDegrees(TestProfile26, 1.0833f, out var euler));
        Assert.Equal(47.5f, euler.X, 3);
    }


    [Fact]
    public void Warden_sonic_boom_head_pitch_at_one_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenSonicBoomHeadRotationDegrees(TestProfile26, 1f, out var euler));
        Assert.Equal(67.5f, euler.X, 3);
    }


    [Fact]
    public void Warden_attack_body_pitch_on_1_21_11_profile_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenAttackBodyRotationDegrees(TestProfile111, 0.2083f, out var euler));
        Assert.Equal(22.5f, euler.X, 3);
    }


    [Fact]
    public void Warden_sonic_boom_right_ribcage_yaw_at_peak_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenSonicBoomRightRibcageRotationDegrees(TestProfile26, 1.875f, out var euler));
        Assert.Equal(125f, euler.Y, 3);
    }

}

