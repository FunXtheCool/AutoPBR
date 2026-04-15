"""Train direct specular predictor (diffuse -> LabPBR _s RGBA) and export ONNX.

Training minimizes L1 on sigmoid(logits) vs. artist _s bytes scaled to [0,1]. Exported ONNX must
emit the same sigmoid(logits) so AutoPBR's MlSpecularInference can treat outputs as linear [0,1]
and multiply by 255 for PNG bytes (LabPBR channel semantics — see docs/ml-specular-labpbr-contract.md).
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import math
import os
import shutil
import subprocess
import sys
import time
from collections import defaultdict
from contextlib import nullcontext
from pathlib import Path
from typing import Any

try:
    import torch
except ImportError as _torch_exc:
    from ml_specular.cuda_torch_bootstrap import reraise_if_torch_cudnn_import_error

    reraise_if_torch_cudnn_import_error(_torch_exc)
import torch.nn as nn
from torch import amp as torch_amp
from torch.utils.data import DataLoader

from ml_specular.bucket_batch_sampler import BucketBatchSampler
from ml_specular.dataset import SpecularManifestDataset
from ml_specular.spatial_policy import TIERS, parse_downscale_policy, tier_from_spatial_hw
from ml_specular.model import DilatedPbrNet
from ml_specular.spec_loss import spec_loss
from ml_specular.torch_ort_extensions import (
    get_ortmodule_torch_extensions_dir as _ortmodule_torch_extensions_dir,
    iter_site_packages_roots,
    ortmodule_extensions_built as _ortmodule_torch_extensions_built,
)


class _SpecularOnnxExportWrapper(nn.Module):
    """
    Core net outputs unconstrained logits; training applies sigmoid in the loss.
    ONNX must include sigmoid so runtime matches training and host postprocess (*255 -> byte).
    """

    def __init__(self, core: DilatedPbrNet) -> None:
        super().__init__()
        self.core = core

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        y = self.core(x)
        return torch.sigmoid(y[:, :4])


def export_onnx(model: nn.Module, out_path: Path, in_channels: int, out_channels: int, opset: int = 17) -> None:
    model.eval()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    _ = out_channels
    wrapped = _SpecularOnnxExportWrapper(model).cpu()
    dummy = torch.randn(1, in_channels, 128, 128, device="cpu", dtype=torch.float32)
    torch.onnx.export(
        wrapped,
        dummy,
        str(out_path),
        input_names=["input"],
        output_names=["spec"],
        dynamic_axes={
            "input": {0: "batch", 2: "height", 3: "width"},
            "spec": {0: "batch", 2: "height", 3: "width"},
        },
        opset_version=opset,
        do_constant_folding=True,
        dynamo=False,
    )


def export_checkpoint_to_onnx(ckpt_path: Path, out_onnx: Path, opset: int) -> int:
    if not ckpt_path.is_file():
        print(f"Checkpoint not found: {ckpt_path}", file=sys.stderr)
        return 1
    ckpt = torch.load(ckpt_path, map_location="cpu", weights_only=True)
    in_ch = int(ckpt["in_channels"])
    out_ch = int(ckpt.get("out_channels", 4))
    width = int(ckpt["width"])
    model = DilatedPbrNet(in_channels=in_ch, out_channels=out_ch, width=width)
    model.load_state_dict(ckpt["model"])
    export_onnx(model, out_onnx, in_ch, out_ch, opset)
    print(f"Exported ONNX: {out_onnx.resolve()}")
    return 0


def _sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        while True:
            chunk = f.read(1024 * 1024)
            if not chunk:
                break
            h.update(chunk)
    return h.hexdigest()


def _dataset_fingerprint(root: Path) -> dict[str, str]:
    manifest = (root / "manifest.jsonl").resolve()
    train = (root / "splits" / "train.txt").resolve()
    val = (root / "splits" / "val.txt").resolve()
    fp: dict[str, str] = {
        "manifest_path": manifest.as_posix(),
        "train_split_path": train.as_posix(),
        "val_split_path": val.as_posix(),
    }
    if manifest.is_file():
        fp["manifest_sha256"] = _sha256_file(manifest)
    if train.is_file():
        fp["train_split_sha256"] = _sha256_file(train)
    if val.is_file():
        fp["val_split_sha256"] = _sha256_file(val)
    return fp


def _run_config(args: argparse.Namespace) -> dict[str, Any]:
    max_side = getattr(args, "max_train_side", None)
    return {
        "data_root": str(args.data_root.resolve()) if args.data_root is not None else None,
        "train_res": int(args.train_res),
        "spatial_mode": str(getattr(args, "spatial_mode", "fixed")),
        "max_train_side": int(max_side) if max_side is not None else None,
        "downscale_for_memory": str(getattr(args, "downscale_for_memory", "box")),
        "grad_accum_steps": int(getattr(args, "grad_accum_steps", 1)),
        "native_restrict_to_target_tier": bool(getattr(args, "native_restrict_to_target_tier", True)),
        "native_target_tier": int(getattr(args, "native_target_tier")) if getattr(args, "native_target_tier", None) is not None else None,
        "strict_manifest_size": bool(getattr(args, "strict_manifest_size", True)),
        "in_channels": int(args.in_channels),
        "out_channels": int(args.out_channels),
        "width": int(args.width),
        "batch": int(args.batch),
        "lr": float(args.lr),
        "batch_policy_enabled": bool(getattr(args, "batch_policy_enabled", True)),
        "batch_policy_lr_mode": str(getattr(args, "batch_policy_lr_mode", "sqrt")),
        "batch_policy_baseline_batch": int(getattr(args, "batch_policy_baseline_batch", 8)),
        "batch_policy_baseline_lr": float(getattr(args, "batch_policy_baseline_lr", 1e-3)),
        "batch_policy_max_lr": float(getattr(args, "batch_policy_max_lr")) if getattr(args, "batch_policy_max_lr", None) is not None else None,
        "warmup_ratio": float(getattr(args, "warmup_ratio", 0.05)),
        "warmup_min_steps": int(getattr(args, "warmup_min_steps", 500)),
        "weight_decay_mode": str(getattr(args, "weight_decay_mode", "off")),
        "grad_clip_norm": float(getattr(args, "grad_clip_norm", 0.0)),
        "ema_enabled": bool(getattr(args, "ema_enabled", False)),
        "ema_decay": float(getattr(args, "ema_decay", 0.999)),
        "transparent_zero_weight": float(max(args.transparent_zero_weight, 0.0)),
        "trainer_backend": str(args.trainer_backend),
        "torch_ort": bool(getattr(args, "torch_ort", False)),
        "torch_ort_provider": str(getattr(args, "torch_ort_provider", "cuda")).lower(),
        "torch_ort_tensorrt_fp16": bool(getattr(args, "torch_ort_tensorrt_fp16", True)),
        "torch_ort_memory_opt_level": int(getattr(args, "torch_ort_memory_opt_level", 0)),
        "torch_ort_triton_op_enabled": bool(getattr(args, "torch_ort_triton_op_enabled", False)),
        "torch_ort_zero_stage3_support": bool(getattr(args, "torch_ort_zero_stage3_support", False)),
    }


def _resolve_best_ckpt_path(args: argparse.Namespace) -> Path:
    return Path(args.best_ckpt)


def _resolve_resume_load_path(args: argparse.Namespace) -> Path:
    """File to load for --resume / --resume-auto: always primary --ckpt unless --resume-from is set."""
    if args.resume_from:
        return Path(args.resume_from)
    return Path(args.ckpt)


def _save_training_checkpoint(
    out_path: Path,
    model: nn.Module,
    args: argparse.Namespace,
    epoch: int,
    best_val: float,
    dataset_fp: dict[str, str],
    optimizer: torch.optim.Optimizer | None = None,
    scaler: Any | None = None,
    ema_model: dict[str, torch.Tensor] | None = None,
) -> None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    payload: dict[str, Any] = {
        "format": "autopbr-spec-train-v2",
        "model": model.state_dict(),
        "in_channels": int(args.in_channels),
        "out_channels": int(args.out_channels),
        "width": int(args.width),
        "epoch": int(epoch),
        "best_val": float(best_val),
        "args": _run_config(args),
        "dataset_fingerprint": dataset_fp,
    }
    if optimizer is not None:
        payload["optimizer"] = optimizer.state_dict()
    if scaler is not None:
        try:
            payload["scaler"] = scaler.state_dict()
        except Exception:
            pass
    if ema_model is not None:
        payload["ema_model"] = ema_model
    torch.save(payload, out_path)


def _normalize_resume_cfg(cfg: dict[str, Any]) -> dict[str, Any]:
    """Fill defaults for keys added after older checkpoints so resume stays compatible."""
    out = dict(cfg)
    out.setdefault("spatial_mode", "fixed")
    out.setdefault("max_train_side", None)
    out.setdefault("downscale_for_memory", "box")
    out.setdefault("grad_accum_steps", 1)
    out.setdefault("native_restrict_to_target_tier", True)
    out.setdefault("native_target_tier", None)
    out.setdefault("strict_manifest_size", True)
    out.setdefault("torch_ort_memory_opt_level", 0)
    out.setdefault("torch_ort_triton_op_enabled", False)
    out.setdefault("torch_ort_zero_stage3_support", False)
    out.setdefault("batch_policy_enabled", True)
    out.setdefault("batch_policy_lr_mode", "sqrt")
    out.setdefault("batch_policy_baseline_batch", 8)
    out.setdefault("batch_policy_baseline_lr", 1e-3)
    out.setdefault("batch_policy_max_lr", None)
    out.setdefault("warmup_ratio", 0.05)
    out.setdefault("warmup_min_steps", 500)
    out.setdefault("weight_decay_mode", "off")
    out.setdefault("grad_clip_norm", 0.0)
    out.setdefault("ema_enabled", False)
    out.setdefault("ema_decay", 0.999)
    return out


def _strict_resume_checks(
    current_cfg: dict[str, Any],
    ckpt_cfg: dict[str, Any] | None,
    current_fp: dict[str, str],
    ckpt_fp: dict[str, str] | None,
    *,
    strict: bool,
) -> tuple[bool, str]:
    if ckpt_cfg is None:
        return True, "Checkpoint has no run config (legacy format); allowing warm-start only."
    current_cfg = _normalize_resume_cfg(current_cfg)
    ckpt_cfg = _normalize_resume_cfg(ckpt_cfg)
    mismatches: list[str] = []
    # ORTModule tuning (memory opt / Triton / ZeRO3) is intentionally not strict: it does not change
    # saved tensor shapes or optimizer tensor layout vs the core model, only the ORT execution path.
    for key in (
        "data_root",
        "train_res",
        "spatial_mode",
        "max_train_side",
        "downscale_for_memory",
        "grad_accum_steps",
        "batch_policy_enabled",
        "batch_policy_lr_mode",
        "batch_policy_baseline_batch",
        "batch_policy_baseline_lr",
        "batch_policy_max_lr",
        "warmup_ratio",
        "warmup_min_steps",
        "weight_decay_mode",
        "grad_clip_norm",
        "ema_enabled",
        "ema_decay",
        "native_restrict_to_target_tier",
        "strict_manifest_size",
        "in_channels",
        "out_channels",
        "width",
        "trainer_backend",
        "torch_ort",
        "torch_ort_provider",
        "torch_ort_tensorrt_fp16",
    ):
        if ckpt_cfg.get(key) != current_cfg.get(key):
            mismatches.append(f"{key}: ckpt={ckpt_cfg.get(key)!r} current={current_cfg.get(key)!r}")
    if ckpt_fp is not None:
        for key in ("manifest_sha256", "train_split_sha256", "val_split_sha256"):
            cv = current_fp.get(key)
            kv = ckpt_fp.get(key)
            if cv and kv and cv != kv:
                mismatches.append(f"{key} differs")
    if not mismatches:
        return True, "Resume checks passed."
    msg = "Resume compatibility mismatches:\n  - " + "\n  - ".join(mismatches)
    if strict:
        return False, msg
    return True, msg + "\nContinuing because --resume-allow-config-mismatch is set."


def _clear_stale_ortmodule_fallback() -> None:
    """If ortmodule was imported before extensions were built, clear cached init failure after build."""
    if not _ortmodule_torch_extensions_built():
        return
    m = sys.modules.get("onnxruntime.training.ortmodule")
    if m is None:
        return
    try:
        m._FALLBACK_INIT_EXCEPTION = None  # type: ignore[attr-defined]
    except Exception:
        pass


def _resolve_cuda_home() -> str | None:
    """CUDA toolkit root for PyTorch CUDAExtension (requires CUDA_HOME when compiling ORT fused ops)."""
    existing = (os.environ.get("CUDA_HOME") or "").strip()
    if existing:
        p = Path(existing)
        if p.is_dir():
            return str(p.resolve())
    try:
        from torch.utils.cpp_extension import _find_cuda_home

        found = _find_cuda_home()
        if found and Path(found).is_dir():
            return str(Path(found).resolve())
    except Exception:
        pass
    nvcc = shutil.which("nvcc")
    if nvcc:
        root = Path(nvcc).resolve().parent.parent
        if root.is_dir():
            return str(root)
    for candidate in ("/usr/local/cuda", "/usr/local/cuda-12.4", "/usr/local/cuda-12"):
        cp = Path(candidate)
        if cp.is_dir() and (cp / "lib64").is_dir():
            return str(cp.resolve())
    return None


def _run_torch_ort_configure() -> bool:
    """Build ORTModule C++ hooks via upstream post-install step (once per env / torch+ort pair)."""
    env = os.environ.copy()
    if not (env.get("CUDA_HOME") or "").strip():
        ch = _resolve_cuda_home()
        if ch:
            env["CUDA_HOME"] = ch
            env.setdefault("CUDA_PATH", ch)
            os.environ["CUDA_HOME"] = ch
            os.environ.setdefault("CUDA_PATH", ch)
            print(
                f"[torch-ort] CUDA_HOME was unset; using {ch} for torch_ort.configure "
                "(PyTorch CUDAExtension requires it).",
                file=sys.stderr,
            )
        else:
            print(
                "[torch-ort] CUDA_HOME is not set and could not be inferred. "
                "Set CUDA_HOME to your CUDA toolkit root (e.g. /usr/local/cuda on Linux, or "
                "C:\\Program Files\\NVIDIA GPU Computing Toolkit\\CUDA\\v12.4 on Windows), "
                "ensure nvcc is on PATH, then retry.",
                file=sys.stderr,
            )
            return False
    print("[torch-ort] Running: python -m torch_ort.configure")
    r = subprocess.run([sys.executable, "-m", "torch_ort.configure"], env=env)
    if r.returncode != 0:
        print(
            f"[torch-ort] torch_ort.configure exited with code {r.returncode}. "
            "Use a CUDA **devel** image or full toolkit (nvcc), plus ninja and a C++ toolchain. "
            "See tools/MlSpecularTrainer/requirements-torch-ort.txt and docker/Dockerfile (cudnn9-devel).",
            file=sys.stderr,
        )
        return False
    _normalize_ortmodule_extension_layout()
    return True


def _normalize_ortmodule_extension_layout() -> bool:
    """Ensure ORTModule can detect built torch extensions in torch_cpp_extensions root.

    Some builds place shared objects under nested build folders. ORTModule's detection can
    require a top-level shared object in torch_cpp_extensions/.
    """
    ext_dir = _ortmodule_torch_extensions_dir()
    if ext_dir is None or not ext_dir.is_dir():
        return False

    top_level = [p for pat in ("*.so", "*.dll", "*.dylib", "*.pyd") for p in ext_dir.glob(pat)]
    if top_level:
        return True

    nested: list[Path] = []
    for pat in ("*.so", "*.dll", "*.dylib", "*.pyd"):
        nested.extend(ext_dir.rglob(pat))
    # Skip files already in root (none expected here, but keep robust).
    nested = [p for p in nested if p.parent != ext_dir]
    if not nested:
        return False

    # Copy to root (not move) so build metadata remains intact.
    copied_any = False
    for src in nested:
        dst = ext_dir / src.name
        if dst.exists():
            continue
        try:
            shutil.copy2(src, dst)
            copied_any = True
        except Exception:
            continue
    if copied_any:
        print(f"[torch-ort] Normalized extensions into {ext_dir}.")
    return any(ext_dir.glob("*.so")) or any(ext_dir.glob("*.dll")) or any(ext_dir.glob("*.dylib")) or any(
        ext_dir.glob("*.pyd")
    )


def _torch_ort_debug_enabled() -> bool:
    v = (os.environ.get("TORCH_ORT_DEBUG") or "").strip().lower()
    return v in ("1", "true", "yes", "on")


def _torch_ort_extension_diagnostics(phase: str) -> None:
    """Print paths and files ORTModule cares about (stderr). Use --torch-ort-debug or TORCH_ORT_DEBUG=1.

    Call only *after* ``_ensure_ortmodule_before_wrap()`` so ``_normalize_ortmodule_extension_layout`` has
    run; otherwise ORTModule(Linear) probe can fail spuriously when *.so exist only under build/.
    """
    _normalize_ortmodule_extension_layout()
    print(f"\n[torch-ort-debug] === {phase} ===", file=sys.stderr)
    print(f"[torch-ort-debug] sys.executable={sys.executable!r}", file=sys.stderr)
    print(f"[torch-ort-debug] sys.prefix={sys.prefix!r}", file=sys.stderr)
    print(
        f"[torch-ort-debug] TORCH_EXTENSIONS_DIR={os.environ.get('TORCH_EXTENSIONS_DIR', '(unset)')!r}",
        file=sys.stderr,
    )
    ext = _ortmodule_torch_extensions_dir()
    print(f"[torch-ort-debug] resolved torch_cpp_extensions dir={ext!r}", file=sys.stderr)
    if ext is not None and ext.is_dir():
        try:
            names = sorted(p.name for p in ext.iterdir())[:80]
            print(f"[torch-ort-debug] top-level names (first 80): {names}", file=sys.stderr)
        except OSError as e:
            print(f"[torch-ort-debug] listdir failed: {e}", file=sys.stderr)
        sos = []
        for pat in ("*.so", "*.dll", "*.dylib", "*.pyd"):
            sos.extend(ext.rglob(pat))
        rels = [str(p.relative_to(ext)) for p in sos[:50]]
        print(f"[torch-ort-debug] extension-like files under dir (first 50): {rels}", file=sys.stderr)
        for fname in ("__init__.py", "install.py", "torch_cpp_extensions.py"):
            p = ext / fname
            if p.is_file():
                try:
                    lines = p.read_text(encoding="utf-8", errors="replace").splitlines()[:35]
                    print(f"[torch-ort-debug] --- head of {fname} ---", file=sys.stderr)
                    for i, ln in enumerate(lines, 1):
                        print(f"[torch-ort-debug] {i:02d} {ln}", file=sys.stderr)
                except OSError as e:
                    print(f"[torch-ort-debug] read {fname}: {e}", file=sys.stderr)
    else:
        print("[torch-ort-debug] torch_cpp_extensions dir missing or not a directory.", file=sys.stderr)

    roots = iter_site_packages_roots()
    print(f"[torch-ort-debug] site-packages roots scanned ({len(roots)}):", file=sys.stderr)
    for r in roots[:12]:
        print(f"  - {r}", file=sys.stderr)
    if len(roots) > 12:
        print(f"  ... and {len(roots) - 12} more", file=sys.stderr)

    r = subprocess.run(
        [sys.executable, "-c", "import onnxruntime as o; print(o.__file__)"],
        capture_output=True,
        text=True,
    )
    print(f"[torch-ort-debug] onnxruntime probe returncode={r.returncode}", file=sys.stderr)
    if r.stdout.strip():
        print(f"[torch-ort-debug] onnxruntime.__file__={r.stdout.strip()}", file=sys.stderr)
    if r.stderr.strip():
        print(f"[torch-ort-debug] onnxruntime probe stderr={r.stderr.strip()[:2000]}", file=sys.stderr)

    if not _ortmodule_torch_extensions_built():
        print(
            "[torch-ort-debug] ORTModule(Linear) probe skipped: no *.so under torch_cpp_extensions "
            "(run after ensure, or run `python -m torch_ort.configure`).",
            file=sys.stderr,
        )
    else:
        probe = (
            "import torch\n"
            "from torch_ort import ORTModule\n"
            "m = torch.nn.Linear(2, 2)\n"
            "ORTModule(m)\n"
            "print('probe_ok')\n"
        )
        pr = subprocess.run([sys.executable, "-c", probe], capture_output=True, text=True)
        print(f"[torch-ort-debug] ORTModule(Linear) probe returncode={pr.returncode}", file=sys.stderr)
        if pr.stdout.strip():
            print(f"[torch-ort-debug] probe stdout={pr.stdout.strip()}", file=sys.stderr)
        if pr.stderr.strip():
            err = pr.stderr.strip()
            if len(err) > 6000:
                err = err[:6000] + "\n[torch-ort-debug] ...(stderr truncated)"
            print(f"[torch-ort-debug] probe stderr=\n{err}", file=sys.stderr)
    print(f"[torch-ort-debug] === end {phase} ===\n", file=sys.stderr)


def _ortmodule_import_ready_subprocess() -> bool:
    """Check ORTModule importability in a clean subprocess.

    This avoids false negatives from path heuristics (site-packages vs torch cache)
    and avoids poisoning the current process with ortmodule fallback state.
    """
    probe = (
        "import torch; from torch_ort import ORTModule; "
        "m=torch.nn.Linear(2,2); ORTModule(m); print('ok')"
    )
    r = subprocess.run([sys.executable, "-c", probe], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    return r.returncode == 0


def _ensure_ortmodule_before_wrap() -> bool:
    """Ensure torch_ort.configure has been run so ORTModule can load."""
    # Try to normalize any nested build output first.
    _normalize_ortmodule_extension_layout()
    # Fast path: check site-packages for compiled *.so first (Dockerfile bakes these in). Avoids a
    # heavy ORTModule subprocess probe on every run when extensions are already present.
    if _ortmodule_torch_extensions_built():
        _clear_stale_ortmodule_fallback()
        return True
    # Subprocess probe: extensions absent or in an unusual layout; import check before compiling.
    if _ortmodule_import_ready_subprocess():
        _clear_stale_ortmodule_fallback()
        return True
    print(
        "[torch-ort] ORTModule torch extensions not found; building them now "
        "(official post-install step; first run may take a minute).",
        file=sys.stderr,
    )
    if not _run_torch_ort_configure():
        return False
    _clear_stale_ortmodule_fallback()
    if not _ortmodule_torch_extensions_built():
        print(
            "[torch-ort] Extensions still missing after torch_ort.configure. "
            "Check compiler output above.",
            file=sys.stderr,
        )
        return False
    return True


def _apply_ort_module(
    model: nn.Module,
    *,
    use_cuda: bool,
    provider: str,
    tensorrt_fp16: bool = True,
    memory_opt_level: int = 0,
    triton_op_enabled: bool = False,
    zero_stage3_support: bool = False,
    debug: bool = False,
) -> nn.Module | None:
    """Wrap ``model`` with ``torch_ort.ORTModule`` if available; return None on import/setup failure."""
    if not use_cuda:
        print("torch-ort (ORTModule) requires CUDA.", file=sys.stderr)
        return None
    provider_norm = str(provider).strip().lower() or "cuda"
    if provider_norm not in {"cuda", "tensorrt"}:
        print(f"Unsupported --torch-ort-provider value: {provider!r}. Use 'cuda' or 'tensorrt'.", file=sys.stderr)
        return None
    # ORTModule currently toggles TensorRT backend through env switches.
    if provider_norm == "tensorrt":
        os.environ["ORTMODULE_USE_TENSORRT_BACKEND"] = "1"
        # ORT TensorRT EP: FP16 engine (default on; disable with --no-torch-ort-tensorrt-fp16).
        os.environ["ORT_TENSORRT_FP16_ENABLE"] = "1" if tensorrt_fp16 else "0"
    else:
        os.environ["ORTMODULE_USE_TENSORRT_BACKEND"] = "0"
    os.environ["ORTMODULE_MEMORY_OPT_LEVEL"] = str(max(0, int(memory_opt_level)))
    os.environ["ORTMODULE_USE_TRITON"] = "1" if triton_op_enabled else "0"
    os.environ["ORTMODULE_ENABLE_ZERO_STAGE3"] = "1" if zero_stage3_support else "0"
    if importlib.util.find_spec("torch_ort") is None:
        print(
            "torch-ort is not installed. Install per tools/MlSpecularTrainer/requirements-torch-ort.txt\n"
            f"(sys.executable={sys.executable!r} — use the same interpreter you used for `pip install torch-ort`.)",
            file=sys.stderr,
        )
        return None
    # Ensure normalize + configure run before any ORTModule probe in diagnostics (avoids false
    # "extensions were not detected" when *.so live only under build/ until normalize copies up).
    if not _ensure_ortmodule_before_wrap():
        return None
    if debug or _torch_ort_debug_enabled():
        _torch_ort_extension_diagnostics("after normalize / configure ensure")
    _clear_stale_ortmodule_fallback()
    try:
        from torch_ort import ORTModule  # type: ignore
    except ImportError as ex:
        print(
            "torch-ort is not installed. Install per tools/MlSpecularTrainer/requirements-torch-ort.txt\n"
            f"Import error: {ex}\n"
            f"(sys.executable={sys.executable!r} — use the same interpreter you used for `pip install torch-ort`.)",
            file=sys.stderr,
        )
        return None
    trt_note = ""
    if provider_norm == "tensorrt":
        trt_note = f" tensorrt_fp16={'on' if tensorrt_fp16 else 'off'}"
    mem_note = f" mem_opt={max(0, int(memory_opt_level))}"
    triton_note = f" triton={'on' if triton_op_enabled else 'off'}"
    zero_note = f" zero_stage3={'on' if zero_stage3_support else 'off'}"
    print(
        f"[torch-ort] Wrapping DilatedPbrNet with ORTModule provider={provider_norm}{trt_note}{mem_note}{triton_note}{zero_note} "
        "(first forward can be slow during ONNX capture)."
    )
    try:
        return ORTModule(model)
    except Exception as ex:
        msg = str(ex)
        if "extensions were not detected" in msg.lower():
            print("[torch-ort] ORTModule reports missing extensions; running configure once and retrying.", file=sys.stderr)
            if _run_torch_ort_configure():
                _clear_stale_ortmodule_fallback()
                try:
                    return ORTModule(model)
                except Exception as ex2:
                    print(f"[torch-ort] ORTModule failed after configure retry: {ex2}", file=sys.stderr)
                    return None
        print(f"[torch-ort] ORTModule failed: {ex}", file=sys.stderr)
        return None


def _build_spec_dataset(
    args: argparse.Namespace,
    split: str,
    *,
    augment: bool,
) -> SpecularManifestDataset:
    policy = parse_downscale_policy(getattr(args, "downscale_for_memory", "box"))
    return SpecularManifestDataset(
        args.data_root,
        split,
        train_res=args.train_res,
        in_channels=args.in_channels,
        augment=augment,
        spatial_mode=getattr(args, "spatial_mode", "fixed"),
        max_train_side=getattr(args, "max_train_side", None),
        downscale_policy=policy,
        native_restrict_to_target_tier=bool(getattr(args, "native_restrict_to_target_tier", True)),
        native_target_tier=getattr(args, "native_target_tier", None),
        strict_manifest_size=bool(getattr(args, "strict_manifest_size", True)),
    )


def _optimizer_step_after_accum(
    *,
    model: nn.Module,
    opt: torch.optim.Optimizer,
    scaler: Any | None,
    accum: int,
    pending: int,
    is_last_batch: bool,
    grad_clip_norm: float = 0.0,
    ema_state: dict[str, torch.Tensor] | None = None,
    ema_decay: float = 0.999,
) -> None:
    """Finish an accumulation window; scale grads when the final window has fewer than accum steps."""
    if scaler is not None:
        if pending < accum and is_last_batch:
            scaler.unscale_(opt)
            sf = accum / float(pending)
            for p in model.parameters():
                if p.grad is not None:
                    p.grad.mul_(sf)
            if grad_clip_norm > 0.0:
                torch.nn.utils.clip_grad_norm_(model.parameters(), grad_clip_norm)
            scaler.step(opt)
            scaler.update()
        else:
            if grad_clip_norm > 0.0:
                scaler.unscale_(opt)
                torch.nn.utils.clip_grad_norm_(model.parameters(), grad_clip_norm)
            scaler.step(opt)
            scaler.update()
    else:
        if pending < accum and is_last_batch:
            sf = accum / float(pending)
            for p in model.parameters():
                if p.grad is not None:
                    p.grad.mul_(sf)
        if grad_clip_norm > 0.0:
            torch.nn.utils.clip_grad_norm_(model.parameters(), grad_clip_norm)
        opt.step()
    if ema_state is not None:
        with torch.no_grad():
            for n, p in model.named_parameters():
                if not p.requires_grad:
                    continue
                if n in ema_state:
                    ema_state[n].mul_(ema_decay).add_(p.detach(), alpha=(1.0 - ema_decay))


def _derive_batch_policy(args: argparse.Namespace, *, effective_batch: int, steps_per_epoch: int) -> dict[str, Any]:
    enabled = bool(getattr(args, "batch_policy_enabled", True))
    base_batch = max(1, int(getattr(args, "batch_policy_baseline_batch", 8)))
    base_lr = float(getattr(args, "batch_policy_baseline_lr", 1e-3))
    lr_mode = str(getattr(args, "batch_policy_lr_mode", "sqrt")).lower()
    ratio = effective_batch / float(base_batch)
    lr = float(args.lr)
    if enabled:
        if lr_mode == "sqrt":
            lr = base_lr * math.sqrt(max(ratio, 1e-12))
        elif lr_mode == "linear":
            lr = base_lr * ratio
    max_lr = getattr(args, "batch_policy_max_lr", None)
    if max_lr is not None:
        lr = min(lr, float(max_lr))
    warmup_ratio = max(0.0, float(getattr(args, "warmup_ratio", 0.05)))
    warmup_min_steps = max(0, int(getattr(args, "warmup_min_steps", 500)))
    total_steps = max(1, int(args.epochs) * max(1, steps_per_epoch))
    warmup_steps = 0
    if enabled:
        warmup_steps = max(int(total_steps * warmup_ratio), warmup_min_steps)
        warmup_steps = min(warmup_steps, total_steps)
    wd_mode = str(getattr(args, "weight_decay_mode", "off")).lower()
    weight_decay = 1e-4
    if enabled and wd_mode == "mild_batch_scaled":
        # bounded mild scaling up to 3x at large effective batch
        weight_decay = min(1e-4 * (1.0 + 0.5 * math.log2(max(ratio, 1.0))), 3e-4)
    grad_clip_norm = float(getattr(args, "grad_clip_norm", 0.0))
    return {
        "enabled": enabled,
        "lr_mode": lr_mode,
        "effective_batch": effective_batch,
        "base_batch": base_batch,
        "base_lr": base_lr,
        "lr": float(lr),
        "warmup_steps": int(warmup_steps),
        "total_steps": int(total_steps),
        "weight_decay": float(weight_decay),
        "grad_clip_norm": grad_clip_norm,
    }


def _set_optimizer_lr(opt: torch.optim.Optimizer, lr: float) -> None:
    for pg in opt.param_groups:
        pg["lr"] = float(lr)


def _warmup_lr(base_lr: float, step_idx: int, warmup_steps: int) -> float:
    if warmup_steps <= 0:
        return float(base_lr)
    frac = min(1.0, max(0.0, step_idx / float(warmup_steps)))
    return float(base_lr * frac)


def _swap_in_ema_weights(model: nn.Module, ema_state: dict[str, torch.Tensor] | None) -> dict[str, torch.Tensor] | None:
    if ema_state is None:
        return None
    backup: dict[str, torch.Tensor] = {}
    with torch.no_grad():
        for n, p in model.named_parameters():
            if n in ema_state:
                backup[n] = p.detach().clone()
                p.copy_(ema_state[n])
    return backup


def _restore_weights(model: nn.Module, backup: dict[str, torch.Tensor] | None) -> None:
    if backup is None:
        return
    with torch.no_grad():
        for n, p in model.named_parameters():
            if n in backup:
                p.copy_(backup[n])


def _format_duration(seconds: float) -> str:
    seconds_i = max(0, int(seconds))
    h, rem = divmod(seconds_i, 3600)
    m, s = divmod(rem, 60)
    if h > 0:
        return f"{h:02d}:{m:02d}:{s:02d}"
    return f"{m:02d}:{s:02d}"


def _write_status_line(message: str, prev_len: int) -> int:
    """Render one persistent terminal status line via carriage return."""
    pad_len = max(prev_len - len(message), 0)
    sys.stdout.write("\r" + message + (" " * pad_len))
    sys.stdout.flush()
    return len(message)


def _print_status_or_line(
    *,
    message: str,
    prev_len: int,
    inline_mode: bool,
    step_idx: int,
    total_steps: int,
) -> int:
    """
    Inline status for interactive terminals; throttled newline logs otherwise.
    Returns updated previous rendered length (inline mode) or 0.
    """
    if inline_mode:
        return _write_status_line(message, prev_len)
    # Non-interactive output (logs/files): avoid giant CR-concatenated lines.
    if step_idx == 0 or (step_idx + 1) % 25 == 0 or (step_idx + 1) == total_steps:
        print("[progress] " + message)
    return 0


def _is_cuda_oom_error(ex: BaseException) -> bool:
    msg = str(ex).lower()
    return (
        "cuda out of memory" in msg
        or "cudnn_status_alloc_failed" in msg
        or ("failed to allocate" in msg and "cuda" in msg)
    )


def _is_ort_cudnn_unsupported_error(ex: BaseException) -> bool:
    """ORT CUDA EP / cuDNN can reject some dynamic spatial shapes with CUDNN_STATUS_NOT_SUPPORTED."""
    msg = str(ex).lower()
    return "cudnn_status_not_supported" in msg or "cudnn failure 9" in msg


def _strip_ortmodule_state_dict(sd: dict[str, Any]) -> dict[str, Any]:
    """Map ORTModule-wrapped state_dict keys back to DilatedPbrNet keys."""
    prefixes = (
        "_torch_module._original_module.",
        "_torch_module.module._original_module.",
        "_original_module.",
        "module._original_module.",
        "module.",
    )
    out: dict[str, Any] = {}
    for k, v in sd.items():
        nk = k
        while True:
            stripped = False
            for p in prefixes:
                if nk.startswith(p):
                    nk = nk[len(p) :]
                    stripped = True
                    break
            if not stripped:
                break
        out[nk] = v
    return out


def _is_torch_ort_module(model: nn.Module) -> bool:
    if type(model).__name__ == "ORTModule":
        return True
    if importlib.util.find_spec("torch_ort") is None:
        return False
    try:
        from torch_ort import ORTModule  # type: ignore

        return isinstance(model, ORTModule)
    except Exception:
        return False


def _replace_ort_with_plain_pytorch_training(
    *,
    model: nn.Module,
    device: torch.device,
    args: argparse.Namespace,
    grad_accum_requested: int,
) -> tuple[DilatedPbrNet, torch.optim.Optimizer, Any | None, Any, int]:
    """
    On ORT CUDA/cuDNN failure, rebuild a plain DilatedPbrNet and training stack so native
    H×W training can continue under normal PyTorch + cuDNN.
    """
    print(
        "[torch-ort] ONNX Runtime (ORTModule) hit cuDNN CUDNN_STATUS_NOT_SUPPORTED for this "
        "input shape — often seen with native-resolution batches. "
        "Switching to standard PyTorch CUDA training for the rest of this run (weights preserved).",
        file=sys.stderr,
    )
    stripped = _strip_ortmodule_state_dict(model.state_dict())
    if device.type == "cuda":
        torch.cuda.empty_cache()
    fresh = DilatedPbrNet(
        in_channels=args.in_channels,
        out_channels=args.out_channels,
        width=args.width,
    ).to(device)
    bad = fresh.load_state_dict(stripped, strict=False)
    if bad.missing_keys or bad.unexpected_keys:
        print(
            "[torch-ort] Non-strict state_dict transfer after ORT unwrap; "
            f"first missing={bad.missing_keys[:3]} first unexpected={bad.unexpected_keys[:3]}",
            file=sys.stderr,
        )
    opt = torch.optim.AdamW(fresh.parameters(), lr=args.lr, weight_decay=1e-4)
    use_amp = device.type == "cuda" and not args.no_amp
    scaler = torch_amp.GradScaler("cuda") if use_amp else None
    amp_ctx: Any = (lambda: torch_amp.autocast("cuda", dtype=torch.float16)) if use_amp else nullcontext
    accum = max(1, int(grad_accum_requested))
    if accum > 1:
        print(
            f"[torch-ort] grad_accum_steps restored to {accum} (no longer limited by ORTModule).",
            file=sys.stderr,
        )
    return fresh, opt, scaler, amp_ctx, accum


def _loss_backward_with_oom_safeguard(
    *,
    model: nn.Module,
    x: torch.Tensor,
    y: torch.Tensor,
    valid: torch.Tensor,
    amp_ctx: Any,
    scaler: Any | None,
    accum_steps: int,
    transparent_zero_weight: float,
) -> tuple[float, bool]:
    """
    Compute forward/loss/backward for one batch.
    If CUDA OOM occurs with batch_size>1, retry this batch as per-sample microbatches.
    Returns (loss_value_for_logging, used_microbatch_fallback).
    """
    try:
        with amp_ctx():
            raw = model(x)
            loss = spec_loss(
                raw,
                y,
                valid,
                transparent_zero_weight=transparent_zero_weight,
            )
        loss_scaled = loss / float(accum_steps)
        if scaler is not None:
            scaler.scale(loss_scaled).backward()
        else:
            loss_scaled.backward()
        return float(loss.detach()), False
    except RuntimeError as ex:
        if not _is_cuda_oom_error(ex):
            raise
        bs = int(x.size(0))
        if bs <= 1:
            raise RuntimeError(
                f"{ex}\n"
                "[safeguard] CUDA OOM even at batch size 1 for this step. "
                "Try lower --max-train-side, use --batch 1, reduce model --width, "
                "or disable TensorRT provider for torch-ort."
            ) from ex
        if x.is_cuda:
            torch.cuda.empty_cache()
        print(
            f"[safeguard] CUDA OOM at batch size {bs}; retrying this step as microbatches of 1.",
            file=sys.stderr,
        )
        weighted_loss = 0.0
        for i in range(bs):
            xi = x[i : i + 1]
            yi = y[i : i + 1]
            vi = valid[i : i + 1]
            with amp_ctx():
                raw_i = model(xi)
                loss_i = spec_loss(
                    raw_i,
                    yi,
                    vi,
                    transparent_zero_weight=transparent_zero_weight,
                )
            # Preserve batch-mean gradient scale, then apply normal accumulation scaling.
            loss_i_scaled = (loss_i / float(bs)) / float(accum_steps)
            if scaler is not None:
                scaler.scale(loss_i_scaled).backward()
            else:
                loss_i_scaled.backward()
            weighted_loss += float(loss_i.detach()) / float(bs)
        return weighted_loss, True


def _eval_loss_with_oom_safeguard(
    *,
    model: nn.Module,
    x: torch.Tensor,
    y: torch.Tensor,
    valid: torch.Tensor,
    amp_ctx: Any,
    transparent_zero_weight: float,
) -> tuple[float, bool]:
    """Eval-only counterpart: retries as per-sample microbatches on CUDA OOM when batch_size>1."""
    try:
        with amp_ctx():
            raw = model(x)
            loss = spec_loss(
                raw,
                y,
                valid,
                transparent_zero_weight=transparent_zero_weight,
            )
        return float(loss.detach()), False
    except RuntimeError as ex:
        if not _is_cuda_oom_error(ex):
            raise
        bs = int(x.size(0))
        if bs <= 1:
            raise RuntimeError(
                f"{ex}\n"
                "[safeguard] CUDA OOM even at batch size 1 during validation. "
                "Try lower --max-train-side, use --batch 1, reduce model --width, "
                "or disable TensorRT provider for torch-ort."
            ) from ex
        if x.is_cuda:
            torch.cuda.empty_cache()
        print(
            f"[safeguard] CUDA OOM in validation at batch size {bs}; retrying as microbatches of 1.",
            file=sys.stderr,
        )
        weighted_loss = 0.0
        for i in range(bs):
            xi = x[i : i + 1]
            yi = y[i : i + 1]
            vi = valid[i : i + 1]
            with amp_ctx():
                raw_i = model(xi)
                loss_i = spec_loss(
                    raw_i,
                    yi,
                    vi,
                    transparent_zero_weight=transparent_zero_weight,
                )
            weighted_loss += float(loss_i.detach()) / float(bs)
        return weighted_loss, True


def _train_pytorch(args: argparse.Namespace) -> int:
    if args.data_root is None:
        raise ValueError("--data-root is required for pytorch backend.")

    if args.device.startswith("cuda") and torch.cuda.is_available():
        device = torch.device(args.device)
    else:
        device = torch.device("cpu")
    use_cuda = device.type == "cuda"
    want_torch_ort = bool(getattr(args, "torch_ort", False))
    requested_provider = str(getattr(args, "torch_ort_provider", "cuda")).strip().lower() or "cuda"
    resolved_provider = requested_provider if want_torch_ort else "disabled"
    print(
        "[runtime] trainer-backend=pytorch "
        f"device={device.type} "
        f"torch-ort={'on' if want_torch_ort else 'off'} "
        f"resolved-torch-ort-backend={resolved_provider}"
    )
    if want_torch_ort and not use_cuda:
        print("--torch-ort requires CUDA and torch.cuda.is_available() (use --device cuda).", file=sys.stderr)
        return 1
    if want_torch_ort:
        fp16 = bool(getattr(args, "torch_ort_tensorrt_fp16", True))
        trt_fp = f" tensorrt_fp16={'on' if fp16 else 'off'}" if resolved_provider == "tensorrt" else ""
        mem_opt = max(0, int(getattr(args, "torch_ort_memory_opt_level", 0)))
        triton = bool(getattr(args, "torch_ort_triton_op_enabled", False))
        zero3 = bool(getattr(args, "torch_ort_zero_stage3_support", False))
        print(
            f"[torch-ort] Requested ORTModule backend: {resolved_provider}{trt_fp} "
            f"mem_opt={mem_opt} triton={'on' if triton else 'off'} zero_stage3={'on' if zero3 else 'off'}"
        )
    # ORTModule owns the training graph; disable native AMP to avoid mixing half/autocast with ORT.
    use_amp = use_cuda and not args.no_amp and not want_torch_ort
    if want_torch_ort and use_cuda and not args.no_amp:
        print("[torch-ort] Native PyTorch AMP disabled while using ORTModule.")
    pin_memory = use_cuda and not args.no_pin_memory
    if use_cuda:
        torch.backends.cudnn.benchmark = True

    ncpu = os.cpu_count() or 4
    num_workers = min(8, max(2, ncpu // 2)) if args.workers < 0 else args.workers

    spatial_mode = getattr(args, "spatial_mode", "fixed")
    grad_accum_requested = max(1, int(getattr(args, "grad_accum_steps", 1)))
    accum_steps = grad_accum_requested
    if want_torch_ort and accum_steps > 1:
        print(
            "[torch-ort] Grad accumulation is not supported with ORTModule; using grad_accum_steps=1.",
            file=sys.stderr,
        )
        accum_steps = 1

    train_ds = _build_spec_dataset(args, "train", augment=True)
    val_ds = _build_spec_dataset(args, "val", augment=False)
    if len(train_ds) == 0:
        print("No training samples.", file=sys.stderr)
        return 1

    if spatial_mode == "native":
        tm = sum(1 for m in train_ds.index_spatial_meta if m.tag_mismatch)
        cap_n = sum(1 for m in train_ds.index_spatial_meta if m.capped)
        nkeys = len(set(train_ds.spatial_keys))
        fs = train_ds.native_filter_stats
        active_tiers = sorted({int(m.tier) for m in train_ds.index_spatial_meta})
        missing_tiers = [int(t) for t in TIERS if int(t) not in active_tiers]
        target_tier = fs.get("target_tier", -1)
        target_tier_s = str(target_tier) if target_tier > 0 else "none"
        print(
            f"[native] spatial_mode=native train={len(train_ds)} val={len(val_ds)} "
            f"unique_hw={nkeys} tag_mismatch={tm} capped_by_max_side={cap_n} "
            f"target_tier={target_tier_s} drop_bucket={fs.get('dropped_bucket_mismatch', 0)} "
            f"drop_tag={fs.get('dropped_tag_mismatch', 0)} "
            f"unknown_tag={fs.get('missing_or_unknown_tag', 0)}"
        )
        print(
            f"[native] active_tiers={active_tiers} absent_tiers={missing_tiers} "
            "(only active tiers produce DataLoader buckets/batches)."
        )

    loader_kw: dict = {"num_workers": num_workers, "pin_memory": pin_memory}
    if num_workers > 0:
        loader_kw["persistent_workers"] = True
        loader_kw["prefetch_factor"] = 2

    if spatial_mode == "native":
        train_gen = torch.Generator()
        train_gen.manual_seed(42)
        train_sampler = BucketBatchSampler(
            train_ds.spatial_keys,
            batch_size=args.batch,
            shuffle=True,
            generator=train_gen,
        )
        val_sampler = BucketBatchSampler(
            val_ds.spatial_keys,
            batch_size=args.batch,
            shuffle=False,
            generator=None,
        )
        train_loader = DataLoader(train_ds, batch_sampler=train_sampler, **loader_kw)
        val_loader = DataLoader(val_ds, batch_sampler=val_sampler, **loader_kw)
    else:
        loader_kw["drop_last"] = False
        train_loader = DataLoader(train_ds, batch_size=args.batch, shuffle=True, **loader_kw)
        val_loader = DataLoader(val_ds, batch_size=args.batch, shuffle=False, **loader_kw)

    dataset_fp = _dataset_fingerprint(args.data_root.resolve())
    cfg = _run_config(args)
    training_ckpt = Path(args.ckpt)
    best_ckpt_path = _resolve_best_ckpt_path(args)
    backup_ckpt_path = Path(args.resume_ckpt) if args.resume_ckpt is not None else None
    start_epoch = 0
    best_val = float("inf")
    pending_optimizer: dict[str, Any] | None = None
    pending_scaler: dict[str, Any] | None = None
    pending_ema: dict[str, torch.Tensor] | None = None

    model = DilatedPbrNet(in_channels=args.in_channels, out_channels=args.out_channels, width=args.width).to(device)

    resume_requested = bool(args.resume or args.resume_auto or args.resume_from)
    resume_path = _resolve_resume_load_path(args)
    if resume_requested:
        if not resume_path.is_file():
            if args.resume:
                print(f"Resume requested but checkpoint not found: {resume_path}", file=sys.stderr)
                return 1
            print(f"[resume-auto] No checkpoint found at {resume_path}; starting fresh.")
        else:
            ckpt = torch.load(resume_path, map_location=device, weights_only=False)
            model.load_state_dict(ckpt["model"])
            ok, msg = _strict_resume_checks(
                cfg,
                ckpt.get("args"),
                dataset_fp,
                ckpt.get("dataset_fingerprint"),
                strict=not args.resume_allow_config_mismatch,
            )
            print(msg)
            if not ok:
                print("Refusing to resume with incompatible checkpoint.", file=sys.stderr)
                return 1
            start_epoch = int(ckpt.get("epoch", -1)) + 1
            best_val = float(ckpt.get("best_val", float("inf")))
            if not args.reset_optimizer and "optimizer" in ckpt:
                pending_optimizer = ckpt["optimizer"]
            if "scaler" in ckpt:
                pending_scaler = ckpt["scaler"]
            if "ema_model" in ckpt and isinstance(ckpt["ema_model"], dict):
                pending_ema = ckpt["ema_model"]
            print(f"Resumed from {resume_path} at epoch={start_epoch} best_val={best_val:.6f}")

    if want_torch_ort:
        ort_debug = bool(getattr(args, "torch_ort_debug", False)) or _torch_ort_debug_enabled()
        wrapped = _apply_ort_module(
            model,
            use_cuda=use_cuda,
            provider=args.torch_ort_provider,
            tensorrt_fp16=bool(getattr(args, "torch_ort_tensorrt_fp16", True)),
            memory_opt_level=max(0, int(getattr(args, "torch_ort_memory_opt_level", 0))),
            triton_op_enabled=bool(getattr(args, "torch_ort_triton_op_enabled", False)),
            zero_stage3_support=bool(getattr(args, "torch_ort_zero_stage3_support", False)),
            debug=ort_debug,
        )
        if wrapped is None:
            return 1
        model = wrapped

    ort_active = _is_torch_ort_module(model)
    effective_batch = int(args.batch) * int(accum_steps)
    policy = _derive_batch_policy(args, effective_batch=effective_batch, steps_per_epoch=len(train_loader))
    print(
        "[batch-policy] "
        f"enabled={'on' if policy['enabled'] else 'off'} mode={policy['lr_mode']} "
        f"effective_batch={policy['effective_batch']} base_batch={policy['base_batch']} "
        f"base_lr={policy['base_lr']:.6g} scaled_lr={policy['lr']:.6g} "
        f"warmup_steps={policy['warmup_steps']} weight_decay={policy['weight_decay']:.6g} "
        f"clip_norm={policy['grad_clip_norm']:.3g} "
        f"ema={'on' if bool(getattr(args, 'ema_enabled', False)) else 'off'}"
    )

    opt = torch.optim.AdamW(model.parameters(), lr=float(policy["lr"]), weight_decay=float(policy["weight_decay"]))
    scaler = torch_amp.GradScaler("cuda") if use_amp else None
    amp_ctx = (lambda: torch_amp.autocast("cuda", dtype=torch.float16)) if use_amp else nullcontext
    ema_enabled = bool(getattr(args, "ema_enabled", False)) and (not ort_active)
    ema_decay = float(getattr(args, "ema_decay", 0.999))
    ema_state: dict[str, torch.Tensor] | None = None
    if bool(getattr(args, "ema_enabled", False)) and ort_active:
        print("[batch-policy] EMA requested but ORTModule is active; EMA disabled for this run.", file=sys.stderr)
    if ema_enabled:
        ema_state = {n: p.detach().clone() for n, p in model.named_parameters() if p.requires_grad}
        if pending_ema is not None:
            for n, t in pending_ema.items():
                if n in ema_state and isinstance(t, torch.Tensor):
                    ema_state[n].copy_(t.to(device=ema_state[n].device, dtype=ema_state[n].dtype))

    if pending_optimizer is not None:
        try:
            opt.load_state_dict(pending_optimizer)
        except Exception as ex:
            print(f"Warning: failed to load optimizer state ({ex}); continuing with fresh optimizer.")
    if pending_scaler is not None and scaler is not None:
        try:
            scaler.load_state_dict(pending_scaler)
        except Exception as ex:
            print(f"Warning: failed to load AMP scaler state ({ex}); continuing with fresh scaler.")

    last_completed_epoch = start_epoch - 1
    total_train_epochs = max(1, int(args.epochs) - int(start_epoch))
    run_start_t = time.perf_counter()
    try:
        if start_epoch >= args.epochs:
            print(
                f"Start epoch ({start_epoch}) >= total epochs ({args.epochs}); nothing to train. "
                "Use a larger --epochs to continue."
            )
        else:
            for epoch in range(start_epoch, args.epochs):
                epoch_start_t = time.perf_counter()
                model.train()
                tr_loss = 0.0
                n_tr = 0
                pending = 0
                opt.zero_grad(set_to_none=True)
                n_tr_batches = len(train_loader)
                step_time_ema = 0.0
                ema_alpha = 0.15
                train_oom_fallback_steps = 0
                status_len = 0
                inline_status = bool(getattr(sys.stdout, "isatty", lambda: False)())
                for bi, (x, y, valid) in enumerate(train_loader):
                    step_t0 = time.perf_counter()
                    global_step = epoch * n_tr_batches + bi + 1
                    _set_optimizer_lr(opt, _warmup_lr(float(policy["lr"]), global_step, int(policy["warmup_steps"])))
                    x = x.to(device, non_blocking=pin_memory)
                    y = y.to(device, non_blocking=pin_memory)
                    valid = valid.to(device, non_blocking=pin_memory)
                    while True:
                        try:
                            loss_value, used_fallback = _loss_backward_with_oom_safeguard(
                                model=model,
                                x=x,
                                y=y,
                                valid=valid,
                                amp_ctx=amp_ctx,
                                scaler=scaler,
                                accum_steps=accum_steps,
                                transparent_zero_weight=max(args.transparent_zero_weight, 0.0),
                            )
                            break
                        except RuntimeError as ex:
                            if ort_active and _is_ort_cudnn_unsupported_error(ex):
                                model, opt, scaler, amp_ctx, accum_steps = _replace_ort_with_plain_pytorch_training(
                                    model=model,
                                    device=device,
                                    args=args,
                                    grad_accum_requested=grad_accum_requested,
                                )
                                ort_active = False
                                opt.zero_grad(set_to_none=True)
                                pending = 0
                                model.train()
                                continue
                            raise
                    if used_fallback:
                        train_oom_fallback_steps += 1
                    pending += 1
                    is_last = bi == n_tr_batches - 1
                    if pending == accum_steps or is_last:
                        _optimizer_step_after_accum(
                            model=model,
                            opt=opt,
                            scaler=scaler,
                            accum=accum_steps,
                            pending=pending,
                            is_last_batch=is_last,
                            grad_clip_norm=float(policy["grad_clip_norm"]),
                            ema_state=ema_state,
                            ema_decay=ema_decay,
                        )
                        opt.zero_grad(set_to_none=True)
                        pending = 0
                    tr_loss += loss_value * x.size(0)
                    n_tr += x.size(0)

                    step_dt = max(1e-6, time.perf_counter() - step_t0)
                    if bi == 0:
                        step_time_ema = step_dt
                    else:
                        step_time_ema = (1.0 - ema_alpha) * step_time_ema + ema_alpha * step_dt
                    steps_done = bi + 1
                    steps_left = max(0, n_tr_batches - steps_done)
                    epoch_eta = steps_left * step_time_ema
                    epoch_progress = steps_done / max(1, n_tr_batches)
                    completed_epochs = epoch - start_epoch
                    overall_progress = min(0.999, (completed_epochs + epoch_progress) / max(1, total_train_epochs))
                    elapsed = time.perf_counter() - run_start_t
                    total_eta = (elapsed * (1.0 - overall_progress) / overall_progress) if overall_progress > 0 else 0.0
                    status_line = (
                        f"epoch {epoch + 1}/{args.epochs} step {steps_done}/{n_tr_batches} "
                        f"step_time={step_dt:.2f}s avg_step={step_time_ema:.2f}s "
                        f"epoch_eta={_format_duration(epoch_eta)} total_eta={_format_duration(total_eta)} "
                        "[Ctrl+C: save+exit]"
                    )
                    status_len = _print_status_or_line(
                        message=status_line,
                        prev_len=status_len,
                        inline_mode=inline_status,
                        step_idx=bi,
                        total_steps=n_tr_batches,
                    )
                if inline_status and status_len > 0:
                    # Finish the in-place status row before regular newline logging.
                    sys.stdout.write("\n")
                    sys.stdout.flush()
                tr_loss /= max(n_tr, 1)

                ema_backup = _swap_in_ema_weights(model, ema_state if ema_enabled else None)
                model.eval()
                va_loss = 0.0
                n_va = 0
                tier_sum: defaultdict[int, float] = defaultdict(float)
                tier_cnt: defaultdict[int, int] = defaultdict(int)
                with torch.no_grad():
                    val_oom_fallback_steps = 0
                    for x, y, valid in val_loader:
                        x = x.to(device, non_blocking=pin_memory)
                        y = y.to(device, non_blocking=pin_memory)
                        valid = valid.to(device, non_blocking=pin_memory)
                        while True:
                            try:
                                loss_value, used_fallback = _eval_loss_with_oom_safeguard(
                                    model=model,
                                    x=x,
                                    y=y,
                                    valid=valid,
                                    amp_ctx=amp_ctx,
                                    transparent_zero_weight=max(args.transparent_zero_weight, 0.0),
                                )
                                break
                            except RuntimeError as ex:
                                if ort_active and _is_ort_cudnn_unsupported_error(ex):
                                    model, opt, scaler, amp_ctx, accum_steps = _replace_ort_with_plain_pytorch_training(
                                        model=model,
                                        device=device,
                                        args=args,
                                        grad_accum_requested=grad_accum_requested,
                                    )
                                    ort_active = False
                                    model.eval()
                                    continue
                                raise
                        if used_fallback:
                            val_oom_fallback_steps += 1
                        bs = x.size(0)
                        va_loss += loss_value * bs
                        n_va += bs
                        if spatial_mode == "native":
                            h_px, w_px = int(y.shape[-2]), int(y.shape[-1])
                            tier = tier_from_spatial_hw(w_px, h_px)
                            tier_sum[tier] += loss_value * bs
                            tier_cnt[tier] += bs
                va_loss /= max(n_va, 1)
                _restore_weights(model, ema_backup)
                model.train()

                line = f"epoch {epoch + 1}/{args.epochs}  train_loss={tr_loss:.4f}  val_loss={va_loss:.4f}"
                if spatial_mode == "native" and tier_cnt:
                    parts = [
                        f"{t}:{tier_sum[t] / tier_cnt[t]:.4f}"
                        for t in sorted(tier_sum.keys())
                        if tier_cnt.get(t, 0) > 0
                    ]
                    if parts:
                        line += "  val_by_tier=" + " ".join(parts)
                if train_oom_fallback_steps > 0 or val_oom_fallback_steps > 0:
                    line += (
                        f"  oom_fallback(train={train_oom_fallback_steps},"
                        f"val={val_oom_fallback_steps})"
                    )
                print(line)

                # Full training state each epoch on --ckpt (primary resume target); optional duplicate on --resume-ckpt.
                _save_training_checkpoint(
                    training_ckpt,
                    model,
                    args,
                    epoch=epoch,
                    best_val=best_val,
                    dataset_fp=dataset_fp,
                    optimizer=opt,
                    scaler=scaler,
                    ema_model=ema_state,
                )
                if backup_ckpt_path is not None:
                    _save_training_checkpoint(
                        backup_ckpt_path,
                        model,
                        args,
                        epoch=epoch,
                        best_val=best_val,
                        dataset_fp=dataset_fp,
                        optimizer=opt,
                        scaler=scaler,
                        ema_model=ema_state,
                    )

                if va_loss < best_val:
                    best_val = va_loss
                    _save_training_checkpoint(
                        best_ckpt_path,
                        model,
                        args,
                        epoch=epoch,
                        best_val=best_val,
                        dataset_fp=dataset_fp,
                        optimizer=opt if args.save_optimizer_in_best_ckpt else None,
                        scaler=scaler if args.save_optimizer_in_best_ckpt else None,
                    )
                    print(f"  [best] saved: {best_ckpt_path} (val={best_val:.6f})")
                epoch_elapsed = time.perf_counter() - epoch_start_t
                print(f"[timing] epoch {epoch + 1} completed in {_format_duration(epoch_elapsed)}")
                last_completed_epoch = epoch
    except KeyboardInterrupt:
        # Keep Ctrl+C ergonomic: save a resumable checkpoint and exit without traceback.
        abort_epoch = max(last_completed_epoch, -1)
        _save_training_checkpoint(
            training_ckpt,
            model,
            args,
            epoch=abort_epoch,
            best_val=best_val,
            dataset_fp=dataset_fp,
            optimizer=opt,
            scaler=scaler,
            ema_model=ema_state,
        )
        if backup_ckpt_path is not None:
            _save_training_checkpoint(
                backup_ckpt_path,
                model,
                args,
                epoch=abort_epoch,
                best_val=best_val,
                dataset_fp=dataset_fp,
                optimizer=opt,
                scaler=scaler,
                ema_model=ema_state,
            )
        next_epoch = abort_epoch + 1
        print(
            f"\n[abort] Caught Ctrl+C. Saved resume checkpoint to {training_ckpt.resolve()} "
            f"(resume starts at epoch index {next_epoch}).",
            file=sys.stderr,
        )
        if backup_ckpt_path is not None:
            print(f"[abort] Backup checkpoint: {backup_ckpt_path.resolve()}", file=sys.stderr)
        return 130

    if best_ckpt_path.is_file():
        ckpt_for_export = best_ckpt_path
    elif training_ckpt.is_file():
        print(
            f"No best checkpoint at {best_ckpt_path} yet; exporting from latest training state: {training_ckpt}"
        )
        ckpt_for_export = training_ckpt
    else:
        print(f"No checkpoint available to export (tried {best_ckpt_path} and {training_ckpt}).", file=sys.stderr)
        return 1

    ckpt = torch.load(ckpt_for_export, map_location=device, weights_only=True)
    model = DilatedPbrNet(
        in_channels=int(ckpt["in_channels"]),
        out_channels=int(ckpt.get("out_channels", 4)),
        width=int(ckpt["width"]),
    ).to(device)
    model.load_state_dict(ckpt["model"])
    export_onnx(
        model,
        args.out_onnx,
        in_channels=int(ckpt["in_channels"]),
        out_channels=int(ckpt.get("out_channels", 4)),
        opset=args.opset,
    )
    print(f"Exported ONNX: {args.out_onnx.resolve()}")
    print(f"Training state (resume): {training_ckpt.resolve()}")
    print(f"Best weights (export): {best_ckpt_path.resolve()}")
    if backup_ckpt_path is not None:
        print(f"Backup copy (optional): {backup_ckpt_path.resolve()}")
    return 0


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description="Train direct specular predictor ONNX for AutoPBR.")
    p.add_argument(
        "--export-only",
        action="store_true",
        help="Load --ckpt and write --out-onnx only (no training; --data-root not required).",
    )
    p.add_argument(
        "--data-root",
        type=Path,
        default=None,
        help="Dataset root with manifest/splits and label_spec. Required unless --export-only.",
    )
    p.add_argument("--epochs", type=int, default=40)
    p.add_argument("--batch", type=int, default=4)
    p.add_argument("--lr", type=float, default=1e-3)
    p.add_argument(
        "--batch-policy-enabled",
        action=argparse.BooleanOptionalAction,
        default=True,
        help="Enable batch-aware safety policy for LR/warmup/weight-decay/grad-clip.",
    )
    p.add_argument(
        "--batch-policy-lr-mode",
        type=str,
        default="sqrt",
        choices=("off", "sqrt", "linear"),
        help="LR scaling rule from baseline effective batch (recommended: sqrt for AdamW).",
    )
    p.add_argument("--batch-policy-baseline-batch", type=int, default=8, help="Baseline effective batch for LR scaling.")
    p.add_argument("--batch-policy-baseline-lr", type=float, default=1e-3, help="Baseline LR matched to --batch-policy-baseline-batch.")
    p.add_argument("--batch-policy-max-lr", type=float, default=None, help="Optional hard cap applied after LR scaling.")
    p.add_argument("--warmup-ratio", type=float, default=0.05, help="Warmup ratio of total train steps when batch policy is enabled.")
    p.add_argument("--warmup-min-steps", type=int, default=500, help="Minimum warmup steps floor when batch policy is enabled.")
    p.add_argument(
        "--weight-decay-mode",
        type=str,
        default="off",
        choices=("off", "mild_batch_scaled"),
        help="Weight decay policy: off or mild batch-scaled bounded increase.",
    )
    p.add_argument("--grad-clip-norm", type=float, default=0.0, help="Global grad norm clipping; <=0 disables.")
    p.add_argument("--ema-enabled", action=argparse.BooleanOptionalAction, default=False, help="Track EMA weights for validation/checkpoints.")
    p.add_argument("--ema-decay", type=float, default=0.999, help="EMA decay when --ema-enabled is on.")
    p.add_argument("--train-res", type=int, default=128)
    p.add_argument(
        "--spatial-mode",
        type=str,
        default="fixed",
        choices=("fixed", "native"),
        help="fixed: resize all to --train-res (legacy). native: train at manifest/native size (bucketed by H×W).",
    )
    p.add_argument(
        "--max-train-side",
        type=int,
        default=None,
        help="Native mode: if max(w,h) exceeds this, downscale diffuse+spec with --downscale-for-memory.",
    )
    p.add_argument(
        "--downscale-for-memory",
        type=str,
        default="box",
        help="Native mode: Pillow resampling for max-side cap (box, lanczos, nearest).",
    )
    p.add_argument(
        "--grad-accum-steps",
        type=int,
        default=1,
        help="Optimizer step every N microbatches; last batch scales grads if fewer than N remain.",
    )
    p.add_argument(
        "--native-restrict-to-target-tier",
        action=argparse.BooleanOptionalAction,
        default=True,
        help=(
            "Native mode: per-sample tag-match restriction. When tagged_resolution is known and in "
            "{16,32,64,128,256,512}, keep samples whose computed geometry tier matches that tag."
        ),
    )
    p.add_argument(
        "--native-target-tier",
        type=int,
        default=None,
        choices=(16, 32, 64, 128, 256, 512),
        help="Deprecated/no-op in per-sample native tag-match mode (kept for backward CLI compatibility).",
    )
    p.add_argument(
        "--strict-manifest-size",
        action=argparse.BooleanOptionalAction,
        default=True,
        help="When manifest lists width/height, require diffuse file dimensions to match (default: on).",
    )
    p.add_argument("--in-channels", type=int, default=4, choices=(3, 4))
    p.add_argument("--width", type=int, default=64)
    p.add_argument(
        "--out-channels",
        type=int,
        default=4,
        choices=(4,),
        help="Logits channel count (RGBA only).",
    )
    p.add_argument("--out-onnx", type=Path, default=Path("artifacts/specular_predictor.onnx"))
    p.add_argument(
        "--ckpt",
        type=Path,
        default=Path("artifacts/specular_predictor.pt"),
        help="Primary training checkpoint: full state saved each epoch; use with --resume-auto to continue.",
    )
    p.add_argument(
        "--best-ckpt",
        type=Path,
        default=None,
        help="Best validation checkpoint for final ONNX export (default: <ckpt_stem>.best.pt).",
    )
    p.add_argument("--device", type=str, default="cpu")
    p.add_argument("--opset", type=int, default=17)
    p.add_argument("--workers", type=int, default=-1)
    p.add_argument("--no-pin-memory", action="store_true")
    p.add_argument("--no-amp", action="store_true")
    p.add_argument(
        "--torch-ort",
        action="store_true",
        help=(
            "Wrap DilatedPbrNet with torch_ort.ORTModule (requires CUDA + onnxruntime-training + torch-ort; "
            "see requirements-torch-ort.txt). Disables native PyTorch AMP for this run."
        ),
    )
    p.add_argument(
        "--torch-ort-provider",
        type=str,
        default="cuda",
        choices=("cuda", "tensorrt"),
        help=(
            "Execution provider for torch-ort ORTModule. 'cuda' uses CUDA EP; "
            "'tensorrt' enables ORTMODULE_USE_TENSORRT_BACKEND=1 before ORTModule init."
        ),
    )
    p.add_argument(
        "--torch-ort-tensorrt-fp16",
        dest="torch_ort_tensorrt_fp16",
        action=argparse.BooleanOptionalAction,
        default=True,
        help=(
            "When --torch-ort-provider tensorrt: set ORT_TENSORRT_FP16_ENABLE (TensorRT engine FP16). "
            "Default: enabled. Use --no-torch-ort-tensorrt-fp16 for FP32."
        ),
    )
    p.add_argument(
        "--torch-ort-debug",
        action="store_true",
        help=(
            "Print ORTModule extension paths, site-packages roots, and subprocess probe output to stderr. "
            "Or set env TORCH_ORT_DEBUG=1."
        ),
    )
    p.add_argument(
        "--torch-ort-memory-opt-level",
        type=int,
        default=0,
        help=(
            "Set ORTMODULE_MEMORY_OPT_LEVEL (>=0). 0=default/manual mode, 1=aggressive layerwise recompute."
        ),
    )
    p.add_argument(
        "--torch-ort-triton-op-enabled",
        action=argparse.BooleanOptionalAction,
        default=False,
        help="Enable ORTMODULE_USE_TRITON=1 for TritonOp integration.",
    )
    p.add_argument(
        "--torch-ort-zero-stage3-support",
        action=argparse.BooleanOptionalAction,
        default=False,
        help="Enable ORTMODULE_ENABLE_ZERO_STAGE3=1 for ZeRO stage3 compatibility path.",
    )
    p.add_argument(
        "--transparent-zero-weight",
        type=float,
        default=0.5,
        help="Weight for pushing RGBA toward 0 on transparent diffuse pixels (alpha below dataset ignore threshold).",
    )
    p.add_argument(
        "--trainer-backend",
        type=str,
        default="pytorch",
        choices=("pytorch", "ort"),
        help="Training backend. 'ort' is experimental and expects ORT training artifacts.",
    )
    p.add_argument("--resume", action="store_true", help="Require checkpoint and resume training state.")
    p.add_argument("--resume-auto", action="store_true", help="Resume if checkpoint exists; else start fresh.")
    p.add_argument("--resume-from", type=Path, default=None, help="Explicit checkpoint path for resume.")
    p.add_argument(
        "--resume-ckpt",
        type=Path,
        default=None,
        help=(
            "Optional: each epoch also write a full duplicate to this path (e.g. backup drive). "
            "Not used for resume; training state for resume is always --ckpt."
        ),
    )
    p.add_argument("--reset-optimizer", action="store_true", help="On resume, keep weights but reset optimizer/scaler.")
    p.add_argument(
        "--resume-allow-config-mismatch",
        action="store_true",
        help="Allow resume even if data_root/train_res/channels/width/fingerprints differ.",
    )
    p.add_argument(
        "--save-optimizer-in-best-ckpt",
        action="store_true",
        help="Also store optimizer/scaler in --best-ckpt when a new best validation is achieved.",
    )
    p.add_argument(
        "--ort-artifacts-dir",
        type=Path,
        default=Path("artifacts/ort"),
        help="ORT training artifacts directory (train/eval/optimizer ONNX). Used by --trainer-backend ort.",
    )
    p.add_argument(
        "--ort-checkpoint",
        type=Path,
        default=None,
        help=(
            "ORT CheckpointState path (file or directory) for load/save. "
            "Default: <ort-artifacts-dir>/ort_training_state. "
            "If missing, loads once from <ort-artifacts-dir>/checkpoint (artifact generator output)."
        ),
    )
    args = p.parse_args(argv)
    if args.best_ckpt is None:
        _ck = Path(args.ckpt)
        args.best_ckpt = _ck.with_name(f"{_ck.stem}.best{_ck.suffix}")

    if int(getattr(args, "grad_accum_steps", 1)) < 1:
        p.error("--grad-accum-steps must be >= 1")
    if int(getattr(args, "batch_policy_baseline_batch", 8)) < 1:
        p.error("--batch-policy-baseline-batch must be >= 1")
    if float(getattr(args, "batch_policy_baseline_lr", 1e-3)) <= 0:
        p.error("--batch-policy-baseline-lr must be > 0")
    if getattr(args, "batch_policy_max_lr", None) is not None and float(args.batch_policy_max_lr) <= 0:
        p.error("--batch-policy-max-lr must be > 0 when provided")
    if not (0.0 <= float(getattr(args, "warmup_ratio", 0.05)) <= 1.0):
        p.error("--warmup-ratio must be in [0,1]")
    if int(getattr(args, "warmup_min_steps", 500)) < 0:
        p.error("--warmup-min-steps must be >= 0")
    if float(getattr(args, "ema_decay", 0.999)) <= 0.0 or float(getattr(args, "ema_decay", 0.999)) >= 1.0:
        p.error("--ema-decay must be in (0,1)")
    if int(getattr(args, "torch_ort_memory_opt_level", 0)) < 0:
        p.error("--torch-ort-memory-opt-level must be >= 0")
    try:
        parse_downscale_policy(args.downscale_for_memory)
    except ValueError as ex:
        p.error(str(ex))

    try:
        if args.export_only:
            return export_checkpoint_to_onnx(args.ckpt, args.out_onnx, args.opset)
        if args.data_root is None:
            p.error("--data-root is required unless --export-only")
        if args.trainer_backend == "pytorch":
            return _train_pytorch(args)

        # Experimental ORT path delegates to dedicated module.
        try:
            from ml_specular.train_spec_ort import run_ort_training
        except Exception as ex:
            print(
                "ORT backend is unavailable. Install/verify ORT training deps and artifacts setup.\n"
                f"Import error: {ex}",
                file=sys.stderr,
            )
            return 1
        return run_ort_training(args)
    except KeyboardInterrupt:
        print("\n[abort] Caught Ctrl+C. Exiting safely.", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
