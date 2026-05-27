using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    private static (float Rh, float Lh, float Rf, float Lf) ComputePreviewStandardQuadrupedLegPitches(
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        string? builderMethod = null,
        string? setupAnimModelJvm = null)
    {
        var state = PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave);
        var model = SetupAnimParityResolver.ResolveModelJvm(builderMethod, setupAnimModelJvm);
        const string quadruped = "net.minecraft.client.model.QuadrupedModel";
        if (VanillaSetupAnimRuntime.TryGetLegXRots(model, state, out var rh, out var lh, out var rf, out var lf) &&
            VanillaSetupAnimRuntime.LegPitchesVaryWithWalk(model, idlePhase01, wave))
        {
            return (rh, lh, rf, lf);
        }

        if (!string.Equals(model, quadruped, StringComparison.Ordinal) &&
            VanillaSetupAnimRuntime.TryGetLegXRots(quadruped, state, out rh, out lh, out rf, out lf))
        {
            return (rh, lh, rf, lf);
        }

        return (0f, 0f, 0f, 0f);
    }

    internal static float ComputePreviewRabbitHopSinTerm(float animationTimeSeconds) =>
        MathF.Sin(animationTimeSeconds * MathF.PI * 2f);

    internal static float ComputeFoxTailBaselinePitchRad(float tailLift) =>
        (0.45f - (tailLift * 0.22f)) * MathF.PI;
}
