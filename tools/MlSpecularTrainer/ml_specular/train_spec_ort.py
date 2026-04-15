"""Experimental ONNX Runtime training backend for specular predictor.

Requires **onnxruntime-training** and artifacts from
``python -m ml_specular.generate_ort_training_artifacts`` (default ``--loss spec``):

  - ``training_model.onnx`` (preferred) or ``train_model.onnx`` (standalone loss export)
  - ``eval_model.onnx``
  - ``optimizer_model.onnx``
  - ``checkpoint/`` from the artifact generator (bootstrap); then session state in
    ``--ort-checkpoint`` (default ``<ort-artifacts-dir>/ort_training_state``)

User feeds (matched by **name** against ``Module.input_names()``):

  ``input`` (NCHW diffuse), ``target_rgba``, ``valid``, ``transparent_zero_weight`` [1]
  — same as PyTorch ``SpecularManifestDataset`` / ``spec_loss``.
"""

from __future__ import annotations

import sys
from pathlib import Path
from typing import Any

import numpy as np
import torch
from torch.utils.data import DataLoader

from ml_specular.dataset import SpecularManifestDataset

# Names we bind from the dataloader (+ transparent weight). Order is resolved from ``Module.input_names()``.
_ORT_USER_FEED_KEYS = frozenset({"input", "target_rgba", "valid", "transparent_zero_weight"})


def _to_numpy(x: torch.Tensor) -> np.ndarray:
    return x.detach().cpu().numpy().astype(np.float32, copy=False)


def _transparent_zero_weight_feed(args: Any) -> np.ndarray:
    w = float(getattr(args, "transparent_zero_weight", 0.0))
    return np.array([max(w, 0.0)], dtype=np.float32)


def _session_checkpoint_path(args: Any, artifacts_dir: Path) -> Path:
    p = getattr(args, "ort_checkpoint", None)
    if p is not None:
        return Path(p).resolve()
    return (artifacts_dir / "ort_training_state").resolve()


def _artifact_bootstrap_checkpoint(artifacts_dir: Path) -> Path:
    return (artifacts_dir / "checkpoint").resolve()


def _load_ort_api():
    try:
        from onnxruntime.training.api import CheckpointState, Module, Optimizer  # type: ignore

        return CheckpointState, Module, Optimizer
    except Exception as ex:
        raise RuntimeError(
            "onnxruntime.training.api is not available.\n"
            "  pip install onnxruntime-training\n"
            "Use a build that matches your platform (CPU / CUDA) — see:\n"
            "  https://onnxruntime.ai/docs/install/#install-onnx-runtime-training\n"
            "The training package version should align with your onnxruntime / onnx training graphs."
        ) from ex


def _load_checkpoint_state(CheckpointState: Any, artifacts_dir: Path, session_ckpt: Path) -> Any:
    bootstrap = _artifact_bootstrap_checkpoint(artifacts_dir)
    if session_ckpt.exists():
        print(f"[ORT] Loading CheckpointState: {session_ckpt}")
        return CheckpointState.load_checkpoint(str(session_ckpt))
    if bootstrap.exists():
        print(f"[ORT] Bootstrapping CheckpointState from artifact generator: {bootstrap}")
        print(f"[ORT] Future runs will reuse: {session_ckpt}")
        return CheckpointState.load_checkpoint(str(bootstrap))
    msg = (
        "No ORT checkpoint found.\n"
        f"  Session checkpoint: {session_ckpt} (missing)\n"
        f"  Artifact bootstrap: {bootstrap} (missing — run generate_ort_training_artifacts)\n\n"
        "Install the training package:\n"
        "  pip install onnxruntime-training\n"
        "See: https://onnxruntime.ai/docs/install/#install-onnx-runtime-training\n\n"
        "Then generate artifacts, e.g.:\n"
        "  python -m ml_specular.export_ort_forward_core --out forward_model.onnx\n"
        "  python -m ml_specular.generate_ort_training_artifacts --loss spec --out-channels 4 "
        f"--base-onnx forward_model.onnx --artifact-directory {artifacts_dir}"
    )
    print(msg, file=sys.stderr)
    raise RuntimeError("ORT checkpoint missing.")


def _ort_device_str(device: str) -> str:
    d = str(device).strip().lower()
    if d.startswith("cuda"):
        return "cuda" if d == "cuda" else d
    return d or "cpu"


def _forward_module(module: Any, x_np: np.ndarray, y_np: np.ndarray, v_np: np.ndarray, tw_np: np.ndarray) -> Any:
    feed: dict[str, np.ndarray] = {
        "input": x_np,
        "target_rgba": y_np,
        "valid": v_np,
        "transparent_zero_weight": tw_np,
    }
    names: list[str] = list(module.input_names())
    args_list = [feed[n] for n in names if n in feed]
    if not args_list:
        print(
            f"[ORT] Module.input_names() has no overlap with {sorted(feed)}.\n"
            f"    input_names={names}",
            file=sys.stderr,
        )
        raise RuntimeError("ORT Module inputs do not match expected spec_loss feeds.")
    missing = _ORT_USER_FEED_KEYS - {n for n in names if n in feed}
    if missing:
        print(
            f"[ORT] Warning: expected inputs {_ORT_USER_FEED_KEYS}; missing from model: {sorted(missing)}.",
            file=sys.stderr,
        )
    if len(args_list) != len(_ORT_USER_FEED_KEYS):
        print(
            f"[ORT] Warning: passing {len(args_list)} user tensors (names={[n for n in names if n in feed]}); "
            f"expect {_ORT_USER_FEED_KEYS}.",
            file=sys.stderr,
        )
    return module(*args_list)


