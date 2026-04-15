"""Smoke tests for native spatial training (run: python -m unittest ml_specular.test_native_spatial_smoke from tools/MlSpecularTrainer)."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import numpy as np
import torch
from PIL import Image
from types import SimpleNamespace

from ml_specular.bucket_batch_sampler import BucketBatchSampler
from ml_specular.dataset import SpecularManifestDataset
from ml_specular.model import DilatedPbrNet
from ml_specular.spatial_policy import assign_bucket_tier, parse_downscale_policy
from ml_specular.spec_loss import spec_loss
from ml_specular.train_spec import _derive_batch_policy


def _write_rgba(path: Path, w: int, h: int, color: tuple[int, int, int, int]) -> None:
    arr = np.zeros((h, w, 4), dtype=np.uint8)
    arr[..., 0] = color[0]
    arr[..., 1] = color[1]
    arr[..., 2] = color[2]
    arr[..., 3] = color[3]
    Image.fromarray(arr, mode="RGBA").save(path)


class TestNativeSpatialSmoke(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self) -> None:
        self.tmp.cleanup()

    def _make_minimal_dataset(self) -> None:
        (self.root / "splits").mkdir(parents=True)
        (self.root / "a").mkdir(parents=True)
        (self.root / "b").mkdir(parents=True)
        _write_rgba(self.root / "a" / "d.png", 16, 16, (100, 100, 100, 255))
        _write_rgba(self.root / "a" / "d_s.png", 16, 16, (40, 180, 230, 255))
        _write_rgba(self.root / "b" / "d.png", 32, 32, (50, 50, 50, 255))
        _write_rgba(self.root / "b" / "d_s.png", 32, 32, (40, 180, 230, 255))
        manifest = [
            {
                "id": "a",
                "image": "a/d.png",
                "label_spec": "a/d_s.png",
                "width": 16,
                "height": 16,
                "tagged_resolution": 16,
                "tagged_resolution_known": True,
            },
            {
                "id": "b",
                "image": "b/d.png",
                "label_spec": "b/d_s.png",
                "width": 32,
                "height": 32,
                "tagged_resolution": 32,
                "tagged_resolution_known": True,
            },
        ]
        with (self.root / "manifest.jsonl").open("w", encoding="utf-8") as f:
            for rec in manifest:
                f.write(json.dumps(rec) + "\n")
        (self.root / "splits" / "train.txt").write_text("a\nb\n", encoding="utf-8")
        (self.root / "splits" / "val.txt").write_text("a\n", encoding="utf-8")

    def test_bucket_sampler_and_forward(self) -> None:
        self._make_minimal_dataset()
        ds = SpecularManifestDataset(
            self.root,
            "train",
            spatial_mode="native",
            downscale_policy=parse_downscale_policy("box"),
        )
        self.assertEqual(ds.spatial_keys.count((16, 16)), 1)
        self.assertEqual(ds.spatial_keys.count((32, 32)), 1)
        sampler = BucketBatchSampler(ds.spatial_keys, batch_size=2, shuffle=False, generator=None)
        batches = list(iter(sampler))
        self.assertEqual(len(batches), 2)
        for batch in batches:
            xs = [ds[i][0] for i in batch]
            shapes = {x.shape for x in xs}
            self.assertEqual(len(shapes), 1)
        net = DilatedPbrNet(in_channels=4, out_channels=4, width=16)
        x, y, v = ds[0]
        raw = net(x.unsqueeze(0))
        loss = spec_loss(raw, y.unsqueeze(0), v.unsqueeze(0), transparent_zero_weight=0.0)
        self.assertTrue(torch.isfinite(loss))

    def test_tag_mismatch(self) -> None:
        tier, mm = assign_bucket_tier(64, 64, tagged_resolution=16, tagged_resolution_known=True)
        self.assertEqual(tier, 64)
        self.assertTrue(mm)

    def test_native_per_sample_tag_match_filter(self) -> None:
        self._make_minimal_dataset()
        # Inject one mismatch record: 64x64 image tagged as 16.
        (self.root / "c").mkdir(parents=True)
        _write_rgba(self.root / "c" / "d.png", 64, 64, (60, 60, 60, 255))
        _write_rgba(self.root / "c" / "d_s.png", 64, 64, (40, 180, 230, 255))
        with (self.root / "manifest.jsonl").open("a", encoding="utf-8") as f:
            f.write(
                json.dumps(
                    {
                        "id": "c",
                        "image": "c/d.png",
                        "label_spec": "c/d_s.png",
                        "width": 64,
                        "height": 64,
                        "tagged_resolution": 16,
                        "tagged_resolution_known": True,
                    }
                )
                + "\n"
            )
        (self.root / "splits" / "train.txt").write_text("a\nb\nc\n", encoding="utf-8")

        ds = SpecularManifestDataset(
            self.root,
            "train",
            spatial_mode="native",
            native_restrict_to_target_tier=True,
            downscale_policy=parse_downscale_policy("box"),
        )
        # Mixed tagged tiers (16 and 32) both remain; only the mismatched sample is dropped.
        self.assertEqual(len(ds), 2)
        self.assertEqual(ds.spatial_keys.count((16, 16)), 1)
        self.assertEqual(ds.spatial_keys.count((32, 32)), 1)
        self.assertEqual(ds.native_filter_stats.get("dropped_tag_mismatch", 0), 1)

    def test_batch_policy_lr_scaling(self) -> None:
        args = SimpleNamespace(
            lr=1e-3,
            epochs=10,
            batch_policy_enabled=True,
            batch_policy_lr_mode="sqrt",
            batch_policy_baseline_batch=8,
            batch_policy_baseline_lr=1e-3,
            batch_policy_max_lr=None,
            warmup_ratio=0.1,
            warmup_min_steps=20,
            weight_decay_mode="off",
            grad_clip_norm=0.0,
        )
        p = _derive_batch_policy(args, effective_batch=128, steps_per_epoch=100)
        self.assertAlmostEqual(p["lr"], 0.004, places=6)
        self.assertEqual(p["warmup_steps"], 100)

    def test_batch_policy_weight_decay_bounded(self) -> None:
        args = SimpleNamespace(
            lr=1e-3,
            epochs=20,
            batch_policy_enabled=True,
            batch_policy_lr_mode="linear",
            batch_policy_baseline_batch=8,
            batch_policy_baseline_lr=1e-3,
            batch_policy_max_lr=0.02,
            warmup_ratio=0.05,
            warmup_min_steps=500,
            weight_decay_mode="mild_batch_scaled",
            grad_clip_norm=1.0,
        )
        p = _derive_batch_policy(args, effective_batch=1024, steps_per_epoch=10)
        self.assertLessEqual(p["weight_decay"], 3e-4)
        self.assertGreaterEqual(p["weight_decay"], 1e-4)


if __name__ == "__main__":
    unittest.main()
