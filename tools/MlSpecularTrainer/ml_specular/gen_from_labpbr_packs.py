"""
Build manifest.jsonl from pre-extracted LabPBR packs.

Each entry has `image` (diffuse) and `label_spec` (artist `_s.png` under packs/) for `train_spec.py`.

Expects each pack as a subfolder of <dataset>/packs/ (e.g. sample_dataset/packs/[16x] UltimaCraft).
Only uses triplets: diffuse.png + same-stem _n.png + _s.png under accepted texture roots.

Resolution tag: folder names may start with [16x], [32x], ... [512x] (case-insensitive).
If present, stored as tagged_resolution; if actual pixel size differs, recorded in width/height
and optionally skipped with --strict-tagged-size.
"""

from __future__ import annotations

import argparse
import json
import os
import random
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

from PIL import Image

from ml_specular.labpbr_pack_scan import (
    PackInfo,
    is_labpbr_emissive_filename,
    iter_pack_roots,
    iter_specular_paths,
    make_entry_id,
    parse_pack_folder_name,
    resolve_triplet,
)


def _merge_skipped(dst: dict[str, int], src: dict[str, int]) -> None:
    for k, v in src.items():
        dst[k] = dst.get(k, 0) + v


def _resolve_manifest_workers(requested: int, degree: int) -> int:
    """degree = number of tasks (pack folders or spec files) to spread across threads."""
    if degree <= 1:
        return 1
    if requested == 1:
        return 1
    if requested > 1:
        return min(requested, degree)
    cpu = os.cpu_count() or 4
    return min(32, degree, max(2, cpu * 2))


def _resolve_pack_parallel_workers(requested: int, num_packs: int) -> int:
    """Thread count for scanning multiple pack folders (one task per pack)."""
    if num_packs <= 1:
        return 1
    return _resolve_manifest_workers(requested, num_packs)


def _try_one_spec_manifest(
    spec_path: Path,
    *,
    pack_name: str,
    dataset_root: Path,
    pack_root: Path,
    strict_tagged_size: bool,
    pinfo: PackInfo,
) -> tuple[dict | None, dict[str, int]]:
    skipped: dict[str, int] = {}

    def bump(key: str) -> None:
        skipped[key] = skipped.get(key, 0) + 1

    triplet = resolve_triplet(spec_path)
    if triplet is None:
        bump("missing_diffuse_or_normal")
        return None, skipped
    diffuse_path, _normal_path, sp_path = triplet
    if is_labpbr_emissive_filename(diffuse_path.name):
        bump("skip_emissive_diffuse_stem")
        return None, skipped
    try:
        rel_diffuse = diffuse_path.relative_to(dataset_root)
    except ValueError:
        bump("diffuse_not_under_dataset_root")
        return None, skipped

    entry_id = make_entry_id(pack_name, diffuse_path, pack_root)

    try:
        with Image.open(diffuse_path) as img_d, Image.open(sp_path) as img_s:
            w, h = img_d.size
            if img_s.size != (w, h):
                bump("size_mismatch_diffuse_spec")
                return None, skipped
    except OSError as e:
        bump(f"open_error:{e}")
        return None, skipped

    if pinfo.tagged_resolution is not None and strict_tagged_size:
        if w != pinfo.tagged_resolution or h != pinfo.tagged_resolution:
            bump("strict_tagged_size_mismatch")
            return None, skipped

    tagged = pinfo.tagged_resolution
    style = f"pixel_{tagged}" if tagged is not None else f"pixel_{max(w, h)}"

    rec = {
        "id": entry_id,
        "image": rel_diffuse.as_posix(),
        "label_spec": sp_path.relative_to(dataset_root).as_posix(),
        "pack": pack_name,
        "tagged_resolution": tagged,
        "tagged_resolution_known": pinfo.tagged_is_known,
        "width": w,
        "height": h,
        "style": style,
        "source": "labpbr_triplet",
    }

    return rec, skipped


