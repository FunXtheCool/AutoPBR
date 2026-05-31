# Manual Explore playbook (human-only)

**Canonical checklist:** [`runtime-ir-preview-plan.md`](runtime-ir-preview-plan.md) § A.4  
**Automated export:** [`tools/export-manual-explore-checklist.ps1`](../tools/export-manual-explore-checklist.ps1)

Explore 3D screenshot + silhouette sign-off **cannot** be automated or agent-signed-off. CI gates (`assemblyGatePass`, T1 viewport, reference topology) are already green; this playbook is the **owner workflow** for criterion **#1** of the plan completion criteria.

---

## Plan criterion #1 — blocked on human

| Sub-criterion | Status | Owner |
|---------------|--------|-------|
| 56-pilot dual gate + T1 viewport | ✅ Done (2026-05-21) | CI / quality JSON |
| § A.4 Manual Explore (every canary row) | ⏳ **Blocked on human** | **You** — screenshot + table update |

**Done when:** Every § A.4 row’s **Manual Explore** column is no longer `pending` (use `signed off YYYY-MM-DD` or `ok`).

**Owner steps (in order):**

1. Confirm local build matches committed quality JSON (`pwsh -File tools/regen-assembly-pilots.ps1` only if you changed lifter/preview).
2. Run **Creeper canary** (below) before any batch sign-off.
3. Work **one 4C batch at a time** (4C-1 → 4C-5), then partial→ok and probes.
4. For each JVM: open Explore 3D with the § A.4 texture path, pass the screenshot checklist, update the plan table.
5. Re-export pending rows to verify nothing left:  
   `pwsh -File tools/export-manual-explore-checklist.ps1 -Format md`
6. When export shows **zero** pending rows, criterion **#1** is satisfied.

Audit trail: [`generated/plan-completion-audit.md`](generated/plan-completion-audit.md).

---

## Creeper canary (run first, every session)

`CreeperModel` is the regression canary for flat quadruped assembly (LER / floating-torso class). **Do not** sign off a 4C batch until Creeper passes.

| Step | Action |
|------|--------|
| 1 | Explore 3D → parity catalog → `assets/minecraft/textures/entity/creeper/creeper.png` |
| 2 | Confirm **one** connected mob: head, body, four legs — no stacked leg Y on body, no floating torso |
| 3 | Optional: compare to `docs/images/quadruped-*.png` and § quadruped regression (Cow/Panda/PolarBear) if touching LER policy |
| 4 | If Creeper fails, stop batch sign-off; file lifter/preview issue (pattern fix, not per-mob hacks) |

Automated backstop: `GeometryIrPartTreeRepairTests.Creeper_repair_does_not_stack_body_y_onto_leg_origins`, `GeometryIrAssemblyViewportSanityTests`.

---

## Per-batch workflow (4C-1 … 4C-5)

Work batches in order. Each batch shares the same **per-JVM** steps; only the JVM list and textures differ.

### Per-JVM steps (repeat)

1. **Texture** — Use the path from § A.4 (or export table). Paste into Explore asset picker if needed.
2. **Explore 3D** — Parity-catalog preview with `RuntimeGeometryIrJson` (not legacy hand mesh).
3. **Silhouette** — Single connected rig for quadrupeds; no floating torso, head-on-ground, or leg islands.
4. **Variants** — Where § A.4 notes variants (e.g. Cow cold/warm), spot-check at least one variant if paths differ in manifest.
5. **Notes** — Honor row notes (`preview-delta`, `LER via JVM+stem`, `detection-only`).
6. **Sign-off** — In § A.4, change `pending` → `signed off YYYY-MM-DD` (or `ok`).
7. **Screenshot (recommended)** — Save under `docs/images/explore/<ShortModel>-YYYY-MM-DD.png` for regressions.

### Batch 4C-1 (16 JVMs)

**Focus:** Core quadrupeds, camel, equine, hoglin — includes **Creeper**, Cow, Pig, Panda, PolarBear, Hoglin×2, Camel×4, Equine×5.

```powershell
pwsh -File tools/export-manual-explore-checklist.ps1 -Batch 4C-1 -Format md
```

**High-attention textures:** `creeper.png`, `cow.png`, `panda.png`, `polarbear.png`, `hoglin.png`, `camel.png`, `horse_brown.png` / `donkey.png`.

### Batch 4C-2 (8 JVMs)

**Focus:** Wolf, goat, fox families.

