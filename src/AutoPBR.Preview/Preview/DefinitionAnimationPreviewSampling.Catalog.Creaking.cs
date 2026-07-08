using System.Numerics;


namespace AutoPBR.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string Creaking = "net.minecraft.client.animation.definitions.CreakingAnimation";

    internal static bool TrySampleCreakingWalkUpperBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Creaking, "CREAKING_WALK", "upper_body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCreakingAttackUpperBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Creaking, "CREAKING_ATTACK", "upper_body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCreakingAttackUpperBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Creaking, "CREAKING_ATTACK", "upper_body", timeSeconds, out translation);
    internal static bool TrySampleCreakingAttackHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Creaking, "CREAKING_ATTACK", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCreakingDeathUpperBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Creaking, "CREAKING_DEATH", "upper_body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCreakingInvulnerableRightArmRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Creaking, "CREAKING_INVULNERABLE", "right_arm", timeSeconds, out eulerDegrees);
    internal static bool TrySampleCreakingInvulnerableUpperBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Creaking, "CREAKING_INVULNERABLE", "upper_body", timeSeconds, out eulerDegrees);
}

