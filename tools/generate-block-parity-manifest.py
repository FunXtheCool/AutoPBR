#!/usr/bin/env python3
"""Generate minecraft_26.1.2_block_textures.json and block_texture_model_manifest.json."""

from __future__ import annotations

import json
import sys
import urllib.request
from collections import defaultdict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = REPO_ROOT / "src/AutoPBR.Core/Data/minecraft-native"
GITHUB_TREE_URL = (
    "https://api.github.com/repos/InventivetalentDev/minecraft-assets/git/trees/26.1.2?recursive=1"
)
BLOCK_PREFIX = "assets/minecraft/textures/block/"

COMPOUND_SUFFIXES = ("_side_overlay",)
FAMILY_SUFFIXES = ("_lower", "_upper", "_bottom", "_top", "_inner", "_side", "_overlay")


def blockstate_stem(stem: str) -> str:
    lower = stem.lower()
    for suf in COMPOUND_SUFFIXES + FAMILY_SUFFIXES:
        if lower.endswith(suf) and len(lower) > len(suf):
            return lower[: -len(suf)]
    return lower


def is_non_cube_stem(stem: str) -> bool:
    lower = stem.lower()
    if lower.endswith("_particle"):
        return True
    if "_overlay" in lower:
        return True
    if lower.endswith("_on") or lower.endswith("_off"):
        return True
    return False


def fetch_block_paths() -> list[str]:
    with urllib.request.urlopen(GITHUB_TREE_URL, timeout=120) as resp:
        data = json.load(resp)
    paths: list[str] = []
    for entry in data.get("tree", []):
        path = entry.get("path", "")
        if path.startswith(BLOCK_PREFIX) and path.lower().endswith(".png"):
            paths.append(path.replace("\\", "/"))
    paths.sort(key=str.lower)
    return paths


CROSS_SPRITE_FAMILIES = frozenset({"fern", "short_grass", "tall_grass", "large_fern"})


def is_pack_model_json_only_family(family_id: str) -> bool:
    fid = family_id.lower()
    if fid.endswith("_on") or fid.endswith("_off"):
        return True
    if "command_block" in fid:
        return True
    if fid in {"fire", "soul_fire", "water_still", "water_flow", "lava_still", "lava_flow"}:
        return True
    return False


def pick_side_stem(family_id: str, stems: set[str]) -> str | None:
    side = f"{family_id}_side"
    if side in stems:
        return side
    if family_id in stems:
        return family_id
    for s in sorted(stems):
        if s.endswith("_side"):
            return s
    return None


def pick_top_stem(family_id: str, stems: set[str]) -> str | None:
    top = f"{family_id}_top"
    if top in stems:
        return top
    for s in sorted(stems):
        if s.endswith("_top"):
            return s
    return None


def pick_bottom_stem(family_id: str, stems: set[str]) -> str | None:
    bottom = f"{family_id}_bottom"
    if bottom in stems:
        return bottom
    if family_id == "grass_block":
        return "dirt"
    return None


def pick_trapdoor_stem(family_id: str, stems: set[str]) -> str:
    if family_id in stems:
        return family_id
    return sorted(stems)[0]


def pick_door_half_stems(family_id: str, stems: set[str]) -> tuple[str, str]:
    bottom = f"{family_id}_bottom"
    top = f"{family_id}_top"
    if bottom not in stems:
        bottom = next((s for s in stems if s.endswith("_bottom")), sorted(stems)[0])
    if top not in stems:
        top = next((s for s in stems if s.endswith("_top")), sorted(stems)[0])
    return bottom, top


def pick_fence_stem(family_id: str, stems: set[str]) -> str:
    if family_id in stems:
        return family_id
    return sorted(stems)[0]


def classify_family(family_id: str, stems: set[str]) -> tuple[str, dict[str, str] | None]:
    fid = family_id.lower()

    if fid == "cake":
        slots = {
            "up": "cake_top",
            "down": "cake_bottom",
            "north": "cake_side",
            "south": "cake_side",
            "east": "cake_side",
            "west": "cake_side",
        }
        return "CakeWedge", slots

    if fid == "cactus":
        slots = {
            "up": "cactus_top",
            "down": "cactus_bottom",
            "north": "cactus_side",
            "south": "cactus_side",
            "east": "cactus_side",
            "west": "cactus_side",
        }
        return "CactusCross", slots

    if "trapdoor" in fid:
        tex = pick_trapdoor_stem(fid, stems)
        return "ThinPlate", {"texture": tex}

    if fid.endswith("_door") or "_door_" in fid:
        bottom, top = pick_door_half_stems(fid, stems)
        return "DoorHalf", {"bottom": bottom, "top": top}

    if fid.endswith("_fence") or fid.endswith("_fence_gate"):
        tex = pick_fence_stem(fid, stems)
        return "FenceWithLink", {"texture": tex}

    if fid in CROSS_SPRITE_FAMILIES:
        tex = pick_fence_stem(fid, stems)
        return "CrossSprite", {"texture": tex}

    if "rail" in fid:
        tex = pick_fence_stem(fid, stems)
        return "RailTrack", {"texture": tex}

    if is_pack_model_json_only_family(fid):
        return "PackModelJsonOnly", None

    if len(stems) == 1:
        only = next(iter(stems))
        slots = {face: only for face in ("up", "down", "north", "south", "east", "west")}
        return "UniformCube", slots

    top = pick_top_stem(family_id, stems)
    side = pick_side_stem(family_id, stems)
    bottom = pick_bottom_stem(family_id, stems)

    if top and side and family_id in stems and f"{family_id}_side" not in stems:
        slots = {
            "up": top,
            "down": top,
            "north": side,
            "south": side,
            "east": side,
            "west": side,
        }
        return "CubeColumnY", slots

    if top and side:
        slots = {
            "up": top,
            "down": bottom or top,
            "north": side,
            "south": side,
            "east": side,
            "west": side,
        }
        return "CubeDirectional", slots

    if top and bottom and not side:
        slots = {
            "up": top,
            "down": bottom,
            "north": top,
            "south": top,
            "east": top,
            "west": top,
        }
        return "CubeDirectional", slots

    return "PackModelJsonOnly", None


