namespace AutoPBR.Core.Preview;

/// <summary>
/// Maps CleanRoom preview timing inputs to vanilla render-state fields consumed by lifted setupAnim IR.
/// This is the only allowed non-lifted math for locomotion timing (not part pose formulas).
/// </summary>
internal static class PreviewRenderStateSynthesis
{
    public const float DegToRad = MathF.PI / 180f;

    public static IReadOnlyDictionary<string, float> ForLivingWalk(
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        float headPitchDegrees = 0f,
        float headYawDegrees = 0f)
    {
        var (walkPos, walkSpeed) = ComputeWalkCycle(animationTimeSeconds, idlePhase01, wave);
        return new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["walkAnimationPos"] = walkPos,
            ["walkAnimationSpeed"] = walkSpeed,
            ["xRot"] = headPitchDegrees,
            ["yRot"] = headYawDegrees,
            ["ageInTicks"] = animationTimeSeconds * 20f
        };
    }

    public static IReadOnlyDictionary<string, float> ForChicken(
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        float headPitchDegrees,
        float headYawDegrees,
        float flapSpeed)
    {
        var state = new Dictionary<string, float>(ForLivingWalk(
            animationTimeSeconds, idlePhase01, wave, headPitchDegrees, headYawDegrees))
        {
            ["flapSpeed"] = flapSpeed,
            ["flap"] = ComputeWalkCycle(animationTimeSeconds, idlePhase01, wave).WalkPos
        };
        state["limbSwing"] = state["walkAnimationPos"];
        state["limbSwingAmount"] = state["walkAnimationSpeed"];
        return state;
    }

    public static (float WalkPos, float WalkSpeed) ComputeWalkCycle(
        float animationTimeSeconds,
        float idlePhase01,
        float wave)
    {
        var limbSwing = animationTimeSeconds * (MathF.PI * 2f * 1.8f);
        var limbSwingAmount = Math.Clamp(0.22f + idlePhase01 * 0.18f + wave * 0.12f, 0.05f, 0.95f);
        return (limbSwing, limbSwingAmount);
    }
}
