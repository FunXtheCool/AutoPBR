using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderEquipmentRoute(

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
        if (TryInvokeParityCatalogBuilderEquipmentRouteA(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderEquipmentRouteB(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderEquipmentRouteC(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderEquipmentRouteD(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderEquipmentRouteE(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        return TryInvokeParityCatalogBuilderEquipmentRouteF(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged);
    }
}
