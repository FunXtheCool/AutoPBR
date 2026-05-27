// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.

namespace AutoPBR.Core.Preview;

/// <summary>Entity-specific GPU bone slot dispatch ladder (<see cref="TryBuildSpecific"/>).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{
    /// <summary>GPU specific-model dispatch slots 51-117.</summary>
    private static bool TryBuildSpecificDispatchSlots51ThroughEnd(
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        int? fastPathOnlySlot,
        ref int slot,
        float wave,
        out MergedJavaBlockModel merged,
        out int specificRouteSlotHit)
    {
        merged = null!;
        specificRouteSlotHit = 0;

        if (TryBuildSpecificDispatchSlots51Through90(
                normalizedAssetPath,
                stem,
                texRef,
                profile,
                isBaby,
                idlePhase01,
                animationTimeSeconds,
                fastPathOnlySlot,
                ref slot,
                wave,
                out merged,
                out specificRouteSlotHit))
        {
            return true;
        }

        if (TryBuildSpecificDispatchSlots91ThroughEnd(
                normalizedAssetPath,
                stem,
                texRef,
                profile,
                isBaby,
                idlePhase01,
                animationTimeSeconds,
                fastPathOnlySlot,
                ref slot,
                wave,
                out merged,
                out specificRouteSlotHit))
        {
            return true;
        }

        return false;
    }
}
