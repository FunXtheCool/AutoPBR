#!/usr/bin/env python3
"""
Hydrate 26.1.2 geometry IR shards that lacked createBodyLayer part-trees, using
vanilla-aligned cuboids from CleanRoomEntityModelRuntime (and static pose baselines).

Also strips stale extractionNotes on shards that already have cuboids.
"""
from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Any

PI = math.pi

ROOT = Path(__file__).resolve().parents[1]
GEOM_DIR = ROOT / "docs" / "generated" / "geometry" / "26.1.2"

STALE_SUBSTRINGS = (
    "No createBodyLayer part-tree IR in repo yet",
    "javap float probe failed or missing createBodyLayer Code block.",
)


def Z() -> dict[str, Any]:
    return {
        "translation": [0.0, 0.0, 0.0],
        "rotationEulerRad": [0.0, 0.0, 0.0],
        "eulerOrder": "XYZ",
    }


def T(tx: float, ty: float, tz: float) -> dict[str, Any]:
    return {
        "translation": [tx, ty, tz],
        "rotationEulerRad": [0.0, 0.0, 0.0],
        "eulerOrder": "XYZ",
    }


def TR(tx: float, ty: float, tz: float, rx: float, ry: float, rz: float) -> dict[str, Any]:
    return {
        "translation": [tx, ty, tz],
        "rotationEulerRad": [rx, ry, rz],
        "eulerOrder": "XYZ",
    }


def cub(
    a: tuple[float, float, float],
    b: tuple[float, float, float],
    u: int,
    v: int,
    prov: str,
    mirror_u: bool = False,
) -> dict[str, Any]:
    o: dict[str, Any] = {
        "from": [a[0], a[1], a[2]],
        "to": [b[0], b[1], b[2]],
        "uvOrigin": [u, v],
        "textureKey": "#skin",
        "provenance": prov,
    }
    if mirror_u:
        o["mirrorU"] = True
    return o


def count_cuboids(parts: list[dict[str, Any]]) -> int:
    n = 0
    for p in parts:
        n += len(p.get("cuboids") or [])
        n += count_cuboids(p.get("children") or [])
    return n


def clean_notes_shard(data: dict[str, Any]) -> bool:
    """Returns True if file was modified."""
    n = count_cuboids(data.get("roots") or [])
    if n == 0:
        return False
    notes = list(data.get("extractionNotes") or [])
    filtered = [x for x in notes if not any(s in x for s in STALE_SUBSTRINGS)]
    if filtered == notes:
        return False
    data["extractionNotes"] = filtered
    if data.get("extractionStatus") == "partial" and not filtered:
        data["extractionStatus"] = "ok"
    return True


def merge_shard(base: dict[str, Any], roots: list[dict[str, Any]], status: str, notes: list[str]) -> dict[str, Any]:
    out = dict(base)
    out["roots"] = roots
    out["extractionStatus"] = status
    out["extractionNotes"] = notes
    out["factoryMethod"] = "createBodyLayer"
    return out


def build_trident() -> tuple[list[dict[str, Any]], str, list[str]]:
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "pole",
                "pose": Z(),
                "cuboids": [cub((-0.5, 2.0, -0.5), (0.5, 27.0, 0.5), 0, 6, "TridentModel pole 1×25×1 @ texOffs(0,6)")],
                "children": [],
            },
            {
                "id": "base",
                "pose": Z(),
                "cuboids": [cub((-1.5, 0.0, -0.5), (1.5, 2.0, 0.5), 4, 0, "TridentModel base 3×2×1 @ texOffs(4,0)")],
                "children": [],
            },
            {
                "id": "left_spike",
                "pose": Z(),
                "cuboids": [cub((-2.5, -3.0, -0.5), (-1.5, 1.0, 0.5), 4, 3, "TridentModel left spike @ texOffs(4,3)")],
                "children": [],
            },
            {
                "id": "middle_spike",
                "pose": Z(),
                "cuboids": [cub((-0.5, -4.0, -0.5), (0.5, 0.0, 0.5), 0, 0, "TridentModel middle spike @ texOffs(0,0)")],
                "children": [],
            },
            {
                "id": "right_spike",
                "pose": Z(),
                "cuboids": [cub((1.5, -3.0, -0.5), (2.5, 1.0, 0.5), 4, 3, "TridentModel right spike @ texOffs(4,3)")],
                "children": [],
            },
        ],
    }
    return (
        [root],
        "ok",
        [
            "Hand-authored from vanilla TridentModel.createBodyLayer / CleanRoomEntityModelRuntime.BuildTrident (32×32)."
        ],
    )


def build_shield() -> tuple[list[dict[str, Any]], str, list[str]]:
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "plate",
                "pose": Z(),
                "cuboids": [cub((-6.0, -11.0, -2.0), (6.0, 11.0, -1.0), 0, 0, "ShieldModel plate @ texOffs(0,0)")],
                "children": [],
            },
            {
                "id": "handle",
                "pose": Z(),
                "cuboids": [cub((-1.0, -3.0, -1.0), (1.0, 3.0, 5.0), 26, 0, "ShieldModel handle @ texOffs(26,0)")],
                "children": [],
            },
        ],
    }
    return (
        [root],
        "ok",
        ["Hand-authored from vanilla ShieldModel / CleanRoomEntityModelRuntime.BuildShield (64×64)."],
    )


