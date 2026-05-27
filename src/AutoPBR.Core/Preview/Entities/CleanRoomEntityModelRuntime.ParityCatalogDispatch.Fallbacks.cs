using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderFallbacks(

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
        if (TryInvokeParityCatalogBuilderFallbacksA(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderFallbacksB(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderFallbacksC(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderFallbacksD(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        return TryInvokeParityCatalogBuilderFallbacksE(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged);
    }
}
