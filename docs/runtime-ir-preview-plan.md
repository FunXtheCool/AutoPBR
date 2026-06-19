# Runtime-IR preview plan (geometry + animation + setupAnim)

**Status:** Active canonical plan — automated tracks complete except human Manual Explore  
**Pinned version:** 26.1.2 (`tools/minecraft-parity/26.1.2/client.jar`)  
**Assembly pilots:** 56 JVMs ([`geometry-assembly-parity-pilots-26.1.2.txt`](generated/geometry-assembly-parity-pilots-26.1.2.txt))  
**Quality snapshot:** [`geometry-lift-quality-26.1.2.json`](generated/geometry-lift-quality-26.1.2.json) — `generatedUtc` 2026-05-26T02:32:22Z · **Completion audit:** [`plan-completion-audit.md`](generated/plan-completion-audit.md) · **Automated tracks:** [`automated-tracks-complete.md`](generated/automated-tracks-complete.md)  
**Related:** [`test-guidance-geometry-animation-ir.md`](test-guidance-geometry-animation-ir.md) (tiers), [`generated/geometry-ir-conventions.md`](generated/geometry-ir-conventions.md), [`vanilla-preview-parity.md`](vanilla-preview-parity.md), [`cleanroom-entity-cuboid.md`](cleanroom-entity-cuboid.md)  
**Superseded plans:** [`archive/README.md`](archive/README.md) (historical roadmaps only — do not edit for new work)

---

## Core principle (read first)

**Runtime-IR is the source of truth.** Lifted geometry shards, animation definition shards, and setupAnim shards — produced by **GeometryCompiler** and **AnimationCompiler** — define what preview must show.

Preview-only layers are **temporary guardrails**, not substitutes for lifter fixes:

| Guardrail | Must not replace |
|-----------|------------------|
| LER / living-entity renderer preview basis | Wrong `PartPose` kind or flat part tree in geometry IR |
| `GeometryIrPartTreeRepair` / reference pose sync | `addOrReplaceChild` recovery in mesh lift |
| Clean-room `Build*` templates | Factory bytecode poses (`offset` vs `offsetAndRotation`) |
| `preview-deltas/*.json` | Lifted cuboids, hierarchy, extraction notes |
| `PreviewRenderStateSynthesis` | Renderer-state lift (P6 — future) |
| Hand-tuned `Compute*` / `*VanillaKeyframes` | `VanillaSetupAnimRuntime` + lifted setupAnim IR |

**Separate schemas:** **Geometry IR** (static body layer), **animation IR** (clinit `AnimationDefinition` keyframes), **setupAnim IR** (procedural `setupAnim` + `playbackSteps`). setupAnim drives procedural pose; animation IR drives definition clips; geometry IR supplies rest mesh.

**Animation IR is separate from geometry assembly work** — do not block mesh pilot promotion on animation sampler gaps, and vice versa.

---

## North star

Explore 3D and CleanRoom parity preview match **vanilla-as-lifted**: correct part hierarchy, factory-accurate rest poses, sampled definition clips where IR is complete, and setupAnim evaluation against a honest render-state bag. Promotion uses **allowlists + quality gates**, not preview hacks.

## Current status (2026-05-26 audit)

**Automated geometry / animation / setupAnim work is complete for this plan.** The remaining plan-done blocker is **§ A.4 Manual Explore**: a human must open the listed parity-catalog previews, refresh/compare screenshots, and mark each row signed off.

**Deferred / optional work remains but does not block the plan:**

- **P6 renderer-state compiler:** hand renderer-state pilot shards + resolver are landed; bytecode `RendererStateLift` stays deferred behind [`archive/p6-renderer-state-lift-blockers.md`](archive/p6-renderer-state-lift-blockers.md).
- **Phase 1C cube deformation:** `inflate` is lifted but parity emit intentionally skips corner expansion until reference bake / viewport policy changes.
- **Reference-output freshness:** JDK 25 reference batches may be stale; re-bake only when validating reference artifacts.
- **Catalog/CleanRoom hygiene:** catalog manifest paths are strict IR; remaining hand `Build*` work is optional CleanRoom cleanup (`BuildQuadruped` legacy path).
- **Regeneration hygiene:** re-run full geometry/setup/animation indexes when new lifts land; current committed geometry index has no `partial` rows.

---

## Architecture (who owns what)

```
client.jar
    │
    ├─► GeometryCompiler ──► geometry/<ver>/*.json ──► quality gates ──► parity emit ──► Explore 3D mesh
    │
    ├─► AnimationCompiler (clinit) ──► animation/<ver>/*Animation.json ──► VanillaAnimationIrPreviewSampler
    │
    ├─► AnimationCompiler (setupAnim) ──► setup-anim/<ver>/*.json ──► VanillaSetupAnimRuntime
    │                                      ▲
    │                                      └── PreviewRenderStateSynthesis (timing only; P6 replaces)
    │
    └─► MinecraftGeometryReference (JDK 25) ──► reference-output/ (cuboid + worldPose bake + optional JVM render affines)
```

| Layer | Owns | Does not own |
|-------|------|----------------|
| **Lifter** | Part tree, cuboids, poses, setupAnim AST, clinit keyframes | Viewport thicken, LER basis |
| **Quality JSON** | `assemblyGatePass`, `javapPoseOracleMatch`, `reference*Match`, backlog | Manual Explore sign-off |
| **Parity emit** | `GeometryIrMeshEmitter`, catalog `Build*` dispatch | Animation channel interpolation policy |
| **Preview guardrails** | No-reparent policy, preview-deltas, synthesis | Permanent pose truth |

Hand-catalog for per-mob `Build*` / renderer javap: exists separately in [`vanilla-preview-parity.md`](vanilla-preview-parity.md) (not duplicated here).

---

## Live metrics (26.1.2, committed artifacts)

| Artifact | Count / metric |
|----------|----------------|
| Geometry index rows | **184** ([`geometry-index-26.1.2.json`](generated/geometry-index-26.1.2.json)) |
| Index `extractionStatus` (2026-05-25/26 Wave C partial drain) | **157** `ok` · **27** `skipped` · **0** `partial` · **0** `fail` |
| Quality report `ok` rows | **157** |
| `prioritizedBacklogJvmNames` | **60** (flat-nested / composed-preview suspects; tracked for Explore/promotion context — see appendix) |
| **Pilot** `assemblyGatePass` | **56 / 56** pass |
| **Pilot** `javapPoseOracleMatch` | **56 / 56** pass (2026-05-21: same-class `createBodyMesh`/`createBase*` + `createLegs` delegate merge) |
| **Pilot dual gate** (`assemblyGatePass` ∧ `javapPoseOracleMatch`) | **56 / 56** (2026-05-21: camel host/saddle + pilot UV atlas gates green after UV batch) |
| **Pilot promotion dual gate** (+ T1 viewport strict on `geometry_ir_partial_to_ok_promotion_jvm.txt`) | **54 / 56** (batch 1 ×16 + batch 2 ×8 + batch 3 ×11 + batch 4 ×11 + batch 5 ×8; `SheepModel` T1 probe only; `QuadrupedModel` abstract — skip 4C) |
| Pilot `referenceWorldPoseMatch` false | **0** |
| Pilot `referenceHierarchyMatch` false | **0** (Phase 2B composed-flat + topology align) |
| Pilots with `suspectedFlatNestedPartCount > 0` | **39 / 56** (`flatCount: 4`; **17** nested-body at **0** — see Phase 1A; post–`SkipLift` regen 2026-05-21) |
| Animation definition index | **16** files, all **`ok`** |
| SetupAnim index | **169** rows — **169** `ok`, **0** `partial` (2026-05-21 Part C final drain: inheritance/effect-only hosts, slime renderer stub, array mob completions, object/effect shells) |

**Allowlists (geometry):** `geometry_ir_partial_to_ok_promotion_jvm.txt` **75** · viewport strict **55** (54 dual-gate + `SheepModel` T1-only) · cuboid strict **116** · mob-family pilot **9**

Regenerate pilots: `pwsh -File tools/regen-assembly-pilots.ps1` (not full 184-class index).

---

# Part A — Geometry mesh (Runtime-IR lift & preview)

## A.1 Current problem class

Legacy `referenceCuboidsMatch` / `referencePosesMatch` / `referenceMeshMatch` validate **local** IR ↔ baked `reference_java` agreement. They can stay green while Explore shows wrong assembly (flat root siblings, wrong pose kind, repair-induced world drift). **CreeperModel** is the regression canary; fixes are **pattern/family** lifter work across the 56-pilot set.

## A.2 Workstreams (priority order)

1. **Lifter (1A–1D)** — `JavapFloatGeometryMeshLift.cs`, `BytecodeMeshResolution.cs`  
   - **1A:** `addOrReplaceChild` / hierarchy (nested vs flat vs binding_gap — see appendix)  
   - **1B:** `PartPose.offset` vs `offsetAndRotation` per javap oracle  
   - **1C:** `CubeDeformation` / inflate (optional)  
   - **1D:** Mesh host / delegation resolution  

2. **Quality gates** — `GeometryIrLiftQualityReport.cs`: `referenceWorldPoseMatch`, `referenceHierarchyMatch`, `assemblyGatePass`, `javapPoseOracleMatch`, `uvWithinAtlasMatch`, `layerAtlasConsistent`, `extractionBindingGap` (`GeometryIrUvAtlasQuality` — cuboid unfolded UV vs shard/per-cuboid atlas; catches multi-`LayerDefinition` merges like **BreezeModel** wind rods on a 32×32 shard)

3. **Shard regen + index** — `Generate-GeometryIndex.ps1`; pilot batch via `regen-assembly-pilots.ps1`

4. **Reference bake (JDK 25)** — `Export-GeometryReference.ps1` → `reference-output/`; batch pilots with `-ModelsFromFile`

5. **Preview guardrails (only)** — `GeometryIrPartTreeRepair` no-reparent for vanilla flat quadruped; LER basis on parity emit; small `preview-deltas/` set

6. **Promotion ritual** — dual gate for pilots: `assemblyGatePass` + T1 viewport; one PR: allowlists + shards + quality JSON

7. **Manual Explore** — canary set (§ A.4)

8. **Preview-deltas backlog** — pilot-wide overlays where interpretation still diverges

9. **Entity cuboid codegen** — `EntityCuboid` + `GeometryIrEntityCuboidTables.g.cs`; IR-first `Build*` paths per [`cleanroom-entity-cuboid.md`](cleanroom-entity-cuboid.md); mob mesh wiring in [`generated/mob-ir-parity-backlog.txt`](generated/mob-ir-parity-backlog.txt) (BuildBaby* / geometry IR only)

## A.3 Pilot gate tables (short names)

**`assemblyGatePass: false` (0):** — (DonkeyModel **1B** fixed 2026-05-20: MeshTransformer `modifyMesh` ear duplicates merged with delegate `AbstractEquineModel.createBodyMesh` `PartPose.ZERO` bindings)

**`javapPoseOracleMatch: false` (0):** — (2026-05-21: Cow/Pig quadruped delegate + camel `createBodyMesh` host scope)

**Oracle parser (2026-05-21, 2C):** `GeometryJavapPoseOracle` — same-class unprefixed `invokestatic` (`createBodyMesh`, `createBaseCowModel`), nested cross-class delegate merge (`QuadrupedModel.createBodyMesh` → `createLegs`), `factoryMethod` fallback when layer slice empty, mesh depth from invoke site. **Tests:** `GeometryJavapPoseOracleTests` (`Delegate_quadruped_and_camel_pilots_resolve_context_oracle`). Re-score: `pwsh -File tools/regen-assembly-pilots.ps1 -SkipLift`.

