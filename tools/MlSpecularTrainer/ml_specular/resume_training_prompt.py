"""Interactive resume for train_spec: load checkpoint metadata and prompt with safe defaults.

Run: python -m ml_specular.resume_training_prompt
     python -m ml_specular.resume_training_prompt --torch-ort
"""

from __future__ import annotations

import argparse
import os
import re
import shlex
import sys
from pathlib import Path
from typing import Any

import torch


def _trainer_tool_root() -> Path:
    """Directory containing ml_specular (tools/MlSpecularTrainer)."""
    return Path(__file__).resolve().parent.parent


def _base_name_stem(path: Path) -> str:
    """specular_predictor / specular_predictor.best / specular_predictor.resume -> specular_predictor."""
    s = path.stem
    if s.endswith(".best"):
        return s[: -len(".best")]
    if s.endswith(".resume"):
        return s[: -len(".resume")]
    return s


def _related_checkpoint_paths(path: Path) -> list[Path]:
    """Primary, sibling .best., and .resume. variants in the same folder."""
    parent = path.parent
    suf = path.suffix
    base = _base_name_stem(path)
    names = (f"{base}{suf}", f"{base}.best{suf}", f"{base}.resume{suf}")
    out: list[Path] = []
    seen: set[Path] = set()
    for n in names:
        p = (parent / n).resolve()
        if p.is_file() and p not in seen:
            seen.add(p)
            out.append(p)
    return out


def _scalar_to_int(v: Any) -> int | None:
    if v is None:
        return None
    if hasattr(v, "item"):
        try:
            return int(v.item())
        except Exception:
            pass
    try:
        return int(v)
    except (TypeError, ValueError):
        return None


def _scalar_to_float(v: Any) -> float | None:
    if v is None:
        return None
    if hasattr(v, "item"):
        try:
            return float(v.item())
        except Exception:
            pass
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def _epoch_from_dict(data: dict[str, Any]) -> int | None:
    for key in ("epoch", "last_epoch", "global_step"):
        if key in data:
            n = _scalar_to_int(data[key])
            if n is not None:
                return n
    return None


def _best_val_from_dict(data: dict[str, Any]) -> float | None:
    for key in ("best_val", "best_val_loss", "val_loss", "best"):
        if key in data:
            f = _scalar_to_float(data[key])
            if f is not None and f == f:  # not NaN
                return f
    return None


def _cfg_from_checkpoint_dict(data: dict[str, Any]) -> dict[str, Any] | None:
    inner = data.get("args")
    if isinstance(inner, dict) and inner:
        return dict(inner)
    # Legacy / partial: hyperparams at root (no nested args)
    if "in_channels" in data and "width" in data:
        return {
            "data_root": data.get("data_root"),
            "train_res": int(data.get("train_res", 128)),
            "spatial_mode": str(data.get("spatial_mode", "fixed")),
            "grad_accum_steps": int(data.get("grad_accum_steps", 1)),
            "max_train_side": data.get("max_train_side"),
            "in_channels": int(data["in_channels"]),
            "out_channels": int(data.get("out_channels", 4)),
            "width": int(data["width"]),
            "batch": int(data.get("batch", 8)),
            "lr": float(data.get("lr", 1e-3)),
            "transparent_zero_weight": float(data.get("transparent_zero_weight", 0.5)),
            "trainer_backend": str(data.get("trainer_backend", "pytorch")),
            "torch_ort": bool(data.get("torch_ort", False)),
            "torch_ort_provider": str(data.get("torch_ort_provider", "cuda")).lower(),
            "torch_ort_tensorrt_fp16": bool(data.get("torch_ort_tensorrt_fp16", True)),
            "torch_ort_memory_opt_level": int(data.get("torch_ort_memory_opt_level", 0)),
            "torch_ort_triton_op_enabled": bool(data.get("torch_ort_triton_op_enabled", False)),
            "torch_ort_zero_stage3_support": bool(data.get("torch_ort_zero_stage3_support", False)),
            "batch_policy_enabled": bool(data.get("batch_policy_enabled", True)),
            "batch_policy_lr_mode": str(data.get("batch_policy_lr_mode", "sqrt")),
            "batch_policy_baseline_batch": int(data.get("batch_policy_baseline_batch", 8)),
            "batch_policy_baseline_lr": float(data.get("batch_policy_baseline_lr", 1e-3)),
            "batch_policy_max_lr": data.get("batch_policy_max_lr"),
            "warmup_ratio": float(data.get("warmup_ratio", 0.05)),
            "warmup_min_steps": int(data.get("warmup_min_steps", 500)),
            "weight_decay_mode": str(data.get("weight_decay_mode", "off")),
            "grad_clip_norm": float(data.get("grad_clip_norm", 0.0)),
            "ema_enabled": bool(data.get("ema_enabled", False)),
            "ema_decay": float(data.get("ema_decay", 0.999)),
        }
    return None


