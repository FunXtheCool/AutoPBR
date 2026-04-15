"""Validate ORT specular training ONNX artifacts: IO contract + optional PyTorch numerical parity."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
import onnx
import torch

from ml_specular.ort_specular_graph import ORT_LOSS_INPUT_NAMES, ORT_LOSS_OUTPUT_NAMES, build_graph


def _feedable_input_names(model: onnx.ModelProto) -> list[str]:
    init = {t.name for t in model.graph.initializer}
    return [i.name for i in model.graph.input if i.name not in init]


def _output_names(model: onnx.ModelProto) -> list[str]:
    return [o.name for o in model.graph.output]


def _check_io(model: onnx.ModelProto, path: Path, label: str) -> list[str]:
    errors: list[str] = []
    ins = _feedable_input_names(model)
    outs = _output_names(model)
    if ins != list(ORT_LOSS_INPUT_NAMES):
        errors.append(
            f"{label} ({path.name}): expected inputs {list(ORT_LOSS_INPUT_NAMES)}, got {ins}. "
            "Re-export with: python -m ml_specular.export_ort_specular_graphs"
        )
    if outs != list(ORT_LOSS_OUTPUT_NAMES):
        errors.append(
            f"{label} ({path.name}): expected outputs {list(ORT_LOSS_OUTPUT_NAMES)}, got {outs}."
        )
    return errors


def _ort_run(
    onnx_path: Path,
    x: np.ndarray,
    tgt: np.ndarray,
    valid: np.ndarray,
    tw: np.ndarray,
) -> np.ndarray:
    import onnxruntime as ort

    so = ort.SessionOptions()
    session = ort.InferenceSession(str(onnx_path), so, providers=["CPUExecutionProvider"])
    feeds = {
        ORT_LOSS_INPUT_NAMES[0]: x.astype(np.float32, copy=False),
        ORT_LOSS_INPUT_NAMES[1]: tgt.astype(np.float32, copy=False),
        ORT_LOSS_INPUT_NAMES[2]: valid.astype(np.float32, copy=False),
        ORT_LOSS_INPUT_NAMES[3]: tw.astype(np.float32, copy=False),
    }
    out = session.run([ORT_LOSS_OUTPUT_NAMES[0]], feeds)
    return out[0]


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(
        description="Check ORT train/eval ONNX IO against spec_loss contract; optionally compare ORT vs PyTorch."
    )
    p.add_argument(
        "--ort-artifacts-dir",
        type=Path,
        default=Path("artifacts/ort"),
        help="Directory containing train_model.onnx / eval_model.onnx (and optionally optimizer_model.onnx).",
    )
    p.add_argument(
        "--ckpt",
        type=Path,
        default=None,
        help="Optional training .pt; loads core weights for PyTorch vs ONNXRuntime loss parity.",
    )
    p.add_argument("--in-channels", type=int, default=4, choices=(3, 4))
    p.add_argument("--out-channels", type=int, default=4, choices=(4,))
    p.add_argument("--width", type=int, default=64)
    p.add_argument("--train-res", type=int, default=32)
    p.add_argument("--transparent-zero-weight", type=float, default=0.5)
    p.add_argument("--atol", type=float, default=1e-4)
    args = p.parse_args(argv)

    root = args.ort_artifacts_dir.resolve()
    train_path = root / "train_model.onnx"
    eval_path = root / "eval_model.onnx"
    opt_path = root / "optimizer_model.onnx"

    if not train_path.is_file():
        print(f"Missing {train_path}", file=sys.stderr)
        return 1
    if not eval_path.is_file():
        print(f"Missing {eval_path}", file=sys.stderr)
        return 1

    train_model = onnx.load(str(train_path))
    eval_model = onnx.load(str(eval_path))
    onnx.checker.check_model(train_model)
    onnx.checker.check_model(eval_model)

    errors: list[str] = []
    errors.extend(_check_io(train_model, train_path, "train_model"))
    errors.extend(_check_io(eval_model, eval_path, "eval_model"))
    if errors:
        for e in errors:
            print(e, file=sys.stderr)
        return 1

    if not opt_path.is_file():
        print(
            f"Note: {opt_path.name} not found — OK for structure/parity checks. "
            "ORT on-device training still requires optimizer_model.onnx from onnxruntime training artifact tooling."
        )
    else:
        print(f"Found optimizer_model: {opt_path}")

    # PrintTraining command
    data_hint = "multi_dataset"
    print("\nExample ORT training invocation:")
    print(
        f"  py -3.12 -m ml_specular.train_spec --trainer-backend ort --data-root {data_hint} "
        f"--ort-artifacts-dir {root} --transparent-zero-weight {args.transparent_zero_weight:g}"
    )

    ckpt: dict | None = None
    if args.ckpt is not None:
        if not args.ckpt.is_file():
            print(f"Checkpoint not found: {args.ckpt}", file=sys.stderr)
            return 1
        # Full checkpoint may include nested dicts under "args"; avoid weights_only=True for compatibility.
        ckpt = torch.load(args.ckpt, map_location="cpu", weights_only=False)

    in_ch = int(ckpt["in_channels"]) if ckpt is not None else int(args.in_channels)
    out_ch = int(ckpt.get("out_channels", args.out_channels)) if ckpt is not None else int(args.out_channels)
    width = int(ckpt.get("width", args.width)) if ckpt is not None else int(args.width)
    if ckpt is not None and (in_ch, out_ch, width) != (
        int(args.in_channels),
        int(args.out_channels),
        int(args.width),
    ):
        print(
            f"Note: random tensors use checkpoint architecture in_ch={in_ch} out_ch={out_ch} width={width} "
            f"(CLI had {args.in_channels}/{args.out_channels}/{args.width})."
        )

    rng = np.random.default_rng(0)
    n, h, w = 2, int(args.train_res), int(args.train_res)
    x = rng.standard_normal((n, in_ch, h, w), dtype=np.float32)
    tgt = rng.random((n, 4, h, w), dtype=np.float32)
    valid = rng.random((n, h, w), dtype=np.float32)
    valid = (valid > 0.3).astype(np.float32)
    tw = np.array([max(float(args.transparent_zero_weight), 0.0)], dtype=np.float32)

    ort_train = _ort_run(train_path, x, tgt, valid, tw).reshape(-1)
    ort_eval = _ort_run(eval_path, x, tgt, valid, tw).reshape(-1)
    if ort_train.shape != (1,) or ort_eval.shape != (1,):
        print(f"Unexpected ORT loss shape: train={ort_train.shape} eval={ort_eval.shape}", file=sys.stderr)
        return 1
    if float(np.abs(ort_train - ort_eval).max()) > float(args.atol):
        print(f"train vs eval ONNX loss mismatch: train={ort_train!r} eval={ort_eval!r}", file=sys.stderr)
        return 1

    if ckpt is None:
        print("\nIO check OK. Skipping PyTorch parity (--ckpt not set).")
        print("For parity: export graphs with matching weights, e.g.")
        print(
            f"  py -3.12 -m ml_specular.export_ort_specular_graphs "
            f"--out-dir {root} --ckpt <same.pt> --in-channels {args.in_channels} "
            f"--out-channels {args.out_channels} --width {args.width}"
        )
        return 0

    g = build_graph(in_ch, out_ch, width)
    g.core.load_state_dict(ckpt["model"])
    g.eval()

    xt = torch.from_numpy(x)
    tt = torch.from_numpy(tgt)
    vt = torch.from_numpy(valid)
    wt = torch.from_numpy(tw)
    with torch.no_grad():
        ref = g(xt, tt, vt, wt).numpy().reshape(-1)

    delta = float(np.abs(ref - ort_train).max())
    print(f"\nPyTorch vs ORT (train_model) max |Δloss| = {delta:.6g} (atol={args.atol:g})")
    if delta > float(args.atol):
        print(
            "Parity failed. Ensure export used the same --ckpt / in-channels / out-channels / width as this check.",
            file=sys.stderr,
        )
        return 1

    print("Parity check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
