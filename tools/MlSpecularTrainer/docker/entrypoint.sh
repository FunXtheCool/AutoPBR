#!/usr/bin/env bash
set -euo pipefail

ROOT="/workspace/tools/MlSpecularTrainer"
VENV_PATH="${VENV_PATH:-/opt/venv}"
# torch_ort.configure / PyTorch CUDAExtension expect CUDA_HOME when compiling fused ops.
if [[ -z "${CUDA_HOME:-}" ]] && [[ -d /usr/local/cuda ]]; then
  export CUDA_HOME=/usr/local/cuda
  export CUDA_PATH=/usr/local/cuda
fi
# libcudnn.so.9: conda base + pip torch/lib (wheels ship libs next to torch); must precede cuDNN 8 path for ORT.
for _tl in "${VENV_PATH}/lib/python3."*/site-packages/torch/lib; do
  if [[ -d "${_tl}" ]]; then
    case ":${LD_LIBRARY_PATH:-}:" in *":${_tl}:"*) ;; *)
      export LD_LIBRARY_PATH="${_tl}${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}"
      ;;
    esac
    break
  fi
done
if [[ -d /opt/conda/lib ]]; then
  case ":${LD_LIBRARY_PATH:-}:" in *":/opt/conda/lib:"*) ;; *)
    export LD_LIBRARY_PATH="/opt/conda/lib${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}"
    ;;
  esac
fi
# onnxruntime-training+cu118 loads libcudnn.so.8; Dockerfile adds nvidia-cudnn-cu11 8.x for ORT.
# Append (do not prepend) that dir so libcudnn.so.9 resolves from conda/torch first.
if [[ -f "${VENV_PATH}/cudnn8_lib_path" ]]; then
  _cudnn="$(tr -d '\r\n' < "${VENV_PATH}/cudnn8_lib_path")"
  if [[ -n "${_cudnn}" ]]; then
    export LD_LIBRARY_PATH="${LD_LIBRARY_PATH:+${LD_LIBRARY_PATH}:}${_cudnn}"
  fi
fi
PY_BIN="${VENV_PATH}/bin/python"
PIP_BIN="${VENV_PATH}/bin/pip"

# Aligned with docker/Dockerfile ARG defaults — override via compose/env when bumping pins.
TORCH_VER="${TORCH_VER:-2.4.1}"
TORCHVISION_VER="${TORCHVISION_VER:-0.19.1}"
PYTORCH_CU118_INDEX="${PYTORCH_CU118_INDEX:-https://download.pytorch.org/whl/cu118}"
ORT_TRAINING_FIND_LINKS="${ORT_TRAINING_FIND_LINKS:-https://download.onnxruntime.ai/onnxruntime_stable_cu118.html}"

# Prefer requirements.docker.txt (no redundant onnxruntime) when present; override with ML_REQUIREMENTS_FILE.
_req_file() {
  if [[ -n "${ML_REQUIREMENTS_FILE:-}" && -f "${ML_REQUIREMENTS_FILE}" ]]; then
    echo "${ML_REQUIREMENTS_FILE}"
  elif [[ -f "${ROOT}/requirements.docker.txt" ]]; then
    echo "${ROOT}/requirements.docker.txt"
  else
    echo "${ROOT}/requirements.txt"
  fi
}

install_if_missing() {
  local pkg="$1"
  if ! "${PY_BIN}" -c "import ${pkg}" >/dev/null 2>&1; then
    return 0
  fi
  return 1
}

