using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    /// <summary>Invokes the rig named by <paramref name="builderMethod"/> (26.1.2 parity manifest <c>builder_method</c>).</summary>
    private static bool TryInvokeParityCatalogBuilder(
        string builderMethod,
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        out MergedJavaBlockModel merged)
    {
        merged = null!;
        var wave = Wave(animationTimeSeconds, 0.8f);
        if (TryInvokeParityCatalogBuilderCatalogRoute(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderEquipmentRoute(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        return TryInvokeParityCatalogBuilderFallbacks(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged);

    }
}
