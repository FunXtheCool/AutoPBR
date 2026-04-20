#!/usr/bin/env bash
# Interactive training menu — same flow as Training Menu.bat (Windows), for Linux / container.
# Usage: bash training_menu.sh   OR   ./training_menu.sh (if executable)

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR" || exit 1
# Docker ml-specular image: interactive bash may prepend conda to PATH, so bare `python` is not always
# /opt/venv (where pip installs land). Prefer explicit interpreters when present.
if [[ -x "/opt/venv/bin/python" ]]; then
  PY="/opt/venv/bin/python"
elif [[ -x "${SCRIPT_DIR}/.venv/bin/python" ]]; then
  PY="${SCRIPT_DIR}/.venv/bin/python"
elif [[ -n "${PYTHON:-}" ]]; then
  PY="${PYTHON}"
else
  PY="python"
fi

LAST_SESSION_SH="${SCRIPT_DIR}/.last_training_session.sh"

save_last_session() {
  {
    echo '#!/usr/bin/env bash'
    echo 'set -u'
    echo "cd \"${SCRIPT_DIR}\" || exit 1"
    printf 'exec '
    printf '%q ' "$@"
    echo
  } > "${LAST_SESSION_SH}"
  chmod +x "${LAST_SESSION_SH}" 2>/dev/null || true
  echo "[Saved last session -> ${LAST_SESSION_SH}]"
}

ask_data_root() {
  local def="multi_dataset"
  read -r -p "Dataset folder [${def}]: " DATA_ROOT
  DATA_ROOT=${DATA_ROOT:-$def}
}

ask_ignore_optifine() {
  IGNORE_OPTIFINE_FLAG=""
  read -r -p "Ignore OptiFine folders (ctm, plants) in manifest? [y/N]: " v
  case "${v:-}" in
    [yY]|[yY][eE][sS]) IGNORE_OPTIFINE_FLAG="--ignore-optifine" ;;
    *) ;;
  esac
}

pause() {
  read -r -p "Press Enter to continue... " _ || true
}

run_gen_labpbr() {
  "${PY}" -m ml_specular.gen_from_labpbr_packs "$@"
}

run_gen_labpbr_all_defaults() {
  local extra=("$@")
  for d in multi_dataset pixelart_dataset realism_dataset stylized_dataset ext_dataset; do
    echo
    echo "=== Manifest: ${d} ==="
    run_gen_labpbr --dataset-root "${d}" "${extra[@]}" || return $?
  done
}

pipeline_menu() {
  while true; do
    echo
    echo "========================================"
    echo "  Specular ML - training pipeline"
    echo "========================================"
    echo "  Working directory: ${PWD}"
    echo
    echo "  [1] PyTorch workflow (manifest, PyTorch train, export-only)"
    echo "  [2] ORT workflow (loss ONNX export, artifact generation, ORT train)"
    echo "  [3] Torch-ORT workflow (same as [1] + ORTModule; CUDA vs TensorRT EP)"
    echo "  [0] Exit"
    echo
    read -r -p "Select 1, 2, 3, or 0: " c
    case "${c}" in
      1) pytorch_main ;;
      2) ort_menu ;;
      3) torch_ort_main ;;
      0) echo "Goodbye."; exit 0 ;;
      *) ;;
    esac
  done
}

pytorch_main() {
  while true; do
    echo
    echo "========================================"
    echo "  Specular ML - PyTorch workflow"
    echo "========================================"
    echo "  [1] Resume training (prompts; defaults from checkpoint)"
    echo "  [2] Refresh LabPBR pack manifest(s)"
    echo "      (single dataset or all defaults: multi/pixelart/realism/stylized/ext)"
    echo "  [3] Train direct artist _s ONNX model (diffuse -> spec RGBA[+conf]) — PyTorch"
    echo "  [4] Refresh manifest, then train (PyTorch)"
    echo "  [5] Export ONNX from checkpoint only (train_spec --export-only)"
    echo "  [6] Repeat last training session (saved shell command)"
    echo "  [0] Back"
    echo
    read -r -p "Select 1-6 or 0: " c
    case "${c}" in
      1) resume_training_pytorch ;;
      2) manifest_scope ;;
      3) train_spec_menu ;;
      4) both_manifest_train ;;
      5) export_only ;;
      6) repeat_last ;;
      0) return ;;
      *) ;;
    esac
  done
}

torch_ort_main() {
  while true; do
    echo
    echo "========================================"
    echo "  Specular ML - Torch-ORT workflow (PyTorch + ORTModule)"
    echo "========================================"
    echo "  [1] Resume training (prompts; defaults from checkpoint)"
    echo "  [2] Refresh LabPBR pack manifest(s)"
    echo "      (single dataset or all defaults: multi/pixelart/realism/stylized/ext)"
    echo "  [3] Train direct artist _s ONNX model — Torch-ORT (CUDA / TensorRT provider)"
    echo "  [4] Refresh manifest, then train (Torch-ORT)"
    echo "  [5] Export ONNX from checkpoint only (train_spec --export-only)"
    echo "  [6] Repeat last training session (saved shell command)"
    echo "  [0] Back"
    echo
    read -r -p "Select 1-6 or 0: " c
    case "${c}" in
      1) resume_training_torch_ort ;;
      2) manifest_scope ;;
      3) train_spec_menu_torch_ort ;;
      4) both_manifest_train_torch_ort ;;
      5) export_only ;;
      6) repeat_last ;;
      0) return ;;
      *) ;;
    esac
  done
}

resume_training_pytorch() {
  echo
  "${PY}" -m ml_specular.resume_training_prompt
  pause
}

resume_training_torch_ort() {
  echo
  "${PY}" -m ml_specular.resume_training_prompt --torch-ort
  pause
}

repeat_last() {
  if [[ ! -f "${LAST_SESSION_SH}" ]]; then
    echo
    echo "[No saved training session yet.]"
    pause
    return
  fi
  echo
  echo "Re-running last session from:"
  echo "  ${LAST_SESSION_SH}"
  echo
  echo "Command:"
  cat "${LAST_SESSION_SH}"
  echo
  pause
  bash "${LAST_SESSION_SH}"
  echo
  pause
}