def build_chest() -> tuple[list[dict[str, Any]], str, list[str]]:
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "bottom",
                "pose": Z(),
                "cuboids": [cub((1.0, 0.0, 1.0), (15.0, 10.0, 15.0), 0, 0, "ChestModel bottom @ texOffs(0,0)")],
                "children": [],
            },
            {
                "id": "lid",
                "pose": Z(),
                "cuboids": [cub((1.0, 10.0, 1.0), (15.0, 14.0, 15.0), 0, 19, "ChestModel lid @ texOffs(0,19)")],
                "children": [],
            },
            {
                "id": "lock",
                "pose": Z(),
                "cuboids": [cub((7.0, 8.0, 15.0), (9.0, 12.0, 16.0), 0, 0, "ChestModel lock @ texOffs(0,0) (nub)")],
                "children": [],
            },
        ],
    }
    return (
        [root],
        "ok",
        ["Hand-authored from vanilla ChestModel / CleanRoomEntityModelRuntime.BuildChestEntity (64×64)."],
    )


def build_bee_stinger() -> tuple[list[dict[str, Any]], str, list[str]]:
    thin = 0.06
    blade = cub((-1.0, -0.5, -thin), (2.0, 1.0, thin), 0, 0, "BeeStingerModel blade 3×1×1 @ texOffs(0,0)", False)
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "blade_1",
                "pose": TR(0.0, 0.0, 0.0, PI / 4.0, 0.0, 0.0),
                "cuboids": [blade],
                "children": [],
            },
            {
                "id": "blade_2",
                "pose": TR(0.0, 0.0, 0.0, 3.0 * PI / 4.0, 0.0, 0.0),
                "cuboids": [
                    cub(
                        (-1.0, -0.5, -thin),
                        (2.0, 1.0, thin),
                        0,
                        0,
                        "BeeStingerModel second blade (cross) @ texOffs(0,0)",
                    )
                ],
                "children": [],
            },
        ],
    }
    return (
        [root],
        "ok",
        [
            "Hand-authored from vanilla BeeStingerModel / CleanRoomEntityModelRuntime.BuildBeeStinger (16×16); adult scale 1.0."
        ],
    )


def build_banner_flag() -> tuple[list[dict[str, Any]], str, list[str]]:
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "flag",
                "pose": Z(),
                "cuboids": [cub((-10.0, 0.0, -2.0), (10.0, 40.0, -1.0), 0, 0, "BannerFlagModel cloth 20×40×1 @ texOffs(0,0)")],
                "children": [],
            },
            {
                "id": "bar",
                "pose": Z(),
                "cuboids": [cub((-10.0, -2.0, -1.0), (10.0, 0.0, 1.0), 0, 42, "BannerFlagModel top bar @ texOffs(0,42)")],
                "children": [],
            },
            {
                "id": "pole",
                "pose": Z(),
                "cuboids": [cub((-1.0, -30.0, -1.0), (1.0, 12.0, 1.0), 44, 0, "BannerFlagModel standing pole @ texOffs(44,0)")],
                "children": [],
            },
        ],
    }
    return (
        [root],
        "ok",
        [
            "Standing banner variant (CleanRoomEntityModelRuntime.BuildBannerFlag isWall=false); wall omits pole.",
            "Hand-authored from vanilla BannerModel flag layer (64×64).",
        ],
    )


def build_elytra() -> tuple[list[dict[str, Any]], str, list[str]]:
    # BuildEquipmentWings: leftRoot T(5,0)*Rx(pi/12)*Rz(-pi/12), rightRoot T(-5,0)*Rx(pi/12)*Rz(pi/12)
    rx = 0.2617994
    rz_l = -0.2617994
    rz_r = 0.2617994
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "left_wing",
                "pose": TR(5.0, 0.0, 0.0, rx, 0.0, rz_l),
                "cuboids": [
                    cub(
                        (-11.0, -1.0, -1.0),
                        (1.0, 21.0, 3.0),
                        22,
                        0,
                        "ElytraEntityModel left wing 10×20×2 + deformation mesh parity @ texOffs(22,0)",
                    )
                ],
                "children": [],
            },
            {
                "id": "right_wing",
                "pose": TR(-5.0, 0.0, 0.0, rx, 0.0, rz_r),
                "cuboids": [
                    cub(
                        (-1.0, -1.0, -1.0),
                        (11.0, 21.0, 3.0),
                        22,
                        0,
                        "ElytraEntityModel right wing (mirror UV) @ texOffs(22,0)",
                        mirror_u=True,
                    )
                ],
                "children": [],
            },
        ],
    }
    return (
        [root],
        "partial",
        [
            "Composite Euler from CleanRoomEntityModelRuntime.BuildEquipmentWings (64×32); verify eulerOrder vs vanilla PartPose chain.",
        ],
    )


def build_nautilus_saddle() -> tuple[list[dict[str, Any]], str, list[str]]:
    d = 0.2
    root = {
        "id": "root",
        "pose": T(0.0, 29.0, -6.0),
        "cuboids": [],
        "children": [
            {
                "id": "shell",
                "pose": T(0.0, -13.0, 5.0),
                "cuboids": [
                    cub(
                        (-7.0 - d, -10.0 - d, -7.0 - d),
                        (7.0 + d, 0.0 + d, 9.0 + d),
                        0,
                        0,
                        "NautilusSaddleModel shell overlay 14×10×16 +0.2 @ texOffs(0,0) (128×128)",
                    )
                ],
                "children": [],
            }
        ],
    }
    return (
        [root],
        "ok",
        [
            "Hand-authored from vanilla NautilusSaddleModel / CleanRoomEntityModelRuntime.BuildNautilusSaddle; root/shell PartPose chain."
        ],
    )


