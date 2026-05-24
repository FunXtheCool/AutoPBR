#!/usr/bin/env python3
"""
Sync geometry IR shards listed in Model-ID.txt to meshes aligned with
CleanRoomEntityModelRuntime (rig-space cuboids; notes cite builder methods).

Reads: Desktop/Model-ID.txt (short class names) or repo tools/model-id-list.txt fallback.
Writes: docs/generated/geometry/26.1.2/<fqn>.json (preserves jarPath, classSha256Hex, bytecodeFloatProbe).
"""
from __future__ import annotations

import copy
import json
import math
from pathlib import Path

PI = math.pi

REPO = Path(__file__).resolve().parents[1]
GEOM = REPO / "docs" / "generated" / "geometry" / "26.1.2"
DESKTOP_LIST = Path(r"C:\Users\John_Phoenix\Desktop\Model-ID.txt")
REPO_LIST = REPO / "tools" / "model-id-list.txt"


def Z():
    return {"translation": [0, 0, 0], "rotationEulerRad": [0, 0, 0], "eulerOrder": "XYZ"}


def T(tx: float, ty: float, tz: float):
    return {"translation": [tx, ty, tz], "rotationEulerRad": [0, 0, 0], "eulerOrder": "XYZ"}


def TR(tx: float, ty: float, tz: float, rx: float, ry: float, rz: float):
    return {"translation": [tx, ty, tz], "rotationEulerRad": [rx, ry, rz], "eulerOrder": "XYZ"}


def cub(a, b, u, v, prov: str, mirror_u: bool = False):
    o = {
        "from": [a[0], a[1], a[2]],
        "to": [b[0], b[1], b[2]],
        "uvOrigin": [u, v],
        "textureKey": "#skin",
        "provenance": prov,
    }
    if mirror_u:
        o["mirrorU"] = True
    return o


def blaze_roots() -> list[dict]:
    """CleanRoomEntityModelRuntime.BuildBlaze (rodSpin=0)."""
    children = [
        {
            "id": "head",
            "pose": T(8, 14, 8),
            "cuboids": [cub((-4, -4, -4), (4, 4, 4), 0, 0, "BlazeModel head 8³ @ texOffs(0,0); root T(8,14,8)")],
            "children": [],
        }
    ]
    for i in range(12):
        base = -PI / 4 + (PI / 6) * i
        ox = math.cos(base) * 5.1
        oz = math.sin(base) * 5.1
        children.append(
            {
                "id": f"part{i}",
                "pose": T(8 + ox, 25, 8 + oz),
                "cuboids": [cub((-1, 0, -1), (1, 8, 1), 0, 16, f"BlazeModel rod {i} 2×8×2 @ texOffs(0,16); rodSpin=0 baseline")],
                "children": [],
            }
        )
    return [{"id": "root", "pose": Z(), "cuboids": [], "children": children}]


def humanoid_biped_roots(prov: str) -> list[dict]:
    """BuildHumanoid / BuildZombieHumanoid rig corners (64×64, before LER basis)."""
    c = [
        cub((4, 12, 6), (12, 24, 10), 16, 16, f"{prov} torso"),
        cub((4, 24, 4), (12, 32, 12), 0, 0, f"{prov} head"),
        cub((0, 12, 6), (4, 24, 10), 40, 16, f"{prov} left arm (pivot baked as identity for IR)"),
        cub((12, 12, 6), (16, 24, 10), 40, 16, f"{prov} right arm"),
        cub((4, 0, 6), (8, 12, 10), 0, 16, f"{prov} left leg"),
        cub((8, 0, 6), (12, 12, 10), 0, 16, f"{prov} right leg"),
    ]
    return [{"id": "root", "pose": Z(), "cuboids": c, "children": []}]