**Phase 2B (landed):** `referenceHierarchyMatch` **56/56** — composed-flat policy when IR has `UsesVanillaFlatQuadrupedLegBake` at root but `reference_java` nests legs; `GeometryIrReferenceTopologyAlign` + pose sync for `referenceWorldPoseMatch` **56/56**.

**Phase 1A (2026-05-20, nested javap):** `AttachLiftedPartToForest` only forces flat quadruped legs when `addOrReplaceChild` receiver is **mesh root** (`IsMeshRootReceiverSlot`), not whenever the part id contains `leg`. Pilots whose javap binds legs on a **body** `PartDefinition` slot (axolotl×2, rabbit×3, `BabyDonkeyModel`, sniffer×2, `EnderDragonModel`, llama×2, wolf×2, …) lift legs under `body` with `suspectedFlatNestedPartCount: 0` — **18 / 56** (2026-05-21 quality JSON). Composed-flat factories (cow, creeper, `AbstractEquineModel`, camel×4, …) keep root sibling legs (`flatCount: 4`) matching bytecode (**38 / 56** still `> 0`). **Tests:** `NestedHierarchyMeshLiftTests`, `QuadrupedMeshLiftTests` (Creeper canary); regen: `pwsh -File tools/regen-assembly-pilots.ps1`.

**Hierarchy class (56 pilots):** flat **32** · nested **17** · binding_gap **7** — full table: [`archive/phase-1a-pilot-hierarchy-expectations.md`](archive/phase-1a-pilot-hierarchy-expectations.md)

### A.3.1 Pilot 4C expansion scan (2026-05-21)

**Scope:** **2 / 56** pilots outside **promotion dual gate** (`SheepModel` T1-only; `QuadrupedModel` abstract). **54** on promotion ∩ strict with assembly + T1 strict (batch 5 landed 2026-05-21). **Do not** batch-edit allowlists until per-JVM T1 strict passes in CI with `AUTOPBR_RUN_ASSEMBLY_VIEWPORT_PROBES=1`.

| Gate | Not-on-4C count | Notes |
|------|-----------------|-------|
| `assemblyGatePass` ∧ `javapPoseOracleMatch` | **40 / 40** | All non-4C pilots green in committed quality JSON |
| T2 viewport (`T2_assembly_pilot_quadruped_viewport_probe`, probes on) | **40 / 40** quadruped probe | **Fixed 2026-05-21:** feline JVM LER gate (`UsesFlatPartPoseOffsetQuadrupedJvm` + stem `feline` exclusion) |
| T1 strict today | **1 / 40** | `SheepModel` probe only (not partial→ok) |

**Optional next strict + partial→ok batches** (dual gate + T2 pass; one PR per family + Explore row):

1. ~~**Camel family (4)**~~ — **landed 4C batch 1 (2026-05-21).**
2. ~~**Equine (5)**~~ — **landed 4C batch 1 (2026-05-21).** (`BabyDonkeyModel` landed batch 5.)
3. ~~**Wolf / goat / fox (8)**~~ — **landed 4C batch 2 (2026-05-21).**
4. ~~**Armadillo + turtle + babies (11)**~~ — **landed 4C batch 3 (2026-05-21):** armadillo×3, turtle×3, `BabyCowModel`, `BabyPandaModel`, `BabyPolarBearModel`, `BabySheepModel`, `SheepFurModel`.
5. ~~**Defer 4C until viewport LER:** `AbstractFelineModel`, `AdultFelineModel`.~~ **Landed (2026-05-21):** T1/T2 viewport green; feline×4 on dual gate (batch 4–5).
6. ~~**Entity-wide pack (11):** axolotl×2, sniffer×2, rabbit×3, llama×2, ocelot×2~~ — **landed 4C batch 4 (2026-05-21).**
7. ~~**Hostile + nested babies (4):** `EnderDragonModel`, `RavagerModel`, `BabyDonkeyModel`, `BabyFelineModel`~~ — **landed 4C batch 5 (2026-05-21).** Remaining: `QuadrupedModel` (abstract, skip 4C); `SheepModel` (T1 probe only).

**Feline×4** (`AbstractFelineModel`, `AdultCatModel`, `AdultFelineModel`, `BabyCatModel`) — **dual gate** batch 4–5 (2026-05-21); T1/T2 viewport green.

**Rig-accuracy family backlogs:** [`generated/rig-accuracy-batches/`](generated/rig-accuracy-batches/) (`quadruped`, `equine`, `humanoid`, `hostile`, `flying`, `aquatic`)

## A.4 Manual Explore checklist (canary)

**Human-only (cannot auto sign-off):** Agents and CI must **not** mark **Manual Explore** as done. Owner workflow: [`manual-explore-playbook.md`](manual-explore-playbook.md) (per-batch 4C-1…5, Creeper canary, screenshot checklist). Plan completion criterion **#1** is **blocked on human** until every row below leaves `pending`.

**Automated gates (current quality snapshot):** For rows below, committed quality JSON has **`assemblyGatePass` ✓**, **`javapPoseOracleMatch` ✓**, **`referenceWorldPoseMatch` ✓**, **`referenceHierarchyMatch` ✓**; T1 viewport **`GeometryIrAssemblyViewportSanityTests`** green for all **55** JVMs on `geometry_ir_assembly_viewport_strict_jvm.txt` (**54** dual-gate + `SheepModel` T1-only). **Remaining work is manual Explore only** (screenshot + silhouette).

**Manual checklist (per JVM):** (1) Open Explore 3D parity-catalog preview with texture below. (2) Confirm single connected quadruped silhouette (no floating torso / head-on-ground / leg islands). (3) Optional: compare to `docs/images/quadruped-*.png` where listed. (4) Mark sign-off in this table.

**Export pending rows:** `pwsh -File tools/export-manual-explore-checklist.ps1` (optional `-Batch 4C-3 -Format md` for one batch).

| JVM | automated_prereq | Auto gates | Viewport T1 | Texture (Explore) | Manual Explore |
|-----|------------------|------------|-------------|-------------------|----------------|
| CreeperModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/creeper/creeper.png` | pending |
| CowModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/cow/cow.png` (+ cold/warm variants) | pending |
| PigModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/pig/pig.png` | pending |
| SheepModel | ✓ | ✓ all | **T1 strict** (probe only) | `assets/minecraft/textures/entity/sheep/sheep.png` | pending |
| PandaModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/panda/panda.png` | pending — preview-delta committed (cow-class LER, § A.8) |
| PolarBearModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/bear/polarbear.png` | pending — preview-delta committed (cow-class LER, § A.8) |
| HoglinModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/hoglin/hoglin.png` | pending — default LER (not cow-class) |
| BabyHoglinModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/hoglin/hoglin.png` | pending |
| AdultCamelModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/camel/camel.png` | pending |
| BabyCamelModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/camel/camel.png` | pending |
| CamelModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/camel/camel.png` | pending |
| CamelSaddleModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/camel/camel.png` | pending |
| AbstractEquineModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/horse/horse_brown.png` (representative) | pending |
| BabyHorseModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/horse/horse_brown.png` | pending |
| DonkeyModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/horse/donkey.png` | pending |
| EquineSaddleModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/horse/horse_brown.png` | pending |
| HorseModel | ✓ | ✓ all | **T1 strict** (4C batch 1) | `assets/minecraft/textures/entity/horse/horse_brown.png` | pending |
| WolfModel | ✓ | ✓ all | **T1 strict** (4C batch 2) | `assets/minecraft/textures/entity/wolf/wolf.png` | pending |
| AdultWolfModel | ✓ | ✓ all | **T1 strict** (4C batch 2) | `assets/minecraft/textures/entity/wolf/wolf.png` | pending |
| BabyWolfModel | ✓ | ✓ all | **T1 strict** (4C batch 2) | `assets/minecraft/textures/entity/wolf/wolf.png` | pending |
| GoatModel | ✓ | ✓ all | **T1 strict** (4C batch 2) | `assets/minecraft/textures/entity/goat/goat.png` | pending |
| BabyGoatModel | ✓ | ✓ all | **T1 strict** (4C batch 2) | `assets/minecraft/textures/entity/goat/goat.png` | pending |
| FoxModel | ✓ | ✓ all | **T1 strict** (4C batch 2) | `assets/minecraft/textures/entity/fox/fox.png` | pending |
| AdultFoxModel | ✓ | ✓ all | **T1 strict** (4C batch 2) | `assets/minecraft/textures/entity/fox/fox.png` | pending |
| BabyFoxModel | ✓ | ✓ all | **T1 strict** (4C batch 2) | `assets/minecraft/textures/entity/fox/fox.png` | pending |
| AdultAxolotlModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/axolotl/axolotl_cyan.png` | pending |
| BabyAxolotlModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/axolotl/axolotl_cyan.png` | pending |
| SnifferModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/sniffer/sniffer.png` | pending |
| SniffletModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/sniffer/sniffer.png` | pending |
| AdultRabbitModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/rabbit/brown.png` | pending |
| BabyRabbitModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/rabbit/brown.png` | pending |
| RabbitModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/rabbit/brown.png` | pending |
| LlamaModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/llama/llama.png` | pending |
| BabyLlamaModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/llama/llama.png` | pending |
| AdultOcelotModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/cat/ocelot.png` | pending |
| BabyOcelotModel | ✓ | ✓ all | **T1 strict** (4C batch 4) | `assets/minecraft/textures/entity/cat/ocelot.png` | pending |
| AdultCatModel | ✓ | ✓ all | **T1 strict** (partial→ok batch 5; not 4C) | `assets/minecraft/textures/entity/cat/cat_tabby.png` | pending |
| BabyCatModel | ✓ | ✓ all | **T1 strict** (partial→ok batch 5; not 4C) | `assets/minecraft/textures/entity/cat/cat_tabby.png` | pending |
| BreezeModel | ✓ | ✓ assembly + UV | T2 / quality JSON | `assets/minecraft/textures/entity/breeze/breeze.png` | detection-only — wind rods; not 56-pilot 4C |
| AdultArmadilloModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/armadillo/armadillo.png` | pending |
| ArmadilloModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/armadillo/armadillo.png` | pending |
| BabyArmadilloModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/armadillo/armadillo.png` | pending |
| AdultTurtleModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/turtle/turtle.png` | pending |
| BabyTurtleModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/turtle/turtle.png` | pending |
| TurtleModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/turtle/turtle.png` | pending |
| BabyCowModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/cow/cow.png` | pending |
| BabyPandaModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/panda/panda.png` | pending — LER via JVM+stem gate (no overlay; adult § A.8) |
| BabyPolarBearModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/bear/polarbear.png` | pending — LER via JVM+stem gate + `geometry_ir_official_jvm_baby` (no overlay) |
| BabySheepModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/sheep/sheep.png` | pending |
| SheepFurModel | ✓ | ✓ all | **T1 strict** (4C batch 3) | `assets/minecraft/textures/entity/sheep/sheep.png` | pending |
| EnderDragonModel | ✓ | ✓ all | **T1 strict** (4C batch 5) | `assets/minecraft/textures/entity/enderdragon/dragon.png` | pending |
| RavagerModel | ✓ | ✓ all | **T1 strict** (4C batch 5) | `assets/minecraft/textures/entity/illager/ravager.png` | pending |
| BabyDonkeyModel | ✓ | ✓ all | **T1 strict** (4C batch 5) | `assets/minecraft/textures/entity/horse/donkey.png` | pending |
| BabyFelineModel | ✓ | ✓ all | **T1 strict** (4C batch 5) | `assets/minecraft/textures/entity/cat/cat_tabby.png` | pending |
| **AdultFelineModel** | ✓ | ✓ assembly/oracle | **T1 strict** (partial→ok batch 5; not 4C) | `assets/minecraft/textures/entity/cat/cat_tabby.png` (host) | pending |

**54 pilot 4C dual-gate** (`geometry_ir_partial_to_ok_promotion_jvm.txt` ∩ 56 pilots): batch 1 (**16**); batch 2 (**8**); batch 3 (**11**); batch 4 (**11**); batch 5 — `EnderDragonModel`, `RavagerModel`, `BabyDonkeyModel`, `BabyFelineModel`, feline×4 (**8**). All automated gates ✓; **all 54 need manual Explore** above (plus T1-only: `SheepModel`).

Flat quadrupeds and most catalog mobs use **column-root LER** (`ApplyLivingEntityRendererColumnRootScale`) after model-space emit — see **§ Quadruped body placement regression**. Re-run Explore after any change to `GeometryIrMeshWalk` or LER helpers.

## A.5 Geometry commands

```powershell
# Pilot regen (56 JVMs + quality + optional reference)
pwsh -File tools/regen-assembly-pilots.ps1
pwsh -File tools/regen-assembly-pilots.ps1 -KeepRevert -JavaHome $env:USERPROFILE\.autopbr\jdk-25

