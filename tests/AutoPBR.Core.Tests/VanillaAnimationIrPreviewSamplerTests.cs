
namespace AutoPBR.Core.Tests;

public sealed class VanillaAnimationIrPreviewSamplerTests
{
    private static MinecraftNativeProfile TestProfile26 =>
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    private static MinecraftNativeProfile TestProfile111 =>
        new("1.21.11", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "1.21.11"), new Version(1, 21, 11));

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
    public void Nautilus_swimming_upper_mouth_matches_ir_at_half_second()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleNautilusSwimmingUpperMouthRotationDegrees(TestProfile26, 0.5f, out var euler));
        Assert.Equal(30f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }

    [Fact]
    public void Nautilus_swimming_body_scale_z_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleNautilusSwimmingBodyScale(TestProfile26, 0.5f, out var scale));
        Assert.Equal(1f, scale.X, 3);
        Assert.Equal(1f, scale.Y, 3);
        Assert.Equal(1.2f, scale.Z, 3);
    }

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
    public void Frog_croak_croaking_body_y_at_one_second_matches_ir_hold()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogCroakCroakingBodyPosition(TestProfile26, 1f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(1f, pos.Y, 3);
        Assert.Equal(0f, pos.Z, 3);
    }

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
    public void Creaking_walk_upper_body_first_keyframe_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCreakingWalkUpperBodyRotationDegrees(TestProfile26, 0f, out var euler));
        Assert.Equal(26.8802f, euler.X, 2);
        Assert.Equal(-23.399f, euler.Y, 2);
        Assert.Equal(-9.0616f, euler.Z, 3);
    }

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
    public void Baby_armadillo_walk_tail_pitch_deg_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyArmadilloWalkTailRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(-4.585f, euler.X, 2);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
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
    public void Camel_baby_walk_head_z_halfway_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelBabyWalkHeadPosition(TestProfile26, 0.22915f, out var pos));
        Assert.Equal(0f, pos.X, 3);
        Assert.Equal(0f, pos.Y, 3);
        Assert.Equal(0.05f, pos.Z, 3);
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
    public void Creaking_attack_upper_body_y_at_twelfth_second_matches_ir()
    {
        // IR uses 0.0833f; sampling slightly past that lands in the next segment (x lerps toward −115°).
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCreakingAttackUpperBodyRotationDegrees(TestProfile26, 0.0833f, out var euler));
        Assert.True(MathF.Abs(euler.X) < 0.08f, $"expected X≈0, got {euler.X}");
        Assert.Equal(45f, euler.Y, 2);
        Assert.True(MathF.Abs(euler.Z) < 0.08f, $"expected Z≈0, got {euler.Z}");
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
    public void Camel_walk_root_roll_at_half_second_uses_catmullrom()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelWalkRootRotationDegrees(TestProfile26, 0.5f, out var euler));
        Assert.InRange(euler.Z, -1.5f, 1.5f);
    }

    [Fact]
    public void Sniffer_walk_head_rotation_at_half_second_uses_catmullrom()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleSnifferWalkHeadRotationDegrees(TestProfile26, 0.5f, out var euler));
        Assert.True(Math.Abs(euler.X) > 0.01f);
    }

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
    public void Armadillo_roll_up_body_y_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleArmadilloRollUpBodyPosition(TestProfile26, 0.25f, out var pos));
        Assert.Equal(6f, pos.Y, 3);
        Assert.Equal(-1f, pos.Z, 3);
    }

    [Fact]
    public void Frog_tongue_rotation_at_hold_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFrogTongueTongueRotationDegrees(TestProfile26, 0.4167f, out var euler));
        Assert.Equal(-18f, euler.X, 3);
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
    public void Fox_baby_walk_left_front_leg_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkLeftFrontLegRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(35f, euler.X, 3);
        Assert.Equal(0f, euler.Y, 3);
        Assert.Equal(0f, euler.Z, 3);
    }

    [Fact]
    public void Fox_baby_walk_left_hind_leg_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkLeftHindLegRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(-35f, euler.X, 3);
    }

    [Fact]
    public void Fox_baby_walk_right_front_leg_pitch_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkRightFrontLegRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(-35f, euler.X, 3);
    }

    [Fact]
    public void Fox_baby_walk_head_y_offset_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleFoxBabyWalkHeadPosition(TestProfile26, 0f, out var pos));
        Assert.Equal(-1.025f, pos.Y, 3);
    }

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
    public void Nautilus_swim_inner_mouth_scale_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleNautilusSwimmingInnerMouthScale(TestProfile26, 0.5f, out var scale));
        Assert.Equal(0.8f, scale.X, 3);
        Assert.Equal(0.8f, scale.Y, 3);
        Assert.Equal(1f, scale.Z, 3);
    }

    [Fact]
    public void Nautilus_swim_lower_mouth_scale_at_half_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleNautilusSwimmingLowerMouthScale(TestProfile26, 0.5f, out var scale));
        Assert.Equal(1f, scale.X, 3);
        Assert.Equal(1.4f, scale.Z, 3);
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
    public void Baby_axolotl_idle_floor_tail_yaw_at_point_seven_two_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyAxolotlIdleFloorTailRotationDegrees(TestProfile26, 0.72f, out var euler));
        Assert.Equal(0f, euler.X, 3);
        Assert.Equal(-14f, euler.Y, 3);
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
    public void Copper_golem_idle_body_yaw_at_point_one_two_five_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCopperGolemIdleBodyRotationDegrees(TestProfile26, 0.125f, out var euler));
        Assert.Equal(-35f, euler.Y, 3);
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
    public void Breeze_shoot_wind_mid_z_at_quarter_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeShootWindMidPosition(TestProfile26, 0.25f, out var pos));
        Assert.Equal(5f, pos.Z, 3);
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
    public void Baby_rabbit_hop_tail_pitch_first_channel_at_third_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBabyRabbitHopTailRotationDegrees(TestProfile26, 0.3333f, out var euler));
        Assert.Equal(15f, euler.X, 3);
    }

    [Fact]
    public void Breeze_jump_wind_mid_yaw_mid_clip_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleBreezeJumpWindMidRotationDegrees(TestProfile26, 0.25f, out var euler));
        Assert.Equal(90f, euler.Y, 3);
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

    [Fact]
    public void Camel_baby_walk_right_front_leg_pitch_at_mid_cycle_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCamelBabyWalkRightFrontLegRotationDegrees(TestProfile26, 0.75f, out var euler));
        Assert.Equal(22.5f, euler.X, 3);
    }

    [Fact]
    public void Rabbit_hop_right_front_leg_roll_at_third_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleRabbitHopRightFrontLegRotationDegrees(TestProfile26, 0.3333f, out var euler));
        Assert.Equal(-17.5f, euler.Z, 3);
    }

    [Fact]
    public void Warden_sonic_boom_right_ribcage_yaw_at_peak_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleWardenSonicBoomRightRibcageRotationDegrees(TestProfile26, 1.875f, out var euler));
        Assert.Equal(125f, euler.Y, 3);
    }

    [Fact]
    public void Creaking_attack_head_pitch_at_lunge_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCreakingAttackHeadRotationDegrees(TestProfile26, 0.2917f, out var euler));
        Assert.Equal(-117.393898f, euler.X, 2);
    }

    [Fact]
    public void Creaking_attack_upper_body_y_at_lunge_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCreakingAttackUpperBodyPosition(TestProfile26, 0.2917f, out var pos));
        Assert.Equal(-2.7716f, pos.Y, 3);
    }

    [Fact]
    public void Creaking_invulnerable_upper_body_pitch_at_eighth_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCreakingInvulnerableUpperBodyRotationDegrees(TestProfile26, 0.0833f, out var euler));
        Assert.Equal(-5f, euler.X, 3);
    }

    [Fact]
    public void Creaking_invulnerable_right_arm_pitch_at_eighth_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCreakingInvulnerableRightArmRotationDegrees(TestProfile26, 0.0833f, out var euler));
        Assert.Equal(17.5f, euler.X, 3);
    }

    [Fact]
    public void Creaking_death_upper_body_pitch_at_eighth_second_matches_ir()
    {
        Assert.True(
            DefinitionAnimationPreviewSampling.TrySampleCreakingDeathUpperBodyRotationDegrees(TestProfile26, 0.0833f, out var euler));
        Assert.Equal(-40f, euler.X, 3);
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