def _merge_meta_across_files(paths: list[Path]) -> tuple[dict[str, Any] | None, int, float | None, bool, list[str]]:
    """
    Load related checkpoint files; take max epoch index, run config from that file (or best available).
    best_val: minimum loss seen across files that record it.
    """
    notes: list[str] = []
    rows: list[tuple[Path, dict[str, Any], int, float | None]] = []
    any_v2 = False

    for path in paths:
        try:
            data = torch.load(path, map_location="cpu", weights_only=False)
        except Exception as ex:
            notes.append(f"skip {path.name}: {ex}")
            continue
        if not isinstance(data, dict):
            notes.append(f"skip {path.name}: not a dict")
            continue
        if data.get("format") == "autopbr-spec-train-v2":
            any_v2 = True
        ep_raw = _epoch_from_dict(data)
        ep = ep_raw if ep_raw is not None else -1
        bv = _best_val_from_dict(data)
        rows.append((path, data, ep, bv))

    if not rows:
        return None, -1, None, False, notes

    max_epoch = max(r[2] for r in rows)
    min_best: float | None = None
    for *_, bv in rows:
        if bv is not None:
            if min_best is None or bv < min_best:
                min_best = bv

    best_cfg: dict[str, Any] | None = None
    for path, data, ep, _bv in rows:
        if ep == max_epoch:
            best_cfg = _cfg_from_checkpoint_dict(data)
            if best_cfg:
                notes.append(f"run config from {path.name} (epoch index {max_epoch})")
                break
    if best_cfg is None:
        for path, data, _ep, _bv in rows:
            best_cfg = _cfg_from_checkpoint_dict(data)
            if best_cfg:
                notes.append(f"run config (partial) from {path.name}")
                break

    return best_cfg, max_epoch, min_best, any_v2, notes


def _parse_last_session_train_spec() -> dict[str, Any]:
    """Parse .last_training_session.{bat,sh} for train_spec argv (trainer root and cwd)."""
    raw: dict[str, Any] = {}
    for root in (_trainer_tool_root(), Path.cwd()):
        for fname in (".last_training_session.sh", ".last_training_session.bat"):
            p = root / fname
            if not p.is_file():
                continue
            text = p.read_text(encoding="utf-8", errors="replace")
            # Drop bat noise; join continuations
            lines: list[str] = []
            for line in text.splitlines():
                t = line.strip()
                if not t or t.lower().startswith("@echo") or t.lower().startswith("setlocal"):
                    continue
                if t.lower().startswith("cd "):
                    continue
                if t.endswith("^"):
                    t = t[:-1].rstrip() + " "
                lines.append(t)
            blob = " ".join(lines)
            m = re.search(r"ml_specular\.train_spec\s+(.*)$", blob, re.DOTALL | re.IGNORECASE)
            if not m:
                continue
            tail = m.group(1).strip()
            try:
                parts = shlex.split(tail, posix=os.name != "nt")
            except ValueError:
                continue
            i = 0
            while i < len(parts):
                tok = parts[i]
                if not tok.startswith("--"):
                    i += 1
                    continue
                key = tok[2:].replace("-", "_")
                if i + 1 < len(parts) and not parts[i + 1].startswith("--"):
                    raw[key] = parts[i + 1]
                    i += 2
                else:
                    raw[key] = True
                    i += 1
            if raw:
                break
        if raw:
            break

    return _normalize_parsed_argv(raw)


