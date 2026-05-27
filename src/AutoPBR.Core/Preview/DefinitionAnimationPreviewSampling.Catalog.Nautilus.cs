using System.Numerics;


namespace AutoPBR.Core.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string Nautilus = "net.minecraft.client.animation.definitions.NautilusAnimation";

    internal static bool TrySampleNautilusSwimmingUpperMouthRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Nautilus, "SWIMMING", "upper_mouth", timeSeconds, out eulerDegrees);
    internal static bool TrySampleNautilusSwimmingBodyScale(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 scale) =>
        SampleScale(profile, Nautilus, "SWIMMING", "body", timeSeconds, out scale);
    internal static bool TrySampleNautilusSwimmingInnerMouthScale(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 scale) =>
        SampleScale(profile, Nautilus, "SWIMMING", "inner_mouth", timeSeconds, out scale);
    internal static bool TrySampleNautilusSwimmingLowerMouthScale(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 scale) =>
        SampleScale(profile, Nautilus, "SWIMMING", "lower_mouth", timeSeconds, out scale);
}

