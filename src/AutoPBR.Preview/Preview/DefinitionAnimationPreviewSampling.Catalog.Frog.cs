using System.Numerics;


namespace AutoPBR.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string Frog = "net.minecraft.client.animation.definitions.FrogAnimation";

    internal static bool TrySampleFrogCroakCroakingBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_CROAK", "croaking_body", timeSeconds, out translation);
    internal static bool TrySampleFrogCroakCroakingBodyScale(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 scale) =>
        SampleScale(profile, Frog, "FROG_CROAK", "croaking_body", timeSeconds, out scale);
    internal static bool TrySampleFrogWalkBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_WALK", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleFrogWalkLeftLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_WALK", "left_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleFrogWalkRightLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_WALK", "right_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleFrogWalkLeftArmRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_WALK", "left_arm", timeSeconds, out eulerDegrees);
    internal static bool TrySampleFrogWalkRightArmRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_WALK", "right_arm", timeSeconds, out eulerDegrees);
    internal static bool TrySampleFrogWalkLeftArmPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_WALK", "left_arm", timeSeconds, out translation);
    internal static bool TrySampleFrogWalkRightArmPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_WALK", "right_arm", timeSeconds, out translation);
    internal static bool TrySampleFrogWalkLeftLegPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_WALK", "left_leg", timeSeconds, out translation);
    internal static bool TrySampleFrogWalkRightLegPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_WALK", "right_leg", timeSeconds, out translation);
    internal static bool TrySampleFrogTongueTongueRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_TONGUE", "tongue", timeSeconds, out eulerDegrees);
    internal static bool TrySampleFrogTongueHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_TONGUE", "head", timeSeconds, out eulerDegrees);
}

