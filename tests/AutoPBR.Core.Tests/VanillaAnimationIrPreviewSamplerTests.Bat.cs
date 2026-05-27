namespace AutoPBR.Core.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Bat_flying_right_wing_yaw_deg_at_zero_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatFlyingRightWingRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(85f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Bat_flying_left_wing_yaw_deg_at_eighth_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatFlyingLeftWingRotationDegrees(TestProfile26, 0.125f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(55f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Bat_resting_right_wing_yaw_matches_ir_constant()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatRestingRightWingRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(-10f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Bat_resting_left_wing_yaw_matches_ir_constant()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatRestingLeftWingRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(10f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Bat_resting_wing_positions_at_zero_match_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatRestingRightWingPosition(TestProfile26, 0f, out var r));
        Assert.Equal(0f, r.X, 3);
        Assert.Equal(0f, r.Y, 3);
        Assert.Equal(1f, r.Z, 3);
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatRestingLeftWingPosition(TestProfile26, 0f, out var l));
        Assert.Equal(0f, l.X, 3);
        Assert.Equal(0f, l.Y, 3);
        Assert.Equal(1f, l.Z, 3);
    }


    [Fact]
    public void Bat_resting_head_pitch_flip_matches_ir_constant()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatRestingHeadRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(180f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Bat_resting_body_pitch_flip_matches_ir_constant()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatRestingBodyRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(180f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Bat_resting_head_body_y_offset_matches_ir_constant()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatRestingHeadPosition(TestProfile26, 0f, out var headPos));
        Assert.Equal(0f, headPos.X, 3);
        Assert.Equal(0.5f, headPos.Y, 3);
        Assert.Equal(0f, headPos.Z, 3);
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatRestingBodyPosition(TestProfile26, 0f, out var bodyPos));
        Assert.Equal(0f, bodyPos.X, 3);
        Assert.Equal(0.5f, bodyPos.Y, 3);
        Assert.Equal(0f, bodyPos.Z, 3);
    }


    [Fact]
    public void Bat_resting_right_wing_tip_yaw_matches_ir_constant()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatRestingRightWingTipRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(-120f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Bat_resting_left_wing_tip_yaw_matches_ir_constant()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatRestingLeftWingTipRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(120f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Bat_flying_right_wing_tip_yaw_at_zero_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatFlyingRightWingTipRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(10.5f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Bat_flying_left_wing_tip_yaw_at_eighth_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatFlyingLeftWingTipRotationDegrees(TestProfile26, 0.0417f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(-65.5f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Bat_flying_head_pitch_at_flap_peak_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatFlyingHeadRotationDegrees(TestProfile26, 0.125f, out var euler));
        Assert.Equal(20f, euler.X, 3);
    }


    [Fact]
    public void Bat_flying_body_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatFlyingBodyRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(52.5f, euler.X, 3);
    }


    [Fact]
    public void Bat_flying_feet_pitch_at_flap_peak_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBatFlyingFeetRotationDegrees(TestProfile26, 0.125f, out var euler));
        Assert.Equal(-21.25f, euler.X, 3);
    }

}

