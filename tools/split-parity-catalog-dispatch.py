#!/usr/bin/env python3
"""Mechanically split a monolithic ParityCatalogDispatch switch into three route partials.

After the initial 3-way split, use split-parity-catalog-route-half.py to halve each route
(CatalogRoute / EquipmentRoute / Fallbacks) while preserving case order.
"""

from __future__ import annotations

import re
from pathlib import Path

SOURCE = Path(__file__).resolve().parents[1] / "src/AutoPBR.Core/Preview/Entities/CleanRoomEntityModelRuntime.ParityCatalogDispatch.cs"

METHOD_PARAMS = """
        string builderMethod,
        string normalizedAssetPath,
        string stem,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float idlePhase01,
        float animationTimeSeconds,
        float wave,
        out MergedJavaBlockModel merged"""

FILE_HEADER = """using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{"""

GROUPS = [
    ("CleanRoomEntityModelRuntime.ParityCatalogDispatch.CatalogRoute.cs", "TryInvokeParityCatalogBuilderCatalogRoute"),
    ("CleanRoomEntityModelRuntime.ParityCatalogDispatch.EquipmentRoute.cs", "TryInvokeParityCatalogBuilderEquipmentRoute"),
    ("CleanRoomEntityModelRuntime.ParityCatalogDispatch.Fallbacks.cs", "TryInvokeParityCatalogBuilderFallbacks"),
]

COORD_BODY = """        if (TryInvokeParityCatalogBuilderCatalogRoute(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        if (TryInvokeParityCatalogBuilderEquipmentRoute(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))
        {
            return true;
        }

        return TryInvokeParityCatalogBuilderFallbacks(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged);
"""


def extract_switch_blocks(lines: list[str], switch_start: int, switch_end: int) -> tuple[list[str], str]:
    blocks: list[list[str]] = []
    current: list[str] = []
    for i in range(switch_start + 1, switch_end):
        line = lines[i]
        if re.match(r'^\s+case "', line) and current:
            blocks.append(current)
            current = []
        if re.match(r'^\s+case "', line) or current:
            current.append(line)
    if current:
        blocks.append(current)

    default_block = ""
    last = blocks[-1]
    default_idx = None
    for j, ln in enumerate(last):
        if re.match(r'^\s+default:', ln):
            default_idx = j
            break
    if default_idx is not None:
        default_block = "".join(last[default_idx:]).rstrip()
        blocks[-1] = last[:default_idx]

    text_blocks = ["\n".join(b).rstrip() for b in blocks]
    return text_blocks, default_block


def main() -> None:
    text = SOURCE.read_text(encoding="utf-8")
    lines = text.splitlines(keepends=True)
    if lines and not lines[-1].endswith("\n"):
        lines[-1] += "\n"

    switch_line = next(i for i, ln in enumerate(lines) if re.search(r"switch \(builderMethod\)", ln))
    depth = 0
    switch_end = -1
    for i in range(switch_line, len(lines)):
        depth += lines[i].count("{") - lines[i].count("}")
        if i > switch_line and depth == 0:
            switch_end = i
            break

    method_line = next(i for i in range(switch_line, -1, -1) if "TryInvokeParityCatalogBuilder" in lines[i])
    method_depth = 0
    method_close = -1
    started = False
    for i in range(method_line, len(lines)):
        for ch in lines[i]:
            if ch == "{":
                method_depth += 1
                started = True
            elif ch == "}":
                method_depth -= 1
        if started and method_depth == 0:
            method_close = i
            break

    blocks, default_block = extract_switch_blocks(lines, switch_line, switch_end)
    per_group = (len(blocks) + 2) // 3
    out_dir = SOURCE.parent

    for g, (filename, method_name) in enumerate(GROUPS):
        start = g * per_group
        end = min((g + 1) * per_group, len(blocks))
        case_text = "\n\n".join(blocks[start:end])
        _ = default_block  # default handled by trailing return false in each partial
        partial = (
            f"{FILE_HEADER}\n"
            f"    private static bool {method_name}(\n{METHOD_PARAMS}\n    )\n"
            f"    {{\n"
            f"        merged = null!;\n"
            f"        switch (builderMethod)\n"
            f"        {{\n{case_text}\n        }}\n\n"
            f"        return false;\n"
            f"    }}\n"
            f"}}\n"
        )
        (out_dir / filename).write_text(partial, encoding="utf-8", newline="\n")
        first = blocks[start].split("\n", 1)[0].strip()
        print(f"{filename}: {end - start} blocks, first={first}")

    # Coordinator: method header + merged/wave setup, then delegate to partials (no switch).
    preamble = "".join(lines[method_line:switch_line])
    tail = "".join(lines[method_close:])
    coordinator = "".join(lines[:method_line]) + preamble + COORD_BODY + "\n" + tail
    SOURCE.write_text(coordinator, encoding="utf-8", newline="\n")
    print(f"Coordinator written, {len(blocks)} case blocks total")


if __name__ == "__main__":
    main()
