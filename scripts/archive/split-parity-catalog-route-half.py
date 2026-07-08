#!/usr/bin/env python3
"""Split a ParityCatalogDispatch route switch into two halves (order preserved)."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ENTITIES = ROOT / "src/AutoPBR.Core/Preview/Entities"

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


def extract_switch_blocks(lines: list[str], switch_start: int, switch_end: int) -> list[str]:
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
    return ["\n".join(b).rstrip() for b in blocks]


def split_route_file(
    source: Path,
    route_label: str,
    method_base: str,
    part_a_suffix: str,
    part_b_suffix: str,
) -> None:
    text = source.read_text(encoding="utf-8")
    lines = text.splitlines(keepends=True)
    if lines and not lines[-1].endswith("\n"):
        lines[-1] += "\n"

    switch_line = next(i for i, ln in enumerate(lines) if "switch (builderMethod)" in ln)
    depth = 0
    switch_end = -1
    for i in range(switch_line, len(lines)):
        depth += lines[i].count("{") - lines[i].count("}")
        if i > switch_line and depth == 0:
            switch_end = i
            break

    blocks = extract_switch_blocks(lines, switch_line, switch_end)
    mid = (len(blocks) + 1) // 2
    blocks_a, blocks_b = blocks[:mid], blocks[mid:]

    method_a = f"{method_base}A"
    method_b = f"{method_base}B"

    def write_partial(filename: str, method_name: str, case_blocks: list[str]) -> None:
        case_text = "\n\n".join(case_blocks)
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
        (ENTITIES / filename).write_text(partial, encoding="utf-8", newline="\n")
        first = case_blocks[0].split("\n", 1)[0].strip() if case_blocks else "(empty)"
        print(f"  {filename}: {len(case_blocks)} cases, first={first}")

    write_partial(
        f"CleanRoomEntityModelRuntime.ParityCatalogDispatch.{route_label}.{part_a_suffix}.cs",
        method_a,
        blocks_a,
    )
    write_partial(
        f"CleanRoomEntityModelRuntime.ParityCatalogDispatch.{route_label}.{part_b_suffix}.cs",
        method_b,
        blocks_b,
    )

    coordinator = (
        f"{FILE_HEADER}\n"
        f"    private static bool {method_base}(\n{METHOD_PARAMS}\n    )\n"
        f"    {{\n"
        f"        merged = null!;\n"
        f"        if ({method_a}(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged))\n"
        f"        {{\n"
        f"            return true;\n"
        f"        }}\n\n"
        f"        return {method_b}(builderMethod, normalizedAssetPath, stem, texRef, profile, isBaby, idlePhase01, animationTimeSeconds, wave, out merged);\n"
        f"    }}\n"
        f"}}\n"
    )
    source.write_text(coordinator, encoding="utf-8", newline="\n")
    print(f"  coordinator {source.name}: {len(blocks)} cases -> {len(blocks_a)}+{len(blocks_b)}")


ROUTES = [
    (
        ENTITIES / "CleanRoomEntityModelRuntime.ParityCatalogDispatch.CatalogRoute.cs",
        "CatalogRoute",
        "TryInvokeParityCatalogBuilderCatalogRoute",
        "A",
        "B",
    ),
    (
        ENTITIES / "CleanRoomEntityModelRuntime.ParityCatalogDispatch.EquipmentRoute.cs",
        "EquipmentRoute",
        "TryInvokeParityCatalogBuilderEquipmentRoute",
        "A",
        "B",
    ),
    (
        ENTITIES / "CleanRoomEntityModelRuntime.ParityCatalogDispatch.Fallbacks.cs",
        "Fallbacks",
        "TryInvokeParityCatalogBuilderFallbacks",
        "A",
        "B",
    ),
]


def main() -> None:
    for source, label, method, a, b in ROUTES:
        if not source.is_file():
            print(f"skip missing {source.name}", file=sys.stderr)
            continue
        print(label)
        split_route_file(source, label, method, a, b)


if __name__ == "__main__":
    main()
