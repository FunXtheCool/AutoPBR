#!/usr/bin/env python3
"""
Build `minecraft_<versionLabel>_client_model_classes.txt` from a committed
`docs/generated/minecraft-client-model-index-<ver>.json`.

Obfuscated client JARs do not contain `net/minecraft/...` entry names, so the
class list cannot be produced by scanning the ZIP; the model index JSON already
carries official <-> obfuscated rows from ProGuard mappings + javap.

Filter policy matches `build_minecraft_client_model_class_index.py`:
  - `net.minecraft.client.model.**` only
  - exclude `geom/` and `builders/` subtrees (slash form under model/)
  - exclude `package-info.class` (Java package metadata, not mesh models)

Usage:
  python tools/build_client_model_class_list_from_model_index_json.py \\
    docs/generated/minecraft-client-model-index-1.21.11.json 1.21.11
"""

from __future__ import annotations

import json
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__.strip(), file=sys.stderr)
        return 2

    index_json = Path(sys.argv[1]).resolve()
    version_label = sys.argv[2].strip()
    if not index_json.is_file():
        print(f"Index JSON not found: {index_json}", file=sys.stderr)
        return 2

    repo = Path(__file__).resolve().parent.parent
    out = repo / f"src/AutoPBR.Core/Data/minecraft-native/minecraft_{version_label}_client_model_classes.txt"

    data = json.loads(index_json.read_text(encoding="utf-8"))
    rows = data.get("classes")
    if not isinstance(rows, list):
        print("JSON missing 'classes' array.", file=sys.stderr)
        return 2

    model_prefix = "net.minecraft.client.model."
    paths: set[str] = set()
    for row in rows:
        if not isinstance(row, dict):
            continue
        name = row.get("officialJvmName")
        if not isinstance(name, str) or not name.startswith(model_prefix):
            continue
        if name.endswith(".package-info"):
            continue
        slash = name.replace(".", "/") + ".class"
        if "/geom/" in slash or "/builders/" in slash:
            continue
        paths.add(slash)

    out.parent.mkdir(parents=True, exist_ok=True)
    ordered = sorted(paths)
    out.write_text("\n".join(ordered) + ("\n" if ordered else ""), encoding="utf-8")
    print(f"Wrote {len(ordered)} model paths -> {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
