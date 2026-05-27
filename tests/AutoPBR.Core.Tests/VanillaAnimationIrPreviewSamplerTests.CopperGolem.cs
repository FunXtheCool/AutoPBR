namespace AutoPBR.Core.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

    [Fact]
    public void Copper_golem_walk_head_rotation_at_ir_keyframe_matches()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCopperGolemWalkHeadRotationDegrees(TestProfile26, 0.2083f, out var euler));
        Assert.Equal(-10f, euler.X, 3);
        Assert.Equal(1.87f, euler.Y, 3);
        Assert.Equal(10f, euler.Z, 3);
    }


    [Fact]
    public void Copper_golem_walk_right_arm_at_mid_cycle_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCopperGolemWalkRightArmRotationDegrees(TestProfile26, 0.4167f, out var euler));
        Assert.Equal(-80f, euler.X, 3);
    }


    [Fact]
    public void Copper_golem_idle_body_yaw_at_point_one_two_five_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCopperGolemIdleBodyRotationDegrees(TestProfile26, 0.125f, out var euler));
        Assert.Equal(-35f, euler.Y, 3);
    }

}