def build_happy_ghast_harness() -> tuple[list[dict[str, Any]], str, list[str]]:
    geo = 1.0
    g_y = 9.0 * geo
    g_z = -5.5 * geo
    g_rx = -PI / 4.0  # gogglesEquippedBlend=0
    gd = 0.15
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "harness",
                "pose": T(0.0, 24.0 * geo, 0.0),
                "cuboids": [
                    cub(
                        (-8.0 * geo, -16.0 * geo, -8.0 * geo),
                        (8.0 * geo, 0.0, 8.0 * geo),
                        0,
                        0,
                        "HappyGhastHarnessModel harness 16³ @ texOffs(0,0); adult geo=1",
                    )
                ],
                "children": [],
            },
            {
                "id": "goggles",
                "pose": TR(0.0, g_y, g_z, g_rx, 0.0, 0.0),
                "cuboids": [
                    cub(
                        ((-8.0 - gd) * geo, (-2.5 - gd) * geo, (-2.5 - gd) * geo),
                        ((8.0 + gd) * geo, (2.5 + gd) * geo, (2.5 + gd) * geo),
                        0,
                        32,
                        "HappyGhastHarnessModel goggles 16×5×5 +0.15 @ texOffs(0,32); idle equipped blend=0",
                    )
                ],
                "children": [],
            },
        ],
    }
    return (
        [root],
        "partial",
        [
            "Adult baseline (geo=1); baby uses BABY_TRANSFORMER 0.2375 in renderer.",
            "Goggles pose interpolates in vanilla setupAnim; IR uses idle (equipped blend 0).",
        ],
    )


def build_slime_outer() -> tuple[list[dict[str, Any]], str, list[str]]:
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "outer",
                "pose": Z(),
                "cuboids": [cub((-4.0, 16.0, -4.0), (4.0, 24.0, 4.0), 0, 0, "SlimeModel.createBodyLayer outer shell 8×8×8 @ texOffs(0,0) (64×32)")],
                "children": [],
            }
        ],
    }
    return (
        [root],
        "partial",
        [
            "Vanilla SlimeModel uses createOuterBodyLayer + createInnerBodyLayer; CleanRoom BuildSlime merges both into one mesh.",
        ],
    )


def build_adult_cat() -> tuple[list[dict[str, Any]], str, list[str]]:
    # FelineModel / AdultCatModel share createBodyLayer mesh — BuildCat adult, zero leg/head animation.
    body_pose = TR(0.0, 12.0, -10.0, PI / 2.0, 0.0, 0.0)
    head_pose = T(0.0, 15.0, -9.0)
    tail1_pose = TR(0.0, 15.0, 8.0, 0.9, 0.0, 0.0)
    tail2_pose = T(0.0, 20.0, 14.0)
    rh = TR(1.1, 18.0, 5.0, 0.0, 0.0, 0.0)
    lh = TR(-1.1, 18.0, 5.0, 0.0, 0.0, 0.0)
    rf = TR(1.2, 14.1, -5.0, 0.0, 0.0, 0.0)
    lf = TR(-1.2, 14.1, -5.0, 0.0, 0.0, 0.0)
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "body",
                "pose": body_pose,
                "cuboids": [
                    cub((-2.0, 3.0, -8.0), (2.0, 19.0, -2.0), 20, 0, "FelineModel body 4×16×6 @ texOffs(20,0)")
                ],
                "children": [],
            },
            {
                "id": "head",
                "pose": head_pose,
                "cuboids": [
                    cub((-2.5, -2.0, -3.0), (2.5, 2.0, 2.0), 0, 0, "FelineModel head 5×4×5 @ texOffs(0,0)"),
                    cub((-1.5, -0.001, -4.0), (1.5, 1.999, -2.0), 0, 24, "FelineModel nose 3×2×2 @ texOffs(0,24)"),
                    cub((-2.0, -3.0, 0.0), (-1.0, -2.0, 2.0), 0, 10, "FelineModel left ear @ texOffs(0,10)"),
                    cub((1.0, -3.0, 0.0), (2.0, -2.0, 2.0), 6, 10, "FelineModel right ear @ texOffs(6,10)"),
                ],
                "children": [],
            },
            {
                "id": "tail1",
                "pose": tail1_pose,
                "cuboids": [cub((-0.5, 0.0, 0.0), (0.5, 8.0, 1.0), 0, 15, "FelineModel tail1 1×8×1 @ texOffs(0,15)")],
                "children": [],
            },
            {
                "id": "tail2",
                "pose": tail2_pose,
                "cuboids": [cub((-0.5, 0.0, 0.0), (0.5, 8.0, 1.0), 4, 15, "FelineModel tail2 @ texOffs(4,15)")],
                "children": [],
            },
            {
                "id": "right_hind_leg",
                "pose": rh,
                "cuboids": [cub((-1.0, 0.0, 1.0), (1.0, 6.0, 3.0), 8, 13, "FelineModel hind leg 2×6×2 @ texOffs(8,13)")],
                "children": [],
            },
            {
                "id": "left_hind_leg",
                "pose": lh,
                "cuboids": [cub((-1.0, 0.0, 1.0), (1.0, 6.0, 3.0), 8, 13, "FelineModel hind leg mirror @ texOffs(8,13)")],
                "children": [],
            },
            {
                "id": "right_front_leg",
                "pose": rf,
                "cuboids": [cub((-1.0, 0.0, 0.0), (1.0, 10.0, 2.0), 40, 0, "FelineModel front leg 2×10×2 @ texOffs(40,0)")],
                "children": [],
            },
            {
                "id": "left_front_leg",
                "pose": lf,
                "cuboids": [cub((-1.0, 0.0, 0.0), (1.0, 10.0, 2.0), 40, 0, "FelineModel front leg mirror @ texOffs(40,0)")],
                "children": [],
            },
        ],
    }
    return (
        [root],
        "ok",
        [
            "AdultCatModel delegates to same FelineModel.createBodyLayer mesh as Cat/Ocelot — CleanRoomEntityModelRuntime.BuildCat baseline (limb pitch 0, headTilt 0).",
        ],
    )