def equine_saddle_roots() -> list[dict]:
    """BuildEquineSaddle — cuboids in model space (ApplyEquineLivingEntityRendererPreviewBasis noted in extractionNotes)."""
    d_s, d_h, d_m = 0.5, 0.22, 0.2
    thin = 0.08
    cubes = [
        cub((-5 - d_s, -8 - d_s, -9 - d_s), (5 + d_s, 1 + d_s, 0 + d_s), 26, 0, "EquineSaddleModel blanket @ texOffs(26,0) 10×9×9"),
        cub((-3 - d_h, -11 - d_h, -1.9 - d_h), (3 + d_h, -6 + d_h, 4.1 + d_h), 1, 1, "EquineSaddleModel head saddle @ texOffs(1,1)"),
        cub((-2 - d_m, -11 - d_m, -4 - d_m), (2 + d_m, -6 + d_m, -2 + d_m), 19, 0, "EquineSaddleModel mouth wrap @ texOffs(19,0)"),
        cub((2, -9, -6), (3, -7, -4), 29, 5, "EquineSaddleModel cheek R @ texOffs(29,5)"),
        cub((-3, -9, -6), (-2, -7, -4), 29, 5, "EquineSaddleModel cheek L @ texOffs(29,5)"),
    ]
    # Rein lines: thin box in local of rotated pose — approximate as part-local cuboid + pose
    # Split reins into parts with poses
    left_rein = {
        "id": "rein_left",
        "pose": TR(3.1, -6, -8, -PI / 6, 0, 0),
        "cuboids": [cub((-thin, 0, 0), (thin, 3, 16), 32, 2, "EquineSaddleModel rein @ texOffs(32,2)")],
        "children": [],
    }
    right_rein = {
        "id": "rein_right",
        "pose": TR(-3.1, -6, -8, -PI / 6, 0, 0),
        "cuboids": [cub((-thin, 0, 0), (thin, 3, 16), 32, 2, "EquineSaddleModel rein mirror @ texOffs(32,2)")],
        "children": [],
    }
    root_cubes = cubes
    return [
        {
            "id": "root",
            "pose": Z(),
            "cuboids": root_cubes,
            "children": [left_rein, right_rein],
        }
    ]


def sheep_fur_roots() -> list[dict]:
    """Wool shell from BuildSheep (fleece layer only for SheepFurModel shard)."""
    body_pose = TR(0, 5, 2, PI / 2, 0, 0)
    return [
        {
            "id": "root",
            "pose": Z(),
            "cuboids": [],
            "children": [
                {
                    "id": "wool",
                    "pose": body_pose,
                    "cuboids": [
                        cub((-4, -10, -7), (4, 6, -1), 28, 8, "SheepFurModel wool 8×16×6 @ texOffs(28,8); body PartPose.offsetAndRotation(0,5,2,π/2,0,0)")
                    ],
                    "children": [],
                }
            ],
        }
    ]


def skipped_roots(kind: str) -> list[dict]:
    return [
        {
            "id": "_no_mesh_layer",
            "pose": Z(),
            "cuboids": [],
            "children": [],
        }
    ]


def player_cape_roots() -> list[dict]:
    """PlayerCapeModel.createCapeLayer (26.x): body/cape only after PlayerModel.createMesh + clearRecursively."""
    cape = {
        "id": "cape",
        "pose": TR(0, 0, 2, 0, PI, 0),
        "cuboids": [
            cub(
                (-5, 0, -1),
                (5, 16, 0),
                0,
                0,
                "PlayerCapeModel.createCapeLayer: texOffs(0,0) addBox(-5,0,-1,10,16,1); PartPose.offsetAndRotation(0,0,2,0,pi,0) on cape",
            )
        ],
        "children": [],
    }
    body = {"id": "body", "pose": Z(), "cuboids": [], "children": [cape]}
    return [{"id": "root", "pose": Z(), "cuboids": [], "children": [body]}]


def player_ears_roots() -> list[dict]:
    """PlayerEarsModel.createEarsLayer: head/left_ear + right_ear (shared CubeListBuilder)."""
    ear = cub(
        (-3, -6, -1),
        (3, 0, 0),
        24,
        0,
        "PlayerEarsModel.createEarsLayer: texOffs(24,0) addBox(-3,-6,-1,6,6,1) + CubeDeformation(1) not expanded in IR",
    )
    left = {"id": "left_ear", "pose": T(-6, -6, 0), "cuboids": [ear], "children": []}
    right = {"id": "right_ear", "pose": T(6, -6, 0), "cuboids": [ear], "children": []}
    head = {"id": "head", "pose": Z(), "cuboids": [], "children": [left, right]}
    return [{"id": "root", "pose": Z(), "cuboids": [], "children": [head]}]


