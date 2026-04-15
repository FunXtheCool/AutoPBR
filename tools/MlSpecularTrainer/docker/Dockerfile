# syntax=docker/dockerfile:1
# Devel (not runtime): torch_ort.configure compiles CUDA fused ops; needs nvcc + CUDA headers (CUDA_HOME).
# CUDA 11.8: official onnxruntime-training GPU wheels are built for cu118 (see download.onnxruntime.ai).
# Plain PyPI onnxruntime-training expects CUDA 11.x libs; a cu124 base lacks those and ORT CUDA/TensorRT EPs fail.
# Image tag is cudnn9 (libcudnn.so.9); onnxruntime-training+cu118 links libcudnn.so.8 — we add nvidia-cudnn-cu11 8.x below.
FROM pytorch/pytorch:2.4.1-cuda11.8-cudnn9-devel

# Pin versions in one place (match torch-ort / onnxruntime-training releases).
ARG TORCH_VER=2.4.1
ARG TORCHVISION_VER=0.19.1
ARG ORT_TRAIN_VER=1.19.2
ARG TORCH_ORT_VER=1.19.2
ARG DEEPSPEED_VER=0.15.4
ARG ORT_TRAINING_FIND_LINKS=https://download.onnxruntime.ai/onnxruntime_stable_cu118.html
ARG PYTORCH_CU118_INDEX=https://download.pytorch.org/whl/cu118
# Latest cuDNN 8.x wheel for CUDA 11 (ORT GPU providers require libcudnn.so.8).
ARG NVIDIA_CUDNN_CU11_VER=8.9.5.30

# libcudnn.so.9 lives under conda in pytorch/pytorch cudnn9 images, not always under /usr/local/cuda/lib64.
ENV CUDA_HOME=/usr/local/cuda \
    PATH=/usr/local/cuda/bin:${PATH} \
    LD_LIBRARY_PATH=/usr/local/cuda/lib64:/opt/conda/lib:${LD_LIBRARY_PATH} \
    DEBIAN_FRONTEND=noninteractive \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    VENV_PATH=/opt/venv

RUN apt-get update && apt-get install -y --no-install-recommends \
    bash \
    git \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN python -m venv "${VENV_PATH}"
ENV PATH="${VENV_PATH}/bin:${PATH}"

WORKDIR /workspace/tools/MlSpecularTrainer

# Dependency layer: requirements.docker.txt avoids onnxruntime (supplied by onnxruntime-training).
COPY requirements.docker.txt ./
RUN --mount=type=cache,target=/root/.cache/pip \
    pip install -r requirements.docker.txt \
    && pip install --force-reinstall \
        torch==${TORCH_VER} torchvision==${TORCHVISION_VER} \
        --index-url ${PYTORCH_CU118_INDEX} \
    && pip install ninja \
    && pip install \
        "onnxruntime-training==${ORT_TRAIN_VER}+cu118" \
        -f ${ORT_TRAINING_FIND_LINKS} \
    && pip install torch-ort==${TORCH_ORT_VER} \
    && pip install deepspeed==${DEEPSPEED_VER} \
    && python -c "import onnxruntime.training; import torch_ort; import deepspeed; print('onnxruntime-training + torch-ort + deepspeed import ok')"

# ORT libonnxruntime_providers_cuda.so needs libcudnn.so.8; PyTorch image only provides cuDNN 9.
RUN --mount=type=cache,target=/root/.cache/pip \
    pip install "nvidia-cudnn-cu11==${NVIDIA_CUDNN_CU11_VER}" \
    && find /opt/venv/lib -path '*/nvidia/cudnn/lib' -type d -print -quit > /opt/venv/cudnn8_lib_path \
    && test -s /opt/venv/cudnn8_lib_path

# ldconfig: libcudnn.so.9 must resolve for bare `python` (docker exec / .last_training_session.sh), not only entrypoint shells.
# Do NOT `import torch` before ldconfig here — torch needs libcudnn.so.9, which we only register below.
RUN set -eux; \
    TORCH_LIB="$(python -c 'import sys; from pathlib import Path; print(Path(sys.prefix) / "lib" / f"python{sys.version_info.major}.{sys.version_info.minor}" / "site-packages" / "torch" / "lib")')"; \
    CUDNN8="$(tr -d '\r\n' < /opt/venv/cudnn8_lib_path)"; \
    { \
      [ -d /opt/conda/lib ] && echo /opt/conda/lib || true; \
      [ -d /usr/local/cuda/lib64 ] && echo /usr/local/cuda/lib64 || true; \
      [ -d "${TORCH_LIB}" ] && echo "${TORCH_LIB}" || true; \
      [ -d "${CUDNN8}" ] && echo "${CUDNN8}" || true; \
      find /opt/conda /usr/local/cuda /opt/venv -name 'libcudnn.so.9*' -printf '%h\n' 2>/dev/null | sort -u || true; \
    } > /etc/ld.so.conf.d/99-ml-specular.conf; \
    ldconfig; \
    python -c "import torch; print('torch import ok', torch.__version__)"

# Bake ORTModule fused_ops into site-packages (extensions land under torch_cpp_extensions/**).
# Those *.so are NOT in the onnxruntime-training wheel; reinstalling that wheel via pip removes them.
# Runtime: keep SKIP_BASE_PIP_INSTALL=1 (compose default) so entrypoint does not reinstall ORT.
RUN TORCH_CUDA_ARCH_LIST="${TORCH_CUDA_ARCH_LIST:-7.5;8.0;8.6;8.9+PTX}" python -m torch_ort.configure \
    && python -c "import sys; from pathlib import Path; \
sp=Path(sys.prefix)/'lib'/f'python{sys.version_info.major}.{sys.version_info.minor}'/'site-packages'; \
ext=sp/'onnxruntime/training/ortmodule/torch_cpp_extensions'; \
assert ext.is_dir() and any(ext.rglob('*.so')), 'torch_ort.configure did not produce a .so'; \
print('torch_ort.configure: OK')"

COPY docker/entrypoint.sh /opt/ml-entrypoint.sh
RUN sed -i 's/\r$//' /opt/ml-entrypoint.sh && chmod +x /opt/ml-entrypoint.sh

ENTRYPOINT ["/opt/ml-entrypoint.sh"]
CMD ["bash"]
