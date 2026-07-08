using System.Numerics;


namespace AutoPBR.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string BabyRabbit = "net.minecraft.client.animation.definitions.BabyRabbitAnimation";

    private const string Rabbit = "net.minecraft.client.animation.definitions.RabbitAnimation";

    internal static bool TrySampleRabbitIdleHeadTiltBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Rabbit, "IDLE_HEAD_TILT", "body", timeSeconds, out translation);
    internal static bool TrySampleRabbitIdleHeadTiltHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Rabbit, "IDLE_HEAD_TILT", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyRabbitHopFrontLegsRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyRabbit, "HOP", "frontlegs", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyRabbitHopFrontLegsPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, BabyRabbit, "HOP", "frontlegs", timeSeconds, out translation);
    internal static bool TrySampleBabyRabbitIdleHeadTiltBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, BabyRabbit, "IDLE_HEAD_TILT", "body", timeSeconds, out translation);
    internal static bool TrySampleRabbitHopFrontLegsPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Rabbit, "HOP", "frontlegs", timeSeconds, out translation);
    internal static bool TrySampleRabbitHopBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Rabbit, "HOP", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleRabbitHopTailRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Rabbit, "HOP", "tail", timeSeconds, out eulerDegrees);
    internal static bool TrySampleRabbitHopRightHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Rabbit, "HOP", "right_hind_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleRabbitHopLeftHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Rabbit, "HOP", "left_hind_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleRabbitHopFrontLegsRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Rabbit, "HOP", "frontlegs", timeSeconds, out eulerDegrees);
    internal static bool TrySampleRabbitHopHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Rabbit, "HOP", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyRabbitHopBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyRabbit, "HOP", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyRabbitHopHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyRabbit, "HOP", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyRabbitHopRightHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyRabbit, "HOP", "right_hind_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyRabbitHopTailRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyRabbit, "HOP", "tail", timeSeconds, out eulerDegrees);
    internal static bool TrySampleRabbitHopRightFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Rabbit, "HOP", "right_front_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleRabbitIdleHeadTiltHeadPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Rabbit, "IDLE_HEAD_TILT", "head", timeSeconds, out translation);
}