def spin_attack_effect_roots() -> list[dict]:
    """SpinAttackEffectModel.createLayer: root children box0/box1 (PartPose.withScale baked into corners)."""
    boxes = []
    for i in range(2):
        f3 = -3.2 + 9.6 * (i + 1)
        y0 = -16.0 + f3
        s = 0.75 * (i + 1)
        x0, z0 = round(-8.0 * s, 3), round(-8.0 * s, 3)
        x1 = round(8.0 * s, 3)
        y0s = round(y0 * s, 3)
        y1 = round((y0 + 32.0) * s, 3)
        z1 = round(8.0 * s, 3)
        boxes.append(
            {
                "id": f"box{i}",
                "pose": Z(),
                "cuboids": [
                    cub(
                        (x0, y0s, z0),
                        (x1, y1, z1),
                        0,
                        0,
                        f"SpinAttackEffectModel.createLayer box{i}: texOffs(0,0) addBox(-8,{y0:.1f},-8,16,32,16); "
                        f"PartPose.withScale({s}) baked into AABB (javap 26.1.2)",
                    )
                ],
                "children": [],
            }
        )
    return [{"id": "root", "pose": Z(), "cuboids": [], "children": boxes}]


def load_json(p: Path) -> dict:
    return json.loads(p.read_text(encoding="utf-8"))


def save_shard(
    path: Path,
    base: dict,
    roots: list,
    status: str,
    notes: list[str],
    *,
    factory_method: str = "createBodyLayer",
) -> None:
    out = dict(base)
    out["roots"] = roots
    out["extractionStatus"] = status
    out["extractionNotes"] = notes
    out["factoryMethod"] = factory_method
    path.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def copy_roots_from(rel: str) -> list[dict]:
    p = GEOM / rel
    data = load_json(p)
    return copy.deepcopy(data["roots"])


# short name (from Model-ID.txt) -> relative path under GEOM
SHORT_TO_REL: dict[str, str] = {
    "AdultAndBabyModelPair": "net.minecraft.client.model.AdultAndBabyModelPair.json",
    "BabyDonkeyModel": "net.minecraft.client.model.animal.equine.BabyDonkeyModel.json",
    "EquineSaddleModel": "net.minecraft.client.model.animal.equine.EquineSaddleModel.json",
    "AbstractFelineModel": "net.minecraft.client.model.animal.feline.AbstractFelineModel.json",
    "AdultOcelotModel": "net.minecraft.client.model.animal.feline.AdultOcelotModel.json",
    "BabyCatModel": "net.minecraft.client.model.animal.feline.BabyCatModel.json",
    "BabyFelineModel": "net.minecraft.client.model.animal.feline.BabyFelineModel.json",
    "BabyOcelotModel": "net.minecraft.client.model.animal.feline.BabyOcelotModel.json",
    "ParrotModel$Pose": "net.minecraft.client.model.animal.parrot.ParrotModel$Pose.json",
    "SheepFurModel": "net.minecraft.client.model.animal.sheep.SheepFurModel.json",
    "ArmedModel": "net.minecraft.client.model.ArmedModel.json",
    "BabyModelTransform": "net.minecraft.client.model.BabyModelTransform.json",
    "BlazeModel": "net.minecraft.client.model.BlazeModel.json",
    "ChickenModel": "net.minecraft.client.model.animal.chicken.ChickenModel.json",
    "effects.SpinAttackEffectModel": "net.minecraft.client.model.effects.SpinAttackEffectModel.json",
    "EntityModel": "net.minecraft.client.model.EntityModel.json",
    "HeadedModel": "net.minecraft.client.model.HeadedModel.json",
    "HumanoidModel$1": "net.minecraft.client.model.HumanoidModel$1.json",
    "HumanoidModel$ArmPose$1": "net.minecraft.client.model.HumanoidModel$ArmPose$1.json",
    "HumanoidModel$ArmPose": "net.minecraft.client.model.HumanoidModel$ArmPose.json",
    "Model$Simple": "net.minecraft.client.model.Model$Simple.json",
    "Model": "net.minecraft.client.model.Model.json",
    "GuardianParticleModel": "net.minecraft.client.model.monster.guardian.GuardianParticleModel.json",
    "AbstractPiglinModel": "net.minecraft.client.model.monster.piglin.AbstractPiglinModel.json",
    "AdultZombifiedPiglinModel": "net.minecraft.client.model.monster.piglin.AdultZombifiedPiglinModel.json",
    "BabyZombifiedPiglinModel": "net.minecraft.client.model.monster.piglin.BabyZombifiedPiglinModel.json",
    "ZombifiedPiglinModel": "net.minecraft.client.model.monster.piglin.ZombifiedPiglinModel.json",
    "AbstractZombieModel": "net.minecraft.client.model.monster.zombie.AbstractZombieModel.json",
    "BabyDrownedModel": "net.minecraft.client.model.monster.zombie.BabyDrownedModel.json",
    "GiantZombieModel": "net.minecraft.client.model.monster.zombie.GiantZombieModel.json",
    "ArmorStandArmorModel": "net.minecraft.client.model.object.armorstand.ArmorStandArmorModel.json",
    "BellModel$1": "net.minecraft.client.model.object.bell.BellModel$1.json",
    "BellModel$State": "net.minecraft.client.model.object.bell.BellModel$State.json",
    "AbstractBoatModel": "net.minecraft.client.model.object.boat.AbstractBoatModel.json",
    "RaftModel": "net.minecraft.client.model.object.boat.RaftModel.json",
    "BookModel$State": "net.minecraft.client.model.object.book.BookModel$State.json",
    "DragonHeadModel": "net.minecraft.client.model.object.skull.DragonHeadModel.json",
    "PiglinHeadModel": "net.minecraft.client.model.object.skull.PiglinHeadModel.json",
    "SkullModelBase$State": "net.minecraft.client.model.object.skull.SkullModelBase$State.json",
    "SkullModelBase": "net.minecraft.client.model.object.skull.SkullModelBase.json",
    "CopperGolemStatueModel": "net.minecraft.client.model.object.statue.CopperGolemStatueModel.json",
    "PlayerCapeModel": "net.minecraft.client.model.player.PlayerCapeModel.json",
    "PlayerEarsModel": "net.minecraft.client.model.player.PlayerEarsModel.json",
    "VillagerLikeModel": "net.minecraft.client.model.VillagerLikeModel.json",
}


