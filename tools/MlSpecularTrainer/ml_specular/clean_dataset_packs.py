"""
Remove loose PNGs under dataset packs/ that are not part of LabPBR-style pairings.

Keeps:
  - Triplets: foo.png + foo_n.png + foo_s.png (same folder, same base stem)
  - Doubles: any two of diffuse / _n / _s present for the same base
  - Emissive: foo_e.png when foo.png exists (same base), for future emissive training

Pairing rules use only PNGs under the same accepted texture trees as gen_from_labpbr_packs; the
**keep** set is built from valid d/n/s (and optional _e) groups there. Any other **.png** under
``assets/`` (solo files, wrong folders, or orphans without a pair) is then removed.

Also (by default) removes all other files under each pack's assets/ tree (JSON, .mcmeta, .txt,
sounds, etc.) so only PNG textures remain for a slim training corpus. Pack-root files
(pack.mcmeta, pack.png next to assets/) are never touched.

Usage:
  python -m ml_specular.clean_dataset_packs --dry-run
  python -m ml_specular.clean_dataset_packs --dataset-root multi_dataset
"""

from __future__ import annotations

import argparse
import sys
from collections import defaultdict
from pathlib import Path
from typing import Literal

from ml_specular.labpbr_pack_scan import iter_pack_roots, is_in_accepted_texture_tree

Kind = Literal["d", "n", "s", "e"]

DEFAULT_DATASET_FOLDERS = (
    "multi_dataset",
    "pixelart_dataset",
    "realism_dataset",
    "stylized_dataset",
    "ext_dataset",
)


def parse_texture_stem(stem: str) -> tuple[str, Kind]:
    """
    Map filename stem to (base_stem, kind).
    base_stem is the shared prefix without _n/_s/_e labpbr suffix.
    Use removesuffix (not [:-2]) so bases like torchflower_n -> torchflower stay correct.
    """
    sl = stem.lower()
    if sl.endswith("_e"):
        return stem.removesuffix("_e"), "e"
    if sl.endswith("_s"):
        return stem.removesuffix("_s"), "s"
    if sl.endswith("_n"):
        return stem.removesuffix("_n"), "n"
    return stem, "d"


def collect_accepted_pngs(pack_root: Path) -> list[Path]:
    """PNG paths under assets/ that lie in LabPBR training scan roots (for pairing only)."""
    assets = pack_root / "assets"
    if not assets.is_dir():
        return []
    out: list[Path] = []
    for p in assets.rglob("*.png"):
        if not p.is_file():
            continue
        if not is_in_accepted_texture_tree(p):
            continue
        out.append(p)
    return out


def collect_all_pngs_under_assets(pack_root: Path) -> list[Path]:
    """Every .png file under pack_root/assets/ (for deletion pass vs. keep set)."""
    assets = pack_root / "assets"
    if not assets.is_dir():
        return []
    return sorted(p for p in assets.rglob("*.png") if p.is_file())


def build_keep_set(pack_root: Path) -> set[Path]:
    """Paths to keep (resolve() for stable comparison)."""
    pngs = collect_accepted_pngs(pack_root)
    # (parent, base_stem) -> kind -> path
    groups: dict[tuple[Path, str], dict[Kind, Path]] = defaultdict(dict)

    for p in pngs:
        parent = p.parent.resolve()
        base, kind = parse_texture_stem(p.stem)
        g = groups[(parent, base)]
        if kind in g:
            # Duplicate stem kind; keep first, second could be deleted later by not being in keep
            continue
        g[kind] = p

    keep: set[Path] = set()
    for _key, g in groups.items():
        d, n, s, e = g.get("d"), g.get("n"), g.get("s"), g.get("e")
        pbr_count = sum(1 for x in (d, n, s) if x is not None)
        if pbr_count >= 2:
            for x in (d, n, s):
                if x is not None:
                    keep.add(x.resolve())
            if e is not None and d is not None:
                keep.add(e.resolve())
            continue
        if d is not None and e is not None:
            keep.add(d.resolve())
            keep.add(e.resolve())
        # else: orphan singles — not added

    return keep


def clean_loose_pngs_in_pack(pack_root: Path, dry_run: bool) -> tuple[int, int]:
    """
    Remove every PNG under assets/ that is not in the keep set (valid pairings in accepted trees).
    Includes solo PNGs outside block/items/entity/… trees and orphans with no _n/_s/_e pair.
    Returns (deleted_count, keep_count).
    """
    keep = build_keep_set(pack_root)
    deleted = 0
    for p in collect_all_pngs_under_assets(pack_root):
        rp = p.resolve()
        if rp in keep:
            continue
        if dry_run:
            print(f"  [dry-run] would delete PNG: {p}")
        else:
            p.unlink()
            print(f"  deleted PNG: {p}")
        deleted += 1
    return deleted, len(keep)