def build_spider() -> tuple[list[dict[str, Any]], str, list[str]]:
    leg_spread = 0.0
    children: list[dict[str, Any]] = [
        {
            "id": "cephalothorax",
            "pose": Z(),
            "cuboids": [cub((6.0, 8.0, 4.0), (14.0, 16.0, 12.0), 0, 0, "SpiderModel cephalothorax 8×8×8 @ texOffs(0,0)")],
            "children": [],
        },
        {
            "id": "abdomen",
            "pose": Z(),
            "cuboids": [cub((0.0, 8.0, 2.0), (10.0, 16.0, 14.0), 0, 11, "SpiderModel abdomen 10×8×12 @ texOffs(0,11)")],
            "children": [],
        },
    ]
    for i in range(4):
        z = 4.5 + i * 2.2
        spread = (leg_spread + i * 0.08) * 0.45
        base_angle = PI / 4.0 + i * 0.04
        ang_l = base_angle + spread
        ang_r = -base_angle - spread
        children.append(
            {
                "id": f"leg_left_{i}",
                "pose": TR(6.0, 10.0, z + 1.0, 0.0, 0.0, ang_l),
                "cuboids": [cub((-16.0, -1.0, -1.0), (0.0, 1.0, 1.0), 18, 0, f"SpiderModel leg L{i} 16×2×2 @ texOffs(18,0)")],
                "children": [],
            }
        )
        children.append(
            {
                "id": f"leg_right_{i}",
                "pose": TR(14.0, 10.0, z + 1.0, 0.0, 0.0, ang_r),
                "cuboids": [cub((0.0, -1.0, -1.0), (16.0, 1.0, 1.0), 18, 0, f"SpiderModel leg R{i} 16×2×2 @ texOffs(18,0)")],
                "children": [],
            }
        )
    root = {"id": "root", "pose": Z(), "cuboids": [], "children": children}
    return (
        [root],
        "partial",
        [
            "Leg hinges approximated as Z-rotation on CleanRoomEntityModelRuntime.BuildSpider (legSpread=0); vanilla uses PartPose + origin pivot.",
        ],
    )


def build_silverfish() -> tuple[list[dict[str, Any]], str, list[str]]:
    seg = [(3.0, 2.0, 2.0), (4.0, 3.0, 2.0), (6.0, 4.0, 3.0), (3.0, 3.0, 2.0), (2.0, 2.0, 1.0), (2.0, 1.0, 1.0), (1.0, 1.0, 2.0)]
    z = -3.5
    body_children: list[dict[str, Any]] = []
    for i, s in enumerate(seg):
        x0 = -s[0] * 0.5
        y0 = 24.0 - s[1]
        z0 = z
        # Local cuboid Z then pose T(0,0,z0) at wave=0 → add z0 to both Z corners.
        zm = -s[2] * 0.5 + z0
        zp = s[2] * 0.5 + z0
        body_children.append(
            {
                "id": f"segment_{i}",
                "pose": Z(),
                "cuboids": [
                    cub(
                        (x0, y0, zm),
                        (x0 + s[0], y0 + s[1], zp),
                        0,
                        i * 4,
                        f"SilverfishModel body segment {i} @ texOffs(0,{i * 4})",
                    )
                ],
                "children": [],
            }
        )
        if i < len(seg) - 1:
            z += (s[2] + seg[i + 1][2]) * 0.5

    sz2 = seg[2][2]
    sz4 = seg[4][2]
    sz1 = seg[1][2]
    wing_layer = [
        cub((-5.0, 16.0, -sz2 * 0.5), (5.0, 24.0, sz2 * 0.5), 20, 0, "SilverfishModel wing layer 1 @ texOffs(20,0)"),
        cub((-3.0, 20.0, -sz4 * 0.5), (3.0, 24.0, sz4 * 0.5), 20, 11, "SilverfishModel wing layer 2 @ texOffs(20,11)"),
        cub((-3.0, 19.0, -sz1 * 0.5), (3.0, 24.0, sz1 * 0.5), 20, 18, "SilverfishModel wing layer 3 @ texOffs(20,18)"),
    ]
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {"id": "body_chain", "pose": Z(), "cuboids": [], "children": body_children},
            {
                "id": "wings",
                "pose": Z(),
                "cuboids": wing_layer,
                "children": [],
            },
        ],
    }
    return (
        [root],
        "partial",
        [
            "Segment YR wobble omitted (wave=0 baseline); wing layers flat per CleanRoomEntityModelRuntime.BuildSilverfish.",
        ],
    )


