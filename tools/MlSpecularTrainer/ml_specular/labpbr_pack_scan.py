"""
Scan pre-extracted resource packs for LabPBR-style triplets: diffuse + _n + _s.

Matches the app scanner's accepted locations:
  - assets/<namespace>/textures/{block,blocks,item,items,entity,particle}/**
  - assets/<namespace>/optifine/ctm/**
  - assets/<namespace>/optifine/{plant,plants}/**
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path


# [16x], [32x], ... [512x] at start of pack folder name (case-insensitive).
_TAGGED_RES_RE = re.compile(r"^\s*\[(\d+)x\]\s*", re.IGNORECASE)

KNOWN_TAG_RESOLUTIONS = frozenset({16, 32, 64, 128, 256, 512})


def is_labpbr_emissive_filename(name: str) -> bool:
    """True if basename (with or without .png) is a LabPBR-style emissive map (*_e)."""
    stem = Path(name).stem
    return stem.lower().endswith("_e")


def diffuse_stem_from_specular_name(spec_name: str) -> str | None:
    """For foo_s.png return 'foo'; not a specular name => None."""
    stem = Path(spec_name).stem
    sl = stem.lower()
    if not sl.endswith("_s"):
        return None
    return stem[: -len("_s")]


@dataclass(frozen=True)
class PackInfo:
    """Metadata derived from a pack root directory name."""

    folder_name: str
    tagged_resolution: int | None  # from [Nx] prefix; None if absent or non-standard number
    tagged_is_known: bool  # True if tagged_resolution in KNOWN_TAG_RESOLUTIONS


def parse_pack_folder_name(folder_name: str) -> PackInfo:
    m = _TAGGED_RES_RE.match(folder_name)
    if not m:
        return PackInfo(folder_name=folder_name, tagged_resolution=None, tagged_is_known=False)
    n = int(m.group(1))
    known = n in KNOWN_TAG_RESOLUTIONS
    return PackInfo(folder_name=folder_name, tagged_resolution=n if known else n, tagged_is_known=known)


def iter_pack_roots(packs_dir: Path) -> list[Path]:
    """Immediate subdirectories of packs_dir (each is one resource pack)."""
    if not packs_dir.is_dir():
        return []
    return sorted(p for p in packs_dir.iterdir() if p.is_dir())


def _assets_index(parts: tuple[str, ...]) -> int | None:
    for i, p in enumerate(parts):
        if p.lower() == "assets":
            return i
    return None


def _namespace_relative_parts(path: Path) -> tuple[str, ...] | None:
    parts = path.parts
    i = _assets_index(parts)
    if i is None:
        return None
    # assets/<namespace>/...
    if len(parts) <= i + 2:
        return None
    return tuple(p.lower() for p in parts[i + 2 :])


def is_in_accepted_texture_tree(path: Path, *, include_optifine: bool = True) -> bool:
    rel = _namespace_relative_parts(path)
    if rel is None or len(rel) < 2:
        return False

    # assets/<ns>/textures/<folder>/...
    if rel[0] == "textures":
        if len(rel) < 3:
            return False
        return rel[1] in {"block", "blocks", "item", "items", "entity", "particle"}

    # assets/<ns>/optifine/<folder>/...
    if rel[0] == "optifine":
        if not include_optifine:
            return False
        if len(rel) < 3:
            return False
        return rel[1] in {"ctm", "plant", "plants"}

    return False


# Same folder sets as `is_in_accepted_texture_tree` (avoid scanning all of `assets/`).
_TEXTURE_SUBDIRS = frozenset({"block", "blocks", "item", "items", "entity", "particle"})
_OPTIFINE_SUBDIRS = frozenset({"ctm", "plant", "plants"})


def iter_specular_paths(pack_root: Path, *, include_optifine: bool = True) -> list[Path]:
    """All *_s.png under app-accepted texture roots (literal suffix _s.png)."""
    out: list[Path] = []
    assets = pack_root / "assets"
    if not assets.is_dir():
        return out
    for ns_dir in sorted(p for p in assets.iterdir() if p.is_dir()):
        tex = ns_dir / "textures"
        if tex.is_dir():
            for subdir in tex.iterdir():
                if not subdir.is_dir() or subdir.name.lower() not in _TEXTURE_SUBDIRS:
                    continue
                for p in subdir.rglob("*_s.png"):
                    if not p.is_file() or p.suffix.lower() != ".png":
                        continue
                    base = diffuse_stem_from_specular_name(p.name)
                    if base is not None and base.lower().endswith("_e"):
                        continue
                    out.append(p)
        if include_optifine:
            opt = ns_dir / "optifine"
            if opt.is_dir():
                for subdir in opt.iterdir():
                    if not subdir.is_dir() or subdir.name.lower() not in _OPTIFINE_SUBDIRS:
                        continue
                    for p in subdir.rglob("*_s.png"):
                        if not p.is_file() or p.suffix.lower() != ".png":
                            continue
                        base = diffuse_stem_from_specular_name(p.name)
                        if base is not None and base.lower().endswith("_e"):
                            continue
                        out.append(p)
    return sorted(out)


def diffuse_path_from_specular(spec_path: Path) -> Path:
    stem = spec_path.name
    if not stem.lower().endswith("_s.png"):
        raise ValueError(f"Not a _s texture: {spec_path}")
    base = stem[: -len("_s.png")] + ".png"
    return spec_path.with_name(base)


def normal_path_from_specular(spec_path: Path) -> Path:
    stem = spec_path.name
    if not stem.lower().endswith("_s.png"):
        raise ValueError(f"Not a _s texture: {spec_path}")
    base = stem[: -len("_s.png")] + "_n.png"
    return spec_path.with_name(base)


def resolve_triplet(spec_path: Path) -> tuple[Path, Path, Path] | None:
    """
    Returns (diffuse, normal, specular) if all three exist; else None.
    """
    if not spec_path.is_file():
        return None
    d = diffuse_path_from_specular(spec_path)
    n = normal_path_from_specular(spec_path)
    if d.is_file() and n.is_file():
        return (d, n, spec_path)
    return None


def make_entry_id(pack_folder_name: str, diffuse_path: Path, pack_root: Path) -> str:
    """Filesystem-safe unique id (truncated)."""
    rel = diffuse_path.relative_to(pack_root)
    rel_key = rel.as_posix().replace("/", "__")
    if rel_key.lower().endswith(".png"):
        rel_key = rel_key[:-4]
    slug = re.sub(r"[^a-zA-Z0-9._-]+", "_", pack_folder_name).strip("_").lower()
    if not slug:
        slug = "pack"
    raw = f"{slug}__{rel_key}"
    return raw[:200]
