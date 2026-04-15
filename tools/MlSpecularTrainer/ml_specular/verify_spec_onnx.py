"""Verify ONNX contract for direct specular predictor (dynamic H/W, RGBA).

Also checks that RGBA looks like linear [0,1] as produced by
train_spec export (sigmoid wrapper). See repo docs/ml-specular-labpbr-contract.md for LabPBR mapping.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
import onnx
import onnxruntime as ort


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
LabPBR _s channel order (NCHW dim 1 or NHWC last dim):
  0=R smoothness, 1=G F0/metal (>=230 metal in AutoPBR heuristics),
  2=B porosity/subsurface, 3=A emission (255=no emission in bytes),
  4=optional unused channel (legacy models may still export five channels; only RGBA is used).
""",
    )
    ap.add_argument("onnx_path", type=Path)
    ap.add_argument(
        "--no-range-check",
        action="store_true",
        help="Skip check that outputs are in ~[0,1] (for legacy models that emit logits or 0-255).",
    )
    args = ap.parse_args(argv)

    model = onnx.load(str(args.onnx_path))
    onnx.checker.check_model(model)

    inp = model.graph.input[0]
    out = model.graph.output[0]
    print("input:", inp.name, [d.dim_value or d.dim_param for d in inp.type.tensor_type.shape.dim])
    print("output (first):", out.name, [d.dim_value or d.dim_param for d in out.type.tensor_type.shape.dim])

    sess = ort.InferenceSession(str(args.onnx_path), providers=["CPUExecutionProvider"])
    in_meta = sess.get_inputs()[0]
    in_name = in_meta.name
    shape = in_meta.shape
    if len(shape) != 4:
        print("Expected rank-4 input.", file=sys.stderr)
        return 1

    def run(hw: tuple[int, int]) -> np.ndarray:
        h, w = hw
        if shape[1] in (3, 4):
            c = int(shape[1])
            x = np.random.rand(1, c, h, w).astype(np.float32)
        elif shape[3] in (3, 4):
            c = int(shape[3])
            x = np.random.rand(1, h, w, c).astype(np.float32)
        else:
            print("Could not infer input channel dim (expected 3 or 4 in dim 1 or 3).", file=sys.stderr)
            raise SystemExit(1)

        y = sess.run(None, {in_name: x})[0]
        print(f"  run H,W={h},{w} -> spec shape {y.shape}")
        if y.ndim != 4:
            print("Expected rank-4 output.", file=sys.stderr)
            raise SystemExit(1)
        ok = False
        if y.shape[1] in (4, 5) and (y.shape[2], y.shape[3]) == (h, w):
            ok = True
        if y.shape[3] in (4, 5) and (y.shape[1], y.shape[2]) == (h, w):
            ok = True
        if not ok:
            print(f"Spatial/channel mismatch: output {y.shape} for H,W={h},{w}", file=sys.stderr)
            raise SystemExit(1)

        if not args.no_range_check:
            nchw = y.shape[1] in (4, 5)
            # Sample many pixels for min/max (first channel slice)
            if nchw:
                rgba = y[0, :4].reshape(4, -1)
            else:
                rgba = y[0, :, :, :4].reshape(-1, 4).T
            rmin, rmax = float(rgba.min()), float(rgba.max())
            eps = 0.05
            if rmin < -eps or rmax > 1.0 + eps:
                print(
                    f"  [warn] RGBA range [{rmin:.4f}, {rmax:.4f}] not in ~[0,1]. "
                    "Expected sigmoid-wrapped export from train_spec; use --no-range-check to allow legacy ONNX.",
                    file=sys.stderr,
                )
                raise SystemExit(1)

        return y

    run((32, 32))
    run((17, 48))
    print("OK: direct spec ONNX contract passed (LabPBR byte mapping: float [0,1] -> round(x*255)).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
