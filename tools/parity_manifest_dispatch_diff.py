"""Compare minecraft_26.1.2_entity_texture_model_manifest builder_method set vs ParityCatalogDispatch cases."""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_texture_model_manifest.json"
DISPATCH = ROOT / "src/AutoPBR.Core/Preview/Entities/CleanRoomEntityModelRuntime.ParityCatalogDispatch.cs"


def main() -> None:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    rules = manifest.get("rules", manifest) if isinstance(manifest, dict) else manifest
    manifest_set = {r["builder_method"] for r in rules}

    dispatch_text = DISPATCH.read_text(encoding="utf-8")
    dispatch_set = set(re.findall(r'case "([^"]+)"', dispatch_text))

    only_manifest = sorted(manifest_set - dispatch_set)
    only_dispatch = sorted(dispatch_set - manifest_set)

    print(f"Manifest unique builders: {len(manifest_set)}")
    print(f"Dispatch unique cases:    {len(dispatch_set)}")
    print()
    print(f"In manifest but NOT in dispatch ({len(only_manifest)}):")
    for x in only_manifest:
        print(f"  {x}")
    print()
    print(f"In dispatch but NOT in manifest ({len(only_dispatch)}):")
    for x in only_dispatch:
        print(f"  {x}")


if __name__ == "__main__":
    main()