def build_endermite() -> tuple[list[dict[str, Any]], str, list[str]]:
    parts = [(4.0, 3.0, 2.0), (6.0, 4.0, 5.0), (3.0, 3.0, 1.0), (1.0, 2.0, 1.0)]
    uvs = [(0, 0), (0, 5), (0, 14), (0, 18)]
    z = -3.5
    children: list[dict[str, Any]] = []
    for i, p in enumerate(parts):
        ty = 24.0 - p[1]
        zm = -p[2] * 0.5 + z
        zp = p[2] * 0.5 + z
        children.append(
            {
                "id": f"segment_{i}",
                "pose": Z(),
                "cuboids": [
                    cub(
                        (-p[0] * 0.5, ty, zm),
                        (p[0] * 0.5, 24.0, zp),
                        uvs[i][0],
                        uvs[i][1],
                        f"EndermiteModel segment {i} @ texOffs({uvs[i][0]},{uvs[i][1]})",
                    )
                ],
                "children": [],
            }
        )
        if i < len(parts) - 1:
            z += (p[2] + parts[i + 1][2]) * 0.5
    root = {"id": "root", "pose": Z(), "cuboids": [], "children": children}
    return (
        [root],
        "partial",
        ["Segment wobble omitted (wave=0); CleanRoomEntityModelRuntime.BuildEndermite baseline."],
    )


def build_breeze() -> tuple[list[dict[str, Any]], str, list[str]]:
    swirl = 0.0
    rods_root = T(0.0, 8.0, 0.0)
    rod1 = TR(2.5981, -3.0, 1.5, -2.7489 + swirl, -1.0472, PI)
    rod2 = TR(-2.5981, -3.0, 1.5, -2.7489 - swirl, 1.0472, PI)
    rod3 = TR(0.0, -3.0, -3.0, 0.3927, 0.0, 0.0)
    head_p = T(0.0, 4.0, 0.0)
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "head",
                "pose": head_p,
                "cuboids": [
                    cub((-5.0, -5.0, -4.2), (5.0, -2.0, -0.2), 4, 24, "BreezeModel.createBodyLayer eyes socket region @ texOffs(4,24)"),
                    cub((-4.0, -8.0, -4.0), (4.0, 0.0, 4.0), 0, 0, "BreezeModel.createBodyLayer head 8×8×8 @ texOffs(0,0)"),
                ],
                "children": [],
            },
            {
                "id": "rods",
                "pose": rods_root,
                "cuboids": [],
                "children": [
                    {
                        "id": "rod_1",
                        "pose": rod1,
                        "cuboids": [cub((-1.0, 0.0, -3.0), (1.0, 8.0, -1.0), 0, 17, "BreezeModel rod @ texOffs(0,17)")],
                        "children": [],
                    },
                    {
                        "id": "rod_2",
                        "pose": rod2,
                        "cuboids": [cub((-1.0, 0.0, -3.0), (1.0, 8.0, -1.0), 0, 17, "BreezeModel rod @ texOffs(0,17)")],
                        "children": [],
                    },
                    {
                        "id": "rod_3",
                        "pose": rod3,
                        "cuboids": [cub((-1.0, 0.0, -3.0), (1.0, 8.0, -1.0), 0, 17, "BreezeModel rod @ texOffs(0,17)")],
                        "children": [],
                    },
                ],
            },
        ],
    }
    return (
        [root],
        "partial",
        [
            "createBodyLayer 32×32 only (head + rods); createWindLayer / eyes layers are separate factories — see CleanRoomEntityModelRuntime.BuildBreeze.",
            "Rod composite Euler is an approximation of EntityParityTemplate.Er (swirl=0).",
        ],
    )


def build_creaking() -> tuple[list[dict[str, Any]], str, list[str]]:
    lean = 0.0
    head_pose = TR(0.0, -8.0, 0.0, 0.0, 0.0, lean)
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "head",
                "pose": head_pose,
                "cuboids": [
                    cub((-3.0, -10.0, -3.0), (3.0, 0.0, 3.0), 28, 31, "CreakingModel head upper @ texOffs(28,31)"),
                    cub((-3.0, 0.0, -3.0), (3.0, 10.0, 3.0), 12, 40, "CreakingModel head lower @ texOffs(12,40)"),
                ],
                "children": [],
            },
            {
                "id": "body",
                "pose": Z(),
                "cuboids": [cub((0.0, -3.0, -3.0), (6.0, 13.0, 5.0), 24, 0, "CreakingModel torso @ texOffs(24,0)")],
                "children": [],
            },
            {
                "id": "right_arm",
                "pose": TR(0.5, -1.5, 0.75, 0.0, lean * 0.5, 0.0),
                "cuboids": [cub((-2.0, -1.5, -1.5), (3.0, 21.0, 3.0), 46, 0, "CreakingModel right arm @ texOffs(46,0)")],
                "children": [],
            },
            {
                "id": "left_arm",
                "pose": TR(-1.5, -1.5, 0.75, 0.0, -lean * 0.5, 0.0),
                "cuboids": [cub((-3.0, -1.5, -1.5), (0.0, 16.0, 3.0), 52, 12, "CreakingModel left arm @ texOffs(52,12)")],
                "children": [],
            },
            {
                "id": "right_leg",
                "pose": Z(),
                "cuboids": [cub((-1.5, 0.0, -1.5), (1.5, 16.0, 1.5), 42, 40, "CreakingModel right leg @ texOffs(42,40)")],
                "children": [],
            },
            {
                "id": "left_leg",
                "pose": Z(),
                "cuboids": [cub((-3.0, 0.0, -1.5), (0.0, 19.0, 1.5), 0, 34, "CreakingModel left leg @ texOffs(0,34)")],
                "children": [],
            },
        ],
    }
    return (
        [root],
        "partial",
        [
            "Lean=0 baseline; some limbs use pivot euler in RigBuilder — poses are first-order parity with CleanRoomEntityModelRuntime.BuildCreaking.",
        ],
    )


