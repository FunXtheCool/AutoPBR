#!/usr/bin/env python3
"""
Emit class-path lists by *reading* a Minecraft client.jar via ZipFile — the JAR is never modified.

Outputs (fixed paths under `src/AutoPBR.Core/Data/minecraft-native/`):

1. `minecraft_26.1.2_client_model_classes.txt` — `net/minecraft/client/model/**` entity/block models
   (excludes `geom/` and `builders/` subtrees, and `package-info.class` stubs; same policy as historical tooling).

2. `minecraft_26.1.2_client_animation_definition_classes.txt` —
   `net/minecraft/client/animation/definitions/*Animation.class` (mob `AnimationDefinition` holders;
   complements `Generate-MinecraftClientModelIndex.ps1`, which adds `javap -c` sidecars for these).

Typical use (developer machine; jar stays read-only):
  python tools/build_minecraft_client_model_class_index.py path/to/client.jar

For **obfuscated** jars (e.g. 1.21.11), entry names are not `net/minecraft/...`; build the model class list from the committed
`docs/generated/minecraft-client-model-index-<ver>.json` instead:

  python tools/build_client_model_class_list_from_model_index_json.py docs/generated/minecraft-client-model-index-1.21.11.json 1.21.11
"""

from __future__ import annotations

import sys
import zipfile
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__.strip(), file=sys.stderr)
        return 2

    jar_path = Path(sys.argv[1]).resolve()
    repo = Path(__file__).resolve().parent.parent
    native = repo / "src/AutoPBR.Core/Data/minecraft-native"
    out_model = native / "minecraft_26.1.2_client_model_classes.txt"
    out_anim = native / "minecraft_26.1.2_client_animation_definition_classes.txt"

    model_prefix = "net/minecraft/client/model/"
    anim_prefix = "net/minecraft/client/animation/definitions/"
    model_lines: list[str] = []
    anim_lines: list[str] = []
    with zipfile.ZipFile(jar_path, "r") as z:
        for name in z.namelist():
            if not name.endswith(".class"):
                continue
            n = name.rstrip("/")
            if n.startswith(model_prefix):
                if "/geom/" in n or "/builders/" in n:
                    continue
                if n.endswith("package-info.class"):
                    continue
                model_lines.append(n)
            elif n.startswith(anim_prefix) and "Animation.class" in n and "$" not in n:
                anim_lines.append(n)

    model_lines.sort()
    anim_lines.sort()
    native.mkdir(parents=True, exist_ok=True)
    out_model.write_text("\n".join(model_lines) + ("\n" if model_lines else ""), encoding="utf-8")
    out_anim.write_text("\n".join(anim_lines) + ("\n" if anim_lines else ""), encoding="utf-8")
    print(f"Wrote {len(model_lines)} model paths -> {out_model}")
    print(f"Wrote {len(anim_lines)} animation definition paths -> {out_anim}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