# Sync base deps against mounted requirements (fast no-op when unchanged).
# SKIP_BASE_PIP_INSTALL=1 skips this entirely (faster starts; use after changing requirements.txt if needed).
#
# IMPORTANT: The onnxruntime-training wheel does NOT include ORTModule's compiled *.so files; those
# are produced by `python -m torch_ort.configure` under
# site-packages/onnxruntime/training/ortmodule/torch_cpp_extensions/.
# Any pip operation that *reinstalls* onnxruntime-training replaces that directory and wipes those
# files — the image bake is lost and train_spec will run torch_ort.configure again (slow).
# Keep SKIP_BASE_PIP_INSTALL=1 for routine training; only enable pip sync when you accept a rebuild.
if [[ "${INSTALL_BASE_DEPS:-1}" == "1" ]] && [[ "${SKIP_BASE_PIP_INSTALL:-0}" != "1" ]]; then
  echo "[ml-container] pip sync enabled — if onnxruntime-training is reinstalled, ORTModule *.so from the image bake are removed; train may re-run torch_ort.configure." >&2
  _req="$(_req_file)"
  if [[ -f "${_req}" ]]; then
    "${PIP_BIN}" install -q --upgrade-strategy only-if-needed -r "${_req}"
    # Keep PyTorch on cu118 + exact pins so host requirements.txt cannot pull 2.5+ and break torch-ort.
    "${PIP_BIN}" install -q --upgrade-strategy only-if-needed \
      "torch==${TORCH_VER}" "torchvision==${TORCHVISION_VER}" \
      --index-url "${PYTORCH_CU118_INDEX}"
  fi
fi

# Pip reinstalls can wipe baked *.so under site-packages/.../torch_cpp_extensions. Rebuild if needed.
# ORTModule only accepts extensions in that tree (not ~/.cache/torch_extensions alone).
if [[ "${SKIP_TORCH_ORT_CONFIGURE:-0}" != "1" ]] && "${PY_BIN}" -c "import torch_ort" 2>/dev/null; then
  if ! PYTHONPATH="${ROOT}:${PYTHONPATH:-}" "${PY_BIN}" -c \
    "import sys; from ml_specular.torch_ort_extensions import ortmodule_extensions_built; sys.exit(0 if ortmodule_extensions_built() else 1)" \
    2>/dev/null; then
    echo "[ml-container] ORTModule extensions missing under site-packages; running torch_ort.configure..." >&2
    if ! "${PY_BIN}" -m torch_ort.configure; then
      echo "[ml-container] ERROR: torch_ort.configure failed. Use a CUDA **devel** image (nvcc), ninja, and g++. See docker/Dockerfile." >&2
    fi
  fi
fi

if [[ "${INSTALL_ORT_TRAINING:-0}" == "1" ]]; then
  ORT_SPEC="${ORT_TRAINING_PIP_SPEC:-onnxruntime-training==1.19.2+cu118}"
  if install_if_missing onnxruntime.training; then
    if [[ "${ORT_SPEC}" == *"+cu118"* ]]; then
      "${PIP_BIN}" install --no-cache-dir -f "${ORT_TRAINING_FIND_LINKS}" "${ORT_SPEC}"
    elif [[ -n "${PIP_EXTRA_INDEX_URL:-}" ]]; then
      "${PIP_BIN}" install --extra-index-url "${PIP_EXTRA_INDEX_URL}" "${ORT_SPEC}"
    else
      "${PIP_BIN}" install "${ORT_SPEC}"
    fi
  fi
fi