export_only() {
  local ex_ckpt="artifacts/SpecLab.pt"
  local ex_out="artifacts/SpecLab.onnx"
  echo
  echo "Export ONNX without retraining (reads in_channels, out_channels, width from .pt)."
  read -r -p "Checkpoint .pt path [${ex_ckpt}]: " v
  ex_ckpt=${v:-$ex_ckpt}
  read -r -p "Output .onnx path [${ex_out}]: " v
  ex_out=${v:-$ex_out}
  echo
  echo "Running: ${PY} -m ml_specular.train_spec --export-only --ckpt \"${ex_ckpt}\" --out-onnx \"${ex_out}\""
  echo
  pause
  save_last_session "${PY}" -m ml_specular.train_spec --export-only --ckpt "${ex_ckpt}" --out-onnx "${ex_out}"
  "${PY}" -m ml_specular.train_spec --export-only --ckpt "${ex_ckpt}" --out-onnx "${ex_out}"
  local ex_err=$?
  echo
  if [[ ${ex_err} -ne 0 ]]; then
    echo "[Export failed with code ${ex_err}.]"
  else
    echo "[Done. ONNX: ${PWD}/${ex_out}]"
  fi
  pause
}

manifest_scope() {
  echo
  echo "Manifest refresh scope:"
  echo "  [1] Single dataset"
  echo "  [2] All defaults (multi_dataset, pixelart_dataset, realism_dataset, stylized_dataset, ext_dataset)"
  read -r -p "Select 1 or 2: " c
  if [[ "${c}" == "2" ]]; then
    ask_ignore_optifine
    echo
    echo "Running: gen_from_labpbr_packs (all default datasets) ${IGNORE_OPTIFINE_FLAG}"
    echo
    run_gen_labpbr_all_defaults ${IGNORE_OPTIFINE_FLAG}
  else
    ask_data_root
    ask_ignore_optifine
    echo
    echo "Running: gen_from_labpbr_packs --dataset-root \"${DATA_ROOT}\" ${IGNORE_OPTIFINE_FLAG}"
    echo
    run_gen_labpbr --dataset-root "${DATA_ROOT}" ${IGNORE_OPTIFINE_FLAG}
  fi
  local st=$?
  echo
  if [[ ${st} -ne 0 ]]; then
    echo "[Manifest step reported an error.]"
  else
    echo "[Manifest refresh done.]"
  fi
  pause
}

both_manifest_train() {
  ask_data_root
  ask_ignore_optifine
  echo
  echo "--- Step 1: manifest ---"
  run_gen_labpbr --dataset-root "${DATA_ROOT}" ${IGNORE_OPTIFINE_FLAG}
  if [[ $? -ne 0 ]]; then
    echo "[Manifest step failed - skipping training.]"
    pause
    return
  fi
  echo
  echo "--- Step 2: training ---"
  train_spec_menu
}

both_manifest_train_torch_ort() {
  ask_data_root
  ask_ignore_optifine
  echo
  echo "--- Step 1: manifest ---"
  run_gen_labpbr --dataset-root "${DATA_ROOT}" ${IGNORE_OPTIFINE_FLAG}
  if [[ $? -ne 0 ]]; then
    echo "[Manifest step failed - skipping training.]"
    pause
    return
  fi
  echo
  echo "--- Step 2: training ---"
  train_spec_menu_torch_ort
}

