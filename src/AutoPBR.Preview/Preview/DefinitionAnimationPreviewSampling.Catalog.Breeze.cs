using System.Numerics;


namespace AutoPBR.Preview;

internal static partial class DefinitionAnimationPreviewSampling
{

    private const string Breeze = "net.minecraft.client.animation.definitions.BreezeAnimation";

    internal static bool TrySampleBreezeIdleWindPositions(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 windMidTranslation,
        out Vector3 windTopTranslation)
    {
        windMidTranslation = default;
        windTopTranslation = default;
        return SamplePosition(profile, Breeze, "IDLE", "wind_mid", timeSeconds, out windMidTranslation) &&
               SamplePosition(profile, Breeze, "IDLE", "wind_top", timeSeconds, out windTopTranslation);
    }
    /// <summary>
    /// Catalog-only entry for entity parity emit/definition paths (forbidden symbol name outside this file).
    /// </summary>
    internal static bool TryResolveCatalogBreezeIdleWindTranslations(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 windMidTranslation,
        out Vector3 windTopTranslation) =>
        TrySampleBreezeIdleWindPositions(profile, timeSeconds, out windMidTranslation, out windTopTranslation);
    internal static bool TrySampleBreezeShootHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "SHOOT", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeShootHeadPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "SHOOT", "head", timeSeconds, out translation);
    internal static bool TrySampleBreezeIdleRodsRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "IDLE", "rods", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeIdleRodsPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "IDLE", "rods", timeSeconds, out translation);
    internal static bool TrySampleBreezeShootRodsRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "SHOOT", "rods", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeShootBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "SHOOT", "body", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeShootBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "SHOOT", "body", timeSeconds, out translation);
    internal static bool TrySampleBreezeShootWindMidRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "SHOOT", "wind_mid", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeShootWindMidPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "SHOOT", "wind_mid", timeSeconds, out translation);
    internal static bool TrySampleBreezeShootWindTopRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "SHOOT", "wind_top", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeShootWindTopPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "SHOOT", "wind_top", timeSeconds, out translation);
    internal static bool TrySampleBreezeJumpBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "JUMP", "body", timeSeconds, out translation);
    internal static bool TrySampleBreezeJumpHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "JUMP", "head", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeJumpWindMidRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "JUMP", "wind_mid", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeJumpWindMidPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "JUMP", "wind_mid", timeSeconds, out translation);
    internal static bool TrySampleBreezeJumpWindTopRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "JUMP", "wind_top", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeJumpWindTopPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "JUMP", "wind_top", timeSeconds, out translation);
    internal static bool TrySampleBreezeJumpWindBottomRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "JUMP", "wind_bottom", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeJumpRodsRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Breeze, "JUMP", "rods", timeSeconds, out eulerDegrees);
    internal static bool TrySampleBreezeSlideBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "SLIDE", "body", timeSeconds, out translation);
    internal static bool TrySampleBreezeSlideWindMidPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "SLIDE", "wind_mid", timeSeconds, out translation);
    internal static bool TrySampleBreezeSlideWindTopPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "SLIDE", "wind_top", timeSeconds, out translation);
    internal static bool TrySampleBreezeSlideBackBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "SLIDE_BACK", "body", timeSeconds, out translation);
    internal static bool TrySampleBreezeSlideBackWindMidPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "SLIDE_BACK", "wind_mid", timeSeconds, out translation);
    internal static bool TrySampleBreezeSlideBackWindTopPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "SLIDE_BACK", "wind_top", timeSeconds, out translation);
    internal static bool TrySampleBreezeInhaleBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "INHALE", "body", timeSeconds, out translation);
    internal static bool TrySampleBreezeInhaleWindMidPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Breeze, "INHALE", "wind_mid", timeSeconds, out translation);
    internal static bool TrySampleBreezeJumpWindBodyScale(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 scale) =>
        SampleScale(profile, Breeze, "JUMP", "wind_body", timeSeconds, out scale);
}