```powershell
pwsh -File tools/export-manual-explore-checklist.ps1 -Batch 4C-2 -Format md
```

### Batch 4C-3 (11 JVMs)

**Focus:** Armadillo, turtle, baby cow/panda/polar/sheep, `SheepFurModel`.

```powershell
pwsh -File tools/export-manual-explore-checklist.ps1 -Batch 4C-3 -Format md
```

### Batch 4C-4 (11 JVMs)

**Focus:** Axolotl, sniffer, rabbit, llama, ocelot.

```powershell
pwsh -File tools/export-manual-explore-checklist.ps1 -Batch 4C-4 -Format md
```

### Batch 4C-5 (4 JVMs on 4C list)

**Focus:** Hostile + nested babies — `EnderDragonModel`, `RavagerModel`, `BabyDonkeyModel`, `BabyFelineModel`.

```powershell
pwsh -File tools/export-manual-explore-checklist.ps1 -Batch 4C-5 -Format md
```

### After 4C — partial→ok, T1 probe, detection

| Batch ID | JVMs | Notes |
|----------|------|-------|
| `partial-5` | `AdultCatModel`, `BabyCatModel`, `AdultFelineModel` | partial→ok promotion, not 4C dual-gate |
| `T1-probe` | `SheepModel` | T1 strict probe only |
| `detection` | `BreezeModel` | Wind rods; assembly/UV gates only — not 56-pilot 4C |

```powershell
pwsh -File tools/export-manual-explore-checklist.ps1 -Batch partial-5 -Format md
pwsh -File tools/export-manual-explore-checklist.ps1 -Batch T1-probe -Format md
pwsh -File tools/export-manual-explore-checklist.ps1 -Batch detection -Format md
```

---

## Screenshot checklist

Use for every signed-off JVM (minimum bar):

- [ ] Full-body default pose visible (idle / walk phase acceptable)
- [ ] No part separated by > ~1 body width from the rest (quadruped rule)
- [ ] Head attached to body (not on ground plane alone)
- [ ] Legs on same visual “ground” band as body
- [ ] UV/atlas looks sane (no smeared wind rods on wrong mobs)
- [ ] If row cites **preview-delta** or touches **LER / `GeometryIrMeshWalk`**: re-run canaries (Creeper, Cow, Pig) and any mob you changed — see plan § **column-root LER** (do not restore `M×S` right-compose on catalog emit)
- [ ] File named and stored if used for regression (`docs/images/explore/…`)

**Known regression references (re-check after LER / walk-order changes):**

| Mob | Reference image | Notes |
|-----|-----------------|-------|
| Creeper | — | **Canary** — flat quadruped + rotated-less body |
| Cow / Pig / Panda | `docs/images/quadruped-*.png` | Torso must stay attached (column-root LER) |
| Chicken | Explore capture | Wings/legs often OK; **body Z displacement** may remain (open) |
| Polar bear | capture when available | Origin tests may still diverge — separate from LER class |

---

## Export script usage

From repo root:

```powershell
# All pending rows → CSV + markdown under docs/generated/
pwsh -File tools/export-manual-explore-checklist.ps1

# Single batch only (markdown table)
pwsh -File tools/export-manual-explore-checklist.ps1 -Batch 4C-3 -Format md

# CSV only
pwsh -File tools/export-manual-explore-checklist.ps1 -Batch 4C-1 -Format csv
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-PlanPath` | `docs/runtime-ir-preview-plan.md` | Source § A.4 table |
| `-OutDir` | `docs/generated` | Output directory |
| `-Format` | `both` | `csv`, `md`, or `both` |
| `-Batch` | *(empty)* | Filter: `4C-1` … `4C-5`, `partial-5`, `T1-probe`, `detection` |

Output files: `docs/generated/manual-explore-checklist-pending-<date>.{csv,md}`.

The script **does not** update the plan table — edit § A.4 manually after sign-off.

---

## What agents must not do

- Mark § A.4 **Manual Explore** as signed off without human screenshots
- Promote pilots on gates alone while § A.4 is `pending`
- Reparent flat quadruped legs under `body` to “fix” Explore (Creeper regression)

---

## Related docs

- Plan § A.4 table and batch counts: [`runtime-ir-preview-plan.md`](runtime-ir-preview-plan.md)
- Quadruped LER regression: same plan, “Quadruped body placement regression”
- Completion audit: [`generated/plan-completion-audit.md`](generated/plan-completion-audit.md)
