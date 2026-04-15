# ML specular predictor ↔ LabPBR contract

This document ties together **training** (`tools/MlSpecularTrainer`), **ONNX export**, and **runtime** (`MlSpecularInference` + `SpecularGenerator`) so `_s.png` bytes match what AutoPBR and LabPBR-style packs expect.

## `_s.png` channel semantics (AutoPBR / LabPBR-style)

| Channel | Role | Notes |
|--------|------|--------|
| **R** | Smoothness | Higher ≈ smoother / more reflective (dielectric path). Heuristic code also applies edge- and luminance-based tweaks; ML learns from pack data. |
| **G** | F0 / metal selector | **`G ≤ 229`**: treated as dielectric F0 range (capped in heuristic path). **`G ≥ 230`**: full metal path in `SpecularGenerator` (see `LabPbrF0CapDielectric`). Training targets from real packs should already follow this convention. |
| **B** | Porosity / subsurface | Heuristic path adds `PorosityBias`; ML copies learned `B` from `label_spec`. |
| **A** | Emission | **`255` = no emission** (see `GetSpecularRgba` default). Lower values indicate emissive where rules/packs define it. |

Byte range is always **0–255** per channel in the saved PNG. The model is trained and exported to predict **linear [0, 1]** per channel (then `round(x * 255)` in the app).

## Tensor layout

- **Input**: NCHW `[1, C, H, W]` with `C ∈ {3, 4}`.
  - Channels 0–2: diffuse **R, G, B** as `float32` in **[0, 1]** (`byte / 255`).
  - Channel 3 (if `C == 4`): **edge magnitude** in **[0, 1]**, matching `SpecularGenerator.BuildLuminanceAndEdge` defaults (Sobel + 12-orientation VC sum, **per-image max norm**). Implemented in `ml_specular/edge_channel.py`.
- **Output**: NCHW `[1, 4 or 5, H, W]` (prefer **4**; a fifth channel is ignored at runtime if present in legacy ONNX).
  - `0–3`: **specular R, G, B, A** as **sigmoid(logits)** ∈ **[0, 1]** (must match training; see below).

NHWC layouts are accepted at runtime for both input and output; channel **order** is unchanged (R, G, B, A, …).

## Training vs ONNX vs C# postprocess

1. **Training** (`train_spec.py`): the core `DilatedPbrNet` outputs **logits**; the loss applies **`sigmoid`** to RGBA. Targets are pack `_s` bytes ÷ 255.
2. **ONNX export**: a wrapper applies the **same `sigmoid`** so the graph output is **[0, 1]**, not logits. Without this, `MlSpecularInference` could mis-scale values (it uses a heuristic: values with `|v| ≤ 1.5` are treated as 0–1 and multiplied by 255).
3. **Runtime** (`MlSpecularInference.Postprocess`): for channels 0–3, if magnitudes look like 0–1, multiply by **255** and clamp to bytes. Extra channels beyond RGBA are ignored.
4. **Transparency handling**: training can penalize non-zero output on transparent diffuse pixels (`--transparent-zero-weight`), and runtime can hard-clamp fully transparent diffuse pixels to RGBA `0,0,0,0` (`MlSpecularZeroTransparentPixels`).

## Native resolution training (`--spatial-mode native`)

By default, training uses **`--spatial-mode fixed`** and nearest-neighbor resizes every diffuse + `_s` pair to **`--train-res`** (legacy, one spatial size for all samples).

With **`--spatial-mode native`**, textures keep their **manifest / file** width and height (after an optional **`--max-train-side`** cap). The VC edge channel is built on that same **operational** grid, matching inference at native `H×W`. Batches are formed only from identical `(H, W)` using bucketed batching; use **`--grad-accum-steps`** when large tiles force microbatch size 1.

- **Geometry follows pixels.** Pack folder tags (`[16x]` … `[512x]`) are recorded in the manifest as `tagged_resolution`; if a tag disagrees with `max(width, height)` beyond a relative tolerance, training still uses **actual dimensions** for the tensors, and the mismatch is counted for diagnostics (e.g. UV atlases or entity/armor sheets).
- **Memory cap:** when `max(width, height) > --max-train-side`, both diffuse and spec are scaled down with **`--downscale-for-memory`** (`box`, `lanczos`, or `nearest`). This is independent of fixed-mode **`--train-res`** NN resize.
- **ORT training backend:** experimental `train_spec_ort` only supports **`--spatial-mode fixed`** today.

## Edge channel parity

- Trainer edge: **non-linear** luma `0.3R + 0.6G + 0.1B` on **sRGB bytes** (same as `SpecularGenerator` when `PreprocessLinearize` is **false**).
- If the app enables **`PreprocessLinearize`**, luminance (and thus edge) differs from the trainer. For closest match to training, keep defaults aligned with the training pipeline or train with the same preprocessing if you change it.

## Post-ML processing in `SpecularGenerator`

After ML pixels are written to the specular buffers, AutoPBR still runs the **per-texture R remap** (percentile or min–max stretch into roughly **10–200**) unless **`SpecularDebugSkipSpecularRemap`** is true (or remap is otherwise skipped). That remap was designed for heuristic **R**; it **distorts** raw ML **R** relative to reference LabPBR packs.

- For **closest match to training data / pack `_s`**, enable **`SpecularDebugSkipSpecularRemap`** (debug) or disable remap via future options if you add them.
- Default behavior keeps older heuristic-friendly contrast on **R**.

## Checklist (release)

- [ ] Train with `label_spec` from real LabPBR `_s.png` (same semantics as above).
- [ ] Export ONNX with current `train_spec.py` (includes sigmoid wrapper).
- [ ] `python -m ml_specular.verify_spec_onnx <model.onnx>` passes.
- [ ] App: `MlSpecularUseEdgeChannel` matches exported `C` (3 vs 4).
- [ ] Decide whether to skip **R** remap for ML fidelity (`SpecularDebugSkipSpecularRemap`).
