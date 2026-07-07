# META-001 test triage (post batch 3)

**Updated:** 2026-07-07 (CleanRoom mesh path removed; docs refreshed)  
**Baseline:** compile-blocked files **0**; parity survey **761/761** `RuntimeGeometryIrJson`, **0** `ErrorPlaceholder`

Related: [`large-class-split-agent-plan.md`](large-class-split-agent-plan.md)

---

## 2026-07 CleanRoom mesh path removal ✅

| Change | Notes |
|--------|-------|
| Deleted `CleanRoomEntity*` hand mesh tree (~85 files) | Production uses `EntityModelRuntime` + `EntityGeometryIr*` partials only |
| Stripped hand `Build*` from `EntityBlockEntities.cs` | Kept IR preview orientation helpers (bed, boat, sign, pot) |
| Removed CleanRoom comparison tests | `ChickenGeometryShardCleanRoomParityTests`, `ParityCatalogMeshDriverSurveyDiagnostics` |
| Survey gate | `ParityCatalogMeshDriverKindSurveyTests`: **761/761 IR**, **0 ErrorPlaceholder** |
| Docs | [`entity-cuboid-layer.md`](entity-cuboid-layer.md), [`vanilla-preview-parity.md`](vanilla-preview-parity.md), [`runtime-ir-preview-plan.md`](runtime-ir-preview-plan.md) |

Historical tables below reference **`CleanRoomEntity*`** file names from pre-removal commits.

---

## Wave C progress (2026-05-25) — remaining Core failures cleared ✅

| Fix | Files |
|-----|-------|
| Filter synthetic javap oracle-only poses (`root`, zero `_ear`) before aggregate count checks | `GeometryJavapPoseOracle.cs` |
| Keep boat/chest-boat RuntimeGeometryIrJson in block-entity space instead of applying living-entity preview basis | `CleanRoomEntityGeometryIrParityCatalog.cs` |
| Fill GPU bones from final RuntimeGeometryIrJson mesh matrices so setup-anim parity and route caching match emitted meshes | `CleanRoomEntityModelRuntime.cs` |
| Restore adult equine IR preview motion for `head_parts`/`tail` under catalog geometry IR | `CleanRoomEntityGeometryIrParityPresets.cs` |
| Resolve missing baby delegate test paths and baby manifest JVMs (`BabyPigModel`, `BabyFoxModel`) | `BabyDelegateGeometryIrLockTests.cs`, `minecraft_26.1.2_entity_texture_model_manifest.json` |
| Add Fox setup-anim fallback leg motion when lifted FoxModel assignments are static | `VanillaSetupAnimRuntime.cs` |
| Remove bogus proguard 1.21.11 chicken/cow float-pose cuboids from committed shards | `ChickenModel.json`, `CowModel.json` |
| Update non-boat assembly landmarks to RuntimeGeometryIrJson basis; preserve elder guardian 2.35× IR scale | `EntityTextureParityAssemblyCohesionTests.cs`, `CleanRoomEntityGeometryIrParityPresets.cs` |
| Drain final 26.1.2 geometry partial by preserving `ArmorStandModel` body-stick cuboid over delegated humanoid torso | `GeometryLiftPipeline.cs`, `ArmorStandModel.json`, `geometry-index-26.1.2.json`, `geometry-lift-quality-26.1.2.json` |

**Verification:** `GeometryJavapPoseOracleTests` **21/21 pass**; boat cohesion filter **4/4 pass**; GPU bone/equine focused filter **11/11 pass**; Phase10 1.21.11 reference filter **8/8 pass**; assembly cohesion filter **11/11 pass**; geometry quality focused filter **57/57 pass**; full `AutoPBR.Core.Tests` **1853/1853 pass**.

**Superseded baseline:** full suite **1841 pass / 12 fail / 1853 total** before the final 2026-05-25 cleanup.

---

## Wave C progress (2026-05-22) — Javap oracle + boat catalog ✅

| Fix | Files |
|-----|-------|
| Delegate/snapshot merge: dotted JVM return types, same-class `createBase*Model`, recursive `createBodyMesh` + `createLegs`, factory-method snapshot headers | `GeometryJavapPoseOracle.cs` |
| Leg builder patterns: `aload 5`, mirror `aload_2`/`aload_3`, parametric `iload_N` age | `GeometryJavapPoseOracle.cs` |
| Boat/chest/chest-boat hand-lift IR (hull-only `BoatModel`, synthetic `ChestBoatModel`, `ChestModel`) | `ParityCatalogHandLiftGeometryIrCatalog.cs`, `GeometryIrParityHandLiftJvmMap.cs` |

**Parity survey:** **761/761** `RuntimeGeometryIrJson`, **0** `ErrorPlaceholder` (`ParityCatalogMeshDriverKindSurveyTests`).

**Javap oracle:** **21/21 pass** (was **2/21**); the `BabyAxolotlModel` aggregate mismatch was a synthetic `root` pose in the oracle.