# Lift-quality report
$env:AUTOPBR_WRITE_GEOMETRY_LIFT_QUALITY = "docs/generated/geometry-lift-quality-26.1.2.json"
dotnet test tests/AutoPBR.Core.Tests --filter "Write_quality_report_when_env_set"

# Reference export (JDK 25+)
pwsh -File tools/Export-GeometryReference.ps1 -ModelsFromFile docs/generated/geometry-assembly-parity-pilots-26.1.2.txt

# Full geometry index (184 classes)
pwsh -File tools/Generate-GeometryIndex.ps1 -ClientJar tools/minecraft-parity/26.1.2/client.jar -VersionLabel 26.1.2
```

## A.8 Preview-deltas (committed overlays)

**Path:** [`docs/generated/preview-deltas/26.1.2/`](generated/preview-deltas/26.1.2/) (linked into `Data/minecraft-native/preview-deltas/` via `AutoPBR.Core.csproj`). **Schema:** [`generated/schema/preview-delta.schema.json`](generated/schema/preview-delta.schema.json).

Document **preview-only** interpretation gaps (LER basis, idle channels, composed-flat notes) — not shard pose truth.

| JVM | Overlay | Notes |
|-----|---------|-------|
| CowModel | ✓ | LER basis + idle channel note |
| PigModel | ✓ | cow-class LER |
| CreeperModel | ✓ | canary; flat quadruped + cow-class LER |
| ChickenModel | ✓ | |
| BlazeModel | ✓ | |
| BatModel | ✓ | |
| CodModel / SalmonModel | ✓ | fish pilots |
| HoglinModel / BabyHoglinModel | ✓ | **default** LER (not cow-class) |
| **PandaModel** | ✓ (2026-05-21) | cow-class `LocalToParent × S`; composed-flat `flatCount: 4` |
| **PolarBearModel** | ✓ (2026-05-21) | same policy as Panda/Cow adults |
| BabyPanda / BabyPolarBear | — | cow-class LER via runtime JVM+stem gate; no separate JSON unless Explore diverges |

**Wave 6 / Explore:** Add overlays only when manual T1 sign-off still diverges after lifter + LER policy. Panda/polar adults landed with quadruped regression fix (§ quadruped body placement).

---

# Part B — Animation IR (definition clips)

## B.1 Pipeline

- **Lift:** `AnimationClinitLift.cs` in **AutoPBR.Tools.AnimationCompiler** (not GeometryCompiler)  
- **Shards:** `docs/generated/animation/26.1.2/*.json` → `Data/minecraft-native/animation/`  
- **Index:** `animation-index-26.1.2.json` (**16** definition classes, all `ok` on 26.1.2)  
- **Runtime:** `VanillaAnimationIrPreviewSampler`, `DefinitionAnimationPreviewSampling`  
- **Geometry IR motion pass:** `TryApplyDefinitionAnimationGeometryIrPreviewPass` after catalog emit  

**Sampler:** LINEAR and CATMULLROM (and per-segment MIXED) supported; channels with empty keyframes or unsupported interpolation are skipped until lift improves.

## B.2 Supported vs deferred (lift)

| Supported | Deferred / difficult |
|-----------|---------------------|
| `withLength` + `putstatic` definitions | Keyframes only via non-inline helpers |
| `addAnimation` builder + inline `AnimationChannel` | Adult `FoxAnimation` — no separate holder in 26.1.2 jar |
| Keyframe `degreeVec`/`posVec`/`scaleVec` FFF and **DDD** | **1.21.11** holders absent from jar (6 classes only on 26.1.2) |
| Array-filled channel keyframes | Part B.3 channel backlog on both profiles (CATMULLROM / empty rows) |
| **1.21.11 ProGuard:** `AnimationJavapObfuscationNormalizer` + `--mappings` before `AnimationClinitLift` | Lift without mappings on obfuscated jar (normalizer no-op) |

```powershell
pwsh -File tools/Generate-AnimationIndex.ps1 -ClientJar tools/minecraft-parity/26.1.2/client.jar -VersionLabel 26.1.2
pwsh -File tools/Generate-AnimationIndex.ps1 -ClientJar tools/minecraft-parity/1.21.11/client.jar -Mappings tools/minecraft-parity/1.21.11/client_mappings.txt -VersionLabel 1.21.11
```

### B.2.1 — 1.21.11 animation lift (2026-05-21, first tractable slice)

Regenerated `animation-index-1.21.11.json` and shards under `docs/generated/animation/1.21.11/` via `Generate-AnimationIndex.ps1` with `tools/minecraft-parity/1.21.11/client_mappings.txt`.

| Profile | Index rows | `ok` | `partial` | Notes |
|---------|------------|------|-----------|-------|
| **26.1.2** (named jar) | **16** | **16** | **0** | Unchanged; 16-file preview set |
| **1.21.11** (ProGuard) | **10** | **10** | **0** | All holders in `minecraft_1.21.11_client_animation_definition_classes.txt` |

**Exemplar classes (mappings + normalizer → `ok`, reconciled vs committed shards):** `BatAnimation` (2 definitions, `AnimationClinitLiftObfuscatedTests`), `BreezeAnimation` (multi-clip idle/shoot stack), `WardenAnimation` (large CATMULLROM surface). Phase 10: `Phase10ObfuscatedAnimationJarLiftReconciliationTests` + `Phase10AnimationIrStrictTests` over all 10 rows.

**ProGuard pattern (no `AnimationClinitLift` change):** [`AnimationJavapObfuscationNormalizer`](../src/AutoPBR.Tools.AnimationCompiler/AnimationJavapObfuscationNormalizer.cs) rewrites obfuscated `javap -c` method/field/type comments to Mojang names (`Builder.withLength`, `addAnimation`, `Targets.*`, `Interpolations.*`, `KeyframeAnimations.*`, per-class `putstatic` field names from mappings) so existing clinit regexes apply.

**Blockers vs 26.1.2 (not obfuscation):** six definition holders exist only in the 26.1.2 jar — `BabyArmadilloAnimation`, `BabyAxolotlAnimation`, `BabyRabbitAnimation`, `CamelBabyAnimation`, `FoxBabyAnimation`, `RabbitAnimation`. No `Nautilus`/`Frog` baby rows on either profile. Runtime preview for 1.21.11 still uses 26.1.2 animation shards where mob clips match the 16-file set.

**26.1.2 guard:** regen command above does not touch `animation/26.1.2/`; Phase 7/10 26.1.2 strict tests unchanged.

## B.3 Cleanroom wiring status (16-file set)

**Wired (sample):** Breeze idle/**SHOOT** full body/head/rods/wind stack (2026-05-21) + **INHALE** body/head/wind + **JUMP `wind_body` SCALE** + **JUMP `wind_mid`/`wind_top` ROTATION** (2026-05-21), Nautilus swim (`body` + **`inner_mouth`/`lower_mouth` SCALE** + `upper_mouth` pitch, 2026-05-21), Sniffer walk + **`SNIFFER_DIG` body/head/position** + **`SNIFFER_STAND_UP` rising body** + **`SNIFFER_HAPPY` head** geometry-IR (2026-05-21; walk head CATMULLROM; hind/mid legs), Frog walk/croak + `FROG_TONGUE` tongue/head, Armadillo tail walk + `ARMADILLO_ROLL_UP` + **`ARMADILLO_PEEK` head + front legs + hind offset** + **`ARMADILLO_PEEK` head POSITION + `ARMADILLO_ROLL_OUT`** + **baby peek/roll-up**, Bat fly/rest + `BAT_FLYING`, Creaking walk/attack + **`CREAKING_INVULNERABLE` / `CREAKING_DEATH`**, Rabbit hop + `IDLE_HEAD_TILT` + **left hind POSITION**, **BabyRabbit HOP body/head/hind/tail + `frontlegs`** (2026-05-21), Camel baby walk head + **hind-leg CATMULLROM** + adult walk/idle/dash + **`CAMEL_SIT` / `CAMEL_STANDUP` body**, CopperGolem **walk + `COPPER_GOLEM_IDLE` body/head** (2026-05-21), **Fox baby `FOX_BABY_WALK` four legs MIXED + head Y** (2026-05-21), **BabyAxolotl idle-floor + `BABY_AXOLOTL_SWIM`/`PLAY_DEAD` body + swim leg/tail** (2026-05-22), **Warden sniff/emerge/roar + `WARDEN_ATTACK`/`WARDEN_SONIC_BOOM` body/head** CATMULLROM geometry-IR (2026-05-21) — see tests `VanillaAnimationIrPreviewSamplerTests`, `GeometryIrDefinitionAnimationPreviewTests`. **1.21.11:** `animation/1.21.11/` **10/10 `ok`** shards + index linked in `AutoPBR.Core.csproj` (2026-05-21).

| IR file | Backlog (compact) |
|---------|-------------------|
| **WardenAnimation** | ~~`WARDEN_SNIFF` body/head CATMULLROM~~ (2026-05-21); ~~`WARDEN_EMERGE` / `WARDEN_ROAR` body/head CATMULLROM~~ (2026-05-21); ~~`WARDEN_ATTACK` / `WARDEN_SONIC_BOOM` body/head~~ (2026-05-21); ~~`WARDEN_SONIC_BOOM` ribcage yaw~~ (2026-05-21); **tendril** — entity float probe only (no definition channels) |
| **SnifferAnimation** | ~~Walk head CATMULLROM~~; ~~hind/mid legs~~; ~~`SNIFFER_DIG` body/head/position~~ (2026-05-21); ~~`SNIFFER_STAND_UP` rising body~~; ~~`SNIFFER_HAPPY` head~~ (2026-05-21); **scent/search — setupAnim-only (deferred)** — `SnifferModel` `isSearching` + `sniffSearch` applyWalk; no definition IR channels |
| **NautilusAnimation** | ~~`SWIMMING` `body` SCALE~~; ~~`inner_mouth`/`lower_mouth` SCALE~~ (2026-05-21, blended on single jaw cuboid — no split mesh) |
| **FrogAnimation** | ~~`FROG_TONGUE` tongue/head~~ (2026-05-21); ~~`FROG_CROAK` croaking_body POSITION/SCALE + `FROG_WALK` body/limbs~~ (2026-05-21) |
| **FoxBabyAnimation** | ~~`FOX_BABY_WALK` four legs MIXED + head Y~~ (2026-05-21) |
| **CopperGolemAnimation** | ~~`COPPER_GOLEM_WALK` body/head/right_arm CATMULLROM~~; ~~`COPPER_GOLEM_IDLE` body/head~~ (2026-05-21); **chest-interaction / walk-item clips** — IR targets `chest` (no part in geometry IR) |
| **CamelAnimation** / **CamelBabyAnimation** | ~~walk/idle/dash/sit/standup wired~~ (2026-05-21); ~~`CAMEL_BABY_WALK` head + front-leg CATMULLROM~~ (2026-05-21); ~~`CAMEL_BABY_WALK` hind-leg CATMULLROM~~ (2026-05-22) |
| **BabyAxolotlAnimation** | ~~`BABY_AXOLOTL_IDLE_FLOOR` tail MIXED + head CATMULLROM~~ (2026-05-21); ~~`BABY_AXOLOTL_SWIM` / `PLAY_DEAD` body~~ (2026-05-21); ~~`BABY_AXOLOTL_SWIM` leg/tail CATMULLROM~~ (2026-05-22) |
| **Armadillo** / **BabyArmadillo** | ~~`ARMADILLO_ROLL_UP` body/head~~; ~~`ARMADILLO_PEEK` head + front legs + hind offset~~ (2026-05-21); ~~baby peek/roll-up~~ (2026-05-21); ~~`ARMADILLO_PEEK` head POSITION + `ARMADILLO_ROLL_OUT` head~~ (2026-05-22) |
| **BatAnimation** | ~~Resting head/body flip~~ (2026-05-20); ~~wing_tip~~; ~~wing POSITION Z~~; ~~`BAT_FLYING` head/body/feet~~ (2026-05-21) |
| **Rabbit** / **BabyRabbit** | ~~adult hop + idle head tilt + left hind POSITION~~; ~~baby hop body/head/hind/tail + `frontlegs`~~ (2026-05-21); ~~`HOP` front-leg ROTATION~~ (2026-05-21) |
| **CreakingAnimation** | ~~`CREAKING_WALK` / `CREAKING_ATTACK` `upper_body` ROTATION~~ (2026-05-21); ~~`CREAKING_ATTACK` `upper_body` POSITION + `head` ROTATION~~ (2026-05-21); ~~`CREAKING_INVULNERABLE` / `CREAKING_DEATH` upper_body + arms/head~~ (2026-05-22) |
| **BreezeAnimation** | ~~IDLE/INHALE/JUMP/SLIDE + SHOOT full stack~~ (2026-05-21); ~~JUMP `wind_mid`/`wind_top` ROTATION~~ (2026-05-21); **SLIDE `wind_*` ROTATION — final IR-gap (deferred)** — clinit lift emits POSITION-only on slide wind parts; no ROTATION channels until lifter depth |

**Cross-cutting:** IR completeness (Nautilus scale notes); no `FoxAnimation.json` in 16-set; optional `Build*` integration smokes.

---

# Part C — SetupAnim IR

## C.1 Pipeline

- **Lift:** `SetupAnimLift.cs`, `AnimationModelWiringLift.cs` (companion to clinit lift)  
- **Shards:** `docs/generated/setup-anim/26.1.2/<Model>.json`  
- **Index:** `setup-anim-index-26.1.2.json` — **169** models, **169** `ok`, **0** `partial` (2026-05-21 Part C final drain: `Model`/`EntityModel`/interface effect-only ok, `SlimeModel` renderer-driven stub, `MagmaCube`/`SpinAttack` array IR, `EvokerFangs` scale, `BookModel` state fields, boat inheritance, object shells)  
- **Runtime:** `VanillaSetupAnimRuntime` evaluates `expr` AST; `PreviewRenderStateSynthesis` supplies **timing/state fields only**

**Inheritance:** `inheritsSetupAnimFrom` when method starts with `invokespecial … setupAnim`; lift delta only. Batch template order: `EntityModel` → `QuadrupedModel` → `HumanoidModel` → leaves.

**Playback steps:** `apply`, `applyWalk`, `setVisible` on `KeyframeAnimation` / `AnimationState`.

**Geometry coupling:** `ApplySetupAnimToGeometryIrMesh` for catalog tiers with lifted shards; definition clip pass after IR emit (chicken, quadruped builders, armadillo, breeze, fox baby, etc.).

Hand `Compute*` setupAnim mirrors and `*VanillaKeyframes` are **forbidden** (`HandParityForbiddenSymbolsTests`).

## C.2 Regenerate

```powershell
pwsh -File tools/Generate-SetupAnimIndex.ps1 -ClientJar tools/minecraft-parity/26.1.2/client.jar -VersionLabel 26.1.2 -Parallel -Stats
dotnet run --project src/AutoPBR.Tools.AnimationCompiler -- --lift-setup-anim <options>
```

---

# Part D — Renderer state lift (P6 backlog)

**Pilots:** **Breeze** (2026-05-20) — `BreezeRenderer.json` + `ForBreeze` / `breeze_clip_cycle`. **Sniffer** (2026-05-21) — `SnifferRenderer.json` + `ForSniffer` / `sniffer_clip_cycle`. **Allay** (2026-05-21) — `AllayRenderer.json` + `ForAllay` / `allay_hold_dance_cycle`. **Camel** (2026-05-21) — `CamelRenderer.json` + `ForCamel` / `camel_clip_cycle`. **Warden** (2026-05-21) — `WardenRenderer.json` + `ForWarden` / `warden_clip_cycle` (six `AnimationState` fields + walk; `tendrilAnimation` / `heartAnimation` preview sinusoids). **Frog** / **Creaking** (2026-05-21) — `FrogRenderer.json` + `frog_clip_cycle`, `CreakingRenderer.json` + `creaking_clip_cycle`. **Nautilus** / **CopperGolem** (2026-05-21) — `NautilusRenderer.json` + `nautilus_swim_walk`, `CopperGolemRenderer.json` + `copper_golem_clip_cycle` (walk + idle; chest-interaction clips deferred — B.3). `RendererStatePreviewResolver` maps preview drivers; inactive clips `-1` skip `apply` in `VanillaSetupAnimRuntime`. **Tests:** `RendererStateBreezePreviewTests`, `RendererStateSnifferPreviewTests`, `RendererStateAllayPreviewTests`, `RendererStateCamelPreviewTests`, `RendererStateWardenPreviewTests`, `RendererStateFrogPreviewTests`, `RendererStateCreakingPreviewTests`, `RendererStateNautilusPreviewTests`, `RendererStateCopperGolemPreviewTests` (T0/T2). Full bytecode lift of `extractRenderState` / `RendererStateLift` compiler still **blocked** — see [`archive/p6-renderer-state-lift-blockers.md`](archive/p6-renderer-state-lift-blockers.md).

**Default (non-pilot):** `PreviewRenderStateSynthesis` maps preview time → `walkAnimationPos`, `walkAnimationSpeed`, head look, `ageInTicks`, entity flags.

**Path (remaining):**

1. Lift `LivingEntityRenderer` / mob `extractRenderState` + `setupRotations` slices (compiler, not hand JSON)  
2. Expand `renderer-state/<version>/<Renderer>.json` index beyond Breeze pilot  
3. Strict parity gated on renderer-state `ok` aligned with setup-anim index  

**Non-goals until P6:** Reintroduce `Compute*` pose math; merge setupAnim into animation clinit shards.

---

# Unified test tiers (T0–T3)

| Tier | Geometry | Animation / setupAnim |
|------|----------|------------------------|
| **T0** | Lifter, schema, mesh walk math | Clinit/setupAnim lift invariants |
| **T1** | Allowlisted shards + reference + viewport strict | `ok` animation shards with full channels; promoted setupAnim |
| **T2** | Probes when jar/shard present; no fail on `partial` | Incomplete channels per Part B backlog — no strict goldens |
| **T3** | `AUTOPBR_RUN_LIFT_QUALITY_INDEX=1`, write quality JSON | Index-wide setupAnim/animation stats (opt-in) |

**Env vars:**

```powershell
$env:AUTOPBR_RUN_LIFT_QUALITY_INDEX = "1"
$env:AUTOPBR_WRITE_GEOMETRY_LIFT_QUALITY = "docs/generated/geometry-lift-quality-26.1.2.json"
$env:AUTOPBR_RUN_ASSEMBLY_VIEWPORT_PROBES = "1"
```

Details and allowlist table: [`test-guidance-geometry-animation-ir.md`](test-guidance-geometry-animation-ir.md).

---

## DONE

- Quality report v2 fields + `geometry-lift-quality-26.1.2.json` (`schemaVersion` 2)  
- 56-pilot manifest + javap snapshots (44 unique files)  
- World-pose reference bake (3A) + `referenceWorldPoseMatch`  
- `tools/regen-assembly-pilots.ps1`; batch 4A/4B shard regen  
- Preview: flat-quadruped no-reparent; `GeometryIrAssemblyViewportSanityTests`  
- Animation: 16/16 definition shards `ok` on 26.1.2; sampler LINEAR + CATMULLROM  
- SetupAnim: **169/169** `ok`; `VanillaSetupAnimRuntime` + wiring lift; hand mirrors removed; feline host hoisted (`AbstractFelineModel`, `AdultFelineModel`, `BabyFelineModel`); **AllayModel** `partial`→`ok` (wing/arm/dance IR, `lerp`/`min`, equine completion gated to `.equine.`); **Part C batch (2026-05-21):** playback-wiring + mob-family drains; **Part C final drain:** abstract/interface/effect-only hosts, slime renderer stub, array effect models, book/state-field lift, boat `AbstractBoatModel` inheritance
- **Feline cat lift (2026-05-21):** `AdultCatModel` / `BabyCatModel` — mesh host resolution (`BabyCat`→`BabyFelineModel.createBabyLayer`, per-host factory in `BytecodeMeshResolution`), delegate reference gate + javap oracle (`BabyFelineModel.createBabyLayer.javap.txt`), manifest `geometry_ir_official_jvm_baby` on 13 cat baby textures; shards regen `ok`; catalog preview uses `BabyFelineModel` IR topology. **BabyOcelot (2026-05-21):** same delegate map + mesh-host-first resolve; reference bake prefers `BabyFeline.createBabyLayer`; regen shard `ok` with baby head cuboid gate.  
- Entity-wide promotion: HumanoidModel, VillagerModel, SkullModel (cuboid strict — not pilot dual gate)  
- Snifflet dedicated geometry IR shard  
- Javap pose oracle parser pass (nested cuboid names, parametric head Y, delegate merge, `root` skip) + `GeometryJavapPoseOracleTests`
- **Pilot 4C promotion (2026-05-21):** **54 / 56** dual gate (`geometry_ir_partial_to_ok_promotion_jvm.txt` ∩ 56 pilots): batch 1 (**16**); batch 2 (**8**); batch 3 (**11**); batch 4 (**11**); batch 5 — `EnderDragonModel`, `RavagerModel`, `BabyDonkeyModel`, `BabyFelineModel`, feline×4 (**8**). **Not on 4C:** `SheepModel` (T1 viewport probe only); `QuadrupedModel` (abstract). **Feline (2026-05-21):** `AbstractFelineModel` + cat×2 on partial→ok + T1 strict (LER fix; not 4C). **AdultOcelot:** delegate → `AdultFelineModel`, manifest `geometry_ir_official_jvm`, shard `ok`.
- **Full 184 geometry index (2026-05-21, Part A.2 #3):** `Generate-GeometryIndex.ps1` batch on `minecraft_26.1.2_client_model_classes.txt`; `geometry-index-26.1.2.json` rows aligned to `docs/generated/geometry/26.1.2/` shards; **56** assembly pilots restored from pre-batch backup (byte-identical)
- **Wave C geometry partial drain (2026-05-25/26):** `geometry-index-26.1.2.json` now has **157** `ok` · **27** `skipped` · **0** `partial` · **0** `fail`; `ArmorStandModel` final body-stick row promoted; quality snapshot `generatedUtc` **2026-05-26T02:32:22Z**.
- **Automated plan closure:** [`automated-tracks-complete.md`](generated/automated-tracks-complete.md) marks every automated workstream complete except documented deferrals; **only § A.4 Manual Explore** blocks plan “done”.

## Remaining / deferred

- **Manual Explore sign-off:** Human-only § A.4 canary rows are still `pending`; use [`manual-explore-playbook.md`](manual-explore-playbook.md) and `tools/export-manual-explore-checklist.ps1`.
- **Reference batch freshness:** JDK 25 required for class file 69; pilot `reference-output` may be stale.
- **P6 renderer lift:** Breeze/Sniffer/Allay/Camel/Warden/Frog/Creaking/**Nautilus**/**CopperGolem** pilot shards + preview resolver landed; synthesis fallback for other mobs; bytecode compiler remains deferred.
- **Phase 1C cube deformation:** `BabyZombieModel` `inflate: 0.25` is recorded in IR; parity emit skips inflate until reference/viewport policy changes.
- **Optional CleanRoom cleanup:** catalog paths are strict IR; remaining hand adult `BuildQuadruped` cleanup is not a plan blocker.

