# Plan completion criteria audit



**Plan:** [`runtime-ir-preview-plan.md`](../runtime-ir-preview-plan.md) § Plan completion criteria  

**Pinned:** 26.1.2 · **Audit UTC:** 2026-05-25 (Wave C geometry partial drain)  

**Quality snapshot:** [`geometry-lift-quality-26.1.2.json`](geometry-lift-quality-26.1.2.json) (`generatedUtc` 2026-05-25T18:14:17Z)  

**Geometry index:** [`geometry-index-26.1.2.json`](geometry-index-26.1.2.json) — **157** `ok` · **27** `skipped` · **0** `partial` · **0** `fail` (Wave C geometry partial drain: `ArmorStandModel` body fingerprint fixed)  

**Automated tracks rollup:** [`automated-tracks-complete.md`](automated-tracks-complete.md)



Legend: **✅ automated** (CI/tests or committed artifacts) · **⏳ human-only** (manual Explore / screenshots) · **🔧 deferred** (explicit backlog; not blocking other tracks)



---



## 1. Geometry — 56 pilots dual gate + § A.4 Manual Explore



| Sub-criterion | Status | Evidence |

|---------------|--------|----------|

| `assemblyGatePass` + `javapPoseOracleMatch` (56 pilots) | ✅ automated | [`geometry-lift-quality-26.1.2.json`](geometry-lift-quality-26.1.2.json); plan § A.3 — **56/56** |

| Promotion dual gate (+ T1 strict on `geometry_ir_partial_to_ok_promotion_jvm.txt`) | ✅ automated | [`geometry_ir_partial_to_ok_promotion_jvm.txt`](../../src/AutoPBR.Core/Data/minecraft-native/geometry_ir_partial_to_ok_promotion_jvm.txt) — **54/56**; `SheepModel` T1-only; `QuadrupedModel` abstract skip |

| T1 viewport strict | ✅ automated | [`geometry_ir_assembly_viewport_strict_jvm.txt`](../../src/AutoPBR.Core/Data/minecraft-native/geometry_ir_assembly_viewport_strict_jvm.txt); `GeometryIrAssemblyViewportSanityTests` |

| § A.4 Manual Explore (screenshot + silhouette) | ⏳ human-only | Plan § A.4 — all canary rows **`pending`**; owner: [`manual-explore-playbook.md`](../manual-explore-playbook.md); export: [`tools/export-manual-explore-checklist.ps1`](../../tools/export-manual-explore-checklist.ps1) (`-Batch 4C-1` …) |



**Overall bullet 1:** ⏳ human-only (automated gates complete; plan not done until Explore sign-off).



---



## 2. Catalog — 761 manifest paths `RuntimeGeometryIrJson`



| Sub-criterion | Status | Evidence |

|---------------|--------|----------|

| No `cleanRoom` fallback on catalogued diffuse paths | ✅ automated | [`ParityCatalogMeshDriverKindSurveyTests.cs`](../../tests/AutoPBR.Core.Tests/ParityCatalogMeshDriverKindSurveyTests.cs) — strict **761** `RuntimeGeometryIrJson`, **0** `cleanRoom` (2026-05-21) |

| Boat / chest / chest-boat block entities | ✅ automated (hand-lift policy) | [`ParityCatalogHandLiftGeometryIrCatalog.cs`](../../src/AutoPBR.Core/Preview/ParityCatalogHandLiftGeometryIrCatalog.cs) + [`GeometryIrParityHandLiftJvmMap.cs`](../../src/AutoPBR.Core/Preview/GeometryIrParityHandLiftJvmMap.cs) — `BoatModel` hull, synthetic `ChestBoatModel`, `ChestModel`; manifest paths **761/761** `RuntimeGeometryIrJson`; geometry index shards for block entities remain `skipped` |

| Adult zombie-villager → `ZombieVillagerModel` | ✅ automated | Manifest `geometry_ir_official_jvm` + lifted `ZombieVillagerModel` shard (`ok`); no `ZombieModel` override |



**Overall bullet 2:** ✅ automated (strict survey + documented hand-lift policy for boat/chest/chest-boat).



---



## 3. SetupAnim — 169/169 `ok`, no hand `Compute*` mirrors



| Sub-criterion | Status | Evidence |

|---------------|--------|----------|

| SetupAnim index all `ok` | ✅ automated | [`setup-anim-index-26.1.2.json`](setup-anim-index-26.1.2.json) — **169/169** `ok`, **0** `partial` |