train_spec_menu() {
  ask_data_root
  local spec_dev="cuda"
  local spec_bat="8"
  local spec_ep="40"
  local spec_spatial="fixed"
  local spec_tr="128"
  local spec_max_side=""
  local spec_downscale="box"
  local spec_accum="1"
  local spec_native_restrict="--native-restrict-to-target-tier"
  local spec_bp_enabled="--batch-policy-enabled"
  local spec_bp_mode="sqrt"
  local spec_bp_base_b="8"
  local spec_bp_base_lr="0.001"
  local spec_bp_max_lr=""
  local spec_bp_warmup_ratio="0.05"
  local spec_bp_warmup_min="500"
  local spec_bp_wd_mode="off"
  local spec_bp_clip="0"
  local spec_bp_ema="--no-ema-enabled"
  local spec_bp_ema_decay="0.999"
  local spec_bp_enabled="--batch-policy-enabled"
  local spec_bp_mode="sqrt"
  local spec_bp_base_b="8"
  local spec_bp_base_lr="0.001"
  local spec_bp_max_lr=""
  local spec_bp_warmup_ratio="0.05"
  local spec_bp_warmup_min="500"
  local spec_bp_wd_mode="off"
  local spec_bp_clip="0"
  local spec_bp_ema="--no-ema-enabled"
  local spec_bp_ema_decay="0.999"
  local spec_wrk="-1"
  local spec_inch="4"
  local spec_wid="64"
  local spec_outch="5"
  local spec_amp=""
  local spec_torch_ort=""
  local spec_torch_ort_provider=""
  local spec_trt_fp16=""
  local spec_torch_ort_debug=""
  local spec_torch_ort_mem_opt="0"
  local spec_torch_ort_triton=""
  local spec_torch_ort_zero3=""
  local spec_backend="pytorch"
  local spec_resume="--resume-auto"
  local spec_resetopt=""
  local spec_ort_dir="artifacts/ort"
  local spec_onnx="artifacts/SpecLab.onnx"
  local spec_ckpt="artifacts/SpecLab.pt"

  echo
  echo "Direct artist-spec training presets (PyTorch backend):"
  echo "  device=${spec_dev} batch=${spec_bat} epochs=${spec_ep} spatial=${spec_spatial} train-res=${spec_tr} workers=${spec_wrk}"
  echo "  in-ch=${spec_inch} width=${spec_wid} out-ch=${spec_outch} (4=RGBA, 5=RGBA+confidence)"
  echo "  backend=${spec_backend} resume=${spec_resume}"
  echo
  read -r -p "device cuda or cpu [${spec_dev}]: " v
  spec_dev=${v:-$spec_dev}
  read -r -p "batch size [${spec_bat}]: " v
  spec_bat=${v:-$spec_bat}
  read -r -p "epochs [${spec_ep}]: " v
  spec_ep=${v:-$spec_ep}
  read -r -p "spatial-mode fixed or native [${spec_spatial}]: " v
  spec_spatial=${v:-$spec_spatial}
  if [[ "${spec_spatial}" != "fixed" && "${spec_spatial}" != "native" ]]; then
    echo "[Warn] Unsupported value \"${spec_spatial}\"; defaulting to fixed."
    spec_spatial="fixed"
  fi
  if [[ "${spec_spatial}" == "fixed" ]]; then
    read -r -p "train-res [${spec_tr}]: " v
    spec_tr=${v:-$spec_tr}
  else
    echo "Native mode: train-res prompt skipped."
  fi
  read -r -p "max-train-side (empty = none) [${spec_max_side}]: " v
  spec_max_side=${v:-$spec_max_side}
  read -r -p "downscale-for-memory box|lanczos|nearest [${spec_downscale}]: " v
  spec_downscale=${v:-$spec_downscale}
  if [[ "${spec_downscale}" != "box" && "${spec_downscale}" != "lanczos" && "${spec_downscale}" != "nearest" ]]; then
    echo "[Warn] Unsupported value \"${spec_downscale}\"; defaulting to box."
    spec_downscale="box"
  fi
  read -r -p "grad-accum-steps [${spec_accum}]: " v
  spec_accum=${v:-$spec_accum}
  read -r -p "Native mode: enforce per-sample tag-match filter? Y/n: " v
  if [[ "${v}" =~ ^[Nn]$ ]]; then
    spec_native_restrict="--no-native-restrict-to-target-tier"
  else
    spec_native_restrict="--native-restrict-to-target-tier"
  fi
  read -r -p "Customize Batch Policy? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then
    read -r -p "Enable batch scaling safety policy? Y/n: " v
    if [[ "${v}" =~ ^[Nn]$ ]]; then spec_bp_enabled="--no-batch-policy-enabled"; else spec_bp_enabled="--batch-policy-enabled"; fi
    read -r -p "batch policy LR mode off|sqrt|linear [${spec_bp_mode}]: " v
    spec_bp_mode=${v:-$spec_bp_mode}
    if [[ "${spec_bp_mode}" != "off" && "${spec_bp_mode}" != "sqrt" && "${spec_bp_mode}" != "linear" ]]; then
      echo "[Warn] Unsupported value \"${spec_bp_mode}\"; defaulting to sqrt."
      spec_bp_mode="sqrt"
    fi
    read -r -p "baseline effective batch [${spec_bp_base_b}]: " v
    spec_bp_base_b=${v:-$spec_bp_base_b}
    read -r -p "baseline LR [${spec_bp_base_lr}]: " v
    spec_bp_base_lr=${v:-$spec_bp_base_lr}
    read -r -p "max scaled LR (empty=none) [${spec_bp_max_lr}]: " v
    spec_bp_max_lr=${v:-$spec_bp_max_lr}
    read -r -p "warmup ratio 0..1 [${spec_bp_warmup_ratio}]: " v
    spec_bp_warmup_ratio=${v:-$spec_bp_warmup_ratio}
    read -r -p "warmup min steps [${spec_bp_warmup_min}]: " v
    spec_bp_warmup_min=${v:-$spec_bp_warmup_min}
    read -r -p "weight decay mode off|mild_batch_scaled [${spec_bp_wd_mode}]: " v
    spec_bp_wd_mode=${v:-$spec_bp_wd_mode}
    if [[ "${spec_bp_wd_mode}" != "off" && "${spec_bp_wd_mode}" != "mild_batch_scaled" ]]; then
      echo "[Warn] Unsupported value \"${spec_bp_wd_mode}\"; defaulting to off."
      spec_bp_wd_mode="off"
    fi
    read -r -p "grad clip norm (<=0 disables) [${spec_bp_clip}]: " v
    spec_bp_clip=${v:-$spec_bp_clip}
    read -r -p "Enable EMA for eval/checkpoints? y/N: " v
    if [[ "${v}" =~ ^[Yy]$ ]]; then spec_bp_ema="--ema-enabled"; else spec_bp_ema="--no-ema-enabled"; fi
    read -r -p "EMA decay (0..1) [${spec_bp_ema_decay}]: " v
    spec_bp_ema_decay=${v:-$spec_bp_ema_decay}
  fi
  read -r -p "workers -1=auto, 0=main only [${spec_wrk}]: " v
  spec_wrk=${v:-$spec_wrk}
  read -r -p "in-channels 3 or 4 [${spec_inch}]: " v
  spec_inch=${v:-$spec_inch}
  read -r -p "model width [${spec_wid}]: " v
  spec_wid=${v:-$spec_wid}
  read -r -p "out-channels 4 or 5 [${spec_outch}]: " v
  spec_outch=${v:-$spec_outch}
  read -r -p "backend pytorch or ort [${spec_backend}]: " v
  spec_backend=${v:-$spec_backend}
  read -r -p "Resume automatically if checkpoint exists? Y/n: " v
  if [[ "${v}" =~ ^[Nn]$ ]]; then spec_resume=""; else spec_resume="--resume-auto"; fi
  read -r -p "On resume, reset optimizer state? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then spec_resetopt="--reset-optimizer"; else spec_resetopt=""; fi
  if [[ "${spec_backend}" == "ort" ]]; then
    read -r -p "ORT artifacts dir [${spec_ort_dir}]: " v
    spec_ort_dir=${v:-$spec_ort_dir}
  fi
  if [[ "${spec_backend}" == "ort" && "${spec_spatial}" == "native" ]]; then
    echo "[Warn] ORT backend only supports spatial-mode=fixed; forcing fixed."
    spec_spatial="fixed"
  fi
  read -r -p "Output .onnx path [${spec_onnx}]: " v
  spec_onnx=${v:-$spec_onnx}
  read -r -p "Checkpoint .pt path [${spec_ckpt}]: " v
  spec_ckpt=${v:-$spec_ckpt}
  read -r -p "Disable AMP on CUDA? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then spec_amp="--no-amp"; else spec_amp=""; fi
  read -r -p "Use torch-ort ORTModule (CUDA only)? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then
    spec_torch_ort="--torch-ort"
    read -r -p "tensor backend for torch-ort cuda or tensorrt [cuda]: " tp
    tp=${tp:-cuda}
    if [[ "${tp}" != "cuda" && "${tp}" != "tensorrt" ]]; then
      echo "[Warn] Unsupported value \"${tp}\"; defaulting to cuda."
      tp="cuda"
    fi
    spec_torch_ort_provider="--torch-ort-provider ${tp}"
    spec_trt_fp16=""
    if [[ "${tp}" == "tensorrt" ]]; then
      read -r -p "TensorRT FP16 engine (ORT_TENSORRT_FP16_ENABLE)? Y/n: " v
      if [[ "${v}" =~ ^[Nn]$ ]]; then spec_trt_fp16="--no-torch-ort-tensorrt-fp16"; else spec_trt_fp16=""; fi
    fi
    read -r -p "Print torch-ort extension diagnostics (--torch-ort-debug)? y/N: " v
    if [[ "${v}" =~ ^[Yy]$ ]]; then spec_torch_ort_debug="--torch-ort-debug"; else spec_torch_ort_debug=""; fi
    read -r -p "ORTModule Memory Optimizer level (--torch-ort-memory-opt-level) [${spec_torch_ort_mem_opt}]: " v
    spec_torch_ort_mem_opt=${v:-$spec_torch_ort_mem_opt}
    if ! [[ "${spec_torch_ort_mem_opt}" =~ ^[0-9]+$ ]]; then
      echo "[Warn] Invalid Memory Optimizer level \"${spec_torch_ort_mem_opt}\"; defaulting to 0."
      spec_torch_ort_mem_opt="0"
    fi
    read -r -p "Enable TritonOp (--torch-ort-triton-op-enabled)? y/N: " v
    if [[ "${v}" =~ ^[Yy]$ ]]; then spec_torch_ort_triton="--torch-ort-triton-op-enabled"; else spec_torch_ort_triton=""; fi
    read -r -p "Enable ZeRO stage3 support (--torch-ort-zero-stage3-support)? y/N: " v
    if [[ "${v}" =~ ^[Yy]$ ]]; then spec_torch_ort_zero3="--torch-ort-zero-stage3-support"; else spec_torch_ort_zero3=""; fi
  fi

  local spec_max_side_arg=""
  if [[ -n "${spec_max_side}" ]]; then
    spec_max_side_arg="--max-train-side ${spec_max_side}"
  fi
  local spec_bp_max_lr_arg=""
  if [[ -n "${spec_bp_max_lr}" ]]; then
    spec_bp_max_lr_arg="--batch-policy-max-lr ${spec_bp_max_lr}"
  fi
  local spec_batch_policy_args="${spec_bp_enabled} --batch-policy-lr-mode ${spec_bp_mode} --batch-policy-baseline-batch ${spec_bp_base_b} --batch-policy-baseline-lr ${spec_bp_base_lr} ${spec_bp_max_lr_arg} --warmup-ratio ${spec_bp_warmup_ratio} --warmup-min-steps ${spec_bp_warmup_min} --weight-decay-mode ${spec_bp_wd_mode} --grad-clip-norm ${spec_bp_clip} ${spec_bp_ema} --ema-decay ${spec_bp_ema_decay}"

  echo
  echo "Will run:"
  echo "  ${PY} -m ml_specular.train_spec --trainer-backend ${spec_backend} --data-root \"${DATA_ROOT}\" --device ${spec_dev} --batch ${spec_bat} --epochs ${spec_ep} --spatial-mode ${spec_spatial} --train-res ${spec_tr} ${spec_max_side_arg} --downscale-for-memory ${spec_downscale} --grad-accum-steps ${spec_accum} ${spec_batch_policy_args} ${spec_native_restrict} --workers ${spec_wrk} --in-channels ${spec_inch} --width ${spec_wid} --out-channels ${spec_outch} ${spec_resume} ${spec_resetopt} ${spec_amp} ${spec_torch_ort} ${spec_torch_ort_provider} ${spec_trt_fp16} ${spec_torch_ort_debug} --torch-ort-memory-opt-level ${spec_torch_ort_mem_opt} ${spec_torch_ort_triton} ${spec_torch_ort_zero3} --ort-artifacts-dir \"${spec_ort_dir}\" --out-onnx \"${spec_onnx}\" --ckpt \"${spec_ckpt}\""
  echo
  pause
  save_last_session "${PY}" -m ml_specular.train_spec --trainer-backend "${spec_backend}" --data-root "${DATA_ROOT}" --device "${spec_dev}" --batch "${spec_bat}" --epochs "${spec_ep}" --spatial-mode "${spec_spatial}" --train-res "${spec_tr}" ${spec_max_side_arg} --downscale-for-memory "${spec_downscale}" --grad-accum-steps "${spec_accum}" ${spec_batch_policy_args} ${spec_native_restrict} --workers "${spec_wrk}" --in-channels "${spec_inch}" --width "${spec_wid}" --out-channels "${spec_outch}" ${spec_resume} ${spec_resetopt} ${spec_amp} ${spec_torch_ort} ${spec_torch_ort_provider} ${spec_trt_fp16} ${spec_torch_ort_debug} --torch-ort-memory-opt-level "${spec_torch_ort_mem_opt}" ${spec_torch_ort_triton} ${spec_torch_ort_zero3} --ort-artifacts-dir "${spec_ort_dir}" --out-onnx "${spec_onnx}" --ckpt "${spec_ckpt}"
  # shellcheck disable=SC2086
  "${PY}" -m ml_specular.train_spec --trainer-backend "${spec_backend}" --data-root "${DATA_ROOT}" --device "${spec_dev}" --batch "${spec_bat}" --epochs "${spec_ep}" --spatial-mode "${spec_spatial}" --train-res "${spec_tr}" ${spec_max_side_arg} --downscale-for-memory "${spec_downscale}" --grad-accum-steps "${spec_accum}" ${spec_batch_policy_args} ${spec_native_restrict} --workers "${spec_wrk}" --in-channels "${spec_inch}" --width "${spec_wid}" --out-channels "${spec_outch}" ${spec_resume} ${spec_resetopt} ${spec_amp} ${spec_torch_ort} ${spec_torch_ort_provider} ${spec_trt_fp16} ${spec_torch_ort_debug} --torch-ort-memory-opt-level "${spec_torch_ort_mem_opt}" ${spec_torch_ort_triton} ${spec_torch_ort_zero3} --ort-artifacts-dir "${spec_ort_dir}" --out-onnx "${spec_onnx}" --ckpt "${spec_ckpt}"
  local specerr=$?
  echo
  if [[ ${specerr} -eq 130 ]]; then
    echo "[Aborted safely (Ctrl+C). Resume later with --resume-auto or Resume training.]"
  elif [[ ${specerr} -ne 0 ]]; then
    echo "[Direct spec training exited with error ${specerr}.]"
  else
    echo "[Done. ONNX: ${PWD}/${spec_onnx}]"
    echo "Running ONNX contract check..."
    "${PY}" -m ml_specular.verify_spec_onnx "${spec_onnx}"
  fi
  pause
}

