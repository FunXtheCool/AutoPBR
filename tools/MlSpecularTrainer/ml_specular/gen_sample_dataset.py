"""Create a tiny synthetic dataset matching MlSpecularTrainer layout (CI / smoke training)."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


def _save_rgb(path: Path, rgb: np.ndarray) -> None:
    Image.fromarray(rgb, mode="RGB").save(path)


def _save_spec_rgba(path: Path, rgba: tuple[int, int, int, int], w: int, h: int) -> None:
    r, g, b, a = rgba
    arr = np.empty((h, w, 4), dtype=np.uint8)
    arr[..., 0] = r
    arr[..., 1] = g
    arr[..., 2] = b
    arr[..., 3] = a
    Image.fromarray(arr, mode="RGBA").save(path)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", type=Path, default=Path("sample_dataset"))
    ap.add_argument("--size", type=int, default=16)
    args = ap.parse_args(argv)

    root = args.out
    size = args.size
    (root / "images").mkdir(parents=True, exist_ok=True)
    (root / "labels").mkdir(parents=True, exist_ok=True)
    (root / "splits").mkdir(parents=True, exist_ok=True)

    h = w = size
    rng = np.random.default_rng(42)

    # Dielectric: noisy gray/brown stone + matte-ish LabPBR _s (low R, mid G)
    stone = np.zeros((h, w, 3), dtype=np.uint8)
    base = rng.integers(90, 140, size=(h, w, 1), dtype=np.int16)
    stone[:] = np.clip(base + rng.integers(-25, 25, size=(h, w, 3), dtype=np.int16), 0, 255).astype(np.uint8)
    _save_rgb(root / "images" / "stone.png", stone)
    _save_spec_rgba(root / "labels" / "stone_s.png", (40, 180, 230, 255), w, h)

    # Full metal: bright metallic gradient + high-G spec
    iron = np.zeros((h, w, 3), dtype=np.uint8)
    for y in range(h):
        v = int(180 + (y / max(h - 1, 1)) * 60)
        iron[y, :] = (v, v, min(255, v + 15))
    _save_rgb(root / "images" / "iron_ingot.png", iron)
    _save_spec_rgba(root / "labels" / "iron_ingot_s.png", (220, 250, 255, 255), w, h)

    # Ore-like: brown base + bright speckles + ore-ish _s
    ore = np.zeros((h, w, 3), dtype=np.uint8)
    ore[..., 0] = rng.integers(100, 160, size=(h, w))
    ore[..., 1] = rng.integers(70, 120, size=(h, w))
    ore[..., 2] = rng.integers(40, 90, size=(h, w))
    for _ in range(max(3, h * w // 8)):
        cy, cx = int(rng.integers(0, h)), int(rng.integers(0, w))
        ore[cy, cx] = (rng.integers(200, 255), rng.integers(200, 255), rng.integers(180, 240))
    _save_rgb(root / "images" / "gold_ore.png", ore)
    _save_spec_rgba(root / "labels" / "gold_ore_s.png", (190, 120, 240, 255), w, h)

    # Mixed: left dielectric spec, right ore-ish spec
    mixed = np.zeros((h, w, 3), dtype=np.uint8)
    mixed[:, : w // 2] = stone[:, : w // 2]
    mixed[:, w // 2 :] = ore[:, w // 2 :]
    _save_rgb(root / "images" / "mixed_edge.png", mixed)
    spec_mix = np.zeros((h, w, 4), dtype=np.uint8)
    spec_mix[:, : w // 2] = (40, 180, 230, 255)
    spec_mix[:, w // 2 :] = (190, 120, 240, 255)
    Image.fromarray(spec_mix, mode="RGBA").save(root / "labels" / "mixed_edge_s.png")

    records = [
        {
            "id": "stone",
            "image": "images/stone.png",
            "label_spec": "labels/stone_s.png",
            "pack": "demo_pack_train",
            "style": "pixel_lowres",
            "source": "synthetic",
            "width": w,
            "height": h,
        },
        {
            "id": "iron_ingot",
            "image": "images/iron_ingot.png",
            "label_spec": "labels/iron_ingot_s.png",
            "pack": "demo_pack_train",
            "style": "pixel_lowres",
            "source": "synthetic",
            "width": w,
            "height": h,
        },
        {
            "id": "gold_ore",
            "image": "images/gold_ore.png",
            "label_spec": "labels/gold_ore_s.png",
            "pack": "demo_pack_train",
            "style": "pixel_lowres",
            "source": "synthetic",
            "width": w,
            "height": h,
        },
        {
            "id": "mixed_edge",
            "image": "images/mixed_edge.png",
            "label_spec": "labels/mixed_edge_s.png",
            "pack": "demo_pack_val",
            "style": "pixel_lowres",
            "source": "synthetic",
            "width": w,
            "height": h,
        },
    ]

    with (root / "manifest.jsonl").open("w", encoding="utf-8") as f:
        for rec in records:
            f.write(json.dumps(rec) + "\n")

    (root / "splits" / "train.txt").write_text("stone\niron_ingot\ngold_ore\n", encoding="utf-8")
    (root / "splits" / "val.txt").write_text("mixed_edge\n", encoding="utf-8")
    (root / "splits" / "test.txt").write_text("mixed_edge\n", encoding="utf-8")

    print(f"Wrote sample dataset to {root.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
