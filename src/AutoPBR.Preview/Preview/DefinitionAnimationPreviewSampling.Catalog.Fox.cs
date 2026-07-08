using System.Numerics;


namespace AutoPBR.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string FoxBaby = "net.minecraft.client.animation.definitions.FoxBabyAnimation";

    internal static bool TrySampleFoxBabyWalkRightHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, FoxBaby, "FOX_BABY_WALK", "right_hind_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleFoxBabyWalkLeftFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, FoxBaby, "FOX_BABY_WALK", "left_front_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleFoxBabyWalkHeadPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, FoxBaby, "FOX_BABY_WALK", "head", timeSeconds, out translation);
    internal static bool TrySampleFoxBabyWalkLeftHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, FoxBaby, "FOX_BABY_WALK", "left_hind_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleFoxBabyWalkRightFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, FoxBaby, "FOX_BABY_WALK", "right_front_leg", timeSeconds, out eulerDegrees);
}

