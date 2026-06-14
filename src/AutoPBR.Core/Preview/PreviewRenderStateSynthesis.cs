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
            ["ageScale"] = 1f,
            ["xRot"] = headPitchDegrees,
            ["yRot"] = headYawDegrees,
            ["ageInTicks"] = animationTimeSeconds * 20f,
            ["isMoving"] = walkSpeed > 0.08f ? 1f : 0f,
        };
    }

    public static IReadOnlyDictionary<string, float> ForChicken(
        float animationTimeSeconds,
        float idlePhase01,
        float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.ChickenRenderer", animationTimeSeconds, idlePhase01, wave);

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

    public static IReadOnlyDictionary<string, float> ForAllay(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.AllayRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForBreeze(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.BreezeRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForArmadillo(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.ArmadilloRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForRabbit(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.RabbitRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForBat(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.BatRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForSniffer(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.SnifferRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForCamel(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.CamelRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForWarden(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.WardenRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForFrog(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.FrogRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForCreaking(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.CreakingRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForNautilus(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.NautilusRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForCopperGolem(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.CopperGolemRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForCod(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.CodRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForSalmon(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.SalmonRenderer", animationTimeSeconds, idlePhase01, wave);

    public static IReadOnlyDictionary<string, float> ForTropicalFish(float animationTimeSeconds, float idlePhase01, float wave) =>
        RendererStatePreviewResolver.SynthesizeForRenderer(
            "net.minecraft.client.renderer.entity.TropicalFishRenderer", animationTimeSeconds, idlePhase01, wave);
}