def read_model_id_lines() -> list[str]:
    src = DESKTOP_LIST if DESKTOP_LIST.is_file() else REPO_LIST
    if not src.is_file():
        raise FileNotFoundError(f"Model-ID list not found: {DESKTOP_LIST} or {REPO_LIST}")
    lines = []
    for line in src.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        lines.append(line)
    return lines


def main() -> None:
    lines = read_model_id_lines()
    updated = 0

    # Also refresh packaged Blaze shard (entity uses this FQN).
    extra_blaze = GEOM / "net.minecraft.client.model.monster.blaze.BlazeModel.json"

    for short in lines:
        rel = SHORT_TO_REL.get(short)
        if not rel:
            print(f"SKIP unknown short id: {short}")
            continue
        path = GEOM / rel
        if not path.is_file():
            print(f"MISSING {path}")
            continue
        base = load_json(path)
        fqn = base.get("officialJvmName", rel)

        notes_header = [
            "Synced from CleanRoomEntityModelRuntime for Model-ID.txt batch (tools/sync_model_id_geometry_cleanroom.py)."
        ]

        if short == "PlayerCapeModel":
            roots = player_cape_roots()
            notes = notes_header + [
                "Hand-authored from client.jar PlayerCapeModel.createCapeLayer (not createBodyLayer); "
                "CleanRoom preview still uses PlayerModel cape child where applicable."
            ]
            save_shard(path, base, roots, "ok", notes, factory_method="createCapeLayer")
            updated += 1
            continue

        if short == "PlayerEarsModel":
            roots = player_ears_roots()
            notes = notes_header + [
                "Hand-authored from client.jar PlayerEarsModel.createEarsLayer; "
                "CubeDeformation(1) on ear box not expanded into corner deltas."
            ]
            save_shard(path, base, roots, "ok", notes, factory_method="createEarsLayer")
            updated += 1
            continue

        if short == "effects.SpinAttackEffectModel":
            roots = spin_attack_effect_roots()
            notes = notes_header + [
                "Hand-authored from client.jar SpinAttackEffectModel.createLayer (children box0/box1; "
                "PartPose.withScale baked into cuboid corners for IR)."
            ]
            save_shard(path, base, roots, "partial", notes, factory_method="createLayer")
            updated += 1
            continue

        if short in (
            "AdultAndBabyModelPair",
            "ArmedModel",
            "BabyModelTransform",
            "EntityModel",
            "HeadedModel",
            "HumanoidModel$1",
            "HumanoidModel$ArmPose$1",
            "HumanoidModel$ArmPose",
            "Model$Simple",
            "Model",
            "ParrotModel$Pose",
            "BellModel$1",
            "BellModel$State",
            "BookModel$State",
            "SkullModelBase$State",
            "SkullModelBase",
            "GuardianParticleModel",
        ):
            roots = skipped_roots(short)
            notes = notes_header + [
                f"{fqn}: no mesh-definition factory on this class (e.g. createBodyLayer) and no CleanRoomEntityModelRuntime "
                "builder for this FQN; shard is metadata-only."
            ]
            save_shard(path, base, roots, "skipped", notes)
            updated += 1
            continue

        if short == "BlazeModel":
            roots = blaze_roots()
            notes = notes_header + [
                "Mirrors CleanRoomEntityModelRuntime.BuildBlaze (rodSpin=0); legacy flat FQN shard aligned to packaged blaze mesh."
            ]
            save_shard(path, base, roots, "ok", notes)
            updated += 1
            continue

        if short == "ChickenModel":
            notes = notes_header + [
                "Canonical chicken geometry IR (javap lift via AdultChickenModel mesh host). "
                "Legacy flat FQN net.minecraft.client.model.ChickenModel shard removed."
            ]
            save_shard(path, base, base["roots"], base.get("extractionStatus", "ok"), notes)
            updated += 1
            continue

        if short == "VillagerLikeModel":
            roots = copy_roots_from("net.minecraft.client.model.npc.VillagerModel.json")
            notes = notes_header + [
                "Roots mirrored from VillagerModel.json (CleanRoomEntityModelRuntime.BuildVillager baseline family)."
            ]
            save_shard(path, base, roots, "ok", notes)
            updated += 1
            continue

        if short in ("AbstractFelineModel", "AdultOcelotModel", "BabyCatModel", "BabyFelineModel", "BabyOcelotModel"):
            roots = copy_roots_from("net.minecraft.client.model.animal.feline.AdultCatModel.json")
            notes = notes_header + [
                "Feline family mesh aligned with CleanRoomEntityModelRuntime.BuildCat (same FelineModel.createBodyLayer topology)."
            ]
            save_shard(path, base, roots, "ok", notes)
            updated += 1
            continue

        if short == "BabyDonkeyModel":
            roots = copy_roots_from("net.minecraft.client.model.animal.equine.BabyHorseModel.json")
            notes = notes_header + [
                "Baby donkey mesh IR copied from BabyHorseModel.json; CleanRoom BuildBabyEquineHorseLike differs by donkey ears/chest flags — see runtime."
            ]
            save_shard(path, base, roots, "partial", notes)
            updated += 1
            continue

        if short == "EquineSaddleModel":
            roots = equine_saddle_roots()
            notes = notes_header + [
                "Hand-authored from CleanRoomEntityModelRuntime.BuildEquineSaddle; ApplyEquineLivingEntityRendererPreviewBasis may adjust world basis in preview."
            ]
            save_shard(path, base, roots, "partial", notes)
            updated += 1
            continue

        if short == "SheepFurModel":
            roots = sheep_fur_roots()
            notes = notes_header + [
                "Wool-only slice from CleanRoomEntityModelRuntime.BuildSheep (SheepModel fur layer / body shell)."
            ]
            save_shard(path, base, roots, "ok", notes)
            updated += 1
            continue

        if short in ("AbstractPiglinModel", "AdultZombifiedPiglinModel", "ZombifiedPiglinModel"):
            roots = copy_roots_from("net.minecraft.client.model.monster.piglin.PiglinModel.json")
            notes = notes_header + [
                "Piglin-family IR copied from PiglinModel.json; CleanRoomEntityModelRuntime.BuildZombifiedPiglin delegates to BuildPiglin."
            ]
            save_shard(path, base, roots, "partial", notes)
            updated += 1
            continue

        if short == "BabyZombifiedPiglinModel":
            roots = copy_roots_from("net.minecraft.client.model.monster.piglin.BabyPiglinModel.json")
            notes = notes_header + [
                "BabyZombifiedPiglinModel.createBodyLayer() forwards to BabyPiglinModel.createBodyLayer() per javap on 26.1.2 client.jar; IR duplicated from BabyPiglinModel.json."
            ]
            save_shard(path, base, roots, "ok", notes)
            updated += 1
            continue

        if short == "AbstractZombieModel":
            roots = copy_roots_from("net.minecraft.client.model.monster.zombie.ZombieModel.json")
            notes = notes_header + [
                "Abstract zombie mesh baseline copied from ZombieModel.json (CleanRoomEntityModelRuntime.BuildZombieHumanoid family)."
            ]
            save_shard(path, base, roots, "partial", notes)
            updated += 1
            continue

        if short == "BabyDrownedModel":
            roots = copy_roots_from("net.minecraft.client.model.monster.zombie.BabyZombieModel.json")
            notes = notes_header + [
                "BabyDrownedModel.createBodyLayer(CubeDeformation) forwards to BabyZombieModel.createBodyLayer(CubeDeformation) per javap on 26.1.2 client.jar; IR duplicated from BabyZombieModel.json."
            ]
            save_shard(path, base, roots, "ok", notes)
            updated += 1
            continue

        if short == "GiantZombieModel":
            roots = humanoid_biped_roots("Giant zombie (BuildHumanoid)")
            notes = notes_header + [
                "CleanRoomEntityModelRuntime routes giant zombies through BuildHumanoid (canonical 64×64 biped boxes)."
            ]
            save_shard(path, base, roots, "ok", notes)
            updated += 1
            continue

        if short == "ArmorStandArmorModel":
            roots = copy_roots_from("net.minecraft.client.model.object.armorstand.ArmorStandModel.json")
            notes = notes_header + [
                "Armor stand equipment layer IR mirrored from ArmorStandModel.json; verify separate armor-only factory in client.jar if needed."
            ]
            save_shard(path, base, roots, "partial", notes)
            updated += 1
            continue

        if short in ("AbstractBoatModel", "RaftModel"):
            roots = copy_roots_from("net.minecraft.client.model.object.boat.BoatModel.json")
            notes = notes_header + [
                "Boat-family placeholder hull copied from BoatModel.json; CleanRoom BuildBoat applies per-plank euler+pivot — refine in a later batch."
            ]
            save_shard(path, base, roots, "partial", notes)
            updated += 1
            continue

        if short == "DragonHeadModel":
            roots = copy_roots_from("net.minecraft.client.model.object.skull.SkullModel.json")
            notes = notes_header + [
                "Dragon head block entity uses skull-style stack in preview; IR mirrored from SkullModel.json pending dedicated DragonHeadModel lift."
            ]
            save_shard(path, base, roots, "partial", notes)
            updated += 1
            continue

        if short == "PiglinHeadModel":
            roots = copy_roots_from("net.minecraft.client.model.object.skull.SkullModel.json")
            notes = notes_header + [
                "Piglin head IR temporarily mirrored from SkullModel.json; CleanRoomEntityModelRuntime.BuildPiglinSkull uses piglin-specific UV stack."
            ]
            save_shard(path, base, roots, "partial", notes)
            updated += 1
            continue

        if short == "CopperGolemStatueModel":
            roots = copy_roots_from("net.minecraft.client.model.animal.golem.CopperGolemModel.json")
            notes = notes_header + [
                "Statue layer copied from animal.golem.CopperGolemModel.json (CleanRoomEntityModelRuntime.BuildCopperGolem mesh)."
            ]
            save_shard(path, base, roots, "partial", notes)
            updated += 1
            continue

    # Packaged blaze entity shard — replace partial head-only lift with full blaze rods.
    if extra_blaze.is_file():
        b = load_json(extra_blaze)
        save_shard(
            extra_blaze,
            b,
            blaze_roots(),
            "ok",
            [
                "Synced from CleanRoomEntityModelRuntime.BuildBlaze (rodSpin=0) via tools/sync_model_id_geometry_cleanroom.py.",
                "Replaces incomplete javap lift missing rod PartPose string anchors.",
            ],
        )
        updated += 1

    print(f"Updated {updated} shard(s).")


if __name__ == "__main__":
    main()
