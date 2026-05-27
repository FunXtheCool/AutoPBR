using System.Numerics;


namespace AutoPBR.Core.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string Warden = "net.minecraft.client.animation.definitions.WardenAnimation";

    internal static bool TrySampleWardenSniffBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_SNIFF", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleWardenSniffHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_SNIFF", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleWardenAttackBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_ATTACK", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleWardenAttackHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_ATTACK", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleWardenEmergeBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_EMERGE", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleWardenEmergeHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_EMERGE", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleWardenRoarBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_ROAR", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleWardenRoarHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_ROAR", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleWardenSonicBoomBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_SONIC_BOOM", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleWardenSonicBoomHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_SONIC_BOOM", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleWardenSonicBoomRightRibcageRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_SONIC_BOOM", "right_ribcage", timeSeconds, out eulerDegrees);
}