def _collect_pack_manifest(
    pack_root: Path,
    dataset_root: Path,
    *,
    include_optifine: bool,
    strict_tagged_size: bool,
    manifest_workers_requested: int,
    parallelize_specs: bool,
) -> tuple[list[dict], dict[str, int], int]:
    records: list[dict] = []
    skipped: dict[str, int] = {}

    pack_name = pack_root.name
    pinfo = parse_pack_folder_name(pack_name)
    spec_paths = iter_specular_paths(pack_root, include_optifine=include_optifine)

    spec_workers = 1
    if parallelize_specs:
        spec_workers = _resolve_manifest_workers(manifest_workers_requested, len(spec_paths))

    use_spec_pool = parallelize_specs and spec_workers > 1 and len(spec_paths) > 1

    if use_spec_pool:

        def work(sp: Path) -> tuple[dict | None, dict[str, int]]:
            return _try_one_spec_manifest(
                sp,
                pack_name=pack_name,
                dataset_root=dataset_root,
                pack_root=pack_root,
                strict_tagged_size=strict_tagged_size,
                pinfo=pinfo,
            )

        with ThreadPoolExecutor(max_workers=spec_workers) as ex:
            for rec, sk in ex.map(work, spec_paths):
                if rec is not None:
                    records.append(rec)
                _merge_skipped(skipped, sk)
        return records, skipped, spec_workers

    for spec_path in spec_paths:
        rec, sk = _try_one_spec_manifest(
            spec_path,
            pack_name=pack_name,
            dataset_root=dataset_root,
            pack_root=pack_root,
            strict_tagged_size=strict_tagged_size,
            pinfo=pinfo,
        )
        if rec is not None:
            records.append(rec)
        _merge_skipped(skipped, sk)

    return records, skipped, 1


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--dataset-root",
        type=Path,
        default=Path("sample_dataset"),
        help="Dataset root (will contain manifest.jsonl, splits/).",
    )
    ap.add_argument(
        "--packs-subdir",
        type=str,
        default="packs",
        help="Subdirectory of dataset-root holding one folder per resource pack.",
    )
    ap.add_argument(
        "--strict-tagged-size",
        action="store_true",
        help="Skip textures where width/height != tagged [Nx] when a tag is present.",
    )
    ap.add_argument(
        "--val-fraction",
        type=float,
        default=0.05,
        help="Fraction of textures per pack assigned to val (rest train). test.txt mirrors val for tiny setups.",
    )
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument(
        "--ignore-optifine",
        action="store_true",
        help="Exclude assets/<namespace>/optifine/** (ctm, plants) from the manifest; use only textures/** trees.",
    )
    ap.add_argument(
        "--manifest-workers",
        type=int,
        default=0,
        help="Parallel manifest threads. 0=auto: multiple packs => one thread per pack; "
        "single pack => parallelize over _s candidates. 1=fully sequential.",
    )
    args = ap.parse_args(argv)

    rng = random.Random(args.seed)
    root: Path = args.dataset_root.resolve()
    packs_dir = (root / args.packs_subdir).resolve()

    if not packs_dir.is_dir():
        print(f"Missing packs directory: {packs_dir}", file=sys.stderr)
        return 1

    splits_dir = root / "splits"
    if not args.dry_run:
        splits_dir.mkdir(parents=True, exist_ok=True)

    records: list[dict] = []
    skipped: dict[str, int] = {}
    pack_roots = iter_pack_roots(packs_dir)
    n_pack_workers = _resolve_pack_parallel_workers(args.manifest_workers, len(pack_roots))
    inc_opt = not args.ignore_optifine
    single_pack_dataset = len(pack_roots) == 1
    max_spec_workers = 1

    if n_pack_workers <= 1:
        for pack_root in pack_roots:
            recs, sk, sw = _collect_pack_manifest(
                pack_root,
                root,
                include_optifine=inc_opt,
                strict_tagged_size=args.strict_tagged_size,
                manifest_workers_requested=args.manifest_workers,
                parallelize_specs=single_pack_dataset,
            )
            records.extend(recs)
            _merge_skipped(skipped, sk)
            max_spec_workers = max(max_spec_workers, sw)
    else:
        with ThreadPoolExecutor(max_workers=n_pack_workers) as ex:
            futs = [
                ex.submit(
                    _collect_pack_manifest,
                    pr,
                    root,
                    include_optifine=inc_opt,
                    strict_tagged_size=args.strict_tagged_size,
                    manifest_workers_requested=args.manifest_workers,
                    parallelize_specs=False,
                )
                for pr in pack_roots
            ]
            for fut in as_completed(futs):
                recs, sk, sw = fut.result()
                records.extend(recs)
                _merge_skipped(skipped, sk)
                max_spec_workers = max(max_spec_workers, sw)

    if not records:
        print("No LabPBR triplets found. Check packs under:", packs_dir, file=sys.stderr)
        print("Skipped counts:", skipped, file=sys.stderr)
        return 1

    by_pack: dict[str, list[dict]] = {}
    for r in records:
        by_pack.setdefault(r["pack"], []).append(r)

    train_ids: list[str] = []
    val_ids: list[str] = []
    for _pname, plist in by_pack.items():
        ids = [r["id"] for r in plist]
        rng.shuffle(ids)
        n_val = int(round(len(ids) * args.val_fraction))
        n_val = min(max(n_val, 0), len(ids))
        val_ids.extend(ids[:n_val])
        train_ids.extend(ids[n_val:])

    if not val_ids and len(train_ids) > 1:
        val_ids.append(train_ids.pop())

    if not args.dry_run:
        manifest_path = root / "manifest.jsonl"
        with manifest_path.open("w", encoding="utf-8") as f:
            for r in records:
                f.write(json.dumps(r, ensure_ascii=False) + "\n")

        (splits_dir / "train.txt").write_text("\n".join(sorted(train_ids)) + "\n", encoding="utf-8")
        (splits_dir / "val.txt").write_text("\n".join(sorted(val_ids)) + "\n", encoding="utf-8")
        (splits_dir / "test.txt").write_text("\n".join(sorted(val_ids)) + "\n", encoding="utf-8")

    print(f"Packs dir: {packs_dir}")
    if len(pack_roots) > 1 and n_pack_workers > 1:
        print(f"Manifest workers: {n_pack_workers} (parallel packs)")
    elif len(pack_roots) == 1 and max_spec_workers > 1:
        print(f"Manifest workers: {max_spec_workers} (parallel specs, 1 pack)")
    else:
        print("Manifest workers: 1 (sequential)")
    print(f"Manifest entries: {len(records)}")
    print(f"train={len(train_ids)} val={len(val_ids)}")
    if skipped:
        print("Skipped:", dict(sorted(skipped.items(), key=lambda x: -x[1])[:20]))
    if args.dry_run:
        print("(dry-run: no files written)")
    else:
        print(f"Wrote {root / 'manifest.jsonl'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
