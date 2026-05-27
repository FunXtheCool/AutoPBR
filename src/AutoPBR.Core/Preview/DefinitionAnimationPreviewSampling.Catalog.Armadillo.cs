using System.Numerics;


namespace AutoPBR.Core.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string Armadillo = "net.minecraft.client.animation.definitions.ArmadilloAnimation";

    private const string BabyArmadillo = "net.minecraft.client.animation.definitions.BabyArmadilloAnimation";

    internal static bool TrySampleArmadilloWalkTailRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Armadillo, "ARMADILLO_WALK", "tail", timeSeconds, out eulerDegrees);
    internal static bool TrySampleArmadilloRollUpBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Armadillo, "ARMADILLO_ROLL_UP", "body", timeSeconds, out translation);
    internal static bool TrySampleArmadilloRollUpHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Armadillo, "ARMADILLO_ROLL_UP", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyArmadilloWalkTailRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyArmadillo, "ARMADILLO_BABY_WALK", "tail", timeSeconds, out eulerDegrees);
    internal static bool TrySampleArmadilloPeekHeadPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Armadillo, "ARMADILLO_PEEK", "head", timeSeconds, out translation);
    internal static bool TrySampleArmadilloPeekHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Armadillo, "ARMADILLO_PEEK", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleArmadilloPeekLeftFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Armadillo, "ARMADILLO_PEEK", "left_front_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleArmadilloPeekRightFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Armadillo, "ARMADILLO_PEEK", "right_front_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleArmadilloPeekRightHindLegPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Armadillo, "ARMADILLO_PEEK", "right_hind_leg", timeSeconds, out translation);
    internal static bool TrySampleArmadilloRollOutHeadPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Armadillo, "ARMADILLO_ROLL_OUT", "head", timeSeconds, out translation);
    internal static bool TrySampleArmadilloRollOutHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Armadillo, "ARMADILLO_ROLL_OUT", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyArmadilloPeekHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyArmadillo, "ARMADILLO_BABY_PEEK", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyArmadilloRollUpBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, BabyArmadillo, "ARMADILLO_BABY_ROLL_UP", "body", timeSeconds, out translation);
}

