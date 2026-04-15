"""Detect ORTModule torch_cpp_extensions without importing torch or onnxruntime."""

from __future__ import annotations

import site
import sys
from pathlib import Path


def iter_site_packages_roots() -> list[Path]:
    """Directories that may contain ``onnxruntime/training/...`` (no ``import onnxruntime``)."""
    roots: list[Path] = []
    seen: set[str] = set()

    def _add(p: Path) -> None:
        try:
            key = str(p.resolve())
        except OSError:
            key = str(p)
        if key not in seen and p.is_dir():
            seen.add(key)
            roots.append(p)

    try:
        for sp in site.getsitepackages():
            _add(Path(sp))
    except Exception:
        pass
    try:
        us = site.getusersitepackages()
        if us:
            _add(Path(us))
    except Exception:
        pass
    _add(Path(sys.prefix) / "lib" / f"python{sys.version_info.major}.{sys.version_info.minor}" / "site-packages")
    for entry in sys.path:
        if not entry or entry == ".":
            continue
        p = Path(entry)
        if p.name == "site-packages" and p.is_dir():
            _add(p)
        if (p / "onnxruntime" / "training").is_dir():
            _add(p)
    return roots


def _torch_cpp_extensions_rel() -> Path:
    return Path("onnxruntime") / "training" / "ortmodule" / "torch_cpp_extensions"


def _iter_ortmodule_torch_extensions_dirs() -> list[Path]:
    """Every ``.../torch_cpp_extensions`` directory found under scanned site-packages roots."""
    rel = _torch_cpp_extensions_rel()
    out: list[Path] = []
    seen: set[str] = set()
    for root in iter_site_packages_roots():
        ext = (root / rel).resolve()
        try:
            key = str(ext)
        except OSError:
            key = str(root / rel)
        if key in seen:
            continue
        if ext.is_dir():
            seen.add(key)
            out.append(ext)
    return out


def _dir_has_extension_artifacts(ext_dir: Path) -> bool:
    for pattern in ("*.so", "*.dll", "*.dylib", "*.pyd"):
        if any(ext_dir.rglob(pattern)):
            return True
    return False


def get_ortmodule_torch_extensions_dir() -> Path | None:
    """Path to ORT ``torch_cpp_extensions`` without importing onnxruntime.

    When multiple site-packages trees each contain a ``torch_cpp_extensions`` folder (e.g. conda
    stub + venv install), prefer the directory that actually contains built ``*.so`` files; among
    those, prefer ``sys.prefix`` (the active venv) so normalize matches ``torch_ort.configure``.
    """
    candidates = _iter_ortmodule_torch_extensions_dirs()
    if not candidates:
        return None
    try:
        pref_r = (
            Path(sys.prefix).resolve()
            / "lib"
            / f"python{sys.version_info.major}.{sys.version_info.minor}"
            / "site-packages"
            / _torch_cpp_extensions_rel()
        ).resolve()
    except OSError:
        pref_r = None
    with_so = [e for e in candidates if _dir_has_extension_artifacts(e)]
    if with_so and pref_r is not None:
        for e in with_so:
            try:
                if e.resolve() == pref_r:
                    return e
            except OSError:
                continue
        return with_so[0]
    if with_so:
        return with_so[0]
    # No compiled artifacts yet: prefer the tree under sys.prefix to match pip/configure target.
    if pref_r is not None:
        for ext in candidates:
            try:
                if ext.resolve() == pref_r:
                    return ext
            except OSError:
                continue
    return candidates[0]


def ortmodule_extensions_built() -> bool:
    """True if ORTModule can see a built extension under its ``torch_cpp_extensions`` tree.

    ONNX Runtime's ORTModule frontend only checks the install under
    ``site-packages/.../onnxruntime/training/ortmodule/torch_cpp_extensions``.
    Builds that exist only under ``~/.cache/torch_extensions`` are **not** sufficient and
    must not skip ``python -m torch_ort.configure``.
    """
    for ext_dir in _iter_ortmodule_torch_extensions_dirs():
        if _dir_has_extension_artifacts(ext_dir):
            return True
    return False
