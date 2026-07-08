using System.Numerics;


namespace AutoPBR.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string Camel = "net.minecraft.client.animation.definitions.CamelAnimation";

    private const string CamelBaby = "net.minecraft.client.animation.definitions.CamelBabyAnimation";

    internal static bool TrySampleCamelBabyWalkHeadPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, CamelBaby, "CAMEL_BABY_WALK", "head", timeSeconds, out translation);
    internal static bool TrySampleCamelBabyWalkRightFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, CamelBaby, "CAMEL_BABY_WALK", "right_front_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCamelBabyWalkLeftFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, CamelBaby, "CAMEL_BABY_WALK", "left_front_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCamelWalkRootRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Camel, "CAMEL_WALK", "root", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCamelIdleTailRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Camel, "CAMEL_IDLE", "tail", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCamelBabyWalkLeftHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, CamelBaby, "CAMEL_BABY_WALK", "left_hind_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCamelBabyWalkRightHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, CamelBaby, "CAMEL_BABY_WALK", "right_hind_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCamelDashHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Camel, "CAMEL_DASH", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCamelSitBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Camel, "CAMEL_SIT", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCamelStandupBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Camel, "CAMEL_STANDUP", "body", timeSeconds, out eulerDegrees);
}

