#!/usr/bin/env python3
"""Reorganize AutoPBR.Core.Preview into nested namespaces (Phase 3)."""
from __future__ import annotations

import os
import re
import shutil

ROOT = os.path.join(os.path.dirname(__file__), "..", "src", "AutoPBR.Core", "Preview")
ROOT = os.path.normpath(ROOT)

PARITY_FILES = {
    "BlockTextureParityRule.cs",
    "BlockTextureParityPreviewShape.cs",
    "BlockTextureParityCatalog.cs",
    "EntityTextureParityRule.cs",
    "EntityTextureParityCatalog.cs",
    "SetupAnimParityResolver.cs",
    "ParityCatalogEntityPreviewDiagnostics.cs",
    "ParityCatalogHandLiftGeometryIrCatalog.cs",
    "EntityParityAnimationMap.cs",
    "GeometryAssemblyParityPilots.cs",
}

BLOCK_PREFIX = "VanillaBlock"
GEOMETRY_PREFIX = "GeometryIr"


def set_namespace(path: str, ns: str) -> None:
    with open(path, encoding="utf-8") as f:
        content = f.read()
    new_content, n = re.subn(
        r"^namespace AutoPBR\.Core\.Preview(?:\.[A-Za-z]+)?;",
        f"namespace {ns};",
        content,
        count=1,
        flags=re.MULTILINE,
    )
    if n == 0:
        raise RuntimeError(f"No namespace line in {path}")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(new_content)


def move_file(name: str, dest_dir: str, ns: str) -> None:
    src = os.path.join(ROOT, name)
    if not os.path.isfile(src):
        return
    os.makedirs(dest_dir, exist_ok=True)
    dst = os.path.join(dest_dir, name)
    if os.path.abspath(src) != os.path.abspath(dst):
        shutil.move(src, dst)
    set_namespace(dst, ns)


def main() -> None:
    entities_dir = os.path.join(ROOT, "Entities")
    for fname in os.listdir(entities_dir):
        if fname.endswith(".cs"):
            set_namespace(os.path.join(entities_dir, fname), "AutoPBR.Core.Preview.Entities")

    blocks_dir = os.path.join(ROOT, "Blocks")
    for fname in os.listdir(ROOT):
        if fname.startswith(BLOCK_PREFIX) and fname.endswith(".cs"):
            move_file(fname, blocks_dir, "AutoPBR.Core.Preview.Blocks")

    parity_dir = os.path.join(ROOT, "Parity")
    for fname in list(PARITY_FILES):
        move_file(fname, parity_dir, "AutoPBR.Core.Preview.Parity")

    geometry_dir = os.path.join(ROOT, "GeometryIr")
    for fname in list(os.listdir(ROOT)):
        if fname.startswith(GEOMETRY_PREFIX) and fname.endswith(".cs"):
            move_file(fname, geometry_dir, "AutoPBR.Core.Preview.GeometryIr")

    print("Preview namespace reorganization complete.")


if __name__ == "__main__":
    main()