## Optional hygiene

- Re-run `Generate-GeometryIndex.ps1`, `Generate-SetupAnimIndex.ps1`, and animation index scripts when new lifts land.
- Re-run `plan-completion-audit.md` / `automated-tracks-complete.md` when completion criteria or generated metrics change.
- Optional: `AUTOPBR_RUN_ASSEMBLY_VIEWPORT_PROBES=1` + pilot regen to refresh viewport/probe confidence after preview-policy edits.

---

## File map (key paths)

| Area | Path |
|------|------|
| **This plan** | `docs/runtime-ir-preview-plan.md` |
| Geometry lifter | `src/AutoPBR.Tools.GeometryCompiler/JavapFloatGeometryMeshLift.cs` |
| Animation/setupAnim lifter | `src/AutoPBR.Tools.AnimationCompiler/` |
| Quality / preview | `src/AutoPBR.Core/Preview/GeometryIrLiftQualityReport.cs`, `GeometryIrMeshEmitter.cs`, `VanillaSetupAnimRuntime.cs`, `PreviewRenderStateSynthesis.cs` |
| Reference bake | `tools/MinecraftGeometryReference/` |
| Geometry allowlists | `src/AutoPBR.Core/Data/minecraft-native/geometry_ir_*.txt` |
| Schemas | `docs/generated/schema/{geometry,animation,preview-delta,setup-anim}*.json` |
| Archives | `docs/archive/` (superseded long-form plans) |

