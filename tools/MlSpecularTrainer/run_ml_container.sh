#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${SCRIPT_DIR}/docker/docker-compose.yml"

INSTALL_ORT_TRAINING=0
INSTALL_TORCH_ORT=0
INSTALL_DEEPSPEED=0
SKIP_BASE_PIP_INSTALL=0
# Match docker-compose.yml / run_ml_container.ps1 defaults (ORT + torch-ort stack).
ORT_TRAINING_SPEC="onnxruntime-training==1.19.2+cu118"
TORCH_ORT_SPEC="torch-ort==1.19.2"
DEEPSPEED_SPEC="deepspeed"
PIP_EXTRA_INDEX_URL=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-base-pip-install)
      SKIP_BASE_PIP_INSTALL=1
      shift
      ;;
    --ort-training)
      INSTALL_ORT_TRAINING=1
      shift
      ;;
    --torch-ort)
      INSTALL_TORCH_ORT=1
      shift
      ;;
    --deepspeed)
      INSTALL_DEEPSPEED=1
      shift
      ;;
    --ort-training-spec)
      ORT_TRAINING_SPEC="$2"
      shift 2
      ;;
    --torch-ort-spec)
      TORCH_ORT_SPEC="$2"
      shift 2
      ;;
    --deepspeed-spec)
      DEEPSPEED_SPEC="$2"
      shift 2
      ;;
    --pip-extra-index-url)
      PIP_EXTRA_INDEX_URL="$2"
      shift 2
      ;;
    --)
      shift
      break
      ;;
    *)
      break
      ;;
  esac
done

if [[ $# -eq 0 ]]; then
  set -- bash
fi

export INSTALL_BASE_DEPS=1
export SKIP_BASE_PIP_INSTALL
export INSTALL_ORT_TRAINING
export INSTALL_TORCH_ORT
export INSTALL_DEEPSPEED
export ORT_TRAINING_PIP_SPEC="${ORT_TRAINING_SPEC}"
export ORT_TRAINING_FIND_LINKS="${ORT_TRAINING_FIND_LINKS:-https://download.onnxruntime.ai/onnxruntime_stable_cu118.html}"
export TORCH_ORT_PIP_SPEC="${TORCH_ORT_SPEC}"
export DEEPSPEED_PIP_SPEC="${DEEPSPEED_SPEC}"
export PIP_EXTRA_INDEX_URL

echo "[ml-container] SKIP_BASE_PIP_INSTALL=${SKIP_BASE_PIP_INSTALL} INSTALL_ORT_TRAINING=${INSTALL_ORT_TRAINING} INSTALL_TORCH_ORT=${INSTALL_TORCH_ORT} INSTALL_DEEPSPEED=${INSTALL_DEEPSPEED}"
echo "[ml-container] Running: docker compose -f ${COMPOSE_FILE} run --rm ml-specular $*"

docker compose -f "${COMPOSE_FILE}" run --rm ml-specular "$@"
