"""Run onnxruntime.training.artifacts.generate_artifacts (optimizer_model.onnx, training/eval graphs)."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import onnx


def _initializer_names(model: onnx.ModelProto) -> list[str]:
    return [t.name for t in model.graph.initializer]


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(
        description=(
            "Generate ORT on-device training artifacts from a forward-only ONNX (logits). "
            "Install: pip install onnxruntime-training (must match onnxruntime version). "
            "Use --loss spec (default) for LabPBR spec_loss via custom onnxblock; use l1/mse for debugging."
        )
    )
    p.add_argument(
        "--base-onnx",
        type=Path,
        default=Path("artifacts/ort/forward_model.onnx"),
        help="Forward model path (export via: python -m ml_specular.export_ort_forward_core).",
    )
    p.add_argument(
        "--artifact-directory",
        type=Path,
        default=Path("artifacts/ort"),
        help="Where to write training_model.onnx, eval_model.onnx, optimizer_model.onnx, checkpoint/.",
    )
    p.add_argument("--prefix", type=str, default="", help="Filename prefix for generated artifacts.")
    p.add_argument(
        "--optimizer",
        type=str,
        default="adamw",
        choices=("adamw", "sgd"),
        help="Optimizer graph to generate.",
    )
    p.add_argument(
        "--loss",
        type=str,
        default="spec",
        choices=("spec", "l1", "mse", "none"),
        help="spec = SpecularLabPbr onnxblock (matches spec_loss); l1/mse/none = built-in enums.",
    )
    p.add_argument(
        "--out-channels",
        type=int,
        default=4,
        choices=(4,),
        help="Logits C dimension (RGBA; must match --base-onnx).",
    )
    p.add_argument(
        "--logits-output-name",
        type=str,
        default=None,
        help="Graph output name for logits (default: first graph output in base ONNX).",
    )
    args = p.parse_args(argv)

    base = args.base_onnx.resolve()
    if not base.is_file():
        print(f"Forward ONNX not found: {base}", file=sys.stderr)
        print("Create it first:", file=sys.stderr)
        print("  python -m ml_specular.export_ort_forward_core --out artifacts/ort/forward_model.onnx", file=sys.stderr)
        return 1

    try:
        from onnxruntime.training.artifacts import OptimType, generate_artifacts
        from onnxruntime.training.artifacts import LossType
    except ImportError:
        print(
            "onnxruntime.training.artifacts is not available. Install the training package, e.g.\n"
            "  pip install onnxruntime-training",
            file=sys.stderr,
        )
        return 1

    loaded = onnx.load(str(base))
    names = _initializer_names(loaded)
    if not names:
        print("No initializers found in base ONNX; cannot set requires_grad.", file=sys.stderr)
        return 1

    logits_name = args.logits_output_name or loaded.graph.output[0].name

    optim = OptimType.AdamW if args.optimizer == "adamw" else OptimType.SGD

    loss = None
    loss_input_names: list[str] | None = None
    if args.loss == "spec":
        try:
            from ml_specular.onnxblock_spec_loss import SpecularLabPbrLoss, spec_loss_feed_input_names
        except ImportError as ex:
            print(
                "Failed to load SpecularLabPbr onnxblock (need onnxruntime-training with onnxblock).",
                file=sys.stderr,
            )
            print(ex, file=sys.stderr)
            return 1
        loss = SpecularLabPbrLoss()
        loss_input_names = spec_loss_feed_input_names(logits_name)
    elif args.loss == "none":
        loss = None
    elif args.loss == "l1":
        loss = LossType.L1Loss
    else:
        loss = LossType.MSELoss

    out_dir = args.artifact_directory.resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    generate_artifacts(
        model=str(base),
        requires_grad=names,
        frozen_params=None,
        loss=loss,
        optimizer=optim,
        artifact_directory=str(out_dir),
        prefix=args.prefix,
        ort_format=False,
        loss_input_names=loss_input_names,
    )

    print(f"Artifacts written under: {out_dir}")
    print("Generated: training_model.onnx, eval_model.onnx, optimizer_model.onnx, checkpoint/")
    if args.loss == "spec":
        print(f"spec_loss feed names: {loss_input_names}")
    print(
        "Train: python -m ml_specular.train_spec --trainer-backend ort --ort-artifacts-dir " + str(out_dir)
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
