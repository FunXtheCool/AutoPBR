using System.Numerics;


namespace AutoPBR.Core.Preview;

/// <summary>Named catalog entries for CleanRoom definition playback (replaces per-entity <c>VanillaAnimationIrPreviewSampler.TrySample*</c>).</summary>
internal static partial class DefinitionAnimationPreviewSampling
{
    private const string Armadillo = "net.minecraft.client.animation.definitions.ArmadilloAnimation";
    private const string BabyArmadillo = "net.minecraft.client.animation.definitions.BabyArmadilloAnimation";
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

    internal static bool TrySampleNautilusSwimmingUpperMouthRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Nautilus, "SWIMMING", "upper_mouth", timeSeconds, out eulerDegrees);

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

    internal static bool TrySampleWardenSniffBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Warden, "WARDEN_SNIFF", "body", timeSeconds, out eulerDegrees);

    internal static bool TrySampleArmadilloWalkTailRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Armadillo, "ARMADILLO_WALK", "tail", timeSeconds, out eulerDegrees);

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

    internal static bool TrySampleCamelBabyWalkHeadPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, CamelBaby, "CAMEL_BABY_WALK", "head", timeSeconds, out translation);

    internal static bool TrySampleCamelWalkRootRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, Camel, "CAMEL_WALK", "root", timeSeconds, out eulerDegrees);

    internal static bool TrySampleRabbitIdleHeadTiltBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Rabbit, "IDLE_HEAD_TILT", "body", timeSeconds, out translation);

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

    internal static bool TrySampleFrogCroakCroakingBodyPosition(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 translation) =>
        SamplePosition(profile, Frog, "FROG_CROAK", "croaking_body", timeSeconds, out translation);

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

    internal static bool TrySampleCopperGolemWalkBodyRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, CopperGolem, "COPPER_GOLEM_WALK", "body", timeSeconds, out eulerDegrees);

    internal static bool TrySampleFoxBabyWalkRightHindLegRotationDegrees(
        MinecraftNativeProfile? profile,
        float timeSeconds,
        out Vector3 eulerDegrees) =>
        SampleRotationDegrees(profile, FoxBaby, "FOX_BABY_WALK", "right_hind_leg", timeSeconds, out eulerDegrees);
}
