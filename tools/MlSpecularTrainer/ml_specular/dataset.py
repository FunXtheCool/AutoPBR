"""JSONL manifest dataset used by MlSpecularTrainer (direct specular training)."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any, Literal

import numpy as np
import numpy.typing as npt
import torch
from PIL import Image, ImageFile
from torch.utils.data import Dataset

# Slightly truncated PNGs (bad export, interrupted copy) often fail with OSError: broken data stream.
ImageFile.LOAD_TRUNCATED_IMAGES = True

from ml_specular.edge_channel import vc_edge_from_rgb_uint8
from ml_specular.spatial_policy import (
    TIERS,
    DownscalePolicy,
    IndexSpatialMeta,
    assign_bucket_tier,
    compute_capped_hw,
    infer_train_hw,
    resize_pair_for_memory,
)

SpatialMode = Literal["fixed", "native"]


def load_manifest(root: Path) -> dict[str, dict[str, Any]]:
    manifest_path = root / "manifest.jsonl"
    by_id: dict[str, dict[str, Any]] = {}
    with manifest_path.open(encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            rec = json.loads(line)
            by_id[rec["id"]] = rec
    return by_id


def load_split_ids(root: Path, split: str) -> list[str]:
    p = root / "splits" / f"{split}.txt"
    lines = p.read_text(encoding="utf-8").splitlines()
    return [ln.strip() for ln in lines if ln.strip()]


def _resize_nn(img: Image.Image, size: int) -> Image.Image:
    return img.resize((size, size), Image.Resampling.NEAREST)


def _apply_augment_rgb(rgb: npt.NDArray[np.uint8]) -> npt.NDArray[np.uint8]:
    """Mild photometric jitter; keeps uint8."""
    out = rgb.astype(np.float32)
    b = 1.0 + np.random.uniform(-0.07, 0.07)
    out *= b
    mean = out.mean()
    c = 1.0 + np.random.uniform(-0.12, 0.12)
    out = (out - mean) * c + mean
    gray = 0.299 * out[..., 0] + 0.587 * out[..., 1] + 0.114 * out[..., 2]
    s = 1.0 + np.random.uniform(-0.12, 0.12)
    out[..., 0] = gray + s * (out[..., 0] - gray)
    out[..., 1] = gray + s * (out[..., 1] - gray)
    out[..., 2] = gray + s * (out[..., 2] - gray)
    if np.random.random() < 0.15:
        noise = np.random.normal(0, 4.0, out.shape).astype(np.float32)
        out += noise
    out = np.clip(out, 0, 255).astype(np.uint8)
    if np.random.random() < 0.2:
        h, w = out.shape[:2]
        ch, cw = np.random.randint(2, max(3, h // 8)), np.random.randint(2, max(3, w // 8))
        cy, cx = np.random.randint(0, h - ch + 1), np.random.randint(0, w - cw + 1)
        out[cy : cy + ch, cx : cx + cw] = np.random.randint(0, 256, (ch, cw, 3), dtype=np.uint8)
    return out


def _open_rgba(path: Path, *, manifest_id: str, role: str) -> Image.Image:
    """Load RGBA; raises with manifest id and path on unreadable/corrupt files."""
    try:
        im = Image.open(path)
        return im.convert("RGBA")
    except OSError as e:
        raise OSError(
            f"Failed to load {role} for id={manifest_id!r} ({path.resolve()}): {e}"
        ) from e


def _build_input(rgb: npt.NDArray[np.uint8], in_channels: int) -> torch.Tensor:
    t = torch.from_numpy(rgb.astype(np.float32) / 255.0).permute(2, 0, 1)
    if in_channels == 4:
        edge = vc_edge_from_rgb_uint8(rgb)
        e = torch.from_numpy(edge).unsqueeze(0)
        t = torch.cat([t, e], dim=0)
    elif in_channels != 3:
        raise ValueError("in_channels must be 3 or 4")
    return t.contiguous()


def _parse_tagged_resolution(rec: dict[str, Any]) -> tuple[int | None, bool]:
    tr = rec.get("tagged_resolution")
    known = bool(rec.get("tagged_resolution_known", False))
    if tr is None or not isinstance(tr, int):
        return None, False
    return tr, known


def _build_index_spatial_meta(
    root: Path,
    manifest: dict[str, dict[str, Any]],
    ids: list[str],
    max_train_side: int | None,
) -> list[IndexSpatialMeta]:
    out: list[IndexSpatialMeta] = []
    for sid in ids:
        rec = manifest[sid]
        ow, oh = infer_train_hw(root, rec)
        tw, th, capped = compute_capped_hw(ow, oh, max_train_side)
        tr, known = _parse_tagged_resolution(rec)
        tier, tag_mismatch = assign_bucket_tier(ow, oh, tagged_resolution=tr, tagged_resolution_known=known)
        out.append(IndexSpatialMeta(train_w=tw, train_h=th, tier=tier, tag_mismatch=tag_mismatch, capped=capped))
    return out


class SpecularManifestDataset(Dataset):
    """
    Loads diffuse + artist _s map.

    fixed (default): nearest-neighbor resize both to train_res×train_res (legacy).
    native: train at manifest/native resolution (optional max-train-side cap with BOX/LANCZOS/NEAREST).

    Returns:
      - input tensor [C, H, W] float32 0..1
      - target tensor [4, H, W] float32 0..1 (spec RGBA)
      - valid mask [H, W] float32 (1 where diffuse alpha >= alpha_ignore_below, else 0)
    """

    def __init__(
        self,
        root: str | Path,
        split: str,
        train_res: int = 128,
        in_channels: int = 4,
        augment: bool = False,
        alpha_ignore_below: int = 128,
        spatial_mode: SpatialMode = "fixed",
        max_train_side: int | None = None,
        downscale_policy: DownscalePolicy = DownscalePolicy.BOX,
        strict_manifest_size: bool = True,
        native_restrict_to_target_tier: bool = False,
        native_target_tier: int | None = None,
    ) -> None:
        self.root = Path(root)
        self.train_res = train_res
        self.in_channels = in_channels
        self.augment = augment
        self.alpha_ignore_below = int(np.clip(alpha_ignore_below, 0, 255))
        self.spatial_mode: SpatialMode = spatial_mode
        self.max_train_side = max_train_side
        self.downscale_policy = downscale_policy
        self.strict_manifest_size = strict_manifest_size
        self.native_restrict_to_target_tier = bool(native_restrict_to_target_tier)
        self.native_target_tier = int(native_target_tier) if native_target_tier is not None else None
        if self.native_target_tier is not None and self.native_target_tier not in TIERS:
            raise ValueError(f"native_target_tier must be one of {TIERS} or None")

        self.manifest = load_manifest(self.root)
        self.ids = load_split_ids(self.root, split)
        # Dedupe stderr spam when the same id is retried across workers / epochs.
        self._warned_bad_ids: set[str] = set()
        missing = [i for i in self.ids if i not in self.manifest]
        if missing:
            raise ValueError(f"Split references unknown ids: {missing[:5]}")

        self._spatial_meta: list[IndexSpatialMeta] | None = None
        self._spatial_keys: list[tuple[int, int]] | None = None
        self._native_filter_stats: dict[str, int] = {}
        if self.spatial_mode == "native":
            all_meta = _build_index_spatial_meta(self.root, self.manifest, self.ids, self.max_train_side)
            if self.native_restrict_to_target_tier:
                kept_ids: list[str] = []
                kept_meta: list[IndexSpatialMeta] = []
                dropped_tag_mismatch = 0
                missing_or_unknown_tag = 0
                for sid, meta in zip(self.ids, all_meta):
                    rec = self.manifest[sid]
                    tr, known = _parse_tagged_resolution(rec)
                    # Per-sample native restriction: when tagged tier is known, require geometry tier match.
                    if known and tr in TIERS and meta.tier != int(tr):
                        dropped_tag_mismatch += 1
                        continue
                    if not known or tr not in TIERS:
                        missing_or_unknown_tag += 1
                    kept_ids.append(sid)
                    kept_meta.append(meta)
                self.ids = kept_ids
                self._spatial_meta = kept_meta
                self._native_filter_stats = {
                    "target_tier": -1,
                    "dropped_bucket_mismatch": 0,
                    "dropped_tag_mismatch": int(dropped_tag_mismatch),
                    "missing_or_unknown_tag": int(missing_or_unknown_tag),
                }
                if len(self.ids) == 0:
                    raise ValueError(
                        "Native tag-match restriction removed all samples. "
                        "Disable native restriction or ensure tagged_resolution matches sample geometry tiers."
                    )
            else:
                self._spatial_meta = all_meta
                self._native_filter_stats = {
                    "target_tier": -1,
                    "dropped_bucket_mismatch": 0,
                    "dropped_tag_mismatch": 0,
                    "missing_or_unknown_tag": 0,
                }
            self._spatial_keys = [(m.train_w, m.train_h) for m in self._spatial_meta]

    @property
    def spatial_keys(self) -> list[tuple[int, int]]:
        """(train_w, train_h) per index; only valid when spatial_mode=='native'."""
        if self._spatial_keys is None:
            raise RuntimeError("spatial_keys only defined for spatial_mode='native'")
        return self._spatial_keys

    @property
    def index_spatial_meta(self) -> list[IndexSpatialMeta]:
        if self._spatial_meta is None:
            raise RuntimeError("index_spatial_meta only defined for spatial_mode='native'")
        return self._spatial_meta

    @property
    def native_filter_stats(self) -> dict[str, int]:
        return dict(self._native_filter_stats)

    def __len__(self) -> int:
        return len(self.ids)

    def _fallback_indices(self, idx: int) -> list[int]:
        """Indices to try when idx fails. Native mode stays within the same H×W bucket."""
        n = len(self.ids)
        if n == 0:
            return []
        if self.spatial_mode == "native" and self._spatial_keys is not None:
            key = self._spatial_keys[idx]
            same = [i for i in range(n) if self._spatial_keys[i] == key]
            others = [i for i in same if i != idx]
            return [idx] + others
        return [(idx + k) % n for k in range(n)]

    def _report_skipped_asset(self, idx: int, exc: BaseException) -> None:
        mid = str(self.manifest[self.ids[idx]].get("id", "<unknown>"))
        if mid in self._warned_bad_ids:
            return
        self._warned_bad_ids.add(mid)
        print(
            f"[SpecularManifestDataset] Skipping unreadable/invalid pair id={mid!r} idx={idx}: {exc}",
            file=sys.stderr,
        )

    def _load_sample(self, idx: int) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        rec = self.manifest[self.ids[idx]]
        img_path = self.root / rec["image"]
        spec_rel = rec.get("label_spec")
        if spec_rel is None:
            raise ValueError(f"manifest record {rec.get('id', '<unknown>')} missing label_spec")
        spec_path = self.root / str(spec_rel)
        mid = str(rec.get("id", "<unknown>"))

        img = _open_rgba(img_path, manifest_id=mid, role="diffuse")
        spec = _open_rgba(spec_path, manifest_id=mid, role="label_spec")
        if spec.size != img.size:
            raise ValueError(f"Spec size mismatch for {rec.get('id', '<unknown>')}: {img.size} vs {spec.size}")

        mw = rec.get("width")
        mh = rec.get("height")
        if isinstance(mw, int) and isinstance(mh, int) and mw > 0 and mh > 0:
            if self.strict_manifest_size and img.size != (mw, mh):
                raise ValueError(
                    f"Diffuse size {img.size} does not match manifest width/height {(mw, mh)} for id={rec.get('id')!r}"
                )

        if self.spatial_mode == "fixed":
            img = _resize_nn(img, self.train_res)
            spec = _resize_nn(spec, self.train_res)
        else:
            assert self._spatial_meta is not None
            meta = self._spatial_meta[idx]
            tw, th = meta.train_w, meta.train_h
            ow, oh = img.size
            if (ow, oh) != (tw, th):
                img, spec = resize_pair_for_memory(img, spec, tw, th, self.downscale_policy)

        rgba = np.array(img, dtype=np.uint8)
        rgb = rgba[:, :, :3]
        alpha = rgba[:, :, 3]
        spec_rgba = np.array(spec, dtype=np.uint8)

        if self.augment:
            rgb = _apply_augment_rgb(rgb)

        x = _build_input(rgb, self.in_channels)
        y = torch.from_numpy(spec_rgba.astype(np.float32) / 255.0).permute(2, 0, 1).contiguous()
        valid = torch.from_numpy((alpha >= self.alpha_ignore_below).astype(np.float32))
        return x, y, valid

    def __getitem__(self, idx: int) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        order = self._fallback_indices(idx)
        last_exc: BaseException | None = None
        for j in order:
            try:
                return self._load_sample(j)
            except (OSError, ValueError) as e:
                last_exc = e
                self._report_skipped_asset(j, e)
        bucket = ""
        if self.spatial_mode == "native" and self._spatial_keys is not None:
            bucket = f" bucket={self._spatial_keys[idx]!r}"
        raise RuntimeError(
            f"No readable sample found for dataloader index {idx}{bucket} "
            f"(tried {len(order)} alternate(s)). Last error: {last_exc}"
        ) from last_exc