---

## Quadruped body placement regression (Cow / PolarBear / Panda)

**Tracked:** 2026-05-20 · **Explore 3D** runtime IR parity-catalog preview · **Subagent investigations:** [ffa01035](agent-transcripts/ffa01035-4752-4037-8b69-9b9a54e56ca2) (`22f80427` preview, `11a8cdcd` lifter, `327219f4` fix loop — in progress)

### Symptoms (manual Explore)

| Mob | Screenshot | What you see |
|-----|------------|--------------|
| **PandaModel** | `docs/images/quadruped-panda-preview.png` *(copy from Cursor attachment `assets/c__Users_John_Phoenix_AppData_Roaming_Cursor_User_workspaceStorage_244a7a0b811e50257e0d5d3346cc8439_images_image-666b10f0-8719-4a16-8537-7e793144c21f.png` when available)* | Torso floats ~one body-height above the four legs; head sits on the ground plane, visually disconnected from the body. |
| **CowModel** | `docs/images/quadruped-cow-preview.png` *(copy from `assets/c__Users_John_Phoenix_AppData_Roaming_Cursor_User_workspaceStorage_244a7a0b811e50257e0d5d3346cc8439_images_image-407d9110-a865-4491-8833-87282a32bfbf.png` when available)* | Body cuboid cluster high in the air; head offset to the side; legs on the grass plane — three separate islands instead of one quadruped. |
| **PolarBearModel** | *(same bug class as Panda/Cow in Explore; capture to `docs/images/quadruped-polarbear-preview.png`)* | Same floating torso / detached head pattern reported alongside Cow and Panda. |

![PandaModel — floating body, head on ground](images/quadruped-panda-preview.png)

![CowModel — body high, head and legs separated](images/quadruped-cow-preview.png)

### What is *not* the root cause

| Layer | Finding |
|-------|---------|
| **Geometry lift (shards)** | `CowModel`, `PandaModel`, `PolarBearModel` (and babies) are `extractionStatus: ok`, `referenceWorldPoseMatch: true`, `referenceHierarchyMatch: true`, `assemblyGatePass: true`. Shards keep **flat** `root` children (`head`, `body`, four legs) with root-absolute `PartPose.offset` — same composed-flat pattern as Creeper / `QuadrupedModel` hosts (`suspectedFlatNestedPartCount: 4`). |
| **`GeometryIrMeshWalk`** | Composes **`localPartPose × parentWorld`** (row-vector / `System.Numerics` — matches `Vector3.Transform` in the baker). Used consistently for emit, setupAnim cuboid order, and world-translation probes. |
| **`GeometryIrEntityCuboidTables.g.cs`** | **20** codegen tables (babies + climate cows + panda/polar + fish/chicken pilots). Temperate **`CowModel`** catalog preview uses lifted IR emit (`TryBuildCowMeshFromGeometryIr`, 2026-05-22). **`PolarBearModel`** adult uses IR + codegen (2026-05-22). |
| **Harmful leg reparent** | `GeometryIrPartTreeRepair` skips `QuadrupedLegReparentRules` when `UsesVanillaFlatQuadrupedLegBake` (body + legs as root siblings). Regressed creeper stacking is covered by `GeometryIrPartTreeRepairTests.Creeper_repair_does_not_stack_body_y_onto_leg_origins`. |

### Root cause (preview assembly, shared bug class) — **regression guard (2026-05-28)**

**Living-entity renderer (LER) preview basis — not a different Blaze3D/OpenGL world, but wrong matrix composition in `System.Numerics`.**

Vanilla applies **`PoseStack.scale(-1,-1,1)` once before the part tree** (column semantics: **`S × (T × R)`** on model points). AutoPBR bakes that into each cuboid’s `ModelElement.LocalToParent` for Explore.

**Two bugs caused the “floating torso / separated body” class** (cow, pig, creeper, chicken, bee, camel, hoglin, and many other catalog IR mobs — not quadruped-only):

| Bug | Symptom | Wrong code | Correct code |
|-----|---------|------------|--------------|
| **Walk multiply order** | Part chains drift even before LER | `parentWorld × local` in `GeometryIrMeshWalk.VisitPart` | **`local × parentWorld`** (row-vector convention used by `Vector3.Transform` in `MinecraftModelBaker`) |
| **LER fold** | Rotated body (`Rx(π/2)` torso) detaches from head/legs; raw `Matrix4x4.Multiply(S, M)` does not flip origins | `ApplyPreviewWorldRoot` / emit `RootTransform = S` / per-element **`M × S`** (`ApplyGlobalTransform`) | **`ApplyLivingEntityRendererColumnRootScale`** — rebuild row affine so column **`S × M`** applies to **corner points** (basis vectors + origin transformed by `S`) |
| **PartPose compose (legacy texel product)** | Chicken (and other `offsetAndRotation` torsos): body centroid on **Z≈16** while head/legs cluster near **Z≈−4** | **`T × Er`** in `TryComposePartPose` (rotates offset into Z) | **`Er × T`** matrix product — translation is not rotated; matches `PartPose.offsetAndRotation` / reference `worldPose` bake |

**Do not reintroduce** per-element `LocalToParent × S` for flat quadrupeds or `RootTransform = S` on the walk without column-root rebuild — tests may pass on translation-only parts while **rotated body cuboids** still float in Explore.

**Lifter angle:** When `referenceWorldPoseMatch` is already true, prefer fixing **walk order + LER fold + compose policy** before `GeometryIrPartTreeRepair` reparent hacks (creeper leg-stack regression).

### PartPose vs ModelPart render — **do not conflate (2026-05-28+)**

Preview emit must match **vanilla cuboid draw**, not only reference **`worldPose.translation`**. Those are related but not identical for rotated attached parts (horns, ears, nested head stacks).

| Layer | Java source | What it answers | C# / reference |
|-------|-------------|-----------------|----------------|
| **`PartPose.offsetAndRotation`** | Factory / `getInitialPose()` | Bind offset + Euler; **offset not rotated into parent axis** | Reference `worldPose` via `PartWorldPoseMath` (`mul(Er, T)` row convention) |
| **`ModelPart.translateAndRotate`** | Runtime PoseStack walk before cuboid draw | **Full 4×4** per part after parent chain; cuboid corners use this | JVM `renderPartAffines` + `renderCuboidCenters` (`ModelPartRenderPoseMath.java`) |

**2026-05-28 adult regression class** (chicken body Z, exploded catalog parts) was **`System.Numerics` composition**, not “Er×T is impossible in C#”:

1. **Walk order** — must be **`local × parentWorld`** (row-vector / `Vector3.Transform`), not `parent × local`.
2. **LER fold** — column-root `ApplyLivingEntityRendererColumnRootScale`, not per-element `M × S`.
3. **Anti-`T × Er`** — legacy `Mul(translation, rotation)` at texel scale rotates bind offsets into the wrong axis (Entity Debug toggle `UseLegacyTranslationTimesRotationPartPose`).

**2026-05-28 horn / attached-part class** (`ColdCowModel` horns inside body while `referenceWorldPoseMatch` stayed green):

- Reference **`worldPose`** for `right_horn` stayed near the body cluster (Z ≈ +0.5) while JVM **`renderCenterTexel`** was on the head cluster (Z ≈ −13).
- Passing **`worldPose`-only** tests does **not** guarantee Explore cuboid placement.
- **Production default (2026-05-28+)** — **ModelPart block stack** in `TryComposePartPose` / `GeometryIrMeshWalk` (not texel-scale `Mul(Er, T)` alone):

| Step | Rule |
|------|------|
| **Units** | PoseStack **block space**: bind translations **÷ 16** before chain (`TexelRowAffineToBlock` / `BlockRowAffineToTexel`). |
| **Local part delta** | **`Er` in upper 3×3 + bind translation in row 4** — same **bind-offset convention** as `PartWorldPoseMath` / Er×T, **not** a full `T × R` matrix product and **not** legacy **`T × Er`**. |
| **Parent chain** | **`localBlock × parentBlock`** then scale row 4 to texel (JVM probe: `TR/localHead` on cold-cow horn). |
| **Ground truth for render** | Prefer **`renderPartAffines` / `renderCuboidCenters`** in `reference-output/*.json` over **`worldPose.translation`** when asserting cuboid placement. |

