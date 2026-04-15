"""Prepare Linux dynamic linker paths before ``import torch`` (Docker / venv without inherited ``LD_LIBRARY_PATH``).

Imported from ``ml_specular.__init__`` so every package entry point sees libcudnn.so.9 (e.g. under
``/opt/conda/lib/python*/site-packages/nvidia/cudnn/lib``) before any submodule loads PyTorch.
"""

from __future__ import annotations

import os
import sys
from pathlib import Path


def _nvidia_pip_lib_dirs(parent_lib: Path) -> list[Path]:
    """Pip NVIDIA wheels (e.g. nvidia-cudnn) install under ``{parent_lib}/python*/site-packages/nvidia/<pkg>/lib``."""
    out: list[Path] = []
    if not parent_lib.is_dir():
        return out
    try:
        for np in parent_lib.glob("python*/site-packages/nvidia"):
            if not np.is_dir():
                continue
            for sub in sorted(np.iterdir()):
                lib = sub / "lib"
                if lib.is_dir():
                    out.append(lib)
    except OSError:
        pass
    return out


def _linux_torch_cuda_lib_roots(prefix: Path) -> list[Path]:
    roots: list[Path] = []
    for p in (
        Path("/opt/conda/lib"),
        Path("/usr/local/cuda/lib64"),
        prefix,
    ):
        if p.is_dir():
            roots.append(p)
    conda_envs = Path("/opt/conda/envs")
    if conda_envs.is_dir():
        for env in sorted(conda_envs.iterdir()):
            elib = env / "lib"
            if elib.is_dir():
                roots.append(elib)
                roots.extend(_nvidia_pip_lib_dirs(elib))
    conda_base_lib = Path("/opt/conda/lib")
    if conda_base_lib.is_dir():
        roots.extend(_nvidia_pip_lib_dirs(conda_base_lib))
    torch_lib = next(prefix.glob("lib/python*/site-packages/torch/lib"), None)
    if torch_lib is not None and torch_lib.is_dir():
        roots.append(torch_lib)
    venv_lib = prefix / "lib"
    roots.extend(_nvidia_pip_lib_dirs(venv_lib))
    out: list[Path] = []
    seen: set[Path] = set()
    for r in roots:
        try:
            rp = r.resolve()
        except OSError:
            rp = r
        if rp not in seen:
            seen.add(rp)
            out.append(r)
    return out


def prepare_linux_torch_cuda_libs() -> None:
    """Set ``LD_LIBRARY_PATH`` and preload libcudnn.so.9 where needed."""
    if sys.platform != "linux":
        return
    import ctypes

    prefix = Path(getattr(sys, "prefix", "") or ".")
    roots = _linux_torch_cuda_lib_roots(prefix)
    cudnn9_parent_dirs: list[str] = []
    for root in roots:
        if not root.is_dir():
            continue
        try:
            if any(root.glob("libcudnn.so.9*")):
                cudnn9_parent_dirs.append(str(root.resolve()))
        except OSError:
            continue

    extra: list[str] = []
    torch_lib = next(prefix.glob("lib/python*/site-packages/torch/lib"), None)
    if torch_lib is not None and torch_lib.is_dir():
        extra.append(str(torch_lib.resolve()))
    for d in ("/opt/conda/lib", "/usr/local/cuda/lib64"):
        if os.path.isdir(d):
            extra.append(d)
    cudnn8_file = prefix / "cudnn8_lib_path"
    if cudnn8_file.is_file():
        try:
            line = cudnn8_file.read_text(encoding="utf-8").strip()
        except OSError:
            line = ""
        if line and os.path.isdir(line):
            extra.append(line)

    order: list[str] = []
    for item in cudnn9_parent_dirs + extra:
        if item not in order:
            order.append(item)
    cur = os.environ.get("LD_LIBRARY_PATH", "")
    if order:
        os.environ["LD_LIBRARY_PATH"] = ":".join(order + ([cur] if cur else []))

    for root in roots:
        if not root.is_dir():
            continue
        candidates = sorted(root.glob("libcudnn.so.9*"))
        for cand in candidates:
            if not (cand.is_file() or (cand.is_symlink() and os.path.lexists(cand))):
                continue
            try:
                ctypes.CDLL(str(cand), mode=ctypes.RTLD_GLOBAL)
                return
            except OSError:
                continue


def reraise_if_torch_cudnn_import_error(exc: ImportError) -> None:
    """Re-raise with a short Docker hint when libcudnn failed to load."""
    if sys.platform == "linux" and "libcudnn" in str(exc).lower():
        raise ImportError(
            f"{exc}\n"
            "Hint: libcudnn.so.9 was not found or could not be loaded. "
            "In Docker, ensure the ml-specular image includes cuDNN 9 (rebuild) or run: "
            "find /opt/conda /usr/local/cuda \"$VIRTUAL_ENV\" -name 'libcudnn.so.9*' 2>/dev/null | head -20"
        ) from exc
    raise exc


prepare_linux_torch_cuda_libs()
