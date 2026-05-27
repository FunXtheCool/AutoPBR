using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderCatalogRoute(

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
        if (TryInvokeParityCatalogBuilderCatalogRouteA(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderCatalogRouteB(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderCatalogRouteC(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderCatalogRouteD(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        return TryInvokeParityCatalogBuilderCatalogRouteE(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged);
    }
}