def build_ender_dragon(wing_sweep: float = 0.0) -> tuple[list[dict[str, Any]], str, list[str]]:
    # Adult scale 1.0 (BabyProfile.Adult head/body/leg = 1)
    hs = 1.0
    bs = 1.0
    ls = 1.0
    children: list[dict[str, Any]] = []

    head_pose = T(0.0, 20.0, -62.0)
    head_cubes = [
        cub((-6.0, -1.0, -24.0), (6.0, 4.0, -8.0), 176, 44, "EnderDragonModel snout upper @ texOffs(176,44)", False),
        cub((-8.0, -8.0, -10.0), (8.0, 8.0, 6.0), 112, 30, "EnderDragonModel head core @ texOffs(112,30)", False),
        cub((-5.0, -12.0, -4.0), (-3.0, -8.0, 2.0), 0, 0, "EnderDragonModel horn L @ texOffs(0,0)", False),
        cub((-5.0, -3.0, -22.0), (-3.0, -1.0, -18.0), 112, 0, "EnderDragonModel detail @ texOffs(112,0)", False),
        cub((3.0, -12.0, -4.0), (5.0, -8.0, 2.0), 0, 0, "EnderDragonModel horn R @ texOffs(0,0)", False),
        cub((3.0, -3.0, -22.0), (5.0, -1.0, -18.0), 112, 0, "EnderDragonModel detail R @ texOffs(112,0)", False),
    ]
    children.append({"id": "head", "pose": head_pose, "cuboids": head_cubes, "children": []})

    jaw_pose = TR(0.0, 4.0, -8.0, 0.0, 0.0, 0.0)  # relative to head — flatten: world = head * jaw offset approximated as child
    children.append(
        {
            "id": "jaw",
            "pose": TR(0.0, 24.0, -70.0, 0.0, 0.0, 0.0),  # head T + jaw T(0,4,-8) on Z
            "cuboids": [cub((-6.0, 0.0, -16.0), (6.0, 4.0, 0.0), 176, 65, "EnderDragonModel jaw @ texOffs(176,65)")],
            "children": [],
        }
    )

    for i in range(5):
        children.append(
            {
                "id": f"neck_{i}",
                "pose": T(0.0, 20.0, -12.0 - 10.0 * i),
                "cuboids": [cub((-5.0, -5.0, -5.0), (5.0, 5.0, 5.0), 192, 104, f"EnderDragonModel neck {i} 10³ @ texOffs(192,104)")],
                "children": [],
            }
        )
    for i in range(12):
        children.append(
            {
                "id": f"tail_{i}",
                "pose": T(0.0, 10.0, 60.0 + 10.0 * i),
                "cuboids": [cub((-5.0, -5.0, -5.0), (5.0, 5.0, 5.0), 192, 104, f"EnderDragonModel tail {i} 10³ @ texOffs(192,104)")],
                "children": [],
            }
        )

    body_pose = T(0.0, 3.0, 8.0)
    body_cubes = [
        cub((-12.0, 1.0, -16.0), (12.0, 25.0, 48.0), 0, 0, "EnderDragonModel body 24×24×64 @ texOffs(0,0)", False),
        cub((-1.0, -5.0, -10.0), (1.0, 1.0, 2.0), 220, 53, "EnderDragonModel dorsal spike 1 @ texOffs(220,53)", False),
        cub((-1.0, -5.0, 10.0), (1.0, 1.0, 22.0), 220, 53, "EnderDragonModel dorsal spike 2 @ texOffs(220,53)", False),
        cub((-1.0, -5.0, 30.0), (1.0, 1.0, 42.0), 220, 53, "EnderDragonModel dorsal spike 3 @ texOffs(220,53)", False),
    ]
    children.append({"id": "body", "pose": body_pose, "cuboids": body_cubes, "children": []})

    # Wings wingSweep=0
    lw_root = TR(12.0, 5.0, 2.0, 0.0, wing_sweep, 0.0)  # body T(0,3,8) * T(12,2,-6) => (12,5,2)
    children.append(
        {
            "id": "left_wing",
            "pose": lw_root,
            "cuboids": [
                cub((0.0, -4.0, -4.0), (56.0, 4.0, 4.0), 112, 88, "EnderDragonModel left wing bone @ texOffs(112,88)", False),
                cub((56.0, -2.0, -2.0), (112.0, 2.0, 2.0), 112, 136, "EnderDragonModel left wing tip @ texOffs(112,136)", False),
            ],
            "children": [],
        }
    )
    rw_root = TR(-12.0, 5.0, 2.0, 0.0, -wing_sweep, 0.0)
    children.append(
        {
            "id": "right_wing",
            "pose": rw_root,
            "cuboids": [
                cub((-56.0, -4.0, -4.0), (0.0, 4.0, 4.0), 112, 88, "EnderDragonModel right wing bone @ texOffs(112,88)", True),
                cub((-112.0, -2.0, -2.0), (0.0, 2.0, 2.0), 112, 136, "EnderDragonModel right wing tip @ texOffs(112,136)", True),
            ],
            "children": [],
        }
    )

    def leg_chain(left: bool, front: bool) -> list[dict[str, Any]]:
        out: list[dict[str, Any]] = []
        if front:
            x = 12.0 if left else -12.0
            thigh = TR(x, 20.0, 2.0, 1.3, 0.0, 0.0)  # bodyPose * T(x,17,-6) * Er(1.3,0,0) approx
            out.append(
                {
                    "id": f"leg_{'L' if left else 'R'}_front_thigh",
                    "pose": thigh,
                    "cuboids": [cub((-4.0, -4.0, -4.0), (4.0, 20.0, 4.0), 112, 104, "Dragon front thigh 8×24×8", False)],
                    "children": [],
                }
            )
            shin = TR(x, 40.0, 1.0, -0.5, 0.0, 0.0)
            out.append(
                {
                    "id": f"leg_{'L' if left else 'R'}_front_shin",
                    "pose": shin,
                    "cuboids": [cub((-3.0, -1.0, -3.0), (3.0, 23.0, 3.0), 226, 138, "Dragon front shin", False)],
                    "children": [],
                }
            )
            foot = TR(x, 63.0, 1.0, 0.75, 0.0, 0.0)
            out.append(
                {
                    "id": f"leg_{'L' if left else 'R'}_front_foot",
                    "pose": foot,
                    "cuboids": [cub((-4.0, 0.0, -12.0), (4.0, 4.0, 4.0), 144, 104, "Dragon front foot", False)],
                    "children": [],
                }
            )
        else:
            x = 16.0 if left else -16.0
            thigh = TR(x, 16.0, 42.0, 1.0, 0.0, 0.0)
            out.append(
                {
                    "id": f"leg_{'L' if left else 'R'}_hind_thigh",
                    "pose": thigh,
                    "cuboids": [cub((-8.0, -4.0, -8.0), (8.0, 28.0, 8.0), 0, 0, "Dragon hind thigh 16×32×16", False)],
                    "children": [],
                }
            )
            shin = TR(x, 44.0, 38.0, 0.5, 0.0, 0.0)
            out.append(
                {
                    "id": f"leg_{'L' if left else 'R'}_hind_shin",
                    "pose": shin,
                    "cuboids": [cub((-6.0, -2.0, -6.0), (6.0, 30.0, 6.0), 196, 0, "Dragon hind shin", False)],
                    "children": [],
                }
            )
            foot = TR(x, 75.0, 42.0, 0.75, 0.0, 0.0)
            out.append(
                {
                    "id": f"leg_{'L' if left else 'R'}_hind_foot",
                    "pose": foot,
                    "cuboids": [cub((-9.0, 0.0, -20.0), (9.0, 6.0, 4.0), 112, 0, "Dragon hind foot", False)],
                    "children": [],
                }
            )
        return out

    for lc in (leg_chain(True, True), leg_chain(False, True), leg_chain(True, False), leg_chain(False, False)):
        children.extend(lc)

    root = {"id": "root", "pose": Z(), "cuboids": [], "children": children}
    return (
        [root],
        "partial",
        [
            "Large 256×256 rig: jaw/leg chain poses are flattened approximations vs nested PartPose; wingSweep=0.",
            "Derived from CleanRoomEntityModelRuntime.BuildEnderDragon summary comments + mesh inspection.",
        ],
    )