train_spec_menu_torch_ort() {
  ask_data_root
  local spec_dev="cuda"
  local spec_bat="8"
  local spec_ep="40"
  local spec_spatial="fixed"
  local spec_tr="128"
  local spec_max_side=""
  local spec_downscale="box"
  local spec_accum="1"
  local spec_native_restrict="--native-restrict-to-target-tier"
  local spec_bp_enabled="--batch-policy-enabled"
  local spec_bp_mode="sqrt"
  local spec_bp_base_b="8"
  local spec_bp_base_lr="0.001"
  local spec_bp_max_lr=""
  local spec_bp_warmup_ratio="0.05"
  local spec_bp_warmup_min="500"
  local spec_bp_wd_mode="off"
  local spec_bp_clip="0"
  local spec_bp_ema="--no-ema-enabled"
  local spec_bp_ema_decay="0.999"
  local spec_wrk="-1"
  local spec_inch="4"
  local spec_wid="64"
  local spec_outch="5"
  local spec_amp=""
  local spec_backend="pytorch"
  local spec_resume="--resume-auto"
  local spec_resetopt=""
  local spec_ort_dir="artifacts/ort"
  local spec_onnx="artifacts/SpecLab.onnx"
  local spec_ckpt="artifacts/SpecLab.pt"
  local tp="cuda"
  local spec_trt_fp16=""
  local spec_torch_ort_debug=""
  local spec_torch_ort_mem_opt="0"
  local spec_torch_ort_triton=""
  local spec_torch_ort_zero3=""

  echo
  echo "Direct artist-spec training (Torch-ORT / ORTModule — requires CUDA + torch-ort):"
  echo "  device=${spec_dev} batch=${spec_bat} epochs=${spec_ep} spatial=${spec_spatial} train-res=${spec_tr} workers=${spec_wrk}"
  echo "  in-ch=${spec_inch} width=${spec_wid} out-ch=${spec_outch} (4=RGBA, 5=RGBA+confidence)"
  echo "  --torch-ort is always on; choose execution provider below (TensorRT needs compatible ORT/GPU stack)."
  echo
  read -r -p "device cuda or cpu [${spec_dev}]: " v
  spec_dev=${v:-$spec_dev}
  read -r -p "batch size [${spec_bat}]: " v
  spec_bat=${v:-$spec_bat}
  read -r -p "epochs [${spec_ep}]: " v
  spec_ep=${v:-$spec_ep}
  read -r -p "spatial-mode fixed or native [${spec_spatial}]: " v
  spec_spatial=${v:-$spec_spatial}
  if [[ "${spec_spatial}" != "fixed" && "${spec_spatial}" != "native" ]]; then
    echo "[Warn] Unsupported value \"${spec_spatial}\"; defaulting to fixed."
    spec_spatial="fixed"
  fi
  if [[ "${spec_spatial}" == "fixed" ]]; then
    read -r -p "train-res [${spec_tr}]: " v
    spec_tr=${v:-$spec_tr}
  else
    echo "Native mode: train-res prompt skipped."
  fi
  read -r -p "max-train-side (empty = none) [${spec_max_side}]: " v
  spec_max_side=${v:-$spec_max_side}
  read -r -p "downscale-for-memory box|lanczos|nearest [${spec_downscale}]: " v
  spec_downscale=${v:-$spec_downscale}
  if [[ "${spec_downscale}" != "box" && "${spec_downscale}" != "lanczos" && "${spec_downscale}" != "nearest" ]]; then
    echo "[Warn] Unsupported value \"${spec_downscale}\"; defaulting to box."
    spec_downscale="box"
  fi
  read -r -p "grad-accum-steps [${spec_accum}]: " v
  spec_accum=${v:-$spec_accum}
  read -r -p "Native mode: enforce per-sample tag-match filter? Y/n: " v
  if [[ "${v}" =~ ^[Nn]$ ]]; then
    spec_native_restrict="--no-native-restrict-to-target-tier"
  else
    spec_native_restrict="--native-restrict-to-target-tier"
  fi
  read -r -p "Customize Batch Policy? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then
    read -r -p "Enable batch scaling safety policy? Y/n: " v
    if [[ "${v}" =~ ^[Nn]$ ]]; then spec_bp_enabled="--no-batch-policy-enabled"; else spec_bp_enabled="--batch-policy-enabled"; fi
    read -r -p "batch policy LR mode off|sqrt|linear [${spec_bp_mode}]: " v
    spec_bp_mode=${v:-$spec_bp_mode}
    if [[ "${spec_bp_mode}" != "off" && "${spec_bp_mode}" != "sqrt" && "${spec_bp_mode}" != "linear" ]]; then
      echo "[Warn] Unsupported value \"${spec_bp_mode}\"; defaulting to sqrt."
      spec_bp_mode="sqrt"
    fi
    read -r -p "baseline effective batch [${spec_bp_base_b}]: " v
    spec_bp_base_b=${v:-$spec_bp_base_b}
    read -r -p "baseline LR [${spec_bp_base_lr}]: " v
    spec_bp_base_lr=${v:-$spec_bp_base_lr}
    read -r -p "max scaled LR (empty=none) [${spec_bp_max_lr}]: " v
    spec_bp_max_lr=${v:-$spec_bp_max_lr}
    read -r -p "warmup ratio 0..1 [${spec_bp_warmup_ratio}]: " v
    spec_bp_warmup_ratio=${v:-$spec_bp_warmup_ratio}
    read -r -p "warmup min steps [${spec_bp_warmup_min}]: " v
    spec_bp_warmup_min=${v:-$spec_bp_warmup_min}
    read -r -p "weight decay mode off|mild_batch_scaled [${spec_bp_wd_mode}]: " v
    spec_bp_wd_mode=${v:-$spec_bp_wd_mode}
    if [[ "${spec_bp_wd_mode}" != "off" && "${spec_bp_wd_mode}" != "mild_batch_scaled" ]]; then
      echo "[Warn] Unsupported value \"${spec_bp_wd_mode}\"; defaulting to off."
      spec_bp_wd_mode="off"
    fi
    read -r -p "grad clip norm (<=0 disables) [${spec_bp_clip}]: " v
    spec_bp_clip=${v:-$spec_bp_clip}
    read -r -p "Enable EMA for eval/checkpoints? y/N: " v
    if [[ "${v}" =~ ^[Yy]$ ]]; then spec_bp_ema="--ema-enabled"; else spec_bp_ema="--no-ema-enabled"; fi
    read -r -p "EMA decay (0..1) [${spec_bp_ema_decay}]: " v
    spec_bp_ema_decay=${v:-$spec_bp_ema_decay}
  fi
  read -r -p "workers -1=auto, 0=main only [${spec_wrk}]: " v
  spec_wrk=${v:-$spec_wrk}
  read -r -p "in-channels 3 or 4 [${spec_inch}]: " v
  spec_inch=${v:-$spec_inch}
  read -r -p "model width [${spec_wid}]: " v
  spec_wid=${v:-$spec_wid}
  read -r -p "out-channels 4 or 5 [${spec_outch}]: " v
  spec_outch=${v:-$spec_outch}
  read -r -p "torch-ort execution provider: cuda or tensorrt [${tp}]: " v
  tp=${v:-$tp}
  if [[ "${tp}" != "cuda" && "${tp}" != "tensorrt" ]]; then
    echo "[Warn] Unsupported value \"${tp}\"; defaulting to cuda."
    tp="cuda"
  fi
  if [[ "${tp}" == "tensorrt" ]]; then
    read -r -p "TensorRT FP16 engine (ORT_TENSORRT_FP16_ENABLE)? Y/n: " v
    if [[ "${v}" =~ ^[Nn]$ ]]; then spec_trt_fp16="--no-torch-ort-tensorrt-fp16"; else spec_trt_fp16=""; fi
  fi
  read -r -p "Resume automatically if checkpoint exists? Y/n: " v
  if [[ "${v}" =~ ^[Nn]$ ]]; then spec_resume=""; else spec_resume="--resume-auto"; fi
  read -r -p "On resume, reset optimizer state? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then spec_resetopt="--reset-optimizer"; else spec_resetopt=""; fi
  read -r -p "Output .onnx path [${spec_onnx}]: " v
  spec_onnx=${v:-$spec_onnx}
  read -r -p "Checkpoint .pt path [${spec_ckpt}]: " v
  spec_ckpt=${v:-$spec_ckpt}
  read -r -p "Disable AMP on CUDA? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then spec_amp="--no-amp"; else spec_amp=""; fi
  read -r -p "Print torch-ort extension diagnostics (--torch-ort-debug)? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then spec_torch_ort_debug="--torch-ort-debug"; else spec_torch_ort_debug=""; fi
  read -r -p "ORTModule Memory Optimizer level (--torch-ort-memory-opt-level) [${spec_torch_ort_mem_opt}]: " v
  spec_torch_ort_mem_opt=${v:-$spec_torch_ort_mem_opt}
  if ! [[ "${spec_torch_ort_mem_opt}" =~ ^[0-9]+$ ]]; then
    echo "[Warn] Invalid Memory Optimizer level \"${spec_torch_ort_mem_opt}\"; defaulting to 0."
    spec_torch_ort_mem_opt="0"
  fi
  read -r -p "Enable TritonOp (--torch-ort-triton-op-enabled)? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then spec_torch_ort_triton="--torch-ort-triton-op-enabled"; else spec_torch_ort_triton=""; fi
  read -r -p "Enable ZeRO stage3 support (--torch-ort-zero-stage3-support)? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then spec_torch_ort_zero3="--torch-ort-zero-stage3-support"; else spec_torch_ort_zero3=""; fi

  local spec_torch_ort="--torch-ort"
  local spec_torch_ort_provider="--torch-ort-provider ${tp}"
  local spec_max_side_arg=""
  if [[ -n "${spec_max_side}" ]]; then
    spec_max_side_arg="--max-train-side ${spec_max_side}"
  fi
  local spec_bp_max_lr_arg=""
  if [[ -n "${spec_bp_max_lr}" ]]; then
    spec_bp_max_lr_arg="--batch-policy-max-lr ${spec_bp_max_lr}"
  fi
  local spec_batch_policy_args="${spec_bp_enabled} --batch-policy-lr-mode ${spec_bp_mode} --batch-policy-baseline-batch ${spec_bp_base_b} --batch-policy-baseline-lr ${spec_bp_base_lr} ${spec_bp_max_lr_arg} --warmup-ratio ${spec_bp_warmup_ratio} --warmup-min-steps ${spec_bp_warmup_min} --weight-decay-mode ${spec_bp_wd_mode} --grad-clip-norm ${spec_bp_clip} ${spec_bp_ema} --ema-decay ${spec_bp_ema_decay}"

  echo
  echo "Will run:"
  echo "  ${PY} -m ml_specular.train_spec --trainer-backend ${spec_backend} --data-root \"${DATA_ROOT}\" --device ${spec_dev} --batch ${spec_bat} --epochs ${spec_ep} --spatial-mode ${spec_spatial} --train-res ${spec_tr} ${spec_max_side_arg} --downscale-for-memory ${spec_downscale} --grad-accum-steps ${spec_accum} ${spec_batch_policy_args} ${spec_native_restrict} --workers ${spec_wrk} --in-channels ${spec_inch} --width ${spec_wid} --out-channels ${spec_outch} ${spec_resume} ${spec_resetopt} ${spec_amp} ${spec_torch_ort} ${spec_torch_ort_provider} ${spec_trt_fp16} ${spec_torch_ort_debug} --torch-ort-memory-opt-level ${spec_torch_ort_mem_opt} ${spec_torch_ort_triton} ${spec_torch_ort_zero3} --ort-artifacts-dir \"${spec_ort_dir}\" --out-onnx \"${spec_onnx}\" --ckpt \"${spec_ckpt}\""
  echo
  pause
  save_last_session "${PY}" -m ml_specular.train_spec --trainer-backend "${spec_backend}" --data-root "${DATA_ROOT}" --device "${spec_dev}" --batch "${spec_bat}" --epochs "${spec_ep}" --spatial-mode "${spec_spatial}" --train-res "${spec_tr}" ${spec_max_side_arg} --downscale-for-memory "${spec_downscale}" --grad-accum-steps "${spec_accum}" ${spec_batch_policy_args} ${spec_native_restrict} --workers "${spec_wrk}" --in-channels "${spec_inch}" --width "${spec_wid}" --out-channels "${spec_outch}" ${spec_resume} ${spec_resetopt} ${spec_amp} ${spec_torch_ort} ${spec_torch_ort_provider} ${spec_trt_fp16} ${spec_torch_ort_debug} --torch-ort-memory-opt-level "${spec_torch_ort_mem_opt}" ${spec_torch_ort_triton} ${spec_torch_ort_zero3} --ort-artifacts-dir "${spec_ort_dir}" --out-onnx "${spec_onnx}" --ckpt "${spec_ckpt}"
  # shellcheck disable=SC2086
  "${PY}" -m ml_specular.train_spec --trainer-backend "${spec_backend}" --data-root "${DATA_ROOT}" --device "${spec_dev}" --batch "${spec_bat}" --epochs "${spec_ep}" --spatial-mode "${spec_spatial}" --train-res "${spec_tr}" ${spec_max_side_arg} --downscale-for-memory "${spec_downscale}" --grad-accum-steps "${spec_accum}" ${spec_batch_policy_args} ${spec_native_restrict} --workers "${spec_wrk}" --in-channels "${spec_inch}" --width "${spec_wid}" --out-channels "${spec_outch}" ${spec_resume} ${spec_resetopt} ${spec_amp} ${spec_torch_ort} ${spec_torch_ort_provider} ${spec_trt_fp16} ${spec_torch_ort_debug} --torch-ort-memory-opt-level "${spec_torch_ort_mem_opt}" ${spec_torch_ort_triton} ${spec_torch_ort_zero3} --ort-artifacts-dir "${spec_ort_dir}" --out-onnx "${spec_onnx}" --ckpt "${spec_ckpt}"
  local specerr=$?
  echo
  if [[ ${specerr} -eq 130 ]]; then
    echo "[Aborted safely (Ctrl+C). Resume later with --resume-auto or Resume training.]"
  elif [[ ${specerr} -ne 0 ]]; then
    echo "[Direct spec training exited with error ${specerr}.]"
  else
    echo "[Done. ONNX: ${PWD}/${spec_onnx}]"
    echo "Running ONNX contract check..."
    "${PY}" -m ml_specular.verify_spec_onnx "${spec_onnx}"
  fi
  pause
}

