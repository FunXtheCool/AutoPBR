# Automated tracks — plan workstream status (26.1.2)

**Canonical plan:** [`runtime-ir-preview-plan.md`](../runtime-ir-preview-plan.md)  
**Completion audit:** [`plan-completion-audit.md`](plan-completion-audit.md)  
**Pinned:** 26.1.2 · **Summary UTC:** 2026-05-25 (Wave C geometry partial drain)

Legend: **✅ automated** (CI/tests or committed artifacts) · **⏳ human-only** · **🔧 deferred** (documented backlog; does not block other automated gates)

**Plan “done” blocker:** **only § A.4 Manual Explore** remains ⏳ human-only. All other completion criteria have green automated gates except explicit 🔧 deferrals below.

---

## Part A — Geometry mesh

| Workstream | Status | Notes |
|------------|--------|-------|
| A.1 Lifter (1A–1D) | ✅ automated | 56/56 pilots: `assemblyGatePass`, `javapPoseOracleMatch`, `referenceWorldPoseMatch`, `referenceHierarchyMatch` |
| A.2 Quality gates + quality JSON | ✅ automated | [`geometry-lift-quality-26.1.2.json`](geometry-lift-quality-26.1.2.json); regen via `regen-assembly-pilots.ps1` |
| A.2 Index + shard regen | ✅ automated | [`geometry-index-26.1.2.json`](geometry-index-26.1.2.json) — **157** `ok` · **27** `skipped` · **0** `partial` · **0** `fail` (Wave C: `ArmorStandModel` body-stick override promoted the final partial row) |
| A.3 Pilot dual gate (56) | ✅ automated | **56/56** assembly + oracle + world + hierarchy (`generatedUtc` 2026-05-22T08:38:46Z); **54/56** promotion dual gate (`SheepModel` T1-only; `QuadrupedModel` abstract skip) |
| A.4 Manual Explore | ⏳ human-only | [`manual-explore-playbook.md`](../manual-explore-playbook.md); export `tools/export-manual-explore-checklist.ps1` |
| A.8 Preview-deltas | ✅ automated (artifacts) | 12 overlays committed; Explore sign-off still ⏳ via § A.4 |
| A.9 Entity cuboid codegen | ✅ automated | **20** tables in `GeometryIrEntityCuboidTables.g.cs` (+`PolarBearModel` 2026-05-22); mob wiring in [`mob-ir-parity-backlog.txt`](mob-ir-parity-backlog.txt) |
| Catalog 761 IR paths | ✅ automated | `ParityCatalogMeshDriverKindSurveyTests` — **761** `RuntimeGeometryIrJson`, **0** `cleanRoom`; hand-lift boat/chest/chest-boat |
| Phase 1C cube deformation | 🔧 deferred | Parity emit skips inflate; re-bake reference or viewport-only path — audit Phase 1C |
| `prioritizedBacklogJvmNames` (40) | ✅ automated (tracked) | Flat-nested suspects; manual Explore when on promotion path |

---

## Part B — Animation IR (definition clips)

| Workstream | Status | Notes |
|------------|--------|-------|
| B.1 Pipeline + 16/16 index | ✅ automated | [`animation-index-26.1.2.json`](animation-index-26.1.2.json) all `ok` |
| B.2 Lift / sampler (LINEAR, CATMULLROM) | ✅ automated | `VanillaAnimationIrPreviewSampler`; geometry-IR motion pass |
| B.2.1 1.21.11 animation (10/10) | ✅ automated | `animation/1.21.11/` linked in Core csproj |
| B.3 Primary channel wiring | ✅ automated | Warden/Sniffer/Nautilus/Frog/Creaking/Fox baby/Copper/Camel/Armadillo/Bat/Rabbit/Breeze stacks |
| B.3 depth / IR-gap rows | 🔧 deferred | Warden tendril; Sniffer scent/search setupAnim-only; Breeze SLIDE wind ROTATION; Fox adult absent; Copper chest clips — ~~Creaking invulnerable/death~~, ~~Camel baby hind~~, ~~BabyAxolotl swim~~, ~~Armadillo peek~~ ✅ wired (Wave 11 B.3 depth) |

---

## Part C — SetupAnim IR

| Workstream | Status | Notes |
|------------|--------|-------|
| SetupAnim index 169/169 | ✅ automated | [`setup-anim-index-26.1.2.json`](setup-anim-index-26.1.2.json) |
| No hand `Compute*` mirrors | ✅ automated | `HandParityForbiddenSymbolsTests` |
| `VanillaSetupAnimRuntime` wiring | ✅ automated | Part C final drain 2026-05-21 |

---

## Part D — Renderer state (P6)

| Workstream | Status | Notes |
|------------|--------|-------|
| Hand pilot shards + resolver | ✅ automated (pilots) | Breeze/Sniffer/Allay/Camel/Warden/Frog/Creaking/**Nautilus**/**CopperGolem** |
| `RendererStateLift` bytecode compiler | 🔧 deferred | [`archive/p6-renderer-state-lift-blockers.md`](../archive/p6-renderer-state-lift-blockers.md) |
| `PreviewRenderStateSynthesis` (non-pilots) | ✅ automated (fallback) | Until compiler replaces synthesis |

---

## Plan completion criteria (§ five bullets)

| # | Criterion | Status |
|---|-----------|--------|
| 1 | 56 pilots dual gate + § A.4 Explore | ⏳ human-only (automated gates ✅) |
| 2 | Catalog 761 `RuntimeGeometryIrJson` | ✅ automated |
| 3 | SetupAnim 169/169 + forbidden symbols | ✅ automated |
| 4 | Animation 16/16 + B.3 primary wiring | ✅ automated (+ 🔧 B.3 depth) |
| 5 | P6 compiler (optional) | 🔧 deferred |

---

## Hygiene / optional (not blocking “done”)

| Item | Status |
|------|--------|
| Full `Generate-GeometryIndex.ps1` batch | ✅ automated (184 rows aligned); re-run when new lifts land |
| `AUTOPBR_RUN_ASSEMBLY_VIEWPORT_PROBES=1` | Optional probe env |
| Reference batch JDK 25 freshness | 🔧 deferred (stale `reference-output` possible) |
| Remaining hand adult `Build*` | Temperate `CowModel` uses lifted IR emit (no codegen table); other adults on mob backlog as needed |