def clean_non_png_under_assets(pack_root: Path, dry_run: bool) -> int:
    """
    Delete every file under pack_root/assets/ that is not a .png (case-insensitive).
    Then remove empty directories under assets/ (deepest first).
    """
    assets = pack_root / "assets"
    if not assets.is_dir():
        return 0

    deleted = 0
    all_paths = list(assets.rglob("*"))
    # Files first (deepest paths first so we do not rely on order for dirs)
    files = [p for p in all_paths if p.is_file()]
    files.sort(key=lambda p: len(p.parts), reverse=True)
    for p in files:
        if p.suffix.lower() == ".png":
            continue
        if dry_run:
            print(f"  [dry-run] would delete non-PNG: {p}")
        else:
            p.unlink()
            print(f"  deleted non-PNG: {p}")
        deleted += 1

    dirs = [p for p in all_paths if p.is_dir()]
    dirs.sort(key=lambda p: len(p.parts), reverse=True)
    for d in dirs:
        if dry_run:
            continue
        try:
            d.rmdir()
        except OSError:
            pass

    return deleted


def clean_pack(pack_root: Path, dry_run: bool, remove_other_assets: bool) -> tuple[int, int, int]:
    """Returns (png_deleted, png_kept, non_png_deleted)."""
    png_del, png_keep = clean_loose_pngs_in_pack(pack_root, dry_run=dry_run)
    other = 0
    if remove_other_assets:
        other = clean_non_png_under_assets(pack_root, dry_run=dry_run)
    return png_del, png_keep, other


def clean_dataset_root(root: Path, packs_subdir: str, dry_run: bool, remove_other_assets: bool) -> int:
    packs_dir = (root / packs_subdir).resolve()
    if not packs_dir.is_dir():
        print(f"Missing packs directory: {packs_dir}", file=sys.stderr)
        return 1

    total_png = 0
    total_other = 0
    for pack_root in iter_pack_roots(packs_dir):
        print(f"Pack: {pack_root.name}")
        n_png, n_keep, n_other = clean_pack(
            pack_root, dry_run=dry_run, remove_other_assets=remove_other_assets
        )
        print(
            f"  removed {n_png} loose PNG(s); keeping {n_keep} PNG(s) in pairings."
            + (f" removed {n_other} non-PNG file(s) under assets/." if remove_other_assets else "")
        )
        total_png += n_png
        total_other += n_other
    print(
        f"Dataset {root.name}: total removed {total_png} loose PNG(s)"
        + (f", {total_other} non-PNG asset file(s)." if remove_other_assets else ".")
    )
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--dataset-root",
        type=Path,
        default=None,
        help=f"Single dataset folder under the trainer directory (contains packs/). "
        f"If omitted, cleans each of: {', '.join(DEFAULT_DATASET_FOLDERS)}.",
    )
    ap.add_argument(
        "--all-default-datasets",
        action="store_true",
        help=argparse.SUPPRESS,
    )
    ap.add_argument("--packs-subdir", type=str, default="packs")
    ap.add_argument(
        "--dry-run",
        action="store_true",
        help="Print actions without deleting files.",
    )
    ap.add_argument(
        "--keep-other-assets",
        action="store_true",
        help="Do not delete JSON, .mcmeta, sounds, etc. under packs/*/assets/ (only clean loose PNGs).",
    )
    args = ap.parse_args(argv)

    script_dir = Path(__file__).resolve().parent.parent
    trainer_root = script_dir

    if args.dataset_root is not None:
        root = args.dataset_root.resolve()
        if not root.is_dir():
            print(f"Not a directory: {root}", file=sys.stderr)
            return 1
        return clean_dataset_root(
            root,
            args.packs_subdir,
            dry_run=args.dry_run,
            remove_other_assets=not args.keep_other_assets,
        )

    # Default: all standard dataset folders (same as --all-default-datasets).
    code = 0
    for name in DEFAULT_DATASET_FOLDERS:
        root = trainer_root / name
        print(f"\n=== {name} ===")
        if not root.is_dir():
            print(f"  (skip: not a directory: {root})")
            continue
        r = clean_dataset_root(
            root,
            args.packs_subdir,
            dry_run=args.dry_run,
            remove_other_assets=not args.keep_other_assets,
        )
        if r != 0:
            code = r
    return code


if __name__ == "__main__":
    raise SystemExit(main())