ort_export_loss() {
  local ort_loss_dir="artifacts/ort"
  local ort_loss_ckpt=""
  local ort_loss_inch="4"
  local ort_loss_outch="5"
  local ort_loss_wid="64"
  local ort_loss_tr="128"
  echo
  echo "Export train_model.onnx and eval_model.onnx (full-batch loss = spec_loss contract)."
  read -r -p "output directory [${ort_loss_dir}]: " v
  ort_loss_dir=${v:-$ort_loss_dir}
  read -r -p "optional checkpoint .pt (empty = random init): " ort_loss_ckpt
  read -r -p "in-channels 3 or 4 [${ort_loss_inch}]: " v
  ort_loss_inch=${v:-$ort_loss_inch}
  read -r -p "out-channels 4 or 5 [${ort_loss_outch}]: " v
  ort_loss_outch=${v:-$ort_loss_outch}
  read -r -p "model width [${ort_loss_wid}]: " v
  ort_loss_wid=${v:-$ort_loss_wid}
  read -r -p "trace train-res [${ort_loss_tr}]: " v
  ort_loss_tr=${v:-$ort_loss_tr}
  echo
  if [[ -n "${ort_loss_ckpt}" && ! -f "${ort_loss_ckpt}" ]]; then
    echo "[Warn] Checkpoint not found, exporting without --ckpt: ${ort_loss_ckpt}"
    ort_loss_ckpt=""
  fi
  if [[ -z "${ort_loss_ckpt}" ]]; then
    echo "Running: ${PY} -m ml_specular.export_ort_specular_graphs --out-dir \"${ort_loss_dir}\" --in-channels ${ort_loss_inch} --out-channels ${ort_loss_outch} --width ${ort_loss_wid} --train-res ${ort_loss_tr}"
    echo
    pause
    save_last_session "${PY}" -m ml_specular.export_ort_specular_graphs --out-dir "${ort_loss_dir}" --in-channels "${ort_loss_inch}" --out-channels "${ort_loss_outch}" --width "${ort_loss_wid}" --train-res "${ort_loss_tr}"
    "${PY}" -m ml_specular.export_ort_specular_graphs --out-dir "${ort_loss_dir}" --in-channels "${ort_loss_inch}" --out-channels "${ort_loss_outch}" --width "${ort_loss_wid}" --train-res "${ort_loss_tr}"
  else
    echo "Running: ${PY} -m ml_specular.export_ort_specular_graphs --out-dir \"${ort_loss_dir}\" --in-channels ${ort_loss_inch} --out-channels ${ort_loss_outch} --width ${ort_loss_wid} --train-res ${ort_loss_tr} --ckpt \"${ort_loss_ckpt}\""
    echo
    pause
    save_last_session "${PY}" -m ml_specular.export_ort_specular_graphs --out-dir "${ort_loss_dir}" --in-channels "${ort_loss_inch}" --out-channels "${ort_loss_outch}" --width "${ort_loss_wid}" --train-res "${ort_loss_tr}" --ckpt "${ort_loss_ckpt}"
    "${PY}" -m ml_specular.export_ort_specular_graphs --out-dir "${ort_loss_dir}" --in-channels "${ort_loss_inch}" --out-channels "${ort_loss_outch}" --width "${ort_loss_wid}" --train-res "${ort_loss_tr}" --ckpt "${ort_loss_ckpt}"
  fi
  echo
  pause
}

