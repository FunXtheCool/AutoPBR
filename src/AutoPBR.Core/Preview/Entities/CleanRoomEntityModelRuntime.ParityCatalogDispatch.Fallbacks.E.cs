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







                merged = BuildHumanoid(texRef, profile, isBaby, armLift: 0.18f + idlePhase01 * 0.25f + wave * 0.08f);







                return true;

            case "PlayerSlim":







                merged = BuildPlayerSlim(texRef, profile, isBaby, armLift: 0.18f + idlePhase01 * 0.25f + wave * 0.08f);







                return true;

            case "PlayerWide":







                merged = BuildPlayerWide(texRef, profile, isBaby, armLift: 0.18f + idlePhase01 * 0.25f + wave * 0.08f);







                return true;

            case "HumanoidGeneric":







                merged = BuildHumanoid(texRef, profile, isBaby, armLift: idlePhase01 * 0.4f + wave * 0.1f);







                return true;

        }

        return false;
    }
}
