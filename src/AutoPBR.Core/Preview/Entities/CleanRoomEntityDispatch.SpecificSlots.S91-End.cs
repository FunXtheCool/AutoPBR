using System.Numerics;
// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Entity-specific GPU bone slot dispatch ladder (<see cref="TryBuildSpecific"/>).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{

/// <summary>GPU specific-model dispatch slots 91-117.</summary>
    private static bool TryBuildSpecificDispatchSlots91ThroughEnd(
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
        if (TryBuildSpecificDispatchSlots91Through104(
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

        return TryBuildSpecificDispatchSlots105ThroughEnd(
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
            out specificRouteSlotHit);
    }
}