if [[ "${INSTALL_TORCH_ORT:-0}" == "1" ]]; then
  "${PIP_BIN}" install --no-cache-dir ninja
  TORCH_ORT_SPEC="${TORCH_ORT_PIP_SPEC:-torch-ort==1.19.2}"
  ORT_TRAIN_SPEC="${ORT_TRAINING_PIP_SPEC:-onnxruntime-training==1.19.2+cu118}"
  if install_if_missing torch_ort; then
    "${PIP_BIN}" uninstall -y onnxruntime onnxruntime-gpu onnxruntime-training 2>/dev/null || true
    if [[ -n "${PIP_EXTRA_INDEX_URL:-}" ]]; then
      "${PIP_BIN}" install --no-cache-dir -f "${ORT_TRAINING_FIND_LINKS}" "${ORT_TRAIN_SPEC}"
      "${PIP_BIN}" install --no-cache-dir --extra-index-url "${PIP_EXTRA_INDEX_URL}" "${TORCH_ORT_SPEC}"
    else
      "${PIP_BIN}" install --no-cache-dir -f "${ORT_TRAINING_FIND_LINKS}" "${ORT_TRAIN_SPEC}"
      "${PIP_BIN}" install --no-cache-dir "${TORCH_ORT_SPEC}"
    fi
  fi
  if ! "${PY_BIN}" -c "import torch_ort" >/dev/null 2>&1; then
    echo "[ml-container] torch_ort import failed; force-reinstalling ${TORCH_ORT_SPEC}..." >&2
    if [[ -n "${PIP_EXTRA_INDEX_URL:-}" ]]; then
      "${PIP_BIN}" install --force-reinstall --extra-index-url "${PIP_EXTRA_INDEX_URL}" "${TORCH_ORT_SPEC}"
    else
      "${PIP_BIN}" install --force-reinstall "${TORCH_ORT_SPEC}"
    fi
  fi
  if ! "${PY_BIN}" -c "import torch_ort" >/dev/null 2>&1; then
    echo "[ml-container] torch_ort still failing; re-pinning torch ${TORCH_VER}+cu118 and ORT training..." >&2
    "${PIP_BIN}" install --force-reinstall \
      "torch==${TORCH_VER}" "torchvision==${TORCHVISION_VER}" \
      --index-url "${PYTORCH_CU118_INDEX}"
    "${PIP_BIN}" install --force-reinstall -f "${ORT_TRAINING_FIND_LINKS}" "${ORT_TRAIN_SPEC}"
    "${PIP_BIN}" install --force-reinstall "${TORCH_ORT_SPEC}"
  fi
  if ! "${PY_BIN}" -c "import torch_ort" >/dev/null 2>&1; then
    echo "[ml-container] ERROR: torch_ort still will not import. Rebuild: docker compose build --no-cache ml-specular" >&2
  fi
  # Skip recompilation when ORTModule extensions already exist (see ml_specular/torch_ort_extensions.py).
  if [[ "${TORCH_ORT_SKIP_CONFIGURE:-0}" == "1" ]]; then
    echo "[ml-container] TORCH_ORT_SKIP_CONFIGURE=1 — skipping torch_ort.configure." >&2
  elif PYTHONPATH="${ROOT}:${PYTHONPATH:-}" "${PY_BIN}" -c \
    "import sys; from ml_specular.torch_ort_extensions import ortmodule_extensions_built; sys.exit(0 if ortmodule_extensions_built() else 1)" \
    2>/dev/null; then
    echo "[ml-container] ORTModule extensions already built; skipping torch_ort.configure."
  else
    echo "[ml-container] Running torch_ort.configure (extensions missing or detection failed)..." >&2
    # In some environments no GPU is visible during configure-time probing; set a safe arch fallback.
    export TORCH_CUDA_ARCH_LIST="${TORCH_CUDA_ARCH_LIST:-7.5;8.0;8.6;8.9+PTX}"
    if ! "${PY_BIN}" -m torch_ort.configure; then
      echo "[ml-container] ERROR: torch_ort.configure failed; fix toolchain/CUDA_HOME or set TORCH_ORT_SKIP_CONFIGURE=1 to skip (torch-ort will not work)." >&2
    fi
  fi
fi

if [[ "${INSTALL_DEEPSPEED:-0}" == "1" ]]; then
  DEEPSPEED_SPEC="${DEEPSPEED_PIP_SPEC:-deepspeed}"
  if install_if_missing deepspeed; then
    echo "[ml-container] Installing DeepSpeed (${DEEPSPEED_SPEC})..." >&2
    if [[ -n "${PIP_EXTRA_INDEX_URL:-}" ]]; then
      "${PIP_BIN}" install --no-cache-dir --extra-index-url "${PIP_EXTRA_INDEX_URL}" "${DEEPSPEED_SPEC}"
    else
      "${PIP_BIN}" install --no-cache-dir "${DEEPSPEED_SPEC}"
    fi
  fi
  if ! "${PY_BIN}" -c "import deepspeed" >/dev/null 2>&1; then
    echo "[ml-container] ERROR: DeepSpeed failed to import after install (${DEEPSPEED_SPEC})." >&2
  fi
fi

export PYTHONPATH="/workspace/tools/MlSpecularTrainer:${PYTHONPATH:-}"
cd "${ROOT}"

if [[ "$#" -eq 0 ]]; then
  echo "[ml-specular] Tip: bash training_menu.sh — same interactive menu as Training Menu.bat (Windows)."
  exec bash
fi

exec "$@"
