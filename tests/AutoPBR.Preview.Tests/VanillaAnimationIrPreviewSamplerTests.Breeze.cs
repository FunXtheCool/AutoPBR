namespace AutoPBR.Preview.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Breeze_idle_wind_mid_at_zero_matches_ir_first_keyframe()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeIdleWindPositions(TestProfile26, 0f, out var mid, out var top),
            "Expected Breeze animation IR under Data/minecraft-native/animation/26.1.2/");
        Assert.Equal(0.5f, mid.X, 4);
        Assert.Equal(0f, mid.Y, 4);
        Assert.Equal(-0.5f, mid.Z, 4);
        Assert.Equal(0.5f, top.X, 4);
        Assert.Equal(0f, top.Y, 4);
        Assert.Equal(0f, top.Z, 4);
    }


    [Fact]
    public void Breeze_shoot_head_pitch_hold_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(-12.5f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Breeze_shoot_head_pitch_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadRotationDegrees(TestProfile26, 0.9167f, out var euler));
        Assert.Equal(5f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Breeze_idle_rods_rotation_at_one_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeIdleRodsRotationDegrees(TestProfile26, 1f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(540f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Breeze_jump_body_position_at_peak_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeJumpBodyPosition(TestProfile26, 0.125f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(11f, pos.Y, 3);
        Assert.Equal(0f, pos.Z, 3);
    }


    [Fact]
    public void Breeze_slide_body_position_mid_clip_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeSlideBodyPosition(TestProfile26, 0.1f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(0f, pos.Y, 3);
        Assert.Equal(-3f, pos.Z, 3);
    }


    [Fact]
    public void Breeze_shoot_head_position_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeShootHeadPosition(TestProfile26, 0.25f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(-2f, pos.Y, 3);
        Assert.Equal(0f, pos.Z, 3);
    }


    [Fact]
    public void Breeze_inhale_body_y_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeInhaleBodyPosition(TestProfile26, 0.5f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(-10f, pos.Y, 3);
        Assert.Equal(0f, pos.Z, 3);
    }


    [Fact]
    public void Breeze_inhale_wind_mid_y_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeInhaleWindMidPosition(TestProfile26, 0.5f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(-6f, pos.Y, 3);
        Assert.Equal(0f, pos.Z, 3);
    }


    [Fact]
    public void Breeze_jump_wind_body_scale_y_at_peak_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeJumpWindBodyScale(TestProfile26, 0.125f, out var scale));
        Assert.Equal(1f, scale.X, 3);
        Assert.Equal(1.3f, scale.Y, 3);
        Assert.Equal(1f, scale.Z, 3);
    }


    [Fact]
    public void Breeze_shoot_wind_mid_z_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeShootWindMidPosition(TestProfile26, 0.25f, out var pos));
        Assert.Equal(5f, pos.Z, 3);
    }


    [Fact]
    public void Breeze_jump_wind_mid_yaw_mid_clip_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeJumpWindMidRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(90f, euler.Y, 3);
    }

}