def _normalize_parsed_argv(raw: dict[str, Any]) -> dict[str, Any]:
    out: dict[str, Any] = dict(raw)
    int_keys = {
        "epochs",
        "batch",
        "train_res",
        "grad_accum_steps",
        "workers",
        "in_channels",
        "width",
        "out_channels",
        "opset",
    }
    float_keys = {"lr", "transparent_zero_weight"}
    for k in int_keys:
        if k in out and out[k] is not True:
            try:
                out[k] = int(str(out[k]).strip().strip('"'))
            except ValueError:
                del out[k]
    for k in float_keys:
        if k in out and out[k] is not True:
            try:
                out[k] = float(str(out[k]).strip().strip('"'))
            except ValueError:
                del out[k]
    if "torch_ort" in out:
        out["torch_ort"] = out["torch_ort"] is True or str(out["torch_ort"]).lower() in ("true", "1", "yes")
    return out


def _merge_cfg(
    ckpt_cfg: dict[str, Any] | None,
    session: dict[str, Any],
) -> tuple[dict[str, Any] | None, list[str]]:
    """Checkpoint wins; fill gaps from last session file."""
    notes: list[str] = []
    if not session:
        return ckpt_cfg, notes
    if not ckpt_cfg:
        notes.append("Defaults filled from .last_training_session.* (no usable args in checkpoint).")
        return dict(session), notes
    merged = dict(ckpt_cfg)
    for k, v in session.items():
        if k in merged and merged[k] not in (None, ""):
            continue
        merged[k] = v
        notes.append(f"filled {k} from last session file")
    return merged, notes


def _inp(prompt: str, default: str) -> str:
    try:
        line = input(f"{prompt} [{default}]: ").strip()
    except EOFError:
        return default
    return line if line else default


def _inp_int(prompt: str, default: int) -> int:
    s = _inp(prompt, str(default))
    try:
        return int(s)
    except ValueError:
        print(f"  (invalid integer, using {default})")
        return default


def _inp_float(prompt: str, default: float) -> float:
    s = _inp(prompt, str(default))
    try:
        return float(s)
    except ValueError:
        print(f"  (invalid float, using {default})")
        return default


def _yes(prompt: str, default_yes: bool = True) -> bool:
    d = "Y/n" if default_yes else "y/N"
    s = _inp(f"{prompt} ({d})", "y" if default_yes else "n").lower()
    if not s:
        return default_yes
    return s in ("y", "yes")


