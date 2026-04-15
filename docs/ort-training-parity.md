# ORT specular training: parity, benchmarks, deprecation

This document defines how to validate the **.NET** runner (`AutoPBR.Training.Ort` / `AutoPBR.Cli train-ort-specular`) against the **Python** baseline (`tools/MlSpecularTrainer`, `train_spec.py --trainer-backend ort`) and when to prefer one stack over the other.

## Why a separate launcher process?

`AutoPBR.Core` pins **Microsoft.ML.OnnxRuntime.Managed 1.24.x** for inference (GPU natives from `Data\native`, not the `Microsoft.ML.OnnxRuntime.Gpu` NuGet meta-package). ORT **training** on NuGet is pinned to **1.19.2** (see `docs/ort-training-dotnet-matrix.md`). Loading both native `onnxruntime` stacks in the same process is unsafe. The CLI therefore starts **`AutoPBR.Training.Ort.Launcher.exe`** next to `AutoPBR.Cli.exe` (copied on build) so training uses only the **1.19.2** training runtime.

## Functional parity checks

Run **both** trainers on the same dataset and ORT artifacts directory:

1. **Artifacts** — Generate once with Python (same machine / commit):
   - `export_ort_forward_core` → forward ONNX
   - `generate_ort_training_artifacts` → `training_model.onnx`, `eval_model.onnx`, `optimizer_model.onnx`, `checkpoint/`
2. **Data** — Same `--data-root`, `--train-res`, `--in-channels`, `--batch`, `--epochs`, `--lr`, `--transparent-zero-weight`, fixed RNG seed where applicable.
3. **Compare**
   - **Per-epoch losses** — Logged `train_loss` / `val_loss` should trend similarly (exact bit-match not required; watch for large drift).
   - **Checkpoint resume** — Train 1 epoch in Python, save; resume in .NET (or reverse) and confirm loss continues without NaNs.
   - **Inference ONNX** — After training, export inference model per your pipeline and run the existing ONNX verifier used by the repo.

## Data / tensor parity

Automated tests in `tests/AutoPBR.Training.Ort.Tests` lock:

- **VC edge channel** — Golden values for a small synthetic pattern; flat regions use double accumulation plus a small magnitude floor (`flatEps` in `VcEdgeChannel`) so near-zero gradients are not normalized to `1.0` (may differ from raw NumPy float32 by ~1e-16 on uniform tiles; negligible for training).
- **Dataset layout** — NCHW input, planar RGBA target, `valid` mask semantics on a tiny fixture.

For stricter cross-language checks, add a small script in `tools/MlSpecularTrainer` that dumps a single batch to `.npy` / JSON and assert `.NET` reads the same floats (optional follow-up).

## Performance

- Measure **steps/sec** on Windows with CUDA EP (and optionally CPU baseline).
- Compare to Python `train_spec_ort` on the same hardware where Python ORT training is available (often Linux).

## Deprecation / fallback policy

| Scenario | Preferred | Fallback |
|----------|-----------|----------|
| Windows, ORT training artifacts ready | `.NET` training (`train-ort-specular`) | PyTorch `train_spec.py` |
| Need `torch-ort` / full PyTorch autograd on device | PyTorch + Linux ORT wheel (if available) | `.NET` ORT training (different API surface) |
| Only inference ONNX in app | `AutoPBR.Core` GPU ORT **1.24.x** | n/a |

**Policy:** Keep Python manifest generation and artifact generators as the **offline** source of training graphs until a replacement exists. Deprecate **Python on-device ORT training on Windows** in favor of `.NET` when parity holds; keep Python ORT path as **optional / Linux** for teams that rely on `onnxruntime-training` wheels.
