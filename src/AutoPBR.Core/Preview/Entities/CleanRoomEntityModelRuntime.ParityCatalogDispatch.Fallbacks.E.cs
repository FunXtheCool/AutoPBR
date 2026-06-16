using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryInvokeParityCatalogBuilderFallbacksE(

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
            case "PlayerHumanoid":







                merged = BuildHumanoid(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, "PlayerHumanoid");







                return true;

            case "PlayerSlim":







                merged = BuildPlayerSlim(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);







                return true;

            case "PlayerWide":







                merged = BuildPlayerWide(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave);







                return true;

            case "HumanoidGeneric":







                merged = BuildHumanoid(texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, "HumanoidGeneric");







                return true;

        }

        return false;
    }
}