def run_interactive(torch_ort_workflow: bool) -> int:
    from ml_specular.train_spec import _resolve_resume_load_path  # noqa: PLC0415

    print()
    print("========================================")
    print("  Resume training (defaults from checkpoint)")
    print("  Torch-ORT workflow:" if torch_ort_workflow else "  PyTorch workflow:")
    print("========================================")
    print()
    print(
        "Strict resume checks require these to match the checkpoint unless you use "
        "--resume-allow-config-mismatch:\n"
        "  data_root, train_res, spatial_mode, grad_accum_steps, in/out channels, width, trainer_backend, "
        "torch_ort + provider/TensorRT FP16 (ORT memory/Triton/ZeRO3 toggles are not strict).\n"
        "Usually safe to change: total --epochs (extend run), --batch, --lr, --workers, --device, output paths.\n"
    )

    ckpt_write = Path(_inp("Primary training checkpoint path (--ckpt; written each epoch)", "artifacts/specular_predictor.pt"))
    ns = argparse.Namespace(resume_from=None, ckpt=ckpt_write)
    resume_load = _resolve_resume_load_path(ns)
    if not resume_load.is_file():
        print(f"No checkpoint to load at {resume_load} (resolved from --ckpt).", file=sys.stderr)
        return 1

    related = _related_checkpoint_paths(resume_load)
    ck_cfg, last_epoch, best_val, any_v2, merge_ck_notes = _merge_meta_across_files(related)
    session = _parse_last_session_train_spec()
    cfg, merge_sess_notes = _merge_cfg(ck_cfg, session)

    start_next = last_epoch + 1
    print()
    print(f"  Resolved load file: {resume_load}")
    if len(related) > 1:
        print(f"  Also scanned related: {', '.join(p.name for p in related if p != resume_load)}")
    for line in merge_ck_notes:
        print(f"  [checkpoint] {line}")
    for line in merge_sess_notes:
        print(f"  [session] {line}")
    print(f"  Last completed epoch index: {last_epoch}  (continue from step {start_next})")
    if best_val is not None:
        print(f"  Best val loss (lowest seen in scanned files): {best_val:.6f}")
    else:
        print("  Best val loss: (not recorded in checkpoint files)")
    if not any_v2 and last_epoch < 0 and ck_cfg is None:
        print(
            "  [warn] No autopbr-spec-train-v2 metadata and no epoch index; "
            "if this is weights-only, set hyperparameters manually or use a full training checkpoint."
        )
    elif not any_v2 and last_epoch >= 0:
        print(
            "  [info] Format tag missing but epoch/args were read (older or partial save). "
            "Bracket defaults use embedded args and/or last session file."
        )

    def g(key: str, fallback: Any) -> Any:
        if cfg is None:
            return fallback
        v = cfg.get(key, fallback)
        if v is None or v == "":
            return fallback
        return v

    dr_raw = g("data_root", None)
    dr_disp = "multi_dataset"
    if dr_raw:
        try:
            p = Path(str(dr_raw))
            cwd = Path.cwd()
            if p.is_absolute():
                try:
                    dr_disp = str(p.relative_to(cwd))
                except ValueError:
                    dr_disp = str(p)
            else:
                dr_disp = str(p)
        except Exception:
            dr_disp = "multi_dataset"

    tr = int(g("train_res", 128))
    spatial_mode = str(g("spatial_mode", "fixed")).strip().lower()
    if spatial_mode not in ("fixed", "native"):
        spatial_mode = "fixed"
    accum_def = int(g("grad_accum_steps", 1))
    inch = int(g("in_channels", 4))
    outch = int(g("out_channels", 4))
    wid = int(g("width", 64))
    tz = float(g("transparent_zero_weight", 0.5))
    ck_torch_ort = bool(g("torch_ort", False))
    ck_provider = str(g("torch_ort_provider", "cuda")).lower()
    if ck_provider not in ("cuda", "tensorrt"):
        ck_provider = "cuda"
    ck_trt_fp16 = bool(g("torch_ort_tensorrt_fp16", True))
    trt_fp16 = ck_trt_fp16

    batch_def = int(g("batch", 8))
    lr_def = float(g("lr", 1e-3))
    bp_enabled = bool(g("batch_policy_enabled", True))
    bp_lr_mode = str(g("batch_policy_lr_mode", "sqrt"))
    if bp_lr_mode not in ("off", "sqrt", "linear"):
        bp_lr_mode = "sqrt"
    bp_base_batch = int(g("batch_policy_baseline_batch", 8))
    bp_base_lr = float(g("batch_policy_baseline_lr", 1e-3))
    bp_max_lr_raw = g("batch_policy_max_lr", None)
    bp_max_lr = float(bp_max_lr_raw) if bp_max_lr_raw not in (None, "") else None
    bp_warmup_ratio = float(g("warmup_ratio", 0.05))
    bp_warmup_min_steps = int(g("warmup_min_steps", 500))
    bp_wd_mode = str(g("weight_decay_mode", "off"))
    if bp_wd_mode not in ("off", "mild_batch_scaled"):
        bp_wd_mode = "off"
    bp_grad_clip = float(g("grad_clip_norm", 0.0))
    bp_ema_enabled = bool(g("ema_enabled", False))
    bp_ema_decay = float(g("ema_decay", 0.999))

    if torch_ort_workflow:
        if cfg is not None and not ck_torch_ort:
            print(
                "\n  [warn] Checkpoint was trained without torch-ort; resuming with ORTModule may fail strict checks.\n"
            )
    else:
        if ck_torch_ort:
            print(
                "\n  [error] Checkpoint was trained with --torch-ort. Use the Torch-ORT workflow menu "
                "(Resume training there) or run with --resume-allow-config-mismatch at your own risk.\n",
                file=sys.stderr,
            )
            if not _yes("Continue anyway (will likely fail strict resume)", default_yes=False):
                return 1

    print()
    print("--- Match checkpoint (change only if you know compatibility implications) ---")
    data_root = _inp("Dataset folder (--data-root)", dr_disp if isinstance(dr_disp, str) else "multi_dataset")
    changed_dataset = bool(dr_raw) and str(data_root) != str(dr_disp)
    if changed_dataset:
        print(
            "  [info] Dataset root differs from the checkpoint. This is a warm-start on a new manifest; "
            "strict resume checks will only pass if you allow config/dataset fingerprint mismatches below."
        )
    train_res = _inp_int("Train resolution (--train-res)", tr)
    spatial_mode = _inp("Spatial mode fixed or native (--spatial-mode)", spatial_mode).strip().lower()
    if spatial_mode not in ("fixed", "native"):
        print("  (invalid spatial-mode; using fixed)")
        spatial_mode = "fixed"
    accum_def = _inp_int("Grad accumulation steps (--grad-accum-steps)", accum_def)
    in_ch = _inp_int("In-channels 3 or 4 (--in-channels)", inch)
    if in_ch not in (3, 4):
        print("  (forcing in-channels to 4)")
        in_ch = 4
    out_ch = _inp_int("Out-channels (--out-channels)", outch)
    if out_ch != 4:
        print("  (forcing out-channels to 4)")
        out_ch = 4
    width = _inp_int("Model width (--width)", wid)
    tzw = _inp_float("Transparent zero weight (--transparent-zero-weight)", tz)

    provider = ck_provider
    ort_mem_opt = 0
    ort_triton = False
    ort_zero3 = False
    if torch_ort_workflow:
        ck_mem_opt = int(g("torch_ort_memory_opt_level", 0))
        ck_triton = bool(g("torch_ort_triton_op_enabled", False))
        ck_zero3 = bool(g("torch_ort_zero_stage3_support", False))
        print()
        print("--- Torch-ORT (must match checkpoint for strict resume) ---")
        provider = _inp("torch-ort provider cuda or tensorrt (--torch-ort-provider)", ck_provider).lower()
        if provider not in ("cuda", "tensorrt"):
            print("  (invalid; using cuda)")
            provider = "cuda"
        if provider == "tensorrt":
            trt_fp16 = _yes(
                "TensorRT FP16 engine (--torch-ort-tensorrt-fp16; use --no-torch-ort-tensorrt-fp16 if No)",
                default_yes=ck_trt_fp16,
            )
        print()
        print("--- ORTModule options (tunable on resume; not strict vs checkpoint) ---")
        ort_mem_opt = _inp_int("ORTModule memory optimizer level (--torch-ort-memory-opt-level)", ck_mem_opt)
        if ort_mem_opt < 0:
            print("  (invalid; using 0)")
            ort_mem_opt = 0
        ort_triton = _yes(
            "Enable TritonOp (--torch-ort-triton-op-enabled)",
            default_yes=ck_triton,
        )
        ort_zero3 = _yes(
            "Enable ZeRO stage3 support (--torch-ort-zero-stage3-support)",
            default_yes=ck_zero3,
        )

    default_epochs = max(40, start_next + 10) if last_epoch >= 0 else int(g("epochs", 40))
    print()
    print("--- Safe to adjust (training hyperparameters / targets) ---")
    epochs = _inp_int(
        f"Total --epochs (must be > last epoch index {last_epoch}; you continue from step {start_next})",
        default_epochs,
    )
    if last_epoch >= 0 and epochs <= last_epoch:
        print(
            f"  [warn] --epochs ({epochs}) <= last epoch index ({last_epoch}); nothing will train. "
            f"Increase total epochs.",
            file=sys.stderr,
        )
    batch = _inp_int("Batch size (--batch)", batch_def)
    lr = _inp_float("Learning rate (--lr)", lr_def)
    print()
    print("--- Batch scaling safety policy ---")
    bp_enabled = _yes("Enable batch policy (--batch-policy-enabled)", default_yes=bp_enabled)
    bp_lr_mode = _inp("Batch policy LR mode off/sqrt/linear (--batch-policy-lr-mode)", bp_lr_mode).strip().lower()
    if bp_lr_mode not in ("off", "sqrt", "linear"):
        print("  (invalid mode; using sqrt)")
        bp_lr_mode = "sqrt"
    bp_base_batch = _inp_int("Baseline effective batch (--batch-policy-baseline-batch)", bp_base_batch)
    bp_base_lr = _inp_float("Baseline LR (--batch-policy-baseline-lr)", bp_base_lr)
    bp_max_lr_in = _inp("Max scaled LR (--batch-policy-max-lr; empty=none)", "" if bp_max_lr is None else str(bp_max_lr)).strip()
    bp_max_lr = float(bp_max_lr_in) if bp_max_lr_in else None
    bp_warmup_ratio = _inp_float("Warmup ratio (--warmup-ratio)", bp_warmup_ratio)
    bp_warmup_min_steps = _inp_int("Warmup min steps (--warmup-min-steps)", bp_warmup_min_steps)
    bp_wd_mode = _inp("Weight decay mode off/mild_batch_scaled (--weight-decay-mode)", bp_wd_mode).strip().lower()
    if bp_wd_mode not in ("off", "mild_batch_scaled"):
        print("  (invalid mode; using off)")
        bp_wd_mode = "off"
    bp_grad_clip = _inp_float("Grad clip norm (--grad-clip-norm; <=0 disables)", bp_grad_clip)
    bp_ema_enabled = _yes("Enable EMA (--ema-enabled)", default_yes=bp_ema_enabled)
    bp_ema_decay = _inp_float("EMA decay (--ema-decay)", bp_ema_decay)
    workers = _inp_int("DataLoader workers (-1=auto) (--workers)", int(g("workers", -1)))
    device = _inp("Device (--device)", str(g("device", "cuda")))

    ck_default = str(g("ckpt", str(ckpt_write)))
    ckpt_out = Path(_inp("Write training state to (--ckpt)", ck_default))
    best_def = str(g("best_ckpt", str(ckpt_out.with_name(f"{ckpt_out.stem}.best{ckpt_out.suffix}"))))
    best_ck = Path(_inp("Best-val checkpoint (--best-ckpt)", best_def))
    out_onnx = Path(_inp("Output ONNX (--out-onnx)", str(g("out_onnx", "artifacts/specular_predictor.onnx"))))
    backup = _inp("Optional backup duplicate path (--resume-ckpt, empty=none)", "").strip()
    resume_ckpt_arg: list[str] = []
    if backup:
        resume_ckpt_arg = ["--resume-ckpt", backup]

    print()
    resume_auto = _yes("Use --resume-auto (continue if state exists; recommended)", default_yes=True)
    use_resume_strict = False
    if not resume_auto:
        use_resume_strict = _yes("Use --resume instead (fail if checkpoint missing)", default_yes=False)
        if not use_resume_strict:
            resume_auto = True
            print("  (defaulting to --resume-auto)")

    reset_opt = _yes("Reset optimizer on load (--reset-optimizer)", default_yes=False)
    allow_mismatch = _yes(
        "Allow config/dataset fingerprint mismatch (--resume-allow-config-mismatch)",
        default_yes=changed_dataset,
    )
    no_amp = _yes("Disable AMP on CUDA (--no-amp)", default_yes=False)

    argv: list[str] = [
        "--trainer-backend",
        "pytorch",
        "--data-root",
        data_root,
        "--epochs",
        str(epochs),
        "--batch",
        str(batch),
        "--lr",
        str(lr),
        "--batch-policy-lr-mode",
        bp_lr_mode,
        "--batch-policy-baseline-batch",
        str(bp_base_batch),
        "--batch-policy-baseline-lr",
        str(bp_base_lr),
        "--warmup-ratio",
        str(bp_warmup_ratio),
        "--warmup-min-steps",
        str(bp_warmup_min_steps),
        "--weight-decay-mode",
        bp_wd_mode,
        "--grad-clip-norm",
        str(bp_grad_clip),
        "--ema-decay",
        str(bp_ema_decay),
        "--train-res",
        str(train_res),
        "--spatial-mode",
        spatial_mode,
        "--grad-accum-steps",
        str(accum_def),
        "--workers",
        str(workers),
        "--in-channels",
        str(in_ch),
        "--width",
        str(width),
        "--out-channels",
        str(out_ch),
        "--transparent-zero-weight",
        str(tzw),
        "--device",
        device,
        "--ckpt",
        str(ckpt_out),
        "--best-ckpt",
        str(best_ck),
        "--out-onnx",
        str(out_onnx),
        "--opset",
        "17",
    ]
    argv.append("--batch-policy-enabled" if bp_enabled else "--no-batch-policy-enabled")
    if bp_max_lr is not None:
        argv.extend(["--batch-policy-max-lr", str(bp_max_lr)])
    argv.append("--ema-enabled" if bp_ema_enabled else "--no-ema-enabled")
    argv.extend(resume_ckpt_arg)
    if torch_ort_workflow:
        argv.extend(["--torch-ort", "--torch-ort-provider", provider])
        if provider == "tensorrt" and not trt_fp16:
            argv.append("--no-torch-ort-tensorrt-fp16")
        argv.extend(["--torch-ort-memory-opt-level", str(ort_mem_opt)])
        if ort_triton:
            argv.append("--torch-ort-triton-op-enabled")
        else:
            argv.append("--no-torch-ort-triton-op-enabled")
        if ort_zero3:
            argv.append("--torch-ort-zero-stage3-support")
        else:
            argv.append("--no-torch-ort-zero-stage3-support")
    if resume_auto:
        argv.append("--resume-auto")
    elif use_resume_strict:
        argv.append("--resume")
    if reset_opt:
        argv.append("--reset-optimizer")
    if allow_mismatch:
        argv.append("--resume-allow-config-mismatch")
    if no_amp:
        argv.append("--no-amp")

    print()
    print("Command:")
    print(f"  python -m ml_specular.train_spec {' '.join(argv)}")
    print()
    if not _yes("Run now", default_yes=True):
        print("Cancelled.")
        return 0

    from ml_specular.train_spec import main as train_spec_main  # noqa: PLC0415

    return train_spec_main(argv)


def main() -> int:
    p = argparse.ArgumentParser(description="Interactive resume prompts for train_spec.")
    p.add_argument(
        "--torch-ort",
        action="store_true",
        help="Torch-ORT workflow (sets --torch-ort and prompts for provider).",
    )
    args = p.parse_args()
    return run_interactive(torch_ort_workflow=bool(args.torch_ort))


if __name__ == "__main__":
    raise SystemExit(main())
