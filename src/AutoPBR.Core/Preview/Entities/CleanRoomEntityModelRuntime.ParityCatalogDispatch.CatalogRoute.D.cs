using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderCatalogRouteD(

        string builderMethod,
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        float wave,
        out MergedJavaBlockModel merged
    )
    {
        merged = null!;
        switch (builderMethod)
        {
            case "Sniffer":







                {







                    var snifferHead = idlePhase01 * 0.12f + wave * 0.08f;







                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferLongSniffHeadRotationDegrees(profile, animationTimeSeconds, out var sniffHeadDeg))







                    {







                        snifferHead += sniffHeadDeg.X * (MathF.PI / 180f);







                    }















                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkHeadRotationDegrees(profile, animationTimeSeconds, out var walkHeadDeg))







                    {







                        snifferHead += walkHeadDeg.X * (MathF.PI / 180f);







                    }















                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkBodyRotationDegrees(profile, animationTimeSeconds, out var walkBodyDeg))







                    {







                        snifferHead += walkBodyDeg.X * (MathF.PI / 180f) * 0.15f;







                    }















                    var sniffWalkRf = 0f;







                    var sniffWalkLf = 0f;







                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkRightFrontLegRotationDegrees(profile, animationTimeSeconds, out var sniffRfDeg))







                    {







                        sniffWalkRf = sniffRfDeg.X * (MathF.PI / 180f);







                    }















                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftFrontLegRotationDegrees(profile, animationTimeSeconds, out var sniffLfDeg))







                    {







                        sniffWalkLf = sniffLfDeg.X * (MathF.PI / 180f);







                    }















                    const float sniffDegToRad = MathF.PI / 180f;







                    var sniffWalkLm = Vector3.Zero;







                    if (DefinitionAnimationPreviewSampling.TrySampleSnifferWalkLeftMidLegRotationDegrees(profile, animationTimeSeconds, out var sniffLmDeg))







                    {







                        sniffWalkLm = new Vector3(







                            sniffLmDeg.X * sniffDegToRad,







                            sniffLmDeg.Y * sniffDegToRad,







                            sniffLmDeg.Z * sniffDegToRad);







                    }















                    merged = BuildSniffer(







                        texRef,







                        profile,







                        isBaby,







                        headPitch: snifferHead,







                        walkRightFrontLegPitchRad: sniffWalkRf,







                        walkLeftFrontLegPitchRad: sniffWalkLf,







                        walkLeftMidLegEulerRad: sniffWalkLm);







                    return true;







                }

            case "Wither":







                // Manifest currently routes wither_skeleton through "Wither"; keep that texture on skeleton rig parity.







                merged = normalizedAssetPath.Contains("/textures/entity/skeleton/wither_skeleton", StringComparison.OrdinalIgnoreCase)







                    ? BuildSkeletonHumanoid(texRef, profile, isBaby, armLift: idlePhase01 * 0.25f + wave * 0.10f)







                    : BuildWither(texRef, profile, isBaby, wave: idlePhase01 * 0.35f + wave * 0.12f);







                return true;

            case "Warden":







                {







                    var wardenSway = idlePhase01 * 0.30f + wave * 0.10f;







                    if (DefinitionAnimationPreviewSampling.TrySampleWardenSniffBodyRotationDegrees(profile, animationTimeSeconds, out var wardenBodyDeg))







                    {







                        wardenSway += wardenBodyDeg.Z * (MathF.PI / 180f) * 0.15f;







                    }















                    merged = BuildWarden(texRef, profile, isBaby, sway: wardenSway);







                    return true;







                }

            case "MagmaCube":







                merged = BuildMagmaCube(texRef, profile, isBaby, squish: MathF.Max(0f, wave));







                return true;

            case "Slime":







                merged = BuildSlime(texRef, profile, isBaby);







                return true;

            case "Silverfish":







                merged = BuildSilverfish(texRef, profile, isBaby, ageInTicks: wave);







                return true;

        }

        return false;
    }
}
