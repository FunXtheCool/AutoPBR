using System.Numerics;


namespace AutoPBR.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string Bat = "net.minecraft.client.animation.definitions.BatAnimation";

    internal static bool TrySampleBatFlyingRightWingRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_FLYING", "right_wing", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatFlyingLeftWingRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_FLYING", "left_wing", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatRestingRightWingRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_RESTING", "right_wing", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatRestingLeftWingRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_RESTING", "left_wing", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatRestingRightWingPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Bat, "BAT_RESTING", "right_wing", timeSeconds, out translation);
    internal static bool TrySampleBatRestingLeftWingPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Bat, "BAT_RESTING", "left_wing", timeSeconds, out translation);
    internal static bool TrySampleBatFlyingRightWingTipRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_FLYING", "right_wing_tip", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatFlyingLeftWingTipRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_FLYING", "left_wing_tip", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatRestingRightWingTipRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_RESTING", "right_wing_tip", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatRestingLeftWingTipRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_RESTING", "left_wing_tip", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatFlyingBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_FLYING", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatFlyingFeetRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_FLYING", "feet", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatFlyingHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_FLYING", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatRestingBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Bat, "BAT_RESTING", "body", timeSeconds, out translation);
    internal static bool TrySampleBatRestingBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_RESTING", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBatRestingHeadPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Bat, "BAT_RESTING", "head", timeSeconds, out translation);
    internal static bool TrySampleBatRestingHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Bat, "BAT_RESTING", "head", timeSeconds, out eulerDegrees);
}

