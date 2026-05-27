"""Mechanically split CleanRoomEntityDispatch.SpecificSlots S51-90 and S91-End shards."""
from pathlib import Path

base = Path(__file__).resolve().parents[1] / "src/AutoPBR.Core/Preview/Entities"

header = """using System.Numerics;
// ReSharper disable CheckNamespace
// ReSharper disable DuplicatedStatements -- GPU fast-path dispatch repeats per-entity slot guards intentionally.

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>Entity-specific GPU bone slot dispatch ladder (<see cref="TryBuildSpecific"/>).</summary>
internal sealed partial class CleanRoomEntityModelRuntime
{

"""

common_params = """        string normalizedAssetPath,
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
        out int specificRouteSlotHit)"""


def wrap(name: str, body: str, doc: str) -> str:
    return (
        header
        + f"/// <summary>{doc}</summary>\n"
        + f"    private static bool {name}(\n"
        + common_params
        + """
    {
        merged = null!;
        specificRouteSlotHit = 0;

"""
        + body
        + """
        return false;
    }
}
"""
    )


def coordinator_51_90() -> str:
    return (
        header
        + """/// <summary>GPU specific-model dispatch slots 51-90.</summary>
    private static bool TryBuildSpecificDispatchSlots51Through90(
"""
        + common_params
        + """
    {
        merged = null!;
        specificRouteSlotHit = 0;
        if (TryBuildSpecificDispatchSlots51Through70(
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

        return TryBuildSpecificDispatchSlots71Through90(
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
"""
    )


def coordinator_91_end() -> str:
    return (
        header
        + """/// <summary>GPU specific-model dispatch slots 91-117.</summary>
    private static bool TryBuildSpecificDispatchSlots91ThroughEnd(
"""
        + common_params
        + """
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
"""
    )


def main() -> None:
    src90 = (base / "CleanRoomEntityDispatch.SpecificSlots.S51-90.cs").read_text(
        encoding="utf-8"
    ).splitlines(keepends=True)
    body_51_70 = "".join(src90[28:520])
    body_71_90 = "".join(src90[522:865]).replace(
        "if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))",
        "if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out var gpuBoneDispatchSlot))",
        1,
    )

    (base / "CleanRoomEntityDispatch.SpecificSlots.S51-90.cs").write_text(
        coordinator_51_90(), encoding="utf-8"
    )
    (base / "CleanRoomEntityDispatch.SpecificSlots.S51-70.cs").write_text(
        wrap(
            "TryBuildSpecificDispatchSlots51Through70",
            body_51_70,
            "GPU specific-model dispatch slots 51-70.",
        ),
        encoding="utf-8",
    )
    (base / "CleanRoomEntityDispatch.SpecificSlots.S71-90.cs").write_text(
        wrap(
            "TryBuildSpecificDispatchSlots71Through90",
            body_71_90,
            "GPU specific-model dispatch slots 71-90.",
        ),
        encoding="utf-8",
    )

    src91 = (base / "CleanRoomEntityDispatch.SpecificSlots.S91-End.cs").read_text(
        encoding="utf-8"
    ).splitlines(keepends=True)
    body_91_104 = "".join(src91[28:389])
    body_105_end = "".join(src91[391:621]).replace(
        "if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out gpuBoneDispatchSlot))",
        "if (TryGpuBoneSpecificDispatchSlot(ref slot, fastPathOnlySlot, out var gpuBoneDispatchSlot))",
        1,
    )

    (base / "CleanRoomEntityDispatch.SpecificSlots.S91-End.cs").write_text(
        coordinator_91_end(), encoding="utf-8"
    )
    (base / "CleanRoomEntityDispatch.SpecificSlots.S91-104.cs").write_text(
        wrap(
            "TryBuildSpecificDispatchSlots91Through104",
            body_91_104,
            "GPU specific-model dispatch slots 91-104.",
        ),
        encoding="utf-8",
    )
    (base / "CleanRoomEntityDispatch.SpecificSlots.S105-End.cs").write_text(
        wrap(
            "TryBuildSpecificDispatchSlots105ThroughEnd",
            body_105_end,
            "GPU specific-model dispatch slots 105-117.",
        ),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