ort_artifacts_chain() {
  local ort_adir="artifacts/ort"
  local ort_fckpt="artifacts/SpecLab.pt"
  local ort_inch="4"
  local ort_outch="5"
  local ort_wid="64"
  local ort_tr="128"
  local ort_opt="adamw"
  echo
  echo "Step A: export forward_model.onnx (logits only)."
  echo "Step B: onnxruntime.training.artifacts — training_model.onnx, eval_model.onnx, optimizer_model.onnx."
  echo "Requires: onnxruntime-training"
  echo
  read -r -p "artifact directory [${ort_adir}]: " v
  ort_adir=${v:-$ort_adir}
  read -r -p "optional .pt for forward weights [${ort_fckpt}]: " v
  ort_fckpt=${v:-$ort_fckpt}
  read -r -p "in-channels 3 or 4 [${ort_inch}]: " v
  ort_inch=${v:-$ort_inch}
  read -r -p "out-channels 4 or 5 [${ort_outch}]: " v
  ort_outch=${v:-$ort_outch}
  read -r -p "model width [${ort_wid}]: " v
  ort_wid=${v:-$ort_wid}
  read -r -p "trace train-res [${ort_tr}]: " v
  ort_tr=${v:-$ort_tr}
  read -r -p "optimizer adamw or sgd [${ort_opt}]: " v
  ort_opt=${v:-$ort_opt}
  echo
  local fout="${ort_adir}/forward_model.onnx"
  if [[ ! -f "${ort_fckpt}" ]]; then
    echo "[Info] No valid checkpoint at ${ort_fckpt} — forward export uses random weights."
  fi
  echo "--- Step A ---"
  if [[ -f "${ort_fckpt}" ]]; then
    "${PY}" -m ml_specular.export_ort_forward_core --out "${fout}" --in-channels "${ort_inch}" --out-channels "${ort_outch}" --width "${ort_wid}" --train-res "${ort_tr}" --ckpt "${ort_fckpt}"
  else
    "${PY}" -m ml_specular.export_ort_forward_core --out "${fout}" --in-channels "${ort_inch}" --out-channels "${ort_outch}" --width "${ort_wid}" --train-res "${ort_tr}"
  fi
  if [[ $? -ne 0 ]]; then
    echo "[Step A failed.]"
    pause
    return
  fi
  echo "--- Step B ---"
  "${PY}" -m ml_specular.generate_ort_training_artifacts --loss spec --out-channels "${ort_outch}" --base-onnx "${fout}" --artifact-directory "${ort_adir}" --optimizer "${ort_opt}"
  local gen_err=$?
  echo
  if [[ ${gen_err} -ne 0 ]]; then
    echo "[Step B failed with code ${gen_err}.]"
  else
    echo "[Artifacts under ${PWD}/${ort_adir}]"
  fi
  pause
}

