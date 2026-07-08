using System.Numerics;


namespace AutoPBR.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string CopperGolem = "net.minecraft.client.animation.definitions.CopperGolemAnimation";

    internal static bool TrySampleCopperGolemWalkBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, CopperGolem, "COPPER_GOLEM_WALK", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCopperGolemWalkHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, CopperGolem, "COPPER_GOLEM_WALK", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCopperGolemWalkRightArmRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, CopperGolem, "COPPER_GOLEM_WALK", "right_arm", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCopperGolemIdleBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, CopperGolem, "COPPER_GOLEM_IDLE", "body", timeSeconds, out eulerDegrees);
}

