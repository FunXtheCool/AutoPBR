namespace AutoPBR.Preview.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Baby_axolotl_idle_floor_head_pitch_at_one_point_three_two_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyAxolotlIdleFloorHeadRotationDegrees(TestProfile26, 1.32f, out var euler));
        Assert.Equal(-6.45f, euler.X, 3);
        Assert.Equal(-1.45f, euler.Y, 3);
        Assert.Equal(-6.5f, euler.Z, 3);
    }


    [Fact]
    public void Baby_axolotl_idle_floor_tail_yaw_at_point_seven_two_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyAxolotlIdleFloorTailRotationDegrees(TestProfile26, 0.72f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(-14f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }


    [Fact]
    public void Baby_axolotl_swim_body_pitch_at_point_two_four_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyAxolotlSwimBodyRotationDegrees(TestProfile26, 0.24f, out var euler));
        Assert.Equal(12f, euler.X, 3);
    }


    [Fact]
    public void Baby_axolotl_play_dead_body_roll_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyAxolotlPlayDeadBodyRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(30f, euler.Z, 3);
    }


    [Fact]
    public void Baby_axolotl_swim_tail_yaw_at_mid_stroke_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyAxolotlSwimTailRotationDegrees(TestProfile26, 0.52f, out var euler));
        Assert.Equal(-15f, euler.Y, 3);
    }


    [Fact]
    public void Baby_axolotl_swim_right_front_leg_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyAxolotlSwimRightFrontLegRotationDegrees(TestProfile26, 0.24f, out var euler));
        Assert.Equal(330f, euler.X, 3);
    }

}