| No forbidden hand mirrors | ✅ automated | [`HandParityForbiddenSymbolsTests.cs`](../../tests/AutoPBR.Core.Tests/HandParityForbiddenSymbolsTests.cs) |



**Overall bullet 3:** ✅ automated.



---



## 4. Animation (26.1.2) — 16/16 `ok`; B.3 backlog wired or deferred



| Sub-criterion | Status | Evidence |

|---------------|--------|----------|

| Definition index 16/16 `ok` | ✅ automated | [`animation-index-26.1.2.json`](animation-index-26.1.2.json) |

| B.3 channel wiring (major clips) | ✅ automated | Plan § B.3 — Warden/Sniffer/Nautilus/**Frog**/**Creaking**/Fox baby/Copper/Camel/Armadillo/Bat/Rabbit/Breeze stacks wired 2026-05-21; tests `VanillaAnimationIrPreviewSamplerTests`, `GeometryIrDefinitionAnimationPreviewTests` |

| B.3 remaining rows (depth / IR-gap) | 🔧 deferred | Warden **tendril**; Sniffer **scent/search setupAnim-only**; Breeze **SLIDE wind ROTATION** IR-gap; Fox adult absent from 16-set; Copper chest clips — ~~Creaking invulnerable/death~~, ~~Camel baby hind~~, ~~BabyAxolotl swim legs/tail~~, ~~Armadillo peek~~ ✅ wired (Wave 11 B.3 depth, 2026-05-22) |



**Overall bullet 4:** ✅ automated for index + primary wiring; 🔧 deferred for documented B.3 depth rows.



---



## 5. P6 renderer-state compiler (optional for “done”)



| Sub-criterion | Status | Evidence |

|---------------|--------|----------|

| Hand pilot shards + preview resolver | ✅ automated (pilots) | Breeze/Sniffer/Allay/Camel/Warden/Frog/Creaking/**Nautilus**/**CopperGolem** — plan Part D; `RendererState*PreviewTests` |

| Bytecode `RendererStateLift` compiler | 🔧 deferred | [`archive/p6-renderer-state-lift-blockers.md`](../archive/p6-renderer-state-lift-blockers.md) |



**Overall bullet 5:** 🔧 deferred (optional); does not block geometry/animation automated “done” per plan.



---



## Phase 1C — `CubeDeformation` at parity emit (probe)



**Target:** `BabyZombieModel` head cuboid `inflate: 0.25` in [`geometry/26.1.2/net.minecraft.client.model.monster.zombie.BabyZombieModel.json`](geometry/26.1.2/net.minecraft.client.model.monster.zombie.BabyZombieModel.json).



| Finding | Detail |

|---------|--------|

| Lifter | ✅ `inflate` recorded in IR |

| Reference bake | ✅ `referenceCuboidsMatch` / `referenceMeshMatch` **true** with **pre-inflate** `from`/`to` corners |

| Parity emit | Intentionally **skips** corner expansion — [`GeometryIrCuboidMetadata.ApplyCubeDeformationInflateIfNonParity`](../../src/AutoPBR.Core/Preview/GeometryIrCuboidMetadata.cs); [`GeometryIrParityEmitTests.Parity_emit_does_not_apply_cube_deformation_inflate`](../../tests/AutoPBR.Core.Tests/GeometryIrParityEmitTests.cs) |

| Applying inflate at `GeometryIrEmitFidelity.Parity` | Would desync [`MinecraftGeometryReference`](../../tools/MinecraftGeometryReference/) bake and break pilot/reference gates |



**Verdict:** 🔧 **deferred** (Wave 8 / Phase 1C). Viable paths: (a) re-bake reference with post-inflate extents + policy JSON, or (b) viewport-only inflate (`Fidelity != Parity`) — not a &lt;50-line parity-only change.



---



## Summary



| # | Criterion | Overall |

|---|-----------|---------|

| 1 | Geometry dual gate + Manual Explore | ⏳ human-only |

| 2 | Catalog 761 IR paths | ✅ automated |

| 3 | SetupAnim 169/169 + forbidden symbols | ✅ automated |

| 4 | Animation 16/16 + B.3 | ✅ automated (+ 🔧 depth backlog) |

| 5 | P6 compiler | 🔧 deferred (optional) |



**Plan “done”** remains blocked on **§ A.4 Manual Explore** (bullet 1) only. See [`automated-tracks-complete.md`](automated-tracks-complete.md) for per-workstream status. All other bullets have green automated gates except documented deferrals above.