**Legacy debug only:** `UseLegacyTranslationTimesRotationPartPose` → texel-scale **`T × Er`** (pre–2026-05-28 adult catalog GPU explosion). Use Entity Debug for A/B; do not ship as default.

**Tests:** `ModelPartTranslateAndRotateProbeTests`, `ColdCowHornPreviewPlacementTests` (JVM `renderCenterTexel`), `ChickenPreviewZClusterTests`, `AttachedPartWorldMatrixParityTests`. Obsolete: `HornComposeOrderProbeTests` (texel Er×T vs T×Er with wrong walk order — superseded by JVM render compare).

### Canonical LER policy (2026-05-28 — do not regress)

| Piece | Rule |
|-------|------|
| **Classification** | `ResolveGeometryIrLerBasis` → **`StandardWorldRoot`** for almost all catalog mobs (`UsesFlatPartPoseOffsetQuadrupedJvm`, hoglin/ravager/rabbit, etc.). **`RightComposeLocalChain`** is **legacy / test-only** (`TryBuildGeometryIrParityMeshForTestsWithLerCompose`). |
| **Emit plan** | `ResolveGeometryIrParityEmitPlan`: **model-space emit** (`EmitRootTransform = Identity`), **one** post-batch after setupAnim/definition-anim → `FinishGeometryIrMeshLivingEntityRendererBasis` → **`ApplyLivingEntityRendererColumnRootScale`**. |
| **Hand `Build*` fallbacks** | Same as catalog: `ApplyLivingEntityRendererPreviewBasis(mesh, GeometryIrLerBasisKind.StandardWorldRoot)` or parameterless overload (column root). **Not** `lerMirrorRightComposeLocalChain: true`. |
| **GPU bones** | `ApplyLivingEntityRendererColumnRootScale` per bone; parity-catalog meshes → **no second LER** in `TryFillBoneMatricesFast`. |
| **Tests** | `PigLerEmitSanityTests`, `GeometryIrLerMirrorComposeClassificationTests`, `GeometryIrQuadrupedReferenceWorldPoseTests`, `GeometryIrHoglinViewportLerTests`, **`ChickenPreviewZClusterTests`** (temperate/cold/baby body Z vs head/legs). |

Implementation: `CleanRoomEntityShared.ApplyLivingEntityRendererColumnRootScale`, `GeometryIrMeshWalk`, `CleanRoomEntityGeometryIrLerTestHooks.ResolveGeometryIrParityEmitPlan`.

### Fix approach / status

| Item | Status | Notes |
|------|--------|--------|
| Column-root LER + walk order | **Done (2026-05-28)** | High-impact across catalog IR (bee, camel, chicken partial, hoglin, quadrupeds, …). |
| Catalog JVM-aware LER dispatch | **Done (2026-05-21)** | `ApplyGeometryIrParityLivingEntityRendererPreviewBasis(officialJvm, …)`. |
| Hand `Build*` aligned to column root | **Done (2026-05-28)** | Quadruped `Build*` + default `ApplyLivingEntityRendererPreviewBasis` overload. |
| Reference world-pose tests | **Done** | `GeometryIrQuadrupedReferenceWorldPoseTests` vs `reference_java`. |
| Chicken body Z displacement | **Done (2026-05-28)** | Anti-**`T × Er`** at texel scale (Entity Debug confirmed adult catalog GPU fix). Legacy **`T × Er`** via `UseLegacyTranslationTimesRotationPartPose` only. |
| ModelPart render stack (attached parts) | **Done (2026-05-28)** | Block-space **`Er` upper 3×3 + bind row 4**, **`local × parent`**, JVM `renderCuboidCenters` (`ColdCowModel` horns). |
| Manual Explore sign-off | **Done (2026-05-28)** | Cow cold horns + adult catalog cohesion verified in Explore. Refresh § A.4 screenshots when convenient. |

**Do not** “fix” floating torsos via `GeometryIrPartTreeRepair` leg-under-body reparent for flat quadruped JVMs.

### Explore scene placement (2026-05-29)

Single policy for emulated / parity-catalog Explore 3D preview (implementation: `EntityPreviewGrounding`, `EntityPreviewPlacement`, `PreviewSubjectPlacement`, `OpenGlPreviewBackend.Render.PassSetup`):

| Policy | Rule |
|--------|------|
| **Animation default** | Parity-catalog textures load with entity animation **off** and legacy whole-mesh wobble **off**; rebake uses `applyGeometryIrSetupAnimMotion: false` until the user enables animation. |
| **Ground contact** | Lift from the **same posed mesh** drawn (bind when anim off, animated bones when anim on). Contact Y prefers **leg** part ids (`leg` ∧ ¬`head`) when geometry IR part ids are on the rebake context; else **bottom quartile** of vertex Y. |
| **Lift application** | **One** path: CPU bakes lift+anchor into vertices (`EntityGpuMeshSpaceLiftY = 0`); GPU uses bind-pose VBO + `uEntityMeshLiftY` only (recomputed per frame when anim on). No second global `min-Y` lift in `GlPbrPreviewControl` for emulated subjects. |
| **Anchor** | After bake+LER, one translation centers XZ and places feet on `PreviewStageConstants.GridWorldY` (+ clearance); stored on `PreviewModelSubject.EntityPreviewAnchorOffset` / rebake diagnostics. |
| **Camera** | Orbit pivot reseeds per entity texture path (not shared with unit-cube bucket); target ≈ chest height from posed AABB. |
| **GPU bones** | Bind VBO from `TryPrepareGpuSkinnedEmulatedMesh` (`applyGeometryIrSetupAnimMotion: false`). **Animation off:** `uEntityGpuSkinning=0`, `uEntityBoneCount>0` → shader `W()`+lift only (vertices already bind posed). **Animation on:** `bone[i]=invBind[i]·M_anim[i]` from full `TryBuildStaticMesh` when inverse bind exists; `uEntityGpuSkinning=1`. See [`entity-preview-gpu-cpu-parity.md`](entity-preview-gpu-cpu-parity.md). |
| **Diagnostics** | `ParityCatalogEntityPreviewDiagnostics.FormatExplorePlacementLine` → preview log: ler basis, gpuSkinning, liftY, animClock, setupAnimMotion, part centroid Y (preview space; refreshed on GPU bind prep). |

Tests: `EntityPreviewPlacementTests.Cow_temperate_bind_pose_foot_contact_near_preview_floor_after_placement`, `PreviewRenderingTests.Placement`, `EntityGpuSkinnedMatrixCpuParityTests` (cow/panda).

### Baby JVM family (same canonical policy as adults — 2026-05-28+)

Baby fixes must **extend** the adult world-preview rules above, not reintroduce hand-builder shortcuts that fight lifted IR. See also [`vanilla-preview-parity.md`](vanilla-preview-parity.md) § *Reusable entity runtime parity playbook* and [`generated/rig-accuracy-batches/equine.md`](generated/rig-accuracy-batches/equine.md).

| Rule | Detail |
|------|--------|
| **IR host** | Resolve dedicated `Baby*Model` shards on 26.1.2+ (`GeometryIrParityJvmResolver`); never scale adult rigs or mis-lifted adult hosts (e.g. adult `HorseModel` for baby horse). |
| **Walk + LER + compose** | Same as adults: `GeometryIrMeshWalk` **`local × parentWorld`**, post-batch **`ApplyLivingEntityRendererColumnRootScale`**, production **ModelPart block stack** (see § PartPose vs ModelPart render). Do not “fix” babies with per-element `M × S`, reversed walk order, or legacy **`T × Er`**. |
| **Flat quadruped legs** | `UsesVanillaFlatQuadrupedLegBake` → **skip** `QuadrupedLegReparentRules` (fox, cow, chicken, `BabyHorseModel`, …). **Do not** reparent to fix floating torsos — that was the adult cow/panda regression class. |
| **Nested-head exception** | `HeadStackNestedUnderBody` (`BabyDonkeyModel`): head stack under `body` but legs still at root with **body-relative** offsets → reparent legs under `body` only (see `GeometryIrPartTreeRepairTests`). |
| **Baby scale emit** | `ParityCatalogDefaultBabyProfile`: dedicated resolved `Baby*` JVM host → `BabyProfile.Adult` (unit scale), regardless of native profile label/version text (`root parsed=?` included). Adult/shared hosts that vanilla still transforms at render time remain the only parity-catalog path eligible for `VanillaUniformBaby` (0.5). |
| **Equine baby tail / hierarchy** | Tail and `tail_r1` use **IR walk world** only; no hand absolute tail overrides in emit presets (they duplicated body pose and separated tail from nested IR). Head idle overrides may remain where javap baseline differs from static layer. |
| **setupAnim on flat IR** | `StripUnsafeFlatQuadrupedPeerPositionChannels` for flat JVM families; merge **assigned** channels with geometry baseline (delta), not full absolute part transforms. Rotation-only overlay for chicken dedicated pass. |
| **Explore defaults** | Parity-catalog preview: animation **off** + legacy wobble **off** until user enables (`ApplyExploreParityCatalogPreviewDefaults`). Initial mesh bake uses `applyGeometryIrSetupAnimMotion: false`. |
| **Verification** | `BabyFamilyAttachmentClusterTests`, `EquinePreviewReferenceClusterTests` (baby leg/tail clusters), `BabyCatalogGeometryIrPreviewTests.Dedicated_baby_ir_uses_unit_cuboid_scale_when_profile_is_unversioned_root`, `EntityPreviewPlacementTests.Baby_rebake_records_meaningful_part_centroid_diagnostics`; manual § A.4 rows for `BabyFoxModel`, `BabyCowModel`, `BabyDonkeyModel`, etc. |

**Do not** add family-wide `TryGetPartPoseOverride` absolute poses for limbs/tail on catalog IR emit — that bypasses the same world-pose chain validated by `referenceWorldPoseMatch`.

### Impact on lift pipeline

| Area | Impact |
|------|--------|
| **Shard regen** | No change expected for Cow/Panda/PolarBear part trees if preview-only LER policy is corrected. |
| **Quality JSON** | `referenceWorldPoseMatch` / `assemblyGatePass` can stay green; add viewport cohesion probes to catch preview-only drift. |
| **Other mobs** | Same class: any flat quadruped with mixed `offset` vs `offsetAndRotation` across parts (goat, llama, fox, etc.) — classify per JVM/oracle, not per screenshot. |
| **Promotion** | `CowModel` on `geometry_ir_partial_to_ok_promotion_jvm.txt` must not promote on gates alone until Explore T1 matches § A.4. |

### Verification checklist

