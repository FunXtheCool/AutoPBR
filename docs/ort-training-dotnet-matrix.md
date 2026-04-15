# ONNX Runtime Training (.NET on Windows) — version matrix

Pinned versions for AutoPBR’s **.NET** specular ORT training path (`AutoPBR.Training.Ort`).

## Process isolation (Cli)

`AutoPBR.Cli train-ort-specular` **does not** load `Microsoft.ML.OnnxRuntime.Training` inside the same process as Core inference (`Microsoft.ML.OnnxRuntime.Managed` + redistributed GPU natives). It spawns **`AutoPBR.Training.Ort.Launcher.exe`** next to `AutoPBR.Cli.exe` (copied on build) so native `onnxruntime` **1.19.x** (training) and **1.24.x** (GPU inference) are not mixed in one load context.

| Component | Pinned version | Notes |
|-----------|----------------|-------|
| `Microsoft.ML.OnnxRuntime.Training` (NuGet) | **1.19.2** | Matches artifact generators that target ORT training 1.19.x. Newer ORT builds need aligned training graphs + checkpoint format. |
| .NET | **net8.0** | Same as `AutoPBR.Cli` / `AutoPBR.Core`. |
| Training artifacts | From `tools/MlSpecularTrainer` | `generate_ort_training_artifacts` / `export_ort_forward_core` — keep ORT Python tooling version aligned with **1.19.2** training package when possible. |
| CUDA (GPU) | Per [ORT CUDA EP requirements](https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html) for **1.19.x** | Training GPU package must match installed CUDA/cuDNN. Add CUDA `bin` and cuDNN `bin` to `PATH` on Windows per [install docs](https://onnxruntime.ai/docs/install/). |
| TensorRT (optional) | Same ORT build family | Configure via `SessionOptions` / EP append when using a TensorRT-enabled ORT training build; not all prebuilt training wheels include TensorRT. |
| Python `onnxruntime-training` / `torch-ort` | **Not required** for .NET training | PyPI `onnxruntime-training` wheels are **Linux-focused** for recent releases; Windows training uses **NuGet** `Microsoft.ML.OnnxRuntime.Training` per [install table](https://onnxruntime.ai/docs/install/). |

## Fallback policy

- **Primary (Windows):** `AutoPBR.Training.Ort` + NuGet `Microsoft.ML.OnnxRuntime.Training`.
- **Fallback:** PyTorch training in `tools/MlSpecularTrainer` (`train_spec.py --trainer-backend pytorch`); experimental Python ORT backend (`train_spec_ort.py`) when a Linux environment or compatible `onnxruntime-training` wheel is available.

## References

- [Install ONNX Runtime](https://onnxruntime.ai/docs/install/)
- [C# TrainingSession API](https://onnxruntime.ai/docs/api/csharp/api/Microsoft.ML.OnnxRuntime.TrainingSession.html)
- `Microsoft.ML.OnnxRuntime.Training` on [NuGet](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.Training)
