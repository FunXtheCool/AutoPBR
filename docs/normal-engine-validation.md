## Normal/Spec/AO validation pack set (16×–512×)

For ML metal segmentation evaluation and A/B against heuristics, see [Metal ML spec](metal-ml-spec.md).

This repo intentionally does **not** ship texture assets. Instead, use this doc to curate a small local test set and keep a consistent “before/after” comparison workflow.

### Suggested test set

Pick a representative mix of:

- **Hard-edged pixel art (16×)**: stone, planks, bricks, ores, wool
- **Alpha / cutout (16×–64×)**: grass, leaves, vines (to exercise foliage rules)
- **Noisy / high-frequency (32×–128×)**: gravel, sand, cobblestone
- **Smooth gradients (64×–256×)**: concrete, terracotta
- **Hi-res photo-ish (256×–512×)**: any pack that includes clean micro-detail + large-scale shading

Recommended “named” categories (use any resource pack that contains them):

- `stone`, `cobblestone`, `bricks`, `oak_planks`, `sand`, `gravel`
- `iron_ore`, `gold_ore`, `copper_ore`, `diamond_ore`
- `grass_block_top`, `oak_leaves`, `vine`
- `glass` (and other nearly-flat surfaces)

### How to build a local test pack zip

1. Create a folder structure like:

```
testpacks/ValidationPack/
  pack.mcmeta
  assets/minecraft/textures/block/
```

2. Copy the chosen textures into `assets/minecraft/textures/block/`.
3. Zip `ValidationPack/` into `ValidationPack.zip`.

### Test matrix (what to compare)

For each run, capture:

- **Normals**: edge fidelity, ringing, seams, “pillow shading”, unwanted bumps on flat regions
- **Height-in-alpha**: stability (avoid “salt & pepper”), plausible macro shape
- **Specular (_s)**: avoid stretched/noisy smoothness on low-res, metal detection sanity
- **AO (_ao)** (if enabled): cavity emphasis without over-darkening
- **Performance**: conversion time and CPU/GPU utilization (DeepBump vs non-DeepBump)

Suggested runs:

- **Balanced** (defaults)
- **LowRes** profile on a 16×/32× pack (with and without preprocessing)
- **HiRes** profile on a 256×/512× pack (with frequency split enabled)
- **DeepBump** on/off, with **Input mode** Auto vs RGB vs Grayscale (when model supports it)

### Keeping a “before/after” record

- Save outputs into folders like:
  - `out/before/<pack>/<profile>/`
  - `out/after/<pack>/<profile>/`
- For a quick visual diff:
  - compare `_n.png` and `_s.png` side-by-side
  - zoom to 800% for 16× textures, 200% for 64×, and 100% for 256×+