def build_camel_saddle() -> tuple[list[dict[str, Any]], str, list[str]]:
    d = 0.05
    thin = 0.08
    cubes = [
        cub(
            (-4.5 - d, -17.0 - d, -15.5 - d),
            (4.5 + d, -12.0 + d, -4.5 + d),
            74,
            64,
            "CamelSaddleModel body stack 1 @ texOffs(74,64) 9×5×11",
        ),
        cub(
            (-3.5 - d, -20.0 - d, -15.5 - d),
            (3.5 + d, -17.0 + d, -4.5 + d),
            92,
            114,
            "CamelSaddleModel body stack 2 @ texOffs(92,114) 7×3×11",
        ),
        cub(
            (-7.5 - d, -12.0 - d, -23.5 - d),
            (7.5 + d, 0.0 + d, 3.5 + d),
            0,
            89,
            "CamelSaddleModel blanket @ texOffs(0,89) 15×12×27",
        ),
        cub(
            (-3.5 - d, -7.0 - d, -15.0 - d),
            (3.5 + d, 1.0 + d, 4.0 + d),
            60,
            87,
            "CamelSaddleModel bridle lower @ texOffs(60,87) 7×8×19",
        ),
        cub(
            (-3.5 - d, -21.0 - d, -15.0 - d),
            (3.5 + d, -7.0 + d, -8.0 + d),
            21,
            64,
            "CamelSaddleModel bridle upper @ texOffs(21,64) 7×14×7",
        ),
        cub(
            (-2.5 - d, -21.0 - d, -21.0 - d),
            (2.5 + d, -16.0 + d, -15.0 + d),
            50,
            64,
            "CamelSaddleModel nose band @ texOffs(50,64) 5×5×6",
        ),
        cub((2.5, -19.0, -18.0), (3.5, -17.0, -16.0), 74, 70, "CamelSaddleModel cheek piece R @ texOffs(74,70)", True),
        cub((-3.5, -19.0, -18.0), (-2.5, -17.0, -16.0), 74, 70, "CamelSaddleModel cheek piece L @ texOffs(74,70)", False),
        cub((3.51, -18.0, -17.0), (3.51 + thin, -11.0, -2.0), 98, 42, "CamelSaddleModel rein R @ texOffs(98,42)", False),
        cub((-3.5, -18.0, -2.0), (3.5, -11.0, -2.0 + thin), 84, 57, "CamelSaddleModel rein cross @ texOffs(84,57)", False),
        cub((-3.51 - thin, -18.0, -17.0), (-3.51, -11.0, -2.0), 98, 42, "CamelSaddleModel rein L @ texOffs(98,42)", False),
    ]
    root = {"id": "root", "pose": Z(), "cuboids": [], "children": [{"id": "saddle", "pose": Z(), "cuboids": cubes, "children": []}]}
    return (
        [root],
        "ok",
        [
            "All createBodyLayer cuboids flattened under one part (model space) per CleanRoomEntityModelRuntime.BuildCamelSaddle (128×128)."
        ],
    )


