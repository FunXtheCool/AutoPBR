#!/usr/bin/env python3
"""
Maintain `minecraft_26.1.2_entity_texture_model_manifest.json` javap reference columns:

- `deobf_model_class` — canonical **post–Java 26.1-style package relocation** class names that appear under
  `net/minecraft/client/model/**` *plus* known renderer/layer classes whose meshes ship outside flat Model sources.

- `deobf_model_class_pre_restructure` — Mojmap-era **`net.minecraft.client.model.<Simple>`** (and legacy Renderer placeholders)
  for tooling pinned to **pre-restructure** client jars.

Inputs:
  • Never modifies any Minecraft jar — parity jars remain read-only.
  • Uses the checked-in index emitted by `build_minecraft_client_model_class_index.py`
    (`minecraft_26.1.2_client_model_classes.txt`; companion `minecraft_26.1.2_client_animation_definition_classes.txt`
    lists `AnimationDefinition` holder classes only), **not** a live `jar tf` inside generators.

Usage:
  python tools/sync_entity_manifest_deobf_from_jar.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path


def build_simple_model_map(model_paths_slash: list[str]) -> dict[str, str]:
    prefix = "net/minecraft/client/model/"
    simple: dict[str, str] = {}
    for slash in model_paths_slash:
        line = slash.strip()
        if not line.startswith(prefix) or not line.endswith(".class"):
            continue
        if "/geom/" in line or "/builders/" in line:
            continue
        fqcn = line[:-6].replace("/", ".")
        name = fqcn.rsplit(".", 1)[-1]
        if name == "package-info":
            continue
        if name in simple:
            raise RuntimeError(f"Duplicate simple model name {name}:\n  {simple[name]}\n  {fqcn}")
        simple[name] = fqcn
    return simple


# Vanilla renamed Model classes (pre manifest tail -> post jar simple name).
ALIASES = {
    "LavaSlimeModel": "MagmaCubeModel",
    "TropicalFishModelA": "TropicalFishSmallModel",
    "TropicalFishModelB": "TropicalFishLargeModel",
    "AxolotlModel": "AdultAxolotlModel",
    "CatModel": "AdultCatModel",
}

# Geometry authored on Renderer classes post-restructure; javap targets Renderer.* directly.
RENDERER_CLASSES = {
    "BeaconBeamModel": "net.minecraft.client.renderer.blockentity.BeaconRenderer",
    "BedModel": "net.minecraft.client.renderer.blockentity.BedRenderer",
    "ConduitModel": "net.minecraft.client.renderer.blockentity.ConduitRenderer",
    "DragonFireballModel": "net.minecraft.client.renderer.entity.DragonFireballRenderer",
    "EndGatewayBeamModel": "net.minecraft.client.renderer.blockentity.TheEndGatewayRenderer",
    "EndPortalModel": "net.minecraft.client.renderer.blockentity.TheEndPortalRenderer",
    "ExperienceOrbModel": "net.minecraft.client.renderer.entity.ExperienceOrbRenderer",
    "FishingHookModel": "net.minecraft.client.renderer.entity.FishingHookRenderer",
    "HangingSignModel": "net.minecraft.client.renderer.blockentity.HangingSignRenderer",
    "SignModel": "net.minecraft.client.renderer.blockentity.StandingSignRenderer",
    "EquipmentModelRenderer": "net.minecraft.client.renderer.entity.layers.EquipmentLayerRenderer",
}

RENDERER_POST_TO_LEGACY = {
    "net.minecraft.client.renderer.blockentity.BeaconRenderer": "net.minecraft.client.model.BeaconBeamModel",
    "net.minecraft.client.renderer.blockentity.BedRenderer": "net.minecraft.client.model.BedModel",
    "net.minecraft.client.renderer.blockentity.ConduitRenderer": "net.minecraft.client.model.ConduitModel",
    "net.minecraft.client.renderer.entity.DragonFireballRenderer": "net.minecraft.client.model.DragonFireballModel",
    "net.minecraft.client.renderer.blockentity.TheEndGatewayRenderer": "net.minecraft.client.model.EndGatewayBeamModel",
    "net.minecraft.client.renderer.blockentity.TheEndPortalRenderer": "net.minecraft.client.model.EndPortalModel",
    "net.minecraft.client.renderer.entity.ExperienceOrbRenderer": "net.minecraft.client.model.ExperienceOrbModel",
    "net.minecraft.client.renderer.entity.FishingHookRenderer": "net.minecraft.client.model.FishingHookModel",
    "net.minecraft.client.renderer.blockentity.HangingSignRenderer": "net.minecraft.client.model.HangingSignModel",
    "net.minecraft.client.renderer.blockentity.StandingSignRenderer": "net.minecraft.client.model.SignModel",
    "net.minecraft.client.renderer.entity.layers.EquipmentLayerRenderer": "net.minecraft.client.model.EquipmentModelRenderer",
    # Older manifests incorrectly cited DecoratedPotModel; jars expose Renderer meshes instead.
    "net.minecraft.client.renderer.blockentity.DecoratedPotRenderer": "net.minecraft.client.model.DecoratedPotModel",
    # Beacon parity historically javaps GuardianRenderer for guardian beam attachment meshes.
    "net.minecraft.client.renderer.entity.GuardianRenderer": "net.minecraft.client.renderer.entity.GuardianRenderer",
}

OTHER_CLASSES = {
    "HumanoidArmorModel": "net.minecraft.client.model.HumanoidModel",
}


LEGACY_FLAT = "net.minecraft.client.model"

ALLOWED_RENDERER_POSTS = frozenset(RENDERER_POST_TO_LEGACY.keys())

# Post-restructure simple name -> legacy flat-class simple tail under LEGACY_FLAT.
POST_SIMPLE_TO_LEGACY_SIMPLE = {
    "MagmaCubeModel": "LavaSlimeModel",
    "TropicalFishSmallModel": "TropicalFishModelA",
    "TropicalFishLargeModel": "TropicalFishModelB",
    "AdultAxolotlModel": "AxolotlModel",
    "AdultCatModel": "CatModel",
}


def is_known_non_model_renderer(post_fqcn: str) -> bool:
    return post_fqcn in RENDERER_POST_TO_LEGACY


def post_exists(post_fqcn: str, model_paths_slash: set[str]) -> bool:
    if is_known_non_model_renderer(post_fqcn):
        return True
    path = post_fqcn.replace(".", "/") + ".class"
    return path in model_paths_slash


def resolve_legacy_to_post(old: str, simple_map: dict[str, str]) -> str:
    """Normalize manifest stubs emitted by generators to canonical post-restructure FQCN."""
    if not old:
        return old

    if old in ALLOWED_RENDERER_POSTS:
        return old

    tail = old.rsplit(".", 1)[-1]

    if tail in OTHER_CLASSES:
        return OTHER_CLASSES[tail]

    if tail in RENDERER_CLASSES:
        return RENDERER_CLASSES[tail]

    simple_key = ALIASES.get(tail, tail)
    if simple_key not in simple_map:
        raise RuntimeError(f"No jar mapping for legacy tail={tail!r} (tried {simple_key!r})")

    return simple_map[simple_key]


def legacy_from_post(post_fqcn: str, builder_method: str) -> str:
    """javap hint against older jars using flat Model naming."""
    if not post_fqcn:
        return ""

    if post_fqcn in RENDERER_POST_TO_LEGACY:
        return RENDERER_POST_TO_LEGACY[post_fqcn]

    simple = post_fqcn.rsplit(".", 1)[-1]

    # Equipment diffuse atlases baked against armor-layer Models historically cited HumanoidArmorModel.
    if (
        post_fqcn == "net.minecraft.client.model.HumanoidModel"
        and builder_method in {"EquipmentHumanoidLeggings", "EquipmentHumanoid", "EquipmentHumanoidBaby"}
    ):
        return f"{LEGACY_FLAT}.HumanoidArmorModel"

    legacy_simple = POST_SIMPLE_TO_LEGACY_SIMPLE.get(simple, simple)
    return f"{LEGACY_FLAT}.{legacy_simple}"


def main() -> int:
    repo = Path(__file__).resolve().parent.parent
    native_dir = repo / "src/AutoPBR.Core/Data/minecraft-native"
    model_idx = native_dir / "minecraft_26.1.2_client_model_classes.txt"
    manifest_path = native_dir / "minecraft_26.1.2_entity_texture_model_manifest.json"

    if not model_idx.is_file():
        print(f"Missing model index (generate via ZipFile read, jars stay read-only):\n  python tools/build_minecraft_client_model_class_index.py <client.jar>\n->{model_idx}", file=sys.stderr)
        return 2

    slash_paths = model_idx.read_text(encoding="utf-8").splitlines()
    path_slash_set = set(slash_paths)
    simple_map = build_simple_model_map(slash_paths)

    doc = json.loads(manifest_path.read_text(encoding="utf-8"))
    upgraded = 0
    normalized_rules: list[dict[str, object]] = []
    for rule in doc.get("rules", []):
        raw = (rule.get("deobf_model_class") or "").strip()
        post = resolve_legacy_to_post(raw, simple_map) if raw else ""
        if post != raw:
            upgraded += 1

        if not post_exists(post, path_slash_set):
            raise RuntimeError(f"Resolved class not in index nor renderer allow-list: {post!r}")

        builder = (rule.get("builder_method") or "").strip()
        legacy = legacy_from_post(post, builder)
        normalized_rules.append(
            {
                "path_prefix": rule["path_prefix"],
                "builder_method": rule["builder_method"],
                "deobf_model_class": post,
                "deobf_model_class_pre_restructure": legacy,
                "notes": rule.get("notes") or "",
            }
        )

    doc["rules"] = normalized_rules

    manifest_path.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Manifest synced ({manifest_path.name}): normalized {upgraded} rows; filled pre/post javap hints.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