- [x] `ApplyParityCatalogGeometryIrPreviewBasis` passes `officialJvmName` into LER resolver (delegates to shared helper).
- [x] `ResolveGeometryIrLerMirrorRightComposeLocalChain(PolarBearModel, …)` → **false** (column-root `StandardWorldRoot`); legacy `M×S` only in explicit test hooks.
- [x] `dotnet test tests/AutoPBR.Core.Tests --filter "GeometryIrLerMirrorComposeClassificationTests|GeometryIrHoglinViewportLerTests|GeometryIrAssemblyViewportSanityTests"` — green (2026-05-21).
- [x] Cohesion tests: body within leg–head vertical band + span cap for Cow/Cold/Warm/Panda/PolarBear IR + catalog static mesh.
- [ ] Explore 3D: Cow temperate/cold/warm, Panda, PolarBear adult+baby — single connected silhouette (compare to screenshots above).
- [x] GPU path: parity-catalog IR records `GeometryIrOfficialJvm` on dispatch route (no double LER on catalog meshes).
- [x] GPU bind-pose shader applies `W()`+lift whenever `uEntityBoneCount>0` (not only when skinning active) — [`entity-preview-gpu-cpu-parity.md`](entity-preview-gpu-cpu-parity.md).
- [x] GPU animated bones use full mesh `LocalToParent` when bind inverse present (same source as CPU).
- [x] LER folded once per build (`ResolveGeometryIrParityEmitPlan`); definition-animation before final LER on catalog path (2026-05-28).
- [x] `GeometryIrQuadrupedReferenceWorldPoseTests` green for cow / panda / creeper / pig / polar (shard + catalog static).
- [ ] Optional: `AUTOPBR_RUN_ASSEMBLY_VIEWPORT_PROBES=1` + pilot regen unchanged for flat-count metrics.

---

## Wave 8 backlog (hygiene + deferred lifts)

| Track | Status | Next action |
|-------|--------|-------------|
| **Manual Explore (§ A.4)** | Pending all canary rows (`automated_prereq` ✓) | **Human-only** — [`manual-explore-playbook.md`](manual-explore-playbook.md); export: `pwsh -File tools/export-manual-explore-checklist.ps1 -Batch 4C-1` … |
| **Plan completion audit** | [`plan-completion-audit.md`](generated/plan-completion-audit.md) (2026-05-25 Wave C) | Re-run when metrics or § completion criteria change |
| **RendererStateLift compiler (Part D)** | Pilots: Breeze/Sniffer/Allay/Camel/Warden/Frog/Creaking/**Nautilus**/**CopperGolem** (hand JSON) | Bytecode lift blocked — [`archive/p6-renderer-state-lift-blockers.md`](archive/p6-renderer-state-lift-blockers.md) |
| **Phase 1C deformation** | Lift ✓; parity emit skips (reference pre-inflate) | **Deferred** — audit Phase 1C probe; re-bake reference or viewport-only path |
| **Geometry index partial drain** | **Complete:** **157** `ok` · **27** `skipped` · **0** `partial` · **0** `fail` | Wave C: `ArmorStandModel` final partial promoted; re-run full regen only when new lifts land |
| **Rig-accuracy batches** | **Populated** under [`generated/rig-accuracy-batches/`](generated/rig-accuracy-batches/) | 56-pilot tables + humanoid/flying quality rows; cross-link § A.3.1 |
| **Entity cuboid codegen (A.9)** | **20** pilot tables (+`PolarBearModel` 2026-05-22); catalog 100% IR | Optional CleanRoom cleanup only; `BuildQuadruped` legacy hand path remains in [`mob-ir-parity-backlog.txt`](generated/mob-ir-parity-backlog.txt) |
| **Animation B.3** | **Closed** (2026-05-21) — primary + depth rows wired | Residual **deferred** only: Warden tendril; Sniffer scent/search **setupAnim-only**; Breeze **SLIDE wind ROTATION** IR-gap; Fox adult absent; Copper chest clips |

### Plan completion criteria

**Audit (2026-05-25 Wave C):** [`plan-completion-audit.md`](generated/plan-completion-audit.md) — per-bullet ✅ automated / ⏳ human-only / 🔧 deferred.

This plan is **done** when all of the following hold (no further edits required except hygiene regen):

1. **Geometry:** All **56** assembly pilots pass automated gates (`assemblyGatePass`, `javapPoseOracleMatch`, reference world/hierarchy, T1 viewport) **and** § A.4 Manual Explore signed off for every canary row — **only remaining blocker; human-only** ([`manual-explore-playbook.md`](manual-explore-playbook.md)).
2. **Catalog:** **761** manifest diffuse paths remain `RuntimeGeometryIrJson` with no `cleanRoom` fallback (`ParityCatalogMeshDriverKindSurveyTests` strict gate — **0** `cleanRoom`). Block-entity boat/chest/chest-boat paths use `ParityCatalogHandLiftGeometryIrCatalog` (`BoatModel` hull, synthetic `ChestBoatModel`, `ChestModel`; index rows stay `skipped`). Adult `HumanoidZombieVillager` paths resolve `ZombieVillagerModel` via manifest `geometry_ir_official_jvm` (no `ZombieModel` override). **Complete.**
3. **SetupAnim:** **169/169** `ok` on 26.1.2 index; no hand `Compute*` mirrors (`HandParityForbiddenSymbolsTests`). **Complete.**
4. **Animation (26.1.2):** 16/16 definition shards `ok`; B.3 backlog table rows either wired or explicitly deferred with IR gap documented. **Complete except documented deferrals.**
5. **P6 (optional for “done”):** Renderer-state compiler replaces synthesis for mob families beyond pilot shards; until then Part D stays **deferred** and does not block geometry/animation “done”.

**Current verdict:** automated plan work is complete; final sign-off is § A.4 Manual Explore.

---

## Wave C landed (2026-05-25/26) — final geometry partial drain

| Track | Outcome |
|-------|---------|
| **Geometry index** | **157** `ok` · **27** `skipped` · **0** `partial` · **0** `fail`; final partial row promoted |
| **ArmorStandModel** | Body-stick override fixed; `ArmorStandModel` shard/index row is `ok` |
| **Quality snapshot** | `geometry-lift-quality-26.1.2.json` `generatedUtc` **2026-05-26T02:32:22Z**, `okEntryCount` **157** |
| **Plan “done” blocker** | Still **only § A.4 Manual Explore**; P6 / Phase 1C / reference freshness remain deferred |

---

## Wave 6 backlog (preview-deltas + entity-wide mesh)

| Track | Status | Next action |
|-------|--------|-------------|
| **Preview-deltas (A.8)** | **12** overlays committed (`cow`, `pig`, `creeper`, `chicken`, `blaze`, `bat`, `cod`, `salmon`, **hoglin×2**, **panda**, **polar** 2026-05-21) | Manual Explore sign-off § A.4; babies use JVM+stem LER gate (no overlay) |
| **BabyOcelot / ocelot×2** | **BabyOcelot** delegate → `BabyFelineModel`; **AdultOcelot** delegate → `AdultFelineModel` + manifest `geometry_ir_official_jvm` on `ocelot.png` (2026-05-21) | Explore sign-off `ocelot.png` / `ocelot_baby.png` |
| **BreezeModel** | Quality: `assemblyGatePass` + `uvWithinAtlasMatch` + wind `wind_*` parts on 32×32 shard | § A.4 detection-only Explore; renderer-state pilot done (Part D) |
| **4C expansion** | **54 / 56** dual gate | Remaining: `SheepModel` T1-only; `QuadrupedModel` abstract — manual Explore § A.4 |

---

## Wave 12 landed (2026-05-22) — hygiene sync + optional PolarBear A.9

| Track | Outcome |
|-------|---------|
| **Pilot quality restore** | Pre-regen backup + targeted lift: **DonkeyModel**/**BabyCatModel** from `.tmpbuild/full-index-pilot-backup`; **BabyOcelotModel** re-lift (`BabyFelineModel` host, was `AdultFelineModel`); `regen-assembly-pilots.ps1 -SkipLift` — **56/56** on all four gate columns |
| **Concat cycle guard** | `BytecodeMeshResolution.BuildMeshConcatDeep` — `concatResolutionChain` set breaks delegate cycles during mesh-host deep concat |
| **Geometry index (honest at Wave 12)** | **146** `ok` · **27** `skipped` · **11** `partial` · **0** `fail` — superseded by Wave C **157/27/0/0** |
| **Partial drain** | Superseded by Wave C; **0** `partial` rows remain |
| **A.9 PolarBear adult** | `PolarBearModel` IR + codegen table (**20** tables); `BuildPolarBear` → `TryBuildPolarBearMeshFromGeometryIr` (PandaModel pattern) |
| **B.3 depth wired** | Creaking invulnerable/death; Camel baby hind CATMULLROM; BabyAxolotl swim legs/tail; Armadillo peek — see [`automated-tracks-complete.md`](generated/automated-tracks-complete.md) |
| **Plan “done” blocker** | **Only § A.4 Manual Explore** ([`automated-tracks-complete.md`](generated/automated-tracks-complete.md)) |

---

## Wave 11 landed (2026-05-22) — plan closure + automated-tracks summary

| Track | Outcome |
|-------|---------|
| **Plan completion audit** | [`plan-completion-audit.md`](generated/plan-completion-audit.md) — criterion **#2** catalog **761** hand-lift policy ✅; A.9 **19** tables (pre–PolarBear) |
| **Automated-tracks rollup** | [`automated-tracks-complete.md`](generated/automated-tracks-complete.md) — every plan workstream tagged ✅ automated / ⏳ human / 🔧 deferred; **only § A.4 Manual Explore** blocks plan “done” |
| **Catalog 761** | Strict `RuntimeGeometryIrJson`, **0** `cleanRoom`; block-entity boat/chest/chest-boat via `ParityCatalogHandLiftGeometryIrCatalog` |
| **A.9 cuboid codegen** | Climate cows + panda + fish/chicken pilots; temperate **`BuildCow`** wired to lifted IR emit (2026-05-22) |
| **§ A.4 playbook** | [`manual-explore-playbook.md`](manual-explore-playbook.md) + `export-manual-explore-checklist.ps1` (`-Batch 4C-1` … `4C-5`) — **human-only** sign-off |
| **P6 pilots** | **Nautilus** + **CopperGolem** renderer-state JSON + preview resolver (hand shards; compiler still 🔧 deferred) |

---

## Wave 10 landed (2026-05-21) — hygiene + audit refresh

| Track | Outcome |
|-------|---------|
| **Plan completion audit** | [`plan-completion-audit.md`](generated/plan-completion-audit.md) — criterion **#2** `ZombieVillagerModel` ✅; catalog **761** hand-lift; B.3 + P6 **Frog**/**Creaking** |
| **Live metrics + Wave 8 table** | Index **157/27/0/0** (Wave C); B.3 **closed**; A.9 **20** cuboid tables (+PolarBear) |
| **Pilot quality regen** | `pwsh -File tools/regen-assembly-pilots.ps1 -SkipLift` — latest quality `generatedUtc` 2026-05-26T02:32:22Z; **56/56** gates; flatCount stable at **4** (**39/56** pilots with `suspectedFlatNestedPartCount > 0`) |
| **Backlog appendix** | `prioritizedBacklogJvmNames` (**40**) — see appendix (pilot regen vs index-only) |
| **Next (post Wave 10)** | § A.4 Manual Explore only for plan “done”; optional `AUTOPBR_RUN_ASSEMBLY_VIEWPORT_PROBES=1`; full index regen when new lifts land |

---

## Wave 9 landed (2026-05-21)

