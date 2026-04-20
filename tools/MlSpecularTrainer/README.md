# MlSpecularTrainer — ONNX specular training for AutoPBR

**Windows:** plain `python` often points at **Python 3.5** and will fail. Use **`py -3.12`** or run **`gen_labpbr.cmd`** from this folder. For a guided menu, run **`Training Menu.bat`** (requires `.venv312`). The first prompt chooses **PyTorch workflow**, **ORT workflow** (loss ONNX export → forward ONNX + ORT artifact generation → ORT training), or **Torch-ORT workflow** (same guided steps as PyTorch with `--torch-ort` and **CUDA vs TensorRT** provider prompts). For the Linux container workflow, use **`run_ml_container.ps1`** / **`run_ml_container.sh`**, or **`Start Training Pipeline.bat`** (pre-enables `onnxruntime-training` + `torch-ort`). **Inside the container**, run **`bash training_menu.sh`** for the same menu as **`Training Menu.bat`**.

For end-to-end Linux wrapping (PyTorch + ORT artifact tooling + optional torch-ort), see **[`DOCKER.md`](DOCKER.md)** and run `run_ml_container.ps1` / `run_ml_container.sh`.

Trains a **specular predictor** (`diffuse -> _s RGBA`) consumed by AutoPBR’s ML specular path.

**LabPBR byte layout, edge parity, postprocess, and R-remap caveats:** [`../../docs/ml-specular-labpbr-contract.md`](../../docs/ml-specular-labpbr-contract.md).

## ONNX contract (specular predictor)

- **Input:** `float32` **NCHW** `[1, 3 or 4, H, W]` — RGB in \([0,1]\) from sRGB bytes ÷ 255; optional 4th channel = VC edge (see below).
- **Output:** first binding **`spec`** NCHW `[1, 4, H, W]` — `RGBA` (legacy ONNX may still list five channels; only RGBA is used).
- **Spatial size:** `H` and `W` are **dynamic** (native texture resolution in AutoPBR).

The network is a **dilated FCN** (no max-pool) so odd sizes (e.g. 17×48) do not break skip connections.

## Fourth channel (edge)

Matches `SpecularGenerator.BuildLuminanceAndEdge` with default options: luma `0.3R+0.6G+0.1B`, Sobel, 12-orientation VC sum, **per-image max normalization**. Implemented in `ml_specular/edge_channel.py`.

Use `--in-channels 3` to train RGB-only inputs (no edge channel).

### GPU training vs DeepBump / ONNX Runtime

**DeepBump** in the AutoPBR app uses **Microsoft.ML.OnnxRuntime** with CUDA — that stack does **not** give PyTorch a GPU.

