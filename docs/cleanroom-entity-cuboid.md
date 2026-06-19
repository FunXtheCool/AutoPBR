# CleanRoom entity cuboid layer

Data-first cuboids sit between bytecode-lifted geometry IR / codegen tables and `RigBuilder.AddBox`. **Lifted IR is authoritative** for static body-layer geometry; legacy code-built rigs remain only when no `ok` shard is packaged.

## 1. Data-first cuboid records (`EntityCuboid`)

**Location:** `CleanRoomEntityShared.cs` — `EntityCuboid`, `EmitCuboids`, `QuadrupedLegCuboidTex016`.

| Field | Meaning |
|-------|---------|
| `X0`…`Z1` | Diagonal corners in **part local space** (same as `RigBuilder.AddBox`) |
| `TexU`, `TexV` | Vanilla `texOffs(u, v)` on the entity atlas |
| `UvSizeW/H/D` | Optional explicit UV footprint; omit (`-1`) to derive from geometry extents |
| `MirrorUv` | Java `mirror()` / geometry IR `mirrorU` |

**Emit:** `cuboid.Emit(builder, parentPose, partScale)` or `EmitCuboids(builder, span, parentPose, partScale)` — forwards to `AddBox` with zero offsets and no local Euler (poses stay on `EntityParityTemplate` chains).

**Invariants:**

- Catalog / parity paths must keep explicit `TexU`/`TexV` (do not rely on `AllocateUvBox` packing).
- Compose `PartPose` only via `EntityParityTemplate` (`Mul`, `T`, `Rx`/`Ry`/`Rz`, `Er`).
- Do not rewrite lifted cuboid corners to match legacy code-built meshes; validate against IR shards or Java reference bakes.

**IR-first models:**

| Builder | File | Notes |
|---------|------|-------|
| `BuildCod` / `BuildSalmon` | `CleanRoomEntityAquatic.cs` | Geometry IR emitter only (`CodModel.json`, `SalmonModel.json`) |
| Parity catalog | `CleanRoomEntityGeometryIrParityCatalog.cs` | IR first; `TryInvokeParityCatalogBuilder` when shard missing or lift rejected |

**Example:**

```csharp
var bodyPose = EntityParityTemplate.T(0f, 22f, 0f);
EmitCuboids(b, [new EntityCuboid(-1f, -2f, 0f, 1f, 2f, 7f, 0, 0)], bodyPose, p.BodyScale);
```

---

## 2. Geometry IR → mesh (runtime emitter)

**Packaged shards:** `Data/minecraft-native/geometry/<versionLabel>/*.json` (copied from `docs/generated/geometry/` at build time).

**Loader:** [`GeometryIrDocumentLoader.cs`](../src/AutoPBR.Core/Preview/GeometryIrDocumentLoader.cs) — `TryLoad` / `TryLoadLiftedOkForParity`.

**Emitter:** [`GeometryIrMeshEmitter.cs`](../src/AutoPBR.Core/Preview/GeometryIrMeshEmitter.cs) — DFS walk of `roots`, `EntityParityTemplate.ComposeEuler`, `EntityCuboid.Emit`.

**Options:** [`GeometryIrMeshEmitOptions`](../src/AutoPBR.Core/Preview/GeometryIrMeshEmitOptions.cs)

| Option | Purpose |
|--------|---------|
| `Fidelity` | `Parity` = exact lifted extents; `Viewport` = optional thicken on zero-extent axes |
| `ResolvePartScale` | Per-part-id baby/body/head/leg scale |
| `TryGetPartPoseOverride` | Animation on top of IR rest pose (e.g. Cod `tail_fin` tail sway) |
| `PreviewDegenerateAxisThickness` | Viewport only: zero-extent IR axes → visible sheet (default `0.08`) |
| `AtlasWidth` / `AtlasHeight` | Atlas dimensions used by the baker when normalizing texel coordinates |

**Baby scale contract (2026-06-19):** Dedicated geometry-IR baby hosts such as `BabyCowModel`, `BabyFoxModel`, `BabyAxolotlModel`, and `BabyArmadilloModel` are already authored as baby meshes. When `GeometryIrParityJvmResolver` resolves a `Baby*Model` host, `ResolvePartScale` must use unit cuboid scale (`BabyProfile.Adult`) even if the native profile label is the unversioned `root` profile. Only adult/shared mesh hosts that vanilla still transforms at render time should receive `VanillaUniformBaby`.

**Scope today:**

- Body layer / rest pose in the emitter — catalog preview animation is a separate pass (e.g. chicken idle drivers).
- **Cod** / **Salmon** use dedicated IR presets (`ForCod`, `ForSalmon`) with `ForCodIrFidelity` / `ForSalmonIrFidelity` for structural tests.

**UV contract learned from Ender Dragon membranes (2026-06-18):**

