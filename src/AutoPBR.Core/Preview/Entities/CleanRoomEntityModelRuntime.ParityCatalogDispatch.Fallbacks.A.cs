using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderFallbacksA(

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
            case "Allay":



                merged = BuildAllay(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.70f + wave * 0.22f);



                return true;

            case "Vex":



                merged = BuildVex(



                    texRef,



                    profile,



                    isBaby,



                    yRotDegrees: animationTimeSeconds * 36f,



                    xRotDegrees: idlePhase01 * 8f + wave * 5f,



                    ageInTicks: animationTimeSeconds * 20f,



                    isCharging: false,



                    rightHandHoldingItem: false,



                    leftHandHoldingItem: false);



                return true;

            case "Phantom":



                merged = BuildPhantom(normalizedAssetPath, texRef, profile, isBaby, flapTime: animationTimeSeconds);



                return true;

            case "Parrot":



                merged = BuildParrot(texRef, profile, isBaby, wingFlap: idlePhase01 * 0.55f + wave * 0.22f);



                return true;

            case "HappyGhastHarness":



                merged = BuildHappyGhastHarness(texRef, profile, isBaby, gogglesEquippedBlend: idlePhase01);



                return true;

            case "HappyGhast":



                merged = BuildHappyGhast(texRef, profile, isBaby, tentacleSway: idlePhase01 * 0.5f + wave * 0.25f);



                return true;

            case "Ghast":



                merged = BuildGhast(texRef, profile, isBaby, tentacleSway: idlePhase01 * 0.5f + wave * 0.25f);



                return true;

            case "GuardianElder":



                merged = BuildGuardian(texRef, profile, isBaby, spinePulse: idlePhase01 * 0.4f + wave * 0.2f, geometryScale: 2.35f);



                return true;

            case "Guardian":



                merged = BuildGuardian(texRef, profile, isBaby, spinePulse: idlePhase01 * 0.4f + wave * 0.2f, geometryScale: 1f);



                return true;

            case "GuardianBeam":



                merged = BuildBeamColumn(texRef, profile, isBaby, twist: idlePhase01 * MathF.PI * 2f);



                return true;

            case "Pufferfish":



                merged = BuildPufferfish(texRef, profile, isBaby, puff: 0.4f + idlePhase01 * 0.25f + wave * 0.12f);



                return true;

            case "Turtle":



                merged = BuildTurtle(texRef, profile, isBaby, swimLift: idlePhase01 * 0.20f + wave * 0.08f);



                return true;

            case "Squid":



                merged = BuildSquid(texRef, profile, isBaby, tentacleWave: idlePhase01 * 0.45f + wave * 0.25f);



                return true;

            case "Salmon":



                merged = BuildSalmon(texRef, profile, isBaby, tailSway: idlePhase01 * 0.7f + wave * 0.22f);



                return true;

            case "Cod":



                merged = BuildCod(texRef, profile, isBaby, tailSway: idlePhase01 * 0.8f + wave * 0.25f);



                return true;

            case "TropicalFishB":



                merged = BuildTropicalFishB(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);



                return true;

            case "TropicalFishA":



                merged = BuildTropicalFishA(texRef, profile, isBaby, tailSway: idlePhase01 * 0.75f + wave * 0.24f);



                return true;

            case "Strider":



                {



                    var (walkPos, rawWalkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);



                    var walkSpeed = MathF.Min(0.25f, rawWalkSpeed);



                    merged = BuildStrider(



                        texRef,



                        profile,



                        isBaby,



                        walkAnimationPos: walkPos,



                        walkAnimationSpeed: walkSpeed,



                        ageInTicks: animationTimeSeconds * 20f);



                    return true;



                }

            case "Tadpole":



                merged = BuildTadpole(texRef, profile, isBaby, tailSway: idlePhase01 * 0.45f + wave * 0.2f);



                return true;

            case "Axolotl":



                {



                    var (rh, lh, rf, lf) = ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "Axolotl");



                    merged = BuildAxolotl(



                        texRef,



                        profile,



                        isBaby,



                        idleBob: idlePhase01 * 0.12f + wave * 0.06f,



                        rightHindLegPitchRad: rh,



                        leftHindLegPitchRad: lh,



                        rightFrontLegPitchRad: rf,



                        leftFrontLegPitchRad: lf);



                    return true;



                }

            case "Frog":



                {



                    var frogCroak = idlePhase01 * 0.08f + wave * 0.05f;



                    if (DefinitionAnimationPreviewSampling.TrySampleFrogCroakCroakingBodyPosition(profile, animationTimeSeconds, out var croakBodyPos))



                    {



                        frogCroak = Math.Clamp(croakBodyPos.Y, 0f, 1f);



                    }







                    var frogLeftLegPitch = 0f;



                    var frogRightLegPitch = 0f;



                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftLegRotationDegrees(profile, animationTimeSeconds, out var frogLLegDeg))



                    {



                        frogLeftLegPitch = frogLLegDeg.X * (MathF.PI / 180f);



                    }







                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightLegRotationDegrees(profile, animationTimeSeconds, out var frogRLegDeg))



                    {



                        frogRightLegPitch = frogRLegDeg.X * (MathF.PI / 180f);



                    }







                    const float frogDegToRad = MathF.PI / 180f;



                    var frogLArmX = 0f;



                    var frogLArmY = 0f;



                    var frogLArmZ = 0f;



                    var frogRArmX = 0f;



                    var frogRArmY = 0f;



                    var frogRArmZ = 0f;



                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftArmRotationDegrees(profile, animationTimeSeconds, out var frogLArmDeg))



                    {



                        frogLArmX = frogLArmDeg.X * frogDegToRad;



                        frogLArmY = frogLArmDeg.Y * frogDegToRad;



                        frogLArmZ = frogLArmDeg.Z * frogDegToRad;



                    }







                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightArmRotationDegrees(profile, animationTimeSeconds, out var frogRArmDeg))



                    {



                        frogRArmX = frogRArmDeg.X * frogDegToRad;



                        frogRArmY = frogRArmDeg.Y * frogDegToRad;



                        frogRArmZ = frogRArmDeg.Z * frogDegToRad;



                    }







                    var frogLArmPos = Vector3.Zero;



                    var frogRArmPos = Vector3.Zero;



                    var frogLLegPos = Vector3.Zero;



                    var frogRLegPos = Vector3.Zero;



                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftArmPosition(profile, animationTimeSeconds, out var pLa))



                    {



                        frogLArmPos = pLa;



                    }







                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightArmPosition(profile, animationTimeSeconds, out var pRa))



                    {



                        frogRArmPos = pRa;



                    }







                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkLeftLegPosition(profile, animationTimeSeconds, out var pLl))



                    {



                        frogLLegPos = pLl;



                    }







                    if (DefinitionAnimationPreviewSampling.TrySampleFrogWalkRightLegPosition(profile, animationTimeSeconds, out var pRl))



                    {



                        frogRLegPos = pRl;



                    }







                    merged = BuildFrog(



                        texRef,



                        profile,



                        isBaby,



                        croakInflate: frogCroak,



                        walkLeftLegPitchRad: frogLeftLegPitch,



                        walkRightLegPitchRad: frogRightLegPitch,



                        walkLeftArmXRad: frogLArmX,



                        walkLeftArmYRad: frogLArmY,



                        walkLeftArmZRad: frogLArmZ,



                        walkRightArmXRad: frogRArmX,



                        walkRightArmYRad: frogRArmY,



                        walkRightArmZRad: frogRArmZ,



                        walkLeftArmPos: frogLArmPos,



                        walkRightArmPos: frogRArmPos,



                        walkLeftLegPos: frogLLegPos,



                        walkRightLegPos: frogRLegPos);



                    return true;



                }
        }

        return false;
    }
}
