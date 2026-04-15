"""Resolution tiers, manifest geometry, memory caps, and pack-tag reconciliation for native training."""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from pathlib import Path
from typing import Any, Literal

from PIL import Image

# LabPBR-style pack folder tiers; also used for logging / optional per-tier val loss.
TIERS: tuple[int, ...] = (16, 32, 64, 128, 256, 512)

TAG_MISMATCH_REL_TOLERANCE = 0.25


class DownscalePolicy(str, Enum):
    """Pillow resampling when scaling down for --max-train-side (not used for fixed train_res NN path)."""

    BOX = "box"
    LANCZOS = "lanczos"
    NEAREST = "nearest"


def _pil_resample(policy: DownscalePolicy) -> Image.Resampling:
    if policy is DownscalePolicy.BOX:
        return Image.Resampling.BOX
    if policy is DownscalePolicy.LANCZOS:
        return Image.Resampling.LANCZOS
    if policy is DownscalePolicy.NEAREST:
        return Image.Resampling.NEAREST
    raise ValueError(f"Unknown downscale policy: {policy}")


def parse_downscale_policy(s: str) -> DownscalePolicy:
    k = str(s).strip().lower()
    for p in DownscalePolicy:
        if p.value == k:
            return p
    raise ValueError(f"Invalid --downscale-for-memory: {s!r}; expected one of {[e.value for e in DownscalePolicy]}")


def infer_train_hw(root: Path, rec: dict[str, Any]) -> tuple[int, int]:
    """
    Return diffuse width/height. Prefer manifest width/height; if missing, open the diffuse once.
    """
    w = rec.get("width")
    h = rec.get("height")
    if isinstance(w, int) and isinstance(h, int) and w > 0 and h > 0:
        return w, h
    img_path = root / rec["image"]
    with Image.open(img_path) as im:
        return im.size


def tier_from_spatial_hw(w: int, h: int) -> int:
    """Smallest TIERS value >= max(w,h), else 512 (for val logging by spatial grid)."""
    m = max(w, h)
    candidates = [t for t in TIERS if t >= m]
    return min(candidates) if candidates else 512


def assign_bucket_tier(
    w: int,
    h: int,
    *,
    tagged_resolution: int | None,
    tagged_resolution_known: bool,
) -> tuple[int, bool]:
    """
    Dimension-first tier: smallest TIERS value >= max(w,h), else 512 for oversized content.
    Returns (tier, tag_mismatch) where tag_mismatch is True when a known tag disagrees with max(w,h)
    beyond TAG_MISMATCH_REL_TOLERANCE (dimensions govern training geometry; tag is diagnostic).
    """
    m = max(w, h)
    candidates = [t for t in TIERS if t >= m]
    tier = min(candidates) if candidates else 512

    tag_mismatch = False
    if tagged_resolution_known and tagged_resolution is not None and tagged_resolution > 0:
        tag = tagged_resolution
        denom = max(tag, 1)
        if abs(m - tag) / denom > TAG_MISMATCH_REL_TOLERANCE:
            tag_mismatch = True

    return tier, tag_mismatch


def compute_capped_hw(w: int, h: int, max_side: int | None) -> tuple[int, int, bool]:
    """
    If max(w,h) <= max_side or max_side is None, return (w,h) and capped=False.
    Otherwise proportionally scale down so the longer side maps to max_side (rounded dims, min side >= 1).
    """
    if max_side is None or max_side <= 0:
        return w, h, False
    m = max(w, h)
    if m <= max_side:
        return w, h, False
    scale = max_side / float(m)
    w2 = max(1, int(round(w * scale)))
    h2 = max(1, int(round(h * scale)))
    return w2, h2, True


def resize_pair_for_memory(
    img: Image.Image,
    spec: Image.Image,
    target_w: int,
    target_h: int,
    policy: DownscalePolicy,
) -> tuple[Image.Image, Image.Image]:
    """Resize both RGBA images to (target_w, target_h) with the same policy."""
    if img.size == (target_w, target_h) and spec.size == (target_w, target_h):
        return img, spec
    resample = _pil_resample(policy)
    if img.size != (target_w, target_h):
        img = img.resize((target_w, target_h), resample)
    if spec.size != (target_w, target_h):
        spec = spec.resize((target_w, target_h), resample)
    return img, spec


SpatialMode = Literal["fixed", "native"]


@dataclass(frozen=True)
class IndexSpatialMeta:
    """Per-sample geometry after manifest + optional cap (used for bucketing and metrics)."""

    train_w: int
    train_h: int
    tier: int
    tag_mismatch: bool
    capped: bool
