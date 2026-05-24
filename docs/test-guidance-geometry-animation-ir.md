# Test guidance: geometry & animation IR

Agent reference for writing and changing tests while geometry/animation IR shards and indexes are still being collected. See also [`geometry-ir-conventions.md`](generated/geometry-ir-conventions.md), [`vanilla-preview-parity.md`](vanilla-preview-parity.md), and the canonical Runtime-IR plan [`runtime-ir-preview-plan.md`](runtime-ir-preview-plan.md) (geometry assembly, animation IR, setupAnim, P6 renderer backlog).

## Problem

Committed IR (`docs/generated/geometry/`, `docs/generated/animation/`, indexes) is **best-effort** and moves often (`ok` → richer trees, `partial` → `ok`, placeholders replaced). Tests that lock **incomplete or moving artifacts** fail when data **improves**, not when code breaks — recurring false regressions.

**Rule of thumb:** *Would this test fail if we fixed the shard tomorrow?* If yes, it must not run in default `dotnet test` unless the model is on an explicit pilot allowlist.

## Test tiers

| Tier | Purpose | Default CI (`dotnet test`) | Examples |
|------|---------|----------------------------|----------|
| **T0 — Invariant** | Lifter, parser, schema shape, runtime math | **Always on** | `DualLiftRegressionTests`, `GeometryIrStructuralValidatorTests`, `GeometryIrLiftTreeValidatorTests`, `GeometryIrLiftPolicyTests`, formula/setup asserts (parity G3–G4) |
| **T1 — Pilot contract** | Committed shard + emit/reference for **promoted** models only | **Always on** (allowlist) | `Phase8GeometryIrStrictTests`, `MobFamilyGeometryIrGoldenTests`, `ChickenGeometryShardCleanRoomParityTests` |
| **T2 — Probe** | Load/compare when assets exist; **no assert** if missing or not promoted | **Always on** (must not fail on `partial`) | `GeometryIrReferenceBakeTests` (non-strict pilots), jar reconciliation when `client.jar` absent |
| **T3 — Diagnostic** | Index-wide stats, lift-quality reports, perf | **Opt-in** | `GeometryIrLiftQualityReportTests` index sweep — set `AUTOPBR_RUN_LIFT_QUALITY_INDEX=1` |
| **T2 — UV atlas probe** | Multi-layer atlas false-green (e.g. Breeze wind) | **Always on** (skip if shard not `ok`) | `GeometryIrLiftQualityReportTests.Breeze_ok_shard_fails_uv_within_atlas_and_assembly_gate`, `GeometryIrUvAtlasQualityTests` |

### `extractionStatus` (geometry)

| Status | Default test treatment |
|--------|-------------------------|
| `ok` | Eligible for T1 if on allowlist; parity load via `TryLoadLiftedOkForParity` |
| `partial` / `heuristic` | Schema-only (T0) if needed; **never** strict structural goldens or exact cuboid/reference asserts |
| `skipped` | Index hygiene only |

Parity emit also uses `GeometryIrLiftPolicy` (rejects non-`exact` cuboids except allowlisted `tex_crop_static`).

## Allowlists (source of truth)

Edit these under `src/AutoPBR.Core/Data/minecraft-native/` when **promoting** a model to T1:

| File | Used by |
|------|---------|
| `minecraft_1.21.11_geometry_strict_ok_model_classes.txt` | Phase 8 strict schema + obfuscated jar reconciliation |
| `geometry_ir_mob_family_pilot_jvm.txt` | Mob-family golden / fidelity tests (26.1.2) |
| `geometry_ir_reference_cuboid_strict_jvm.txt` | Java reference ↔ IR cuboid asserts (both version labels) |
| `phase4_strict_ok_model_classes.txt` | Phase 4 / lift promotion targets (class path lines) |
| `geometry_ir_partial_to_ok_promotion_jvm.txt` | Jar lift + optional committed-shard ok check |
| `geometry_ir_assembly_viewport_strict_jvm.txt` | T1 legs-below-head viewport sanity after LER preview basis (`GeometryIrAssemblyViewportSanityTests`) |

