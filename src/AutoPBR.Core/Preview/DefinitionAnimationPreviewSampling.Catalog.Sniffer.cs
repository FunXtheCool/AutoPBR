using System.Numerics;


namespace AutoPBR.Core.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string Sniffer = "net.minecraft.client.animation.definitions.SnifferAnimation";

    internal static bool TrySampleSnifferLongSniffHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_LONGSNIFF", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferWalkHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_WALK", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferWalkBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_WALK", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferWalkRightFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_WALK", "right_front_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferWalkLeftFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_WALK", "left_front_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferWalkLeftMidLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_WALK", "left_mid_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferWalkRightMidLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_WALK", "right_mid_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferWalkRightHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_WALK", "right_hind_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferWalkLeftHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_WALK", "left_hind_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferWalkRightHindLegPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Sniffer, "SNIFFER_WALK", "right_hind_leg", timeSeconds, out translation);
    internal static bool TrySampleSnifferWalkLeftHindLegPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Sniffer, "SNIFFER_WALK", "left_hind_leg", timeSeconds, out translation);
    internal static bool TrySampleSnifferWalkRightMidLegPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Sniffer, "SNIFFER_WALK", "right_mid_leg", timeSeconds, out translation);
    internal static bool TrySampleSnifferDigBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_DIG", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferDigBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Sniffer, "SNIFFER_DIG", "body", timeSeconds, out translation);
    internal static bool TrySampleSnifferDigHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_DIG", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferHappyHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_HAPPY", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleSnifferStandUpBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Sniffer, "SNIFFER_STAND_UP", "body", timeSeconds, out translation);
    internal static bool TrySampleSnifferStandUpBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Sniffer, "SNIFFER_STAND_UP", "body", timeSeconds, out eulerDegrees);
}

