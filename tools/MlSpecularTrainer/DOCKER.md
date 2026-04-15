# Linux container workflow (full PyTorch/ORT tooling)

This wraps the entire `tools/MlSpecularTrainer` Python toolchain in a Linux CUDA container so Windows host limitations on Python wheels (notably `onnxruntime-training` / `torch-ort`) do not block workflows.

## Prereqs

- Docker Desktop (or Docker Engine + Compose)
- NVIDIA Container Toolkit / WSL2 GPU integration for CUDA workflows
- Repo checked out on host (mounted into container at `/workspace`)

## Interactive menu (same as `Training Menu.bat` on Windows)

Inside the container shell (working directory is `tools/MlSpecularTrainer`):

```bash
bash training_menu.sh
```

The top level matches **Training Menu.bat**: **[1] PyTorch**, **[2] ORT training graphs**, **[3] Torch-ORT** (PyTorch + `ORTModule` with **cuda** vs **tensorrt** execution provider prompts), **[0] Exit**.

## PowerShell quick start

From `tools/MlSpecularTrainer`:

```powershell
# Open shell in container (base deps only)
.\run_ml_container.ps1

# ORT artifact tooling (installs onnxruntime-training in container)
.\run_ml_container.ps1 -InstallOrtTraining -- python -m ml_specular.generate_ort_training_artifacts --help

# PyTorch + torch-ort path (TensorRT backend requested)
.\run_ml_container.ps1 -InstallOrtTraining -InstallTorchOrt -- python -m ml_specular.train_spec --trainer-backend pytorch --torch-ort --torch-ort-provider tensorrt --help

# Also install DeepSpeed (needed for --torch-ort-zero-stage3-support)
.\run_ml_container.ps1 -InstallOrtTraining -InstallTorchOrt -InstallDeepSpeed -- python -c "import deepspeed; print(deepspeed.__version__)"
```

(`-OrtTraining` / `-TorchOrt` still work as aliases; avoid passing those to `docker compose` — only the script understands them.)

## Bash quick start

```bash
chmod +x run_ml_container.sh

# Shell
./run_ml_container.sh

# ORT artifact tooling
./run_ml_container.sh --ort-training -- python -m ml_specular.generate_ort_training_artifacts --help

# torch-ort path
./run_ml_container.sh --ort-training --torch-ort -- python -m ml_specular.train_spec --trainer-backend pytorch --torch-ort --torch-ort-provider tensorrt --help

# Also install DeepSpeed (needed for --torch-ort-zero-stage3-support)
./run_ml_container.sh --ort-training --torch-ort --deepspeed -- python -c "import deepspeed; print(deepspeed.__version__)"
```

## Troubleshooting

- **`unknown shorthand flag: 'O' in -OrtTraining`** — You passed `-OrtTraining` to **`docker compose`** (or put it before `run` / the service name). Those switches belong only to **`run_ml_container.ps1`**. From `tools/MlSpecularTrainer` run: `.\run_ml_container.ps1 -InstallOrtTraining -- …` (do not prefix the same flags on `docker compose`).
- **`No module named 'torch_ort'` after a successful `pip install`** — The PyTorch image’s **conda** Python can take precedence over **`/opt/venv`** in **interactive** shells (`training_menu.sh` used plain `python`). The image now bakes **torch-ort** into `/opt/venv`, sets **`PYTHON=/opt/venv/bin/python`**, and **`training_menu.sh`** prefers `/opt/venv/bin/python` when it exists. Rebuild the image; if you still see it, check `train_spec`’s `sys.executable` line in the error and align `pip`/`python` to that path.
- **`/usr/bin/env: 'bash\r': No such file or directory`** — `entrypoint.sh` had CRLF line endings, so the kernel shebang pointed at `bash\r`. The Dockerfile now runs `sed` in the image to strip `\r` (so Windows checkouts still work). **Rebuild the image without cache** after pulling:
  - `docker compose -f tools/MlSpecularTrainer/docker/docker-compose.yml build --no-cache ml-specular`
  Also ensure `.gitattributes` keeps `docker/**` as LF for a clean repo copy.
- **`torch_ort.configure` fails with `IndexError: list index out of range` in `torch.utils.cpp_extension._get_cuda_arch_flags`** — this usually means no visible GPU during build/configure-time probing and no `TORCH_CUDA_ARCH_LIST` set. The Dockerfile and entrypoint now set a fallback arch list (`7.5;8.0;8.6;8.9+PTX`) before running `torch_ort.configure`.

## Notes

- Outputs are written to your host repo (shared volume), including `artifacts/`.
- **Base Python deps** are installed from **`requirements.docker.txt`** at image build (no standalone `onnxruntime` wheel — **`onnxruntime-training`** supplies the Python package and avoids an install/uninstall cycle). Local workflows can still use **`requirements.txt`** (includes `onnxruntime` for inference scripts). The entrypoint syncs the mounted requirements file when **`SKIP_BASE_PIP_INSTALL=0`**; **`docker-compose.yml` defaults `SKIP_BASE_PIP_INSTALL=1`** for faster container starts (rely on the baked image). Set it to **`0`** after editing requirements so `/opt/venv` picks up changes without a rebuild. **`run_ml_container.ps1`** / **`run_ml_container.sh`** default to syncing (`SKIP_BASE_PIP_INSTALL=0`) unless you pass **`-SkipBasePipInstall`** / **`--skip-base-pip-install`**. **Rebuild** after changing Docker pins: `docker compose -f tools/MlSpecularTrainer/docker/docker-compose.yml build ml-specular`. The Dockerfile uses **BuildKit pip cache mounts** (`# syntax=docker/dockerfile:1`) and a **named pip cache volume** so rebuilds reuse downloads.
- **`torch-ort` / `onnxruntime-training`:** Version pins live as **`ARG`** in **`docker/Dockerfile`** (torch **2.4.1**, torchvision **0.19.1**, ORT training **1.19.2+cu118**, torch-ort **1.19.2**). PyTorch must stay **2.4.x** with **cu118** — **2.5+** can break ORT training (`torch.onnx._internal.jit_utils`). GPU **`onnxruntime-training==1.19.2+cu118`** is the latest **cu118** stable on **`download.onnxruntime.ai`**. Base image **CUDA 11.8** matches those wheels. The PyTorch tag **`cudnn9`** ships **`libcudnn.so.9`** under **`/opt/conda/lib`** (the Dockerfile **`ENV`** and **`entrypoint.sh`** also prepend **`/opt/venv/.../site-packages/torch/lib`**). ORT’s CUDA provider still links **`libcudnn.so.8`**, so the image **`pip install`s `nvidia-cudnn-cu11==8.9.5.30`** and **appends** that **`site-packages/nvidia/cudnn/lib`** path (via **`cudnn8_lib_path`**) after those paths. If imports still fail: `docker compose build --no-cache ml-specular`.
- `onnxruntime-training` and `torch-ort` installs are toggled by wrapper flags; they only **pip install when the import is missing** (first run with those flags), not every time.
- If your required wheel is on a custom index, pass:
  - PowerShell: `-PipExtraIndexUrl <url>`
  - Bash: `--pip-extra-index-url <url>`
- torch-ort still depends on CUDA and provider support; TensorRT availability depends on installed ORT/torch-ort build and compatible NVIDIA runtime.
