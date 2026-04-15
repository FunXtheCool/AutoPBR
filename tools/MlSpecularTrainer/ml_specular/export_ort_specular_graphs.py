"""Export train_model.onnx / eval_model.onnx for ORT specular training (loss graph = spec_loss contract)."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import torch

from ml_specular.ort_specular_graph import ORT_LOSS_INPUT_NAMES, ORT_LOSS_OUTPUT_NAMES, build_graph


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(
        description=(
            "Export ONNX graphs with inputs "
            f"{list(ORT_LOSS_INPUT_NAMES)} and output {list(ORT_LOSS_OUTPUT_NAMES)}. "
            "Train and eval exports are the same forward (full-batch loss). "
            "ORT optimizer_model.onnx must still be produced by onnxruntime training artifact tooling."
        )
    )
    p.add_argument("--out-dir", type=Path, default=Path("artifacts/ort"))
    p.add_argument("--train-onnx", type=Path, default=None, help="Default: <out-dir>/train_model.onnx")
    p.add_argument("--eval-onnx", type=Path, default=None, help="Default: <out-dir>/eval_model.onnx")
    p.add_argument("--in-channels", type=int, default=4, choices=(3, 4))
    p.add_argument("--out-channels", type=int, default=4, choices=(4,))
    p.add_argument("--width", type=int, default=64)
    p.add_argument("--opset", type=int, default=17)
    p.add_argument(
        "--ckpt",
        type=Path,
        default=None,
        help="Optional .pt checkpoint (loads model weights into core net; architecture must match).",
    )
    p.add_argument("--train-res", type=int, default=128, help="Dummy spatial size for tracing.")
    args = p.parse_args(argv)

    out_dir = args.out_dir.resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    train_path = (args.train_onnx or (out_dir / "train_model.onnx")).resolve()
    eval_path = (args.eval_onnx or (out_dir / "eval_model.onnx")).resolve()

    g = build_graph(args.in_channels, args.out_channels, args.width)
    if args.ckpt is not None:
        if not args.ckpt.is_file():
            print(f"Checkpoint not found: {args.ckpt}", file=sys.stderr)
            return 1
        ckpt = torch.load(args.ckpt, map_location="cpu", weights_only=True)
        g.core.load_state_dict(ckpt["model"])
    g.eval()
    g.cpu()

    n = 1
    h = w = int(args.train_res)
    dummy_in = torch.randn(n, args.in_channels, h, w, dtype=torch.float32)
    dummy_tgt = torch.rand(n, 4, h, w, dtype=torch.float32)
    dummy_valid = torch.ones(n, h, w, dtype=torch.float32)
    dummy_tw = torch.tensor([0.5], dtype=torch.float32)

    dummy_args = (dummy_in, dummy_tgt, dummy_valid, dummy_tw)
    torch.onnx.export(
        g,
        dummy_args,
        str(train_path),
        input_names=list(ORT_LOSS_INPUT_NAMES),
        output_names=list(ORT_LOSS_OUTPUT_NAMES),
        opset_version=int(args.opset),
        do_constant_folding=True,
        dynamo=False,
        dynamic_axes={
            ORT_LOSS_INPUT_NAMES[0]: {0: "batch", 2: "height", 3: "width"},
            ORT_LOSS_INPUT_NAMES[1]: {0: "batch", 2: "height", 3: "width"},
            ORT_LOSS_INPUT_NAMES[2]: {0: "batch", 1: "height", 2: "width"},
        },
    )
    # Identical forward for eval (loss-only). ORT may require separate files for train vs eval sessions.
    import shutil

    shutil.copyfile(train_path, eval_path)

    print(f"Wrote {train_path}")
    print(f"Wrote {eval_path} (copy of train graph)")
    print(
        "Next: run `python -m ml_specular.verify_ort_specular_training --ort-artifacts-dir "
        f"{out_dir}` to validate IO and numerical parity with PyTorch."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
