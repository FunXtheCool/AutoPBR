using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class EntityModelRuntime
{

    internal static void ComputeChickenParityPreviewDrivers(
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        out float headPitchRad,
        out float headYawRad,
        out float wingZRadians,
        out float rightLegPitchRad,
        out float leftLegPitchRad)
    {
        var flapSpeed = 0.18f + Math.Clamp(0.22f + idlePhase01 * 0.18f + wave * 0.12f, 0.05f, 0.95f) * 0.55f;
        var state = PreviewRenderStateSynthesis.ForChicken(
            animationTimeSeconds,
            idlePhase01,
            wave,
            (idlePhase01 * 8f) + (wave * 5f),
            wave * 10f,
            flapSpeed);
        headPitchRad = headYawRad = wingZRadians = rightLegPitchRad = leftLegPitchRad = 0f;
        const string chickenModel = "net.minecraft.client.model.animal.chicken.ChickenModel";
        if (!VanillaSetupAnimRuntime.TryEvaluate(chickenModel, state, animationTimeSeconds, out var pose))
        {
            VanillaSetupAnimRuntime.TryEvaluate("net.minecraft.client.model.QuadrupedModel", state, animationTimeSeconds, out pose);
        }

        if (pose.Parts.TryGetValue("head", out var head))
        {
            headPitchRad = head.XRot;
            headYawRad = head.YRot;
        }
        else if (state.TryGetValue("xRot", out var pitchDeg) && state.TryGetValue("yRot", out var yawDeg))
        {
            headPitchRad = pitchDeg * PreviewRenderStateSynthesis.DegToRad;
            headYawRad = yawDeg * PreviewRenderStateSynthesis.DegToRad;
        }

        if (pose.Parts.TryGetValue("rightWing", out var rw))
        {
            wingZRadians = rw.ZRot;
        }

        if (pose.Parts.TryGetValue("rightLeg", out var rl))
        {
            rightLegPitchRad = rl.XRot;
        }

        if (pose.Parts.TryGetValue("leftLeg", out var ll))
        {
            leftLegPitchRad = ll.XRot;
        }
    }

    /// <summary>Adult <c>chicken_cold.png</c> uses <c>ColdChickenModel.createBodyLayer</c> (26.1.2 javap), not the temperate head/beak/wattle split.</summary>
    private static bool IsAdultColdChickenStem(string stemLower) =>
        string.Equals(stemLower, "chicken_cold", StringComparison.OrdinalIgnoreCase);

}
