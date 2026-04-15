"""Export forward-only (logits) ONNX for onnxruntime.training.artifacts.generate_artifacts."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import torch
import torch.nn as nn

from ml_specular.model import DilatedPbrNet


class _ForwardExport(nn.Module):
    def __init__(self, core: DilatedPbrNet) -> None:
        super().__init__()
        self.core = core

    def forward(self, input: torch.Tensor) -> torch.Tensor:
        return self.core(input)


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(
        description="Export DilatedPbrNet forward to ONNX (output: logits). Used as --base-onnx for ORT artifact generation."
    )
    p.add_argument("--out", type=Path, default=Path("artifacts/ort/forward_model.onnx"))
    p.add_argument("--in-channels", type=int, default=4, choices=(3, 4))
    p.add_argument("--out-channels", type=int, default=4, choices=(4,))
    p.add_argument("--width", type=int, default=64)
    p.add_argument("--opset", type=int, default=17)
    p.add_argument("--train-res", type=int, default=128)
    p.add_argument("--ckpt", type=Path, default=None, help="Optional .pt to load core weights.")
    args = p.parse_args(argv)

    core = DilatedPbrNet(in_channels=args.in_channels, out_channels=args.out_channels, width=args.width)
    if args.ckpt is not None:
        if not args.ckpt.is_file():
            print(f"Checkpoint not found: {args.ckpt}", file=sys.stderr)
            return 1
        ckpt = torch.load(args.ckpt, map_location="cpu", weights_only=True)
        core.load_state_dict(ckpt["model"])
    net = _ForwardExport(core).eval().cpu()

    h = w = int(args.train_res)
    dummy = torch.randn(1, args.in_channels, h, w, dtype=torch.float32)
    out_path = args.out.resolve()
    out_path.parent.mkdir(parents=True, exist_ok=True)

    torch.onnx.export(
        net,
        dummy,
        str(out_path),
        input_names=["input"],
        output_names=["logits"],
        opset_version=int(args.opset),
        do_constant_folding=True,
        dynamo=False,
        dynamic_axes={"input": {0: "batch", 2: "height", 3: "width"}, "logits": {0: "batch", 2: "height", 3: "width"}},
    )
    print(f"Wrote {out_path}")
    print("Next: python -m ml_specular.generate_ort_training_artifacts --base-onnx " + str(out_path))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