def build_boat_placeholder() -> tuple[list[dict[str, Any]], str, list[str]]:
    root = {
        "id": "root",
        "pose": Z(),
        "cuboids": [],
        "children": [
            {
                "id": "hull_envelope",
                "pose": Z(),
                "cuboids": [
                    cub(
                        (-15.0, -10.0, -12.0),
                        (15.0, 10.0, 12.0),
                        0,
                        0,
                        "Approximate neutral AABB for oak boat preview mesh — CleanRoomEntityModelRuntime.BuildBoat uses per-part euler+pivot; full part-tree deferred.",
                    )
                ],
                "children": [],
            }
        ],
    }
    return (
        [root],
        "partial",
        [
            "Hull envelope placeholder only; next batch: decompose bottom/back/front/walls/paddles (+ chest boat extras) with vanilla PartPose from BoatModel.createBodyLayer.",
        ],
    )


HYDRATORS: dict[str, Any] = {
    "net.minecraft.client.model.object.projectile.TridentModel.json": build_trident,
    "net.minecraft.client.model.object.equipment.ShieldModel.json": build_shield,
    "net.minecraft.client.model.object.chest.ChestModel.json": build_chest,
    "net.minecraft.client.model.animal.bee.BeeStingerModel.json": build_bee_stinger,
    "net.minecraft.client.model.object.banner.BannerFlagModel.json": build_banner_flag,
    "net.minecraft.client.model.object.equipment.ElytraModel.json": build_elytra,
    "net.minecraft.client.model.animal.nautilus.NautilusSaddleModel.json": build_nautilus_saddle,
    "net.minecraft.client.model.animal.ghast.HappyGhastHarnessModel.json": build_happy_ghast_harness,
    "net.minecraft.client.model.monster.slime.SlimeModel.json": build_slime_outer,
    "net.minecraft.client.model.animal.feline.AdultCatModel.json": build_adult_cat,
    "net.minecraft.client.model.monster.spider.SpiderModel.json": build_spider,
    "net.minecraft.client.model.monster.silverfish.SilverfishModel.json": build_silverfish,
    "net.minecraft.client.model.monster.endermite.EndermiteModel.json": build_endermite,
    "net.minecraft.client.model.monster.breeze.BreezeModel.json": build_breeze,
    "net.minecraft.client.model.monster.creaking.CreakingModel.json": build_creaking,
    "net.minecraft.client.model.monster.dragon.EnderDragonModel.json": build_ender_dragon,
    "net.minecraft.client.model.animal.camel.CamelSaddleModel.json": build_camel_saddle,
    "net.minecraft.client.model.object.boat.BoatModel.json": build_boat_placeholder,
}


def main() -> None:
    hydrated = 0
    for name, fn in HYDRATORS.items():
        path = GEOM_DIR / name
        if not path.exists():
            print(f"MISSING {path}")
            continue
        base = json.loads(path.read_text(encoding="utf-8"))
        roots, status, notes = fn()
        merged = merge_shard(base, roots, status, notes)
        path.write_text(json.dumps(merged, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        hydrated += 1
        print(f"hydrated {name} cuboids={count_cuboids(roots)}")

    cleaned = 0
    for path in sorted(GEOM_DIR.glob("*.json")):
        if path.name in HYDRATORS:
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        if clean_notes_shard(data):
            path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
            cleaned += 1
    print(f"cleaned stale notes on {cleaned} shards")


if __name__ == "__main__":
    main()