- Preserve lifted `texOffs(u, v)` exactly in Geometry IR runtime/codegen emit. Do not pre-shift negative `u`/`v` origins to make one model line up; that caused broader entity UV regressions such as cow front/back texture drift.
- Compare suspicious UVs to `javap` of `ModelPart$Cube` and the model factory. Ender Dragon wing membranes are ordinary zero-height `addBox` cuboids with `texOffs(-56, 88/144)`, not a special direction-mask plane.
- Java's visible membrane artwork is on the `DOWN` slot for those zero-height cuboids; `UP` is the neighboring slot. If a lifted zero-height horizontal sheet arrives as `faceMask:["up"]`, preview may add a double-sided `down` face, but it must use the real Java `DOWN` UV rectangle rather than copying `UP`.
- Keep atlas wrapping/normalization in the bake step (`MinecraftModelBaker.NormalizeAtlasTexel`). Entity elements should retain Java texel-space UV bounds, including negative origins, so tests can distinguish raw IR parity from final baked UV samples.

**Tests:** `GeometryIrMeshEmitterTests` — shard load, lifted rest poses, IR-fidelity cuboid extents, viewport thicken from IR zero axes.

---

## 3. Parity-catalog IR rollout

**Entry:** `CleanRoomEntityDispatch.TryDispatchEntityStaticMeshBuild` → `TryBuildParityCatalogMeshFromGeometryIr` (before legacy `ParityCatalogDispatch` switch).

**Status (26.1.2 catalog):** All **761** manifest diffuse paths report `PreviewMeshDriverKind.RuntimeGeometryIrJson` in `ParityCatalogMeshDriverKindSurveyTests` (strict `cleanRoom == 0` gate). CleanRoom builders remain for cohesion tests and non-catalog paths.

**Requirements:**

- Packaged shard with `extractionStatus: ok`, or **`ParityCatalogHandLiftGeometryIrCatalog`** for renderer/block-entity/equipment-overlay hosts without vanilla `*Model` classes.
- **`GeometryIrLiftPolicy`**: cuboids must be `liftKind: exact` (empty `faceMask` cuboids skipped at emit). Approximate lifts fall through to legacy builders.
- Atlas: manifest `geometry_ir_texture_width` / `height`, shard fields, materialized PNG, then `GeometryIrParityAtlasDefaults`.
- JVM resolution: `GeometryIrParityJvmResolver` (hand-lift → equipment map → manifest overrides → climate/baby → pre-restructure → deobf).
- Baby scale: classify by the resolved JVM host, not by `MinecraftNativeProfile` version text. A `Baby*Model` IR shard remains unit scale; adult/shared hosts remain eligible for the legacy uniform baby transformer.

**Emit:** `GeometryIrParityEmitPresetRegistry` + motion tier from `minecraft_26.1.2_geometry_ir_parity_policy.json` (`prefer_ir` or `ir_geometry_preview_anim`). Equipment leggings use part-filtered emit (`body`, `left_leg`, `right_leg`) or the hand-lift `EquipmentHumanoidLeggingsModel` shard.

**Tree repair:** `GeometryIrPartTreeRepair` reparents known flat IR mistakes (Chicken `beak` / `red_thing` under `head`) before emit.

**Diagnostics:** `CleanRoomEntityModelRuntime.ClassifyParityCatalogGeometryIrFailure` mirrors atlas/emit steps for survey taxonomy (`atlas_failed`, `emit_failed:*`, `empty_elements`).

---

## 4. Lift metadata and preview fidelity

IR v2 fields: [`geometry-ir-conventions.md`](generated/geometry-ir-conventions.md). Preview reads **`textureKey`**, **`faceMask`**, `mirrorU`, `inflate`, `uvSpan`, and poses.

`RigBuilder.AddBox` supports partial **`faceMask`**.

---

## 5. Direction-mask cuboids

Vanilla `CubeListBuilder.addBox(FFFFFF, Set<Direction>)` lifts to `faceMask` / `liftKind` per conventions doc.

---

## 6. Compile-time codegen (pilot)

**Output:** `src/AutoPBR.Core/Preview/Generated/GeometryIrEntityCuboidTables.g.cs` via `codegen-entity-cuboids` CLI.

**Rollout:** Cod, Salmon, Chicken pilot tables; runtime builders should consume codegen spans when wired. Keep runtime IR emitter as reference and for models not yet codegen'd.

---

## Related docs and code

- [`geometry-ir-conventions.md`](generated/geometry-ir-conventions.md)
- [`GeometryIrStructuralValidator.cs`](../src/AutoPBR.Tools.GeometryCompiler/GeometryIrStructuralValidator.cs) — strict `ok` shard validation
- [`vanilla-preview-parity.md`](vanilla-preview-parity.md) — preview routing inventory