**Promotion ritual:** shard reaches `ok` + strict validation (+ reference alignment if applicable) → update allowlist **in the same PR** as the shard → add/adjust T1 tests.

**Pilot 4C dual gate:** For JVMs on `geometry-assembly-parity-pilots-26.1.2.txt`, do **not** add to `geometry_ir_partial_to_ok_promotion_jvm.txt` until **both** `assemblyGatePass` (quality JSON) and T1 viewport strict pass. Viewport strict list may include T1-only probes (e.g. `SheepModel`) without partial→ok promotion. Re-run 4C in one PR: allowlists + shards + quality JSON after viewport fixes.

**Assembly viewport (Phase 5B):** T1 strict cases run only for JVMs on `geometry_ir_assembly_viewport_strict_jvm.txt` (**54** dual-gate pilots + `SheepModel` T1-only; `QuadrupedModel` abstract — not on strict list). T2 loads every row in `docs/generated/geometry-assembly-parity-pilots-26.1.2.txt` when `client.jar` and an `ok` shard exist (smoke: parity mesh builds); legs-below-head assert on quadruped-class pilots only when `AUTOPBR_RUN_ASSEMBLY_VIEWPORT_PROBES=1`. Parity emit via `TryBuildGeometryIrParityMeshForTests` (includes `ApplyLivingEntityRendererPreviewBasis`); invariant is mean leg cuboid centroid Y &lt; head in LER preview space.

## What to avoid

- Asserting on **every** geometry-index row or regenerating index counts (`OkEntryCount > 50`) in default CI.
- Exact JSON/mesh snapshots for models not on a T1 list.
- Encoding **wrong** geometry as expected (fails when lift fixes the shard).
- `Assert.Equal("ok", …)` on committed files without allowlist or without skipping when status is still `partial`.

## What to prefer

- **Lifter vs javap** on pinned `client.jar` (T0) — locks tools, not draft shards.
- **Part-tree probes** (T0, `PartialModelLiftDiagnosticsTests`): `AdultAxolotlModel` / `BabyAxolotlModel` body + hind-leg cuboid ownership; `Lifted_tree_passes_semantic_validator_for_adult_axolotl`; Humanoid/Bat duplicate-cuboid probes (no assert when reference JSON absent).
- **Min cuboid counts** only on T1 pilots (lower bound, not exact snapshot).
- **Strict reference alignment** only for names in `geometry_ir_reference_cuboid_strict_jvm.txt`.
- **Early return** when jar/reference/shard missing (T2), not silent pass on broken code paths.

## Running diagnostic tests

- **Lift-quality baseline:** [`runtime-ir-preview-plan.md`](runtime-ir-preview-plan.md) Part A + unified tiers — regenerate and commit `docs/generated/geometry-lift-quality-26.1.2.json` (command below).

```powershell
$env:AUTOPBR_RUN_LIFT_QUALITY_INDEX = "1"
dotnet test tests/AutoPBR.Core.Tests --filter "FullyQualifiedName~GeometryIrLiftQualityReportTests"

$env:AUTOPBR_WRITE_GEOMETRY_LIFT_QUALITY = "docs/generated/geometry-lift-quality-26.1.2.json"
dotnet test tests/AutoPBR.Core.Tests --filter "Write_quality_report_when_env_set"

$env:AUTOPBR_RUN_ASSEMBLY_VIEWPORT_PROBES = "1"
dotnet test tests/AutoPBR.Core.Tests --filter "FullyQualifiedName~GeometryIrAssemblyViewportSanityTests.T2"
```

Filter xUnit by trait (optional): `Category=Diagnostic`.

## Shared helpers

`tests/Shared/GeometryIrTestTierSupport.cs` — repo root, allowlist loading, diagnostic env gates, committed-shard probes. Linked from Core and GeometryCompiler test projects.

## Animation IR

Same tier model applies to `docs/generated/animation/`:

- **T1:** classes listed in phase animation strict tests / committed `ok` shards with full channels.
- **T2:** incomplete channels documented in [`runtime-ir-preview-plan.md`](runtime-ir-preview-plan.md) Part B — do not add strict golden coverage until IR is complete.