train_spec_ort_menu() {
  ask_data_root
  local spec_dev="cuda"
  local spec_bat="8"
  local spec_ep="40"
  local spec_tr="128"
  local spec_wrk="-1"
  local spec_inch="4"
  local spec_wid="64"
  local spec_outch="5"
  local spec_amp=""
  local spec_resume="--resume-auto"
  local spec_resetopt=""
  local spec_ort_dir="artifacts/ort"
  local spec_onnx="artifacts/SpecLab.onnx"
  local spec_ckpt="artifacts/SpecLab.pt"

  echo
  echo "ORT backend training (requires training_model.onnx or train_model.onnx + eval + optimizer):"
  echo "  device=${spec_dev} batch=${spec_bat} epochs=${spec_ep} train-res=${spec_tr} workers=${spec_wrk}"
  echo "  in-ch=${spec_inch} width=${spec_wid} out-ch=${spec_outch}"
  echo "  ort-artifacts-dir=${spec_ort_dir}"
  echo
  read -r -p "device cuda or cpu [${spec_dev}]: " v
  spec_dev=${v:-$spec_dev}
  read -r -p "batch size [${spec_bat}]: " v
  spec_bat=${v:-$spec_bat}
  read -r -p "epochs [${spec_ep}]: " v
  spec_ep=${v:-$spec_ep}
  read -r -p "train-res [${spec_tr}]: " v
  spec_tr=${v:-$spec_tr}
  read -r -p "workers -1=auto, 0=main only [${spec_wrk}]: " v
  spec_wrk=${v:-$spec_wrk}
  read -r -p "in-channels 3 or 4 [${spec_inch}]: " v
  spec_inch=${v:-$spec_inch}
  read -r -p "model width [${spec_wid}]: " v
  spec_wid=${v:-$spec_wid}
  read -r -p "out-channels 4 or 5 [${spec_outch}]: " v
  spec_outch=${v:-$spec_outch}
  read -r -p "Resume automatically if checkpoint exists? Y/n: " v
  if [[ "${v}" =~ ^[Nn]$ ]]; then spec_resume=""; else spec_resume="--resume-auto"; fi
  read -r -p "On resume, reset optimizer state? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then spec_resetopt="--reset-optimizer"; else spec_resetopt=""; fi
  read -r -p "ORT artifacts dir [${spec_ort_dir}]: " v
  spec_ort_dir=${v:-$spec_ort_dir}
  read -r -p "Output .onnx path (inference export after train) [${spec_onnx}]: " v
  spec_onnx=${v:-$spec_onnx}
  read -r -p "Checkpoint .pt path [${spec_ckpt}]: " v
  spec_ckpt=${v:-$spec_ckpt}
  read -r -p "Disable AMP on CUDA? y/N: " v
  if [[ "${v}" =~ ^[Yy]$ ]]; then spec_amp="--no-amp"; else spec_amp=""; fi

  echo
  echo "Will run:"
  echo "  ${PY} -m ml_specular.train_spec --trainer-backend ort --data-root \"${DATA_ROOT}\" --device ${spec_dev} --batch ${spec_bat} --epochs ${spec_ep} --train-res ${spec_tr} --workers ${spec_wrk} --in-channels ${spec_inch} --width ${spec_wid} --out-channels ${spec_outch} ${spec_resume} ${spec_resetopt} ${spec_amp} --ort-artifacts-dir \"${spec_ort_dir}\" --out-onnx \"${spec_onnx}\" --ckpt \"${spec_ckpt}\""
  echo
  pause
  save_last_session "${PY}" -m ml_specular.train_spec --trainer-backend ort --data-root "${DATA_ROOT}" --device "${spec_dev}" --batch "${spec_bat}" --epochs "${spec_ep}" --train-res "${spec_tr}" --workers "${spec_wrk}" --in-channels "${spec_inch}" --width "${spec_wid}" --out-channels "${spec_outch}" ${spec_resume} ${spec_resetopt} ${spec_amp} --ort-artifacts-dir "${spec_ort_dir}" --out-onnx "${spec_onnx}" --ckpt "${spec_ckpt}"
  # shellcheck disable=SC2086
  "${PY}" -m ml_specular.train_spec --trainer-backend ort --data-root "${DATA_ROOT}" --device "${spec_dev}" --batch "${spec_bat}" --epochs "${spec_ep}" --train-res "${spec_tr}" --workers "${spec_wrk}" --in-channels "${spec_inch}" --width "${spec_wid}" --out-channels "${spec_outch}" ${spec_resume} ${spec_resetopt} ${spec_amp} --ort-artifacts-dir "${spec_ort_dir}" --out-onnx "${spec_onnx}" --ckpt "${spec_ckpt}"
  local specerr=$?
  echo
  if [[ ${specerr} -eq 130 ]]; then
    echo "[Aborted safely (Ctrl+C). Resume later with --resume-auto or Resume training.]"
  elif [[ ${specerr} -ne 0 ]]; then
    echo "[ORT training exited with error ${specerr}.]"
  else
    echo "[Done. ONNX: ${PWD}/${spec_onnx}]"
    echo "Running ONNX contract check..."
    "${PY}" -m ml_specular.verify_spec_onnx "${spec_onnx}"
  fi
  pause
}

ort_menu() {
  while true; do
    echo
    echo "========================================"
    echo "  Specular ML - ORT training workflow"
    echo "========================================"
    echo "  [1] Export train/eval loss ONNX (spec_loss graph; our script)"
    echo "  [2] Export forward ONNX, then ORT artifact generation"
    echo "      (optimizer_model.onnx + training/eval graphs from onnxruntime.training)"
    echo "  [3] Run train_spec ORT backend (training / resume)"
    echo "  [4] Export ONNX from checkpoint only (train_spec --export-only)"
    echo "  [0] Back"
    echo
    read -r -p "Select 1-4 or 0: " c
    case "${c}" in
      1) ort_export_loss ;;
      2) ort_artifacts_chain ;;
      3) train_spec_ort_menu ;;
      4) export_only ;;
      0) return ;;
      *) ;;
    esac
  done
}

pipeline_menu
