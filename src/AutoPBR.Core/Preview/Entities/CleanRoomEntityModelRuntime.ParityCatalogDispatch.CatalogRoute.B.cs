using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderCatalogRouteB(

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
            case "Slime":



                merged = BuildSlime(texRef, profile, isBaby);



                return true;

            case "Silverfish":



                merged = BuildSilverfish(texRef, profile, isBaby, ageInTicks: wave);



                return true;

            case "Endermite":



                merged = BuildEndermite(texRef, profile, isBaby, ageInTicks: wave);



                return true;

            case "ShulkerBullet":



                merged = BuildShulkerBullet(texRef, profile, isBaby, yRotDegrees: animationTimeSeconds * 45f, xRotDegrees: wave * 25f);



                return true;

            case "Shulker":



                merged = BuildShulker(



                    texRef,



                    profile,



                    isBaby,



                    peekAmount: Math.Clamp((wave + 1f) * 0.5f, 0f, 1f),



                    ageInTicks: animationTimeSeconds * 20f,



                    xRotDegrees: 0f,



                    yHeadRotDegrees: 180f,



                    yBodyRotDegrees: 0f);



                return true;

            case "SnowGolem":



                merged = BuildSnowGolem(texRef, profile, isBaby, yRotDegrees: animationTimeSeconds * 40f, xRotDegrees: 0f);



                return true;

            case "IronGolem":



                {



                    var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);



                    merged = BuildIronGolem(



                        texRef,



                        profile,



                        isBaby,



                        attackTicksRemaining: 0f,



                        offerFlowerTick: 0,



                        walkAnimationPos: walkPos,



                        walkAnimationSpeed: walkSpeed,



                        yRotDegrees: animationTimeSeconds * 28f,



                        xRotDegrees: 0f);



                    return true;



                }

            case "EndCrystal":



                merged = BuildEndCrystal(texRef, profile, isBaby, spin: idlePhase01 * 180f + animationTimeSeconds * 30f);



                return true;

            case "EvokerFangs":



                merged = BuildEvokerFangs(texRef, profile, isBaby, bitePhase: idlePhase01);



                return true;

            case "LlamaSpit":



                merged = BuildLlamaSpit(texRef, profile, isBaby);



                return true;

            case "Arrow":



                merged = BuildArrow(texRef, profile, isBaby, wobble: wave);



                return true;

            case "ArrowSpectral":



                merged = BuildArrow(texRef, profile, isBaby, wobble: wave);



                return true;

            case "ArrowTipped":



                merged = BuildArrow(texRef, profile, isBaby, wobble: wave);



                return true;

            case "WindCharge":



                merged = BuildWindCharge(texRef, profile, isBaby, spin: animationTimeSeconds);



                return true;

            case "Trident":



                merged = BuildTrident(texRef, profile, isBaby);



                return true;

            case "Shield":



                merged = BuildShield(texRef, profile, isBaby);



                return true;

            case "BannerFlagStanding":



                merged = BuildBannerFlag(texRef, profile, isBaby, isWall: false);



                return true;

            case "BannerFlagWall":



                merged = BuildBannerFlag(texRef, profile, isBaby, isWall: true);



                return true;

            case "Bed":



                merged = BuildBed(texRef, profile, isBaby);



                return true;

            case "EquipmentLayer":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;

            case "EquipmentWings":



                merged = BuildEquipmentLayer(texRef, profile, isBaby, normalizedAssetPath);



                return true;
        }

        return false;
    }
}