Training here uses **PyTorch**. If you followed the CPU install (`.../whl/cpu`), `torch.cuda.is_available()` is **False** and `--device cuda` falls back to CPU. To train on the GPU, reinstall PyTorch with a CUDA wheel in **this** venv (see [pytorch.org](https://pytorch.org/get-started/locally/)), for example:

```bash
pip uninstall torch torchvision -y
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu124
```

Then run `python -c "import torch; print(torch.cuda.is_available())"` — it should print `True`.

## Quick start (CPU)

Use **Python 3.10+** (avoid legacy `python` → 3.5 on some Windows setups).

```bash
cd tools/MlSpecularTrainer
python3.12 -m venv .venv312
.venv312\Scripts\activate   # Windows
pip install torch torchvision --index-url https://download.pytorch.org/whl/cpu
pip install -r requirements.txt

python -m ml_specular.gen_sample_dataset --out sample_dataset
python -m ml_specular.train_spec --data-root sample_dataset --epochs 30 --batch 4 ^
  --out-onnx artifacts/SpecLab.onnx --ckpt artifacts/SpecLab.pt

python -m ml_specular.verify_spec_onnx artifacts/SpecLab.onnx
```

### Native resolution (optional)

Train at each texture’s real size instead of resizing everything to `--train-res`:

```bash
python -m ml_specular.train_spec --data-root sample_dataset --spatial-mode native --epochs 30 --batch 4 ^
  --out-onnx artifacts/SpecLab.onnx --ckpt artifacts/SpecLab.pt
```

Use **`--max-train-side 512`** and **`--downscale-for-memory box`** (or `lanczos`, `nearest`) when VRAM limits require downscaling large inputs. **`--grad-accum-steps`** accumulates gradients across microbatches (e.g. size-1 batches for 512×512). See **`docs/ml-specular-labpbr-contract.md`** (native resolution section) for tags vs pixel dimensions.

### Batch scaling safety policy (AdamW)

`train_spec` now includes a batch-aware safety policy that can derive optimizer behavior from your selected
effective batch (`batch * grad_accum_steps`):

- `--batch-policy-enabled/--no-batch-policy-enabled` (default enabled)
- `--batch-policy-lr-mode off|sqrt|linear` (default `sqrt`)
- `--batch-policy-baseline-batch` + `--batch-policy-baseline-lr`
- `--batch-policy-max-lr` (optional cap)
- `--warmup-ratio` + `--warmup-min-steps`
- `--weight-decay-mode off|mild_batch_scaled`
- `--grad-clip-norm` (`<=0` disables)
- `--ema-enabled/--no-ema-enabled` + `--ema-decay`

Recommended starting points (guidance, not guarantees):

- `B=64`: `sqrt` LR mode, warmup `5-8%`.
- `B=256`: `sqrt` LR mode, warmup `8-10%`, consider mild weight-decay scaling.
- `B=512`: `sqrt` LR mode, warmup `10%`, enable clipping (`--grad-clip-norm 1.0`) and consider `--batch-policy-max-lr`.

Use `--no-batch-policy-enabled` when you want fully manual LR/warmup/regularization tuning.

## Real datasets

Layout: [`sample_dataset/README.md`](sample_dataset/README.md) — `manifest.jsonl`, diffuse `image` paths, `label_spec` pointing at artist `_s.png`, `splits/train.txt` & `val.txt` (one **id** per line).

### From pre-extracted LabPBR resource packs

Put unpacked packs under `<dataset>/packs/`. Then:

```bash
py -3.12 -m ml_specular.gen_from_labpbr_packs --dataset-root sample_dataset
```

From **cmd.exe** in `tools/MlSpecularTrainer` run `gen_labpbr.cmd --dataset-root sample_dataset`.

Triplet discovery (same roots as the app):

- `assets/*/textures/{block,blocks,item,items,entity,particle}/**/*_s.png`
- `assets/*/optifine/ctm/**/*_s.png`
- `assets/*/optifine/{plant,plants}/**/*_s.png`

Each `_s` requires matching diffuse + `_n`. Manifest lines include `image` + `label_spec`.

### Clean loose PNGs in packs

To drop PNGs that are not part of a diffuse / `_n` / `_s` double-or-triplet pairing (and optional `*_e.png` when `foo.png` exists), the tool deletes **every** `*.png` under each pack’s `assets/` tree that is not part of a kept pairing—including solo PNGs in folders outside the usual texture roots (e.g. gui, misc) and orphan diffuse-only files.

**Default:** cleans **all** dataset folders (`multi_dataset`, `pixelart_dataset`, `realism_dataset`, `stylized_dataset`, `ext_dataset`) under `tools/MlSpecularTrainer/`. Dry-run:

```bash
py -3.12 -m ml_specular.clean_dataset_packs --dry-run
```

Or: `clean_dataset_packs.cmd --dry-run` (no args).

Single folder only: `--dataset-root multi_dataset`.

Remove `--dry-run` to delete. By default, the cleaner also **removes every non-PNG file** under each pack’s `assets/` tree (JSON, `.mcmeta`, sounds, etc.), then prunes empty folders. Pack-root `pack.mcmeta` / `pack.png` are untouched. To **only** remove unpaired PNGs and leave JSON and other assets:

```bash
py -3.12 -m ml_specular.clean_dataset_packs --keep-other-assets
```

### Optional: torch-ort (ORTModule) for PyTorch training

Accelerate the **same** `train_spec` PyTorch loop by wrapping `DilatedPbrNet` with **`torch_ort.ORTModule`**
(ONNX Runtime executes forward/backward). Requires **CUDA**, **`onnxruntime-training`**, **`torch-ort`**, and the
post-install step **`python -m torch_ort.configure`**. See
[`requirements-torch-ort.txt`](requirements-torch-ort.txt) and [Install torch-ort](https://docs.pytorch.org/ort/install.html).

```bash
python -m ml_specular.train_spec --trainer-backend pytorch --device cuda --torch-ort --data-root multi_dataset ...
```

`--torch-ort` **disables native PyTorch AMP** for that run (ORT drives the graph). You can also choose provider with
`--torch-ort-provider cuda|tensorrt` (default `cuda`; TensorRT sets `ORTMODULE_USE_TENSORRT_BACKEND=1` before wrap). With TensorRT, FP16 is **on** by default (`ORT_TENSORRT_FP16_ENABLE=1`); use `--no-torch-ort-tensorrt-fp16` for FP32.
Base PyTorch does **not** expose a `--tensorrt` training backend in this tool; TensorRT here is a torch-ort/ORTModule path.
Resume checkpoints should use the same `--torch-ort` + `--torch-ort-provider` settings (`torch_ort` and
`torch_ort_provider` are stored in run config for strict resume).

## Training throughput (GPU looks idle)

The model is small; **low GPU % is normal** for small manifests. Increase **`--batch`**, **`--width`**, or **`--train-res`** if you want more load.

`train_spec.py` uses DataLoader workers, **`pin_memory`** on CUDA, **`cudnn.benchmark`**, and **AMP** on CUDA when available. Tune with `--workers`, `--no-amp`, `--no-pin-memory`.

## Export ONNX without retraining

```bash
python -m ml_specular.train_spec --export-only --ckpt artifacts/SpecLab.pt --out-onnx artifacts/SpecLab.onnx
```

Architecture (`in_channels`, `out_channels`, `width`) is read from the checkpoint.

## Full training CLI

```bash
python -m ml_specular.train_spec --data-root sample_dataset --epochs 40 --batch 8 ^
  --out-onnx artifacts/SpecLab.onnx --ckpt artifacts/SpecLab.pt
```

`--out-channels` is **4** (RGBA logits).
Use `--transparent-zero-weight` (default `0.5`) to explicitly push predicted RGBA toward zero on transparent diffuse pixels.

### Resume training (PyTorch / Torch-ORT backend)

Full training state (model + optimizer + AMP scaler + epoch + best val) is written **each epoch** to **`--ckpt`** (your main pathed `.pt`). **`--resume-auto`** / **`--resume`** load from **`--ckpt`** by default (same path as in the training menu). A separate **`--best-ckpt`** (default `<ckpt_stem>.best.pt`) stores weights when validation improves; **final ONNX export** uses **`--best-ckpt`** when it exists, otherwise the latest **`--ckpt`**.

Optional **`--resume-ckpt`** is **not** used for resume: if set, each epoch also writes a **duplicate** full state to that path (e.g. backup disk). Legacy runs that used `<stem>.resume.pt` as the only per-epoch file are still supported: if **`--ckpt`** is missing but **`*.resume.pt`** exists, that legacy file is loaded (or, if both exist, the **newer by epoch** is chosen).

```bash
python -m ml_specular.train_spec --data-root multi_dataset --resume-auto
```

- `--resume` = require checkpoint and continue; fail if missing
- `--resume-auto` = continue if found, otherwise start fresh
- `--resume-from <path>` = explicit checkpoint path for resume (overrides `--ckpt` / legacy lookup)
- `--best-ckpt <path>` = best validation weights for export (default `<ckpt_stem>.best.pt`)
- `--resume-ckpt <path>` = optional duplicate write each epoch (backup only)
- `--reset-optimizer` = load weights but reset optimizer/scaler state

By default, strict compatibility checks compare run config and dataset fingerprints. Use
`--resume-allow-config-mismatch` only when you intentionally want a warm-start.

**ORT training backend** (`--trainer-backend ort`) uses **`--ort-checkpoint`** and artifact `checkpoint/` — not the PyTorch `.pt` resume files above.

**Interactive resume (menus):** In **Training Menu.bat** / **`training_menu.sh`**, **[1] Resume training** runs `python -m ml_specular.resume_training_prompt` (add `--torch-ort` from the Torch-ORT submenu). It reads your checkpoint, pre-fills prompts from embedded run config, and separates **match-checkpoint** fields from **safe-to-adjust** ones (epochs, batch, lr, workers, device, paths). **[6] Repeat last session** still replays the saved `.last_training_session.*` command.

### Experimental ORT backend

Install the **training** wheel (in addition to `onnxruntime` from `requirements.txt`). See comment lines in
[`requirements-ort-training.txt`](requirements-ort-training.txt) and
[ORT install: training](https://onnxruntime.ai/docs/install/#install-onnx-runtime-training).

- **Checkpoint bootstrap:** `generate_ort_training_artifacts` writes `<ort-artifacts-dir>/checkpoint/`.
  The first `train_spec --trainer-backend ort` run loads that, then saves session state to
  **`--ort-checkpoint`** (default `<ort-artifacts-dir>/ort_training_state`).
- **Feeds:** `Module.input_names()` is printed at startup; we pass `input`, `target_rgba`, `valid`,
  `transparent_zero_weight` by **name** in graph order.
- **Constructor:** `Module(training_model, state, eval_model, device=...)` per ORT docs.

Use `--trainer-backend ort` to run the experimental ONNX Runtime training path.
It expects ORT training artifacts in `--ort-artifacts-dir` (default `artifacts/ort`):

- **`training_model.onnx`**, **`eval_model.onnx`**, **`optimizer_model.onnx`** — produced by
  `python -m ml_specular.generate_ort_training_artifacts` (default **`--loss spec`** uses custom
  onnxblock `SpecularLabPbrLoss` in `ml_specular/onnxblock_spec_loss.py`, matching PyTorch `spec_loss`).
- **`train_model.onnx`** (optional alternate) — standalone loss export via
  `export_ort_specular_graphs` for parity checks / tooling that needs a single ONNX file.

**Loss graph contract** (matches PyTorch `spec_loss` / `train_spec`):

- `input` — NCHW float32 diffuse (+ optional edge), same as `SpecularManifestDataset`
- `target_rgba` — NCHW `[N,4,H,W]` float32 specular RGBA in \[0,1\]
- `valid` — `[N,H,W]` float32 mask (1 = supervised pixels)
- `transparent_zero_weight` — shape `[1]` float32; same semantics as `--transparent-zero-weight`

Export train/eval loss ONNX (optionally warm-start core from a `.pt` checkpoint):

```bash
python -m ml_specular.export_ort_specular_graphs --out-dir artifacts/ort --ckpt artifacts/SpecLab.pt
```

Forward-only ONNX (for `onnxruntime.training.artifacts`) and optimizer graph generation:

```bash
python -m ml_specular.export_ort_forward_core --out artifacts/ort/forward_model.onnx --ckpt artifacts/SpecLab.pt
python -m ml_specular.generate_ort_training_artifacts --loss spec --out-channels 4 --base-onnx artifacts/ort/forward_model.onnx --artifact-directory artifacts/ort
```

`--out-channels` must match the forward ONNX (four RGBA logits). For troubleshooting you can pass **`--loss l1`** / **`mse`** (built-in enums; not `spec_loss`).

Requires **`onnxruntime-training`** (and a base env with `onnx`) so `onnxblock_spec_loss` can import `onnxruntime.training.onnxblock`.

`train_spec` with `--trainer-backend ort` prefers **`training_model.onnx`** if present; otherwise **`train_model.onnx`**. With **`--loss spec`**, training feeds align with the PyTorch dataset: diffuse **`input`**, **`target_rgba`**, **`valid`**, **`transparent_zero_weight`** (see `train_spec_ort`).

Verify IO names and optional PyTorch vs ONNXRuntime numerical parity:

```bash
python -m ml_specular.verify_ort_specular_training --ort-artifacts-dir artifacts/ort --ckpt artifacts/SpecLab.pt
```

If artifacts or ORT training API are missing, the script exits with actionable errors.

Verify ONNX:

```bash
python -m ml_specular.verify_spec_onnx artifacts/SpecLab.onnx
```

## Ops

- Default ONNX **opset 17** (compatible with Microsoft.ML.OnnxRuntime **1.24.x** used by AutoPBR).