def build_manifest(paths: list[str]) -> tuple[dict, dict]:
    families: dict[str, set[str]] = defaultdict(set)
    for path in paths:
        rel = path[len(BLOCK_PREFIX) :]
        stem = Path(rel).stem.lower()
        family_id = blockstate_stem(stem)
        families[family_id].add(stem)

    family_shape: dict[str, tuple[str, dict[str, str] | None]] = {}
    for family_id, stems in families.items():
        family_shape[family_id] = classify_family(family_id, stems)

    rules: list[dict] = []
    for path in paths:
        rel = path[len(BLOCK_PREFIX) :]
        stem = Path(rel).stem.lower()
        family_id = blockstate_stem(stem)
        shape, slots = family_shape[family_id]
        if stem == "cake_inner":
            shape = "CakeSlice"
            slots = {
                "up": "cake_top",
                "down": "cake_bottom",
                "north": "cake_side",
                "south": "cake_side",
                "east": "cake_side",
                "west": "cake_side",
                "inside": "cake_inner",
            }
        if is_non_cube_stem(stem):
            shape = "PackModelJsonOnly"
            slots = None
        prefix = path[:-4]
        rule: dict = {
            "path_prefix": prefix,
            "family_id": family_id,
            "preview_shape": shape,
        }
        if slots is not None:
            rule["texture_slots"] = slots
        rules.append(rule)

    rules.sort(key=lambda r: r["path_prefix"].lower())

    inventory = {
        "source": {
            "minecraft_version": "26.1.2",
            "source_kind": "client_jar_extracted_assets_mirror",
            "source_repository": "InventivetalentDev/minecraft-assets",
            "source_branch": "26.1.2",
            "root_path": "assets/minecraft/textures/block",
        },
        "files": [{"path": p} for p in paths],
        "counts": {"files": len(paths)},
    }
    manifest = {
        "minecraft_version": "26.1.2",
        "rules": rules,
        "counts": {
            "rules": len(rules),
            "uniform_cube": sum(1 for r in rules if r["preview_shape"] == "UniformCube"),
            "cube_directional": sum(1 for r in rules if r["preview_shape"] == "CubeDirectional"),
            "cube_column_y": sum(1 for r in rules if r["preview_shape"] == "CubeColumnY"),
            "thin_plate": sum(1 for r in rules if r["preview_shape"] == "ThinPlate"),
            "door_half": sum(1 for r in rules if r["preview_shape"] == "DoorHalf"),
            "cake_wedge": sum(1 for r in rules if r["preview_shape"] == "CakeWedge"),
            "cake_slice": sum(1 for r in rules if r["preview_shape"] == "CakeSlice"),
            "cactus_cross": sum(1 for r in rules if r["preview_shape"] == "CactusCross"),
            "fence_post": sum(1 for r in rules if r["preview_shape"] == "FencePost"),
            "fence_with_link": sum(1 for r in rules if r["preview_shape"] == "FenceWithLink"),
            "rail_track": sum(1 for r in rules if r["preview_shape"] == "RailTrack"),
            "stair_wedge": sum(1 for r in rules if r["preview_shape"] == "StairWedge"),
            "cross_sprite": sum(1 for r in rules if r["preview_shape"] == "CrossSprite"),
            "pack_model_json_only": sum(
                1 for r in rules if r["preview_shape"] == "PackModelJsonOnly"
            ),
        },
    }
    return inventory, manifest


def main() -> int:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    print("Fetching block texture inventory from minecraft-assets 26.1.2...", file=sys.stderr)
    paths = fetch_block_paths()
    if not paths:
        print("No block textures found.", file=sys.stderr)
        return 1

    inventory, manifest = build_manifest(paths)
    inv_path = OUT_DIR / "minecraft_26.1.2_block_textures.json"
    man_path = OUT_DIR / "minecraft_26.1.2_block_texture_model_manifest.json"
    inv_path.write_text(json.dumps(inventory, indent=2) + "\n", encoding="utf-8")
    man_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {inv_path} ({inventory['counts']['files']} files)", file=sys.stderr)
    print(f"Wrote {man_path} ({manifest['counts']['rules']} rules)", file=sys.stderr)
    print(json.dumps(manifest["counts"], indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