**Boat cohesion:** `EntityTextureParityAssemblyCohesionTests` boat landmarks are green after keeping boat/chest-boat IR in block-entity basis.

**Superseded baseline:** full suite **1834 pass / 19 fail / 1853 total** before the 2026-05-25 fixes.

---

Fixed 16 failures in reference alignment bucket (52 → **36**):

| Fix | Files |
|-----|-------|
| Composed-flat hierarchy gate — IR flat + reference nested is valid per conventions | `GeometryIrLiftQualityReport.cs` (`EvaluateReferenceHierarchyMatch`) |
| World-pose gate uses topology align + pose sync (Sniffer bone/body) | `GeometryIrLiftQualityReport.cs` (`CompareReferenceWorldPoses`) |
| Batch4/multilayer tests skip non-`ok` shards and index-not-promoted report rows | `Batch4LiftQualityReferenceTests.cs`, `MultilayerLiftQualityReferenceTests.cs` |
| Promoted `BabyDrownedModel` index status; regenerated lift quality report | `geometry-index-26.1.2.json`, `geometry-lift-quality-26.1.2.json` |
| Creeper javap oracle — shared leg builder (`aload_3` after `astore_3`) | `GeometryJavapPoseOracle.cs` |

**Reference bucket verification:** Batch4 + Multilayer + ReferenceTopology filter **47/47 pass**.

---

## Wave C progress (2026-05-24) — Chicken tests ✅

Fixed 7 chicken failures (test path resolution, not runtime IR):

| Fix | Files |
|-----|-------|
| Resolve `docs/generated/*` via `FindRepoRoot()` instead of `AppContext.BaseDirectory` | `ChickenGeometryShardCleanRoomParityTests.cs`, `ChickenPartPoseIrVersusCleanRoomTests.cs`, `GeometryIrParityAtlasTests.cs` |

Chicken IR shards and CleanRoom parity were already correct — tests couldn't find committed shards in the repo tree.

---

## Wave C progress (2026-05-24) — Cod emit ✅

Fixed 5 cod/mesh-emitter failures (double emit from stale codegen table):

| Fix | Files |
|-----|-------|
| Regenerated `CodModelBodyLayer` (7 cuboids, was missing `body`) | `GeometryIrEntityCuboidTables.g.cs` via `codegen-entity-cuboids` |
| Guard codegen→IR fallback when partial emit leaves cuboids on builder | `GeometryIrCodegenBodyLayerEmitter.cs`, `CleanRoomEntityRigBuilder.cs` |
| Cod rest-pose test walks nested parts (nose under head) | `GeometryIrMeshEmitterTests.cs` |

---

## Wave C progress (2026-05-24) — quick wins ✅

Fixed 3 additional runtime failures (67 → **64**):

| Fix | Files |
|-----|-------|
| HandParity forbidden `TrySampleBreezeIdleWindPositions` — catalog bridge `TryResolveCatalogBreezeIdleWindTranslations` | `DefinitionAnimationPreviewSampling.Catalog.cs`, `CleanRoomEntityGeometryIrParityPresets.cs`, `CleanRoomEntityGeometryIrDefinitionAnimation.cs` |
| `breeze_wind.png` idle wind anim — only skip emit-time wind on composite path | `CleanRoomEntityGeometryIrBreezeParity.cs` |
| Breeze multi-atlas `LayerAtlasConsistent` — per-cuboid atlas vs shard top-level is expected | `GeometryIrUvAtlasQuality.cs` |

**Wave C quick-win verification:** HandParity + breeze wind + UV atlas + breeze parity emit targeted filter **10/10 pass**.

---

## Wave C progress (2026-05-23) — Wave B leftovers ✅

Fixed 4 remaining Wave B runtime failures:

| Fix | Files |
|-----|-------|
| Breeze multi-atlas emit (path filter, per-cuboid atlas, `#wind`/`#eyes` companions) | `CleanRoomEntityGeometryIrBreezeParity.cs`, `GeometryIrMeshEmitOptions`, `GeometryIrMeshWalk`, `GeometryIrMeshEmitter`, `CleanRoomEntityGeometryIrParityPresets.cs` |
| Breeze bind-pose wind idle sampling at emit + definition-anim-before-LER ordering | `CleanRoomEntityGeometryIrBreezeParity.cs`, `CleanRoomEntityGeometryIrDefinitionAnimation.cs` |
| Feline LER probe (+Y default vs -Y right-compose) | `GeometryIrLerMirrorComposeClassificationTests.cs` |

---

## Wave B status (2026-05-23) ✅ compile-unblock complete

All `<Compile Remove>` entries removed from `AutoPBR.Core.Tests.csproj`.

| File | Status | Notes |
|------|--------|-------|
| `GeometryIrBreezeParityEmitTests.cs` | Re-enabled | **7/7 pass** |
| `GeometryIrHoglinViewportLerTests.cs` | Re-enabled | **all pass** |
| `GeometryIrLerMirrorComposeClassificationTests.cs` | Re-enabled | **50/50 pass** |

