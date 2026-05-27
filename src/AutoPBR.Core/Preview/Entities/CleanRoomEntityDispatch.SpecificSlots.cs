using System.Numerics;
// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Entity-specific GPU bone slot dispatch ladder (<see cref="TryBuildSpecific"/>).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryGpuBoneSpecificDispatchSlot(ref int slot, int? fastPathOnlySlot, out int dispatchSlot)
    {
        dispatchSlot = ++slot;
        return !fastPathOnlySlot.HasValue || fastPathOnlySlot.GetValueOrDefault() == dispatchSlot;
    }

    private static bool TryBuildSpecific(
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        int? fastPathOnlySlot,
        out MergedJavaBlockModel merged,
        out int specificRouteSlotHit)
    {
        merged = null!;
        specificRouteSlotHit = 0;
        var wave = Wave(animationTimeSeconds, 0.8f);
        var slot = 0;

        if (TryBuildSpecificDispatchSlots1Through50(
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

        if (TryBuildSpecificDispatchSlots51ThroughEnd(
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
