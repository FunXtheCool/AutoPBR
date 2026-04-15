# Native GPU runtime DLLs for ONNX Runtime (Windows x64)

Place redistributable native libraries here so the App and CLI build copy them to `runtimes\win-x64\native`. The build **does not** download anything from the network; you supply binaries under your licenses.

**CUDA / cuDNN:** redistribution under the [CUDA EULA](https://docs.nvidia.com/cuda/eula/) and [cuDNN license](https://docs.nvidia.com/deeplearning/cudnn/latest/reference/eula.html) when incorporated into your application.

**ONNX Runtime GPU (CUDA 13):** `AutoPBR.Core` references **`Microsoft.ML.OnnxRuntime.Managed`** only (not **`Microsoft.ML.OnnxRuntime.Gpu`**), so NuGet does **not** ship Windows GPU natives—the default Gpu package carries **CUDA 12**–linked providers. Copy the full set of GPU runtime DLLs from the official **`onnxruntime-win-x64-gpu_cuda13`** zip for the **same version** as `Microsoft.ML.OnnxRuntime.Managed` (e.g. **1.24.3**), including at least `onnxruntime.dll`, `onnxruntime_providers_shared.dll`, `onnxruntime_providers_cuda.dll`, and `onnxruntime_providers_tensorrt.dll` if you use TensorRT. See the [ONNX Runtime license](https://github.com/microsoft/onnxruntime/blob/main/LICENSE).

**Training:** `Microsoft.ML.OnnxRuntime.Training` is only used by **`AutoPBR.Training.Ort`** (Docker/tooling), not by the App or Core inference path.

Official dependency list (see [ONNX Runtime CUDA EP](https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html)): **libcudart**, **libcufft**, **libcurand**, **libcublasLt**, **libcublas**, **libcudnn**.

## Full list – place in this folder

### From CUDA Toolkit 13.1.x (`CUDA_PATH\bin\x64`)

| DLL | Library |
|-----|---------|
| `cudart64_13.dll` | CUDA runtime |
| `cufft64_13.dll` | CUDA FFT |
| `curand64_13.dll` | CUDA random number |
| `cublasLt64_13.dll` | cuBLAS (lightweight) |
| `cublas64_13.dll` | cuBLAS |

Copy every `*.dll` from `CUDA_PATH\bin\x64` if you prefer (covers optional deps like `nvrtc64_13*.dll` if needed).

### From cuDNN 9 for CUDA 13 (cuDNN package `bin` or `bin\x64`)

In cuDNN 9 the API is split across multiple DLLs. **`cudnnCreate`** (required by ONNX Runtime) lives in the **graph** library, so you must include all cuDNN DLLs, not just the main one.

| DLL | Notes |
|-----|--------|
| `cudnn64_9.dll` | Main/shim library |
| **`cudnn_graph64_9.dll`** | **Required – contains `cudnnCreate`** (avoids "Cannot load symbol cudnnCreate") |
| `cudnn_ops_infer64_9.dll` | Inference ops |
| `cudnn_cnn_infer64_9.dll` | CNN inference |
| `cudnn_adv_infer64_9.dll` | If present |
| Any other `cudnn*.dll` in the package | Engines, heuristics, etc. |

**Important:** Copy **every** `cudnn*.dll` from the cuDNN package `bin` (or `bin\x64`) folder. Missing the graph or engine DLLs causes "Invalid handle. Cannot load symbol cudnnCreate".

## How to obtain

1. **CUDA**: Install [CUDA Toolkit 13.1.x](https://developer.nvidia.com/cuda-downloads) or use the [redistributable packages](https://developer.download.nvidia.com/compute/cuda/redist/). Copy the DLLs above from the install `bin\x64` folder.
2. **cuDNN**: Download [cuDNN for CUDA 13](https://developer.nvidia.com/cudnn). Copy all `cudnn*.dll` from the package `bin` or `bin\x64` folder.

Any `*.dll` in this folder is copied to the App/CLI output `runtimes\win-x64\native` during build (see **`AutoPBR.Core.csproj`** and **`AutoPBR.App`** / **`AutoPBR.Cli`**).

## TensorRT (optional, ONNX Runtime TensorRT execution provider)

Place any TensorRT runtime DLLs you redistribute under **`Data\native\`** (for example **`nvinfer_10.dll`**). They are copied to **`runtimes\win-x64\native`** on build (see **`AutoPBR.Core.csproj`** and **`AutoPBR.App`** / **`AutoPBR.Cli`** content items). Add more DLLs from your NVIDIA TensorRT package’s **`lib`** folder as needed (e.g. `nvonnxparser_10.dll`, `nvinfer_plugin_10.dll`, builder resource DLLs) if inference fails with missing-dependency errors.

The build does **not** download TensorRT from the network; you supply binaries here under your [NVIDIA TensorRT license](https://docs.nvidia.com/deeplearning/tensorrt/latest/installing-tensorrt/overview.html).

**Session startup:** TensorRT **compiles** ONNX subgraphs into engines on first use, which can take much longer than the CUDA EP (which does not perform that step). The app enables **engine + timing caches** under `%LocalAppData%\AutoPBR\onnx_tensorrt_cache`, so **later** runs with the same model and shapes should start much faster; the first run after a change may still spend tens of seconds at high GPU usage. **CUDA** avoids that compile phase—if TensorRT session creation fails, the app retries with CUDA only.
