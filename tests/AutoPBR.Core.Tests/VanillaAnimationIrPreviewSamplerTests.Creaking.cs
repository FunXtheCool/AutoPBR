namespace AutoPBR.Core.Tests;

public sealed partial class VanillaAnimationIrPreviewSamplerTests
{

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

}