def run_ort_training(args: Any) -> int:
    CheckpointState, Module, Optimizer = _load_ort_api()

    if str(getattr(args, "spatial_mode", "fixed")) != "fixed":
        print(
            "ORT training backend does not support --spatial-mode native yet. "
            "Use --trainer-backend pytorch with native spatial training, or --spatial-mode fixed.",
            file=sys.stderr,
        )
        return 1

    if args.data_root is None:
        print("--data-root is required for ORT training.", file=sys.stderr)
        return 1

    artifacts_dir = Path(getattr(args, "ort_artifacts_dir", "artifacts/ort")).resolve()
    train_model = artifacts_dir / "training_model.onnx"
    if not train_model.is_file():
        train_model = artifacts_dir / "train_model.onnx"
    eval_model = artifacts_dir / "eval_model.onnx"
    optimizer_model = artifacts_dir / "optimizer_model.onnx"
    missing = [p for p in (train_model, eval_model, optimizer_model) if not p.is_file()]
    if missing:
        print("Missing ORT training artifacts:", file=sys.stderr)
        for p in missing:
            print(f"  - {p}", file=sys.stderr)
        print(
            "Generate with:\n"
            "  python -m ml_specular.export_ort_forward_core --out .../forward_model.onnx\n"
            "  python -m ml_specular.generate_ort_training_artifacts --loss spec --out-channels 4 "
            f"--base-onnx ... --artifact-directory {artifacts_dir}",
            file=sys.stderr,
        )
        return 1

    session_ckpt = _session_checkpoint_path(args, artifacts_dir)
    resume_strict = bool(getattr(args, "resume", False))

    bootstrap_ckpt = _artifact_bootstrap_checkpoint(artifacts_dir)
    if resume_strict and not session_ckpt.exists() and not bootstrap_ckpt.exists():
        print(
            f"--resume requires ORT checkpoint at {session_ckpt} "
            f"or artifact bootstrap at {bootstrap_ckpt}.",
            file=sys.stderr,
        )
        return 1

    try:
        state = _load_checkpoint_state(CheckpointState, artifacts_dir, session_ckpt)
    except RuntimeError:
        return 1

    device_str = _ort_device_str(args.device)
    # API: Module(train_model_uri, state, eval_model_uri=..., device=...)
    try:
        module = Module(str(train_model), state, str(eval_model), device=device_str)
    except Exception as ex:
        print(f"Failed to construct ORT Module(train, state, eval, device={device_str!r}): {ex}", file=sys.stderr)
        print(
            "If your package expects a different constructor, update train_spec_ort.py.\n"
            "Reference: https://onnxruntime.ai/docs/api/python/on_device_training/training_api.html",
            file=sys.stderr,
        )
        return 1

    try:
        opt = Optimizer(str(optimizer_model), module)
    except Exception as ex:
        print(f"Failed to construct ORT Optimizer: {ex}", file=sys.stderr)
        return 1

    if hasattr(opt, "set_learning_rate"):
        try:
            opt.set_learning_rate(float(args.lr))
        except Exception as ex:
            print(f"[ORT] Warning: set_learning_rate failed ({ex}).")

    in_names = module.input_names()
    print(f"[ORT] Module input_names (user feeds matched by name): {in_names}")

    train_ds = SpecularManifestDataset(
        args.data_root, "train", train_res=args.train_res, in_channels=args.in_channels, augment=True
    )
    val_ds = SpecularManifestDataset(
        args.data_root, "val", train_res=args.train_res, in_channels=args.in_channels, augment=False
    )
    if len(train_ds) == 0:
        print("No training samples.", file=sys.stderr)
        return 1

    train_loader = DataLoader(train_ds, batch_size=args.batch, shuffle=True, num_workers=0, drop_last=False)
    val_loader = DataLoader(val_ds, batch_size=args.batch, shuffle=False, num_workers=0, drop_last=False)

    tw = _transparent_zero_weight_feed(args)

    for epoch in range(args.epochs):
        module.train()
        tr_loss = 0.0
        n_tr = 0
        for x, y, valid in train_loader:
            loss = _forward_module(module, _to_numpy(x), _to_numpy(y), _to_numpy(valid), tw)
            opt.step()
            module.lazy_reset_grad()
            tr_loss += float(np.mean(loss)) * x.size(0)
            n_tr += x.size(0)
        tr_loss /= max(n_tr, 1)

        module.eval()
        va_loss = 0.0
        n_va = 0
        for x, y, valid in val_loader:
            loss = _forward_module(module, _to_numpy(x), _to_numpy(y), _to_numpy(valid), tw)
            va_loss += float(np.mean(loss)) * x.size(0)
            n_va += x.size(0)
        va_loss /= max(n_va, 1)
        print(f"[ORT] epoch {epoch + 1}/{args.epochs}  train_loss={tr_loss:.4f}  val_loss={va_loss:.4f}")

    session_ckpt.parent.mkdir(parents=True, exist_ok=True)
    try:
        CheckpointState.save_checkpoint(state, str(session_ckpt), include_optimizer_state=True)
    except Exception as ex:
        print(f"Warning: save_checkpoint include_optimizer_state failed ({ex}); retrying without.", file=sys.stderr)
        try:
            CheckpointState.save_checkpoint(state, str(session_ckpt))
        except Exception as ex2:
            print(f"Failed to save ORT checkpoint: {ex2}", file=sys.stderr)
            return 1
    print(f"[ORT] Saved CheckpointState: {session_ckpt}")

    print(
        "ORT training finished. Export inference ONNX from the trained module if needed, e.g.\n"
        "  module.export_model_for_inferencing(..., graph_output_names=[...])"
    )
    return 0
