using System.Numerics;


namespace AutoPBR.Core.Preview;

/// <summary>Named catalog entries for CleanRoom definition playback (replaces per-entity <c>VanillaAnimationIrPreviewSampler.TrySample*</c>).</summary>
internal static partial class DefinitionAnimationPreviewSampling
{
    private const string Armadillo = "net.minecraft.client.animation.definitions.ArmadilloAnimation";
    private const string BabyArmadillo = "net.minecraft.client.animation.definitions.BabyArmadilloAnimation";
    private const string BabyAxolotl = "net.minecraft.client.animation.definitions.BabyAxolotlAnimation";
    private const string BabyRabbit = "net.minecraft.client.animation.definitions.BabyRabbitAnimation";
    private const string Bat = "net.minecraft.client.animation.definitions.BatAnimation";
    private const string Breeze = "net.minecraft.client.animation.definitions.BreezeAnimation";
    private const string Camel = "net.minecraft.client.animation.definitions.CamelAnimation";
    private const string CamelBaby = "net.minecraft.client.animation.definitions.CamelBabyAnimation";
    private const string CopperGolem = "net.minecraft.client.animation.definitions.CopperGolemAnimation";
    private const string Creaking = "net.minecraft.client.animation.definitions.CreakingAnimation";
    private const string FoxBaby = "net.minecraft.client.animation.definitions.FoxBabyAnimation";
    private const string Frog = "net.minecraft.client.animation.definitions.FrogAnimation";
    private const string Nautilus = "net.minecraft.client.animation.definitions.NautilusAnimation";
    private const string Rabbit = "net.minecraft.client.animation.definitions.RabbitAnimation";
    private const string Sniffer = "net.minecraft.client.animation.definitions.SnifferAnimation";
    private const string Warden = "net.minecraft.client.animation.definitions.WardenAnimation";

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

    internal static bool TrySampleNautilusSwimmingUpperMouthRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Nautilus, "SWIMMING", "upper_mouth", timeSeconds, out eulerDegrees);

    internal static bool TrySampleNautilusSwimmingBodyScale(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 scale) =>
        SampleScale(profile, Nautilus, "SWIMMING", "body", timeSeconds, out scale);

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

    internal static bool TrySampleFrogCroakCroakingBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_CROAK", "croaking_body", timeSeconds, out translation);

    internal static bool TrySampleFrogCroakCroakingBodyScale(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 scale) =>
        SampleScale(profile, Frog, "FROG_CROAK", "croaking_body", timeSeconds, out scale);

    internal static bool TrySampleFrogWalkBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_WALK", "body", timeSeconds, out eulerDegrees);

    internal static bool TrySampleFrogWalkLeftLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_WALK", "left_leg", timeSeconds, out eulerDegrees);

    internal static bool TrySampleFrogWalkRightLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_WALK", "right_leg", timeSeconds, out eulerDegrees);

    internal static bool TrySampleFrogWalkLeftArmRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_WALK", "left_arm", timeSeconds, out eulerDegrees);

    internal static bool TrySampleFrogWalkRightArmRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_WALK", "right_arm", timeSeconds, out eulerDegrees);

    internal static bool TrySampleFrogWalkLeftArmPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_WALK", "left_arm", timeSeconds, out translation);

    internal static bool TrySampleFrogWalkRightArmPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_WALK", "right_arm", timeSeconds, out translation);

    internal static bool TrySampleFrogWalkLeftLegPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_WALK", "left_leg", timeSeconds, out translation);

    internal static bool TrySampleFrogWalkRightLegPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_WALK", "right_leg", timeSeconds, out translation);

    internal static bool TrySampleFrogTongueTongueRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_TONGUE", "tongue", timeSeconds, out eulerDegrees);

    internal static bool TrySampleFrogTongueHeadRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Frog, "FROG_TONGUE", "head", timeSeconds, out eulerDegrees);

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

    internal static bool TrySampleFoxBabyWalkRightHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, FoxBaby, "FOX_BABY_WALK", "right_hind_leg", timeSeconds, out eulerDegrees);

    internal static bool TrySampleFoxBabyWalkLeftFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, FoxBaby, "FOX_BABY_WALK", "left_front_leg", timeSeconds, out eulerDegrees);

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

    internal static bool TrySampleCopperGolemIdleBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, CopperGolem, "COPPER_GOLEM_IDLE", "body", timeSeconds, out eulerDegrees);

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

    internal static bool TrySampleFoxBabyWalkHeadPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, FoxBaby, "FOX_BABY_WALK", "head", timeSeconds, out translation);

    internal static bool TrySampleFoxBabyWalkLeftHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, FoxBaby, "FOX_BABY_WALK", "left_hind_leg", timeSeconds, out eulerDegrees);

    internal static bool TrySampleFoxBabyWalkRightFrontLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, FoxBaby, "FOX_BABY_WALK", "right_front_leg", timeSeconds, out eulerDegrees);

    internal static bool TrySampleNautilusSwimmingInnerMouthScale(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 scale) =>
        SampleScale(profile, Nautilus, "SWIMMING", "inner_mouth", timeSeconds, out scale);

    internal static bool TrySampleNautilusSwimmingLowerMouthScale(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 scale) =>
        SampleScale(profile, Nautilus, "SWIMMING", "lower_mouth", timeSeconds, out scale);

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