| Track | Outcome |
|-------|---------|
| **Plan completion audit** | [`plan-completion-audit.md`](generated/plan-completion-audit.md) — five § criteria tagged; Phase 1C `BabyZombieModel` inflate probe **deferred** (reference pre-inflate) |
| **Geometry index drain** | **153** `ok` · **31** `skipped` · **0** `partial` · **0** `fail` — climate (`ColdCow`/`WarmCow`/`ColdPig`) + `PandaModel` + `ColdChickenModel` |
| **ZombieVillager lift** | Adult shard `ok`; catalog **761** strict `RuntimeGeometryIrJson` |
| **§ A.4 `automated_prereq`** | Column **✓** on all canary rows; batch 5 hosts (`EnderDragon`, `Ravager`, `BabyDonkey`, `BabyFeline`) added to checklist |
| **§ A.4 export + playbook** | [`manual-explore-playbook.md`](manual-explore-playbook.md); `export-manual-explore-checklist.ps1` — `-Batch 4C-1`…`4C-5`, markdown table |
| **Wave 8 refresh** | B.3 primary wiring **closed**; index **0 partial**; Phase 1C documented deferred |
| **Backlog appendix** | `prioritizedBacklogJvmNames` (**40**) → regen vs Explore — see appendix below |

---

## Wave 7 landed (2026-05-21)

| Track | Outcome |
|-------|---------|
| **4C batches 4–5** | **54 / 56** promotion dual gate — entity-wide (axolotl, sniffer, rabbit, llama, ocelot), hostile (`EnderDragon`, `Ravager`), nested babies (`BabyDonkey`, `BabyFeline`), feline×4 |
| **Feline LER + T1/T2** | `AbstractFelineModel` / cat×2 partial→ok; viewport green after stem `feline` exclusion |
| **SetupAnim index** | **169 / 169** `ok` (Part C final drain) |
| **Geometry index** | **184** rows; **150** `ok` · **34** `skipped` · **0** `partial` · **0** `fail` (2026-05-21 partial drain) |
| **1.21.11 animation** | **10 / 10** `ok` on jar holders (§ B.2.1) |
| **Catalog IR survey** | Strict **761** `RuntimeGeometryIrJson`, **0** `cleanRoom` (2026-05-21 boat/chest hand-lift); zombie-villager → `ZombieVillagerModel` (`ok`) |

---

## Wave 4 backlog (post–pilot-4C hygiene)

Canonical tracking after **54 / 56** promotion dual-gate pilots (batches 1–5 landed 2026-05-21). No new feature code in hygiene PRs unless a T1 probe fix is trivial (e.g. allowlist-only).

| Track | Remaining | Action |
|-------|-----------|--------|
| **SetupAnim `partial` → `ok`** | **0 / 169** | Complete on 26.1.2 (2026-05-21 Part C final drain); regen `Generate-SetupAnimIndex.ps1` when lifting new models |
| **Animation IR Part B.3** | 16-file set; rows in § B.3 table | CATMULLROM / MIXED / empty-channel backlog (Warden, Sniffer dig, Nautilus scale, Fox adult absent, …) |
| **Preview-deltas** | **12** overlays (§ A.8) | Manual Explore § A.4; rig-accuracy `preview-delta` column in [`rig-accuracy-batches/`](generated/rig-accuracy-batches/) |
| **Geometry index** | **184** classes | Full `Generate-GeometryIndex.ps1` alignment; non-pilot shards outside 56-pilot regen |
| **Manual Explore sign-off** | § A.4 canary rows | Pending for all strict/T1 pilots after screenshot refresh |
| **1.21.11 animation lift** | **10/10 `ok`** (§ B.2.1); 6 holders 26.1.2-only | ~~Shards + index linked in Core csproj~~ (2026-05-21); 1.21.11 profile uses jar-local clips; 26.1.2-only holders still fallback |
| **4C expansion** | **54 / 56** dual gate | Batches 1–5 landed; § A.3.1 optional batches closed — manual Explore only |
| **ZombieVillagerModel lift** | **Done** (2026-05-21) | Adult shard `ok` (humanoid concat + villager head/nose/body); manifest overrides removed |

**Regen commands (hygiene):**

```powershell
pwsh -File tools/regen-assembly-pilots.ps1 -SkipLift   # quality JSON + gates, no shard lift
pwsh -File tools/Generate-SetupAnimIndex.ps1 -ClientJar tools/minecraft-parity/26.1.2/client.jar -VersionLabel 26.1.2 -Parallel -Stats
```

---

## Appendix — `prioritizedBacklogJvmNames` (60)

Flat-nested / composed-preview suspects with `reference*Match` true in [`geometry-lift-quality-26.1.2.json`](generated/geometry-lift-quality-26.1.2.json) (`generatedUtc` 2026-05-26T02:32:22Z). This list is **not** an active lift-failure queue; it preserves Explore/promotion context for flat-offset families. **56-pilot / promotion path:** `pwsh -File tools/regen-assembly-pilots.ps1` (add `-SkipLift` for quality-only refresh). **Index-only** (e.g. `BabyPigModel`, climate variants): `Generate-GeometryIndex.ps1 -Single <fqn>`. **Sign-off:** manual Explore § A.4 when on canary/promotion path ([`manual-explore-playbook.md`](manual-explore-playbook.md)).

| Short name | Official JVM | Regen | Explore / promotion |
|------------|--------------|-------|---------------------|
| QuadrupedModel | `net.minecraft.client.model.QuadrupedModel` | pilot regen | skip 4C (abstract); pattern reference only |
| AdultArmadilloModel | `…armadillo.AdultArmadilloModel` | pilot regen | § A.4 batch 3 |
| ArmadilloModel | `…armadillo.ArmadilloModel` | pilot regen | § A.4 batch 3 |
| BabyArmadilloModel | `…armadillo.BabyArmadilloModel` | pilot regen | § A.4 batch 3 |
| AdultAxolotlModel | `…axolotl.AdultAxolotlModel` | pilot regen | § A.4 batch 4 |
| BabyAxolotlModel | `…axolotl.BabyAxolotlModel` | pilot regen | § A.4 batch 4 |
| AdultCamelModel | `…camel.AdultCamelModel` | pilot regen | § A.4 batch 1 |
| BabyCamelModel | `…camel.BabyCamelModel` | pilot regen | § A.4 batch 1 |
| CamelModel | `…camel.CamelModel` | pilot regen | § A.4 batch 1 |
| CamelSaddleModel | `…camel.CamelSaddleModel` | pilot regen | § A.4 batch 1 |
| BabyCowModel | `…cow.BabyCowModel` | pilot regen | § A.4 batch 3 |
| ColdCowModel | `…cow.ColdCowModel` | index regen | climate cow Explore when promoted |
| CowModel | `…cow.CowModel` | pilot regen | § A.4 batch 1 |
| WarmCowModel | `…cow.WarmCowModel` | index regen | climate cow Explore when promoted |
| AbstractEquineModel | `…equine.AbstractEquineModel` | pilot regen | § A.4 batch 1 |
| BabyDonkeyModel | `…equine.BabyDonkeyModel` | pilot regen | § A.4 batch 5 |
| BabyHorseModel | `…equine.BabyHorseModel` | pilot regen | § A.4 batch 1 |
| DonkeyModel | `…equine.DonkeyModel` | pilot regen | § A.4 batch 1 |
| EquineSaddleModel | `…equine.EquineSaddleModel` | pilot regen | § A.4 batch 1 |
| HorseModel | `…equine.HorseModel` | pilot regen | § A.4 batch 1 |
| AbstractFelineModel | `…feline.AbstractFelineModel` | pilot regen | § A.4 partial→ok batch 5 |
| AdultCatModel | `…feline.AdultCatModel` | pilot regen | § A.4 partial→ok batch 5 |
| AdultFelineModel | `…feline.AdultFelineModel` | pilot regen | § A.4 partial→ok batch 5 |
| AdultOcelotModel | `…feline.AdultOcelotModel` | pilot regen | § A.4 batch 4 |
| BabyCatModel | `…feline.BabyCatModel` | pilot regen | § A.4 partial→ok batch 5 |
| BabyFelineModel | `…feline.BabyFelineModel` | pilot regen | § A.4 batch 5 |
| BabyOcelotModel | `…feline.BabyOcelotModel` | pilot regen | § A.4 batch 4 |
| AdultFoxModel | `…fox.AdultFoxModel` | pilot regen | § A.4 batch 2 |
| BabyFoxModel | `…fox.BabyFoxModel` | pilot regen | § A.4 batch 2 |
| FoxModel | `…fox.FoxModel` | pilot regen | § A.4 batch 2 |
| BabyGoatModel | `…goat.BabyGoatModel` | pilot regen | § A.4 batch 2 |
| GoatModel | `…goat.GoatModel` | pilot regen | § A.4 batch 2 |
| BabyLlamaModel | `…llama.BabyLlamaModel` | pilot regen | § A.4 batch 4 |
| LlamaModel | `…llama.LlamaModel` | pilot regen | § A.4 batch 4 |
| BabyPandaModel | `…panda.BabyPandaModel` | pilot regen | § A.4 batch 3 |
| PandaModel | `…panda.PandaModel` | pilot regen | § A.4 batch 1 |
| BabyPigModel | `…pig.BabyPigModel` | index regen | Explore when promoted (not 56-pilot) |
| ColdPigModel | `…pig.ColdPigModel` | index regen | climate pig Explore when promoted |
| PigModel | `…pig.PigModel` | pilot regen | § A.4 batch 1 |
| BabyPolarBearModel | `…polarbear.BabyPolarBearModel` | pilot regen | § A.4 batch 3 |
| PolarBearModel | `…polarbear.PolarBearModel` | pilot regen | § A.4 batch 1 |
| AdultRabbitModel | `…rabbit.AdultRabbitModel` | pilot regen | § A.4 batch 4 |
| BabyRabbitModel | `…rabbit.BabyRabbitModel` | pilot regen | § A.4 batch 4 |
| RabbitModel | `…rabbit.RabbitModel` | pilot regen | § A.4 batch 4 |
| BabySheepModel | `…sheep.BabySheepModel` | pilot regen | § A.4 batch 3 |
| SheepFurModel | `…sheep.SheepFurModel` | pilot regen | § A.4 batch 3 |
| SheepModel | `…sheep.SheepModel` | pilot regen | § A.4 T1-only (not 4C) |
| SnifferModel | `…sniffer.SnifferModel` | pilot regen | § A.4 batch 4 |
| SniffletModel | `…sniffer.SniffletModel` | pilot regen | § A.4 batch 4 |
| AdultTurtleModel | `…turtle.AdultTurtleModel` | pilot regen | § A.4 batch 3 |
| BabyTurtleModel | `…turtle.BabyTurtleModel` | pilot regen | § A.4 batch 3 |
| TurtleModel | `…turtle.TurtleModel` | pilot regen | § A.4 batch 3 |
| AdultWolfModel | `…wolf.AdultWolfModel` | pilot regen | § A.4 batch 2 |
| BabyWolfModel | `…wolf.BabyWolfModel` | pilot regen | § A.4 batch 2 |
| WolfModel | `…wolf.WolfModel` | pilot regen | § A.4 batch 2 |
| CreeperModel | `…creeper.CreeperModel` | pilot regen | § A.4 batch 1 (canary) |
| EnderDragonModel | `…dragon.EnderDragonModel` | pilot regen | § A.4 batch 5 |
| BabyHoglinModel | `…hoglin.BabyHoglinModel` | pilot regen | § A.4 batch 1 |
| HoglinModel | `…hoglin.HoglinModel` | pilot regen | § A.4 batch 1 |
| RavagerModel | `…ravager.RavagerModel` | pilot regen | § A.4 batch 5 |

Future pilot-only backlog JVMs still use pilot regen + § A.4 when added to the 56-pilot manifest.

---

*Update live metrics when regenerating `geometry-lift-quality-26.1.2.json`, `geometry-index-26.1.2.json`, or pilot regen. Creeper remains geometry regression canary.*