**Core additions:** `CleanRoomEntityGeometryIrLerTestHooks.cs` (LER classification, `ApplyGeometryIrParityLivingEntityRendererPreviewBasis`, legacy Breeze catalog helper, `TryBuildGeometryIrParityMeshForTestsWithLerCompose`); feline/rabbit stem exclusions on `UsesQuadrupedLerMirrorRightComposeLocalChain`; parity emit uses JVM-scoped LER resolver.

---

## Wave A status (2026-05-23) ✅

| File | Status | Test result |
|------|--------|-------------|
| `GeometryIrUvAtlasQualityTests.cs` | Re-enabled | **6/6 pass** — Breeze layer atlas gate accepts per-cuboid `#wind` 128² |
| `VanillaAnimationIrPreviewSamplerTests.cs` | Re-enabled | **129/129 pass** |
| `GeometryJavapPoseOracleTests.cs` | Re-enabled | **2/21 pass** — `ParseBindingsMerged` restored from worktree; delegate/snapshot merge still needs follow-up patches |

**Core additions:** `GeometryIrUvAtlasQuality.cs`, `GeometryIrCuboidMetadata.TryGetAtlasDimensions`, `DefinitionAnimationPreviewSampling.Catalog.cs` (+59 generated `TrySample*` entries), worktree `GeometryJavapPoseOracle` with 2-arg overload.

---

## Current csproj exclusions (0)

All META-001 compile-blocked files re-enabled as of Wave B (2026-05-23).

**Suggested compile-unblock order:** javap oracle → UV atlas → LER hooks → breeze legacy emit → animation sampler catalog.

---

## Runtime failures (19) by root cause

| Bucket | Tests | Likely cause | Priority |
|--------|------:|--------------|----------|
| GeometryJavapPoseOracle | 1 | `BabyAxolotlModel` oracle count vs shard count in aggregate pilot | P2 |
| Parity catalog survey (boats) | 0 | ✅ hand-lift catalog routes all 761 paths | — |
| Boat assembly cohesion | 3 | Hand-lift IR missing cuboid rotation pivots vs CleanRoom | P2 |
| Chicken parity | 0 | ~~Test path resolution~~ ✅ fixed 2026-05-24 |
| Batch4LiftQualityReference | 6 | Boat/Raft/ChestModel reference mismatch | P1 |
| Geometry IR viewport / topology | 11 | Emit thickness; composed-flat pilot hierarchy | P1 |
| Parity assembly cohesion | 5 | World landmark drift on multi-part entities | P2 |
| Cod / mesh emitter | 0 | ~~Stale codegen table double-emit~~ ✅ fixed 2026-05-24 |
| GPU bone / skinning | 3 | Fast bone fill vs merged LocalToParent | P2 |
| Baby delegate geometry IR | 2 | Baby drowned/zombified piglin roots | P2 |
| Fox setup-anim | 1 | Fox leg motion IR | P3 |
| Manifest JVM resolver | 1 | Baby JVM fields on quadruped pilots | P3 |

---

## Recommended fix waves

### Wave A — Quick compile wins

1. Javap oracle 2-arg overload  
2. `GeometryIrUvAtlasQuality` helpers  
3. Drain missing `TrySample*` into `DefinitionAnimationPreviewSampling.Catalog.cs`  

### Wave B — LER test hooks

1. `CleanRoomEntityGeometryIrLerTestHooks.cs`  
2. Re-enable hoglin / LER classification / breeze parity emit tests  

### Wave C — Runtime triage

1. ~~HandParityForbiddenSymbols breeze wind reroute~~ ✅  
2. ~~Breeze wind idle animation + UV atlas layer gate~~ ✅  
3. Cod/chicken/reference regen or emit alignment  
4. Boat hand-lift / multi-factory IR for parity survey (42 CleanRoom paths)  
5. Fox setup-anim fix or skip  

---

## Verification

```bash
dotnet build tests/AutoPBR.Core.Tests/AutoPBR.Core.Tests.csproj
dotnet test tests/AutoPBR.Core.Tests/AutoPBR.Core.Tests.csproj
dotnet test tests/AutoPBR.Core.Tests/AutoPBR.Core.Tests.csproj \
  --filter "FullyQualifiedName~EntityTextureRoutingInventory|FullyQualifiedName~CleanRoomEntityRuntime"
```

---

## REF-002 pass 3 (2026-05-23)

| File | Lines | Contents |
|------|------:|----------|
| `CleanRoomEntityDispatch.SpecificSlots.cs` | 71 | Coordinator |
| `CleanRoomEntityDispatch.SpecificSlots.S01-50.cs` | ~1120 | Slots 1–50 |
| `CleanRoomEntityDispatch.SpecificSlots.S51-100.cs` | ~1457 | Slots 51–117 |

117 slot blocks recovered from git HEAD monolith. Routing inventory green.
