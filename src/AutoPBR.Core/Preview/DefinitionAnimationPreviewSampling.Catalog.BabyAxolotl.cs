using System.Numerics;


namespace AutoPBR.Core.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string BabyAxolotl = "net.minecraft.client.animation.definitions.BabyAxolotlAnimation";

    internal static bool TrySampleBabyAxolotlIdleFloorHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyAxolotl, "BABY_AXOLOTL_IDLE_FLOOR", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyAxolotlIdleFloorTailRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyAxolotl, "BABY_AXOLOTL_IDLE_FLOOR", "tail", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyAxolotlPlayDeadBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyAxolotl, "BABY_AXOLOTL_PLAY_DEAD", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyAxolotlSwimBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyAxolotl, "BABY_AXOLOTL_SWIM", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyAxolotlSwimRightFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyAxolotl, "BABY_AXOLOTL_SWIM", "right_front_leg", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBabyAxolotlSwimTailRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, BabyAxolotl, "BABY_AXOLOTL_SWIM", "tail", timeSeconds, out eulerDegrees);
}

