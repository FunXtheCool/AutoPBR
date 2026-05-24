# Geometry IR conventions

This file documents how to read [`schema/geometry-ir.schema.json`](schema/geometry-ir.schema.json) shards under `geometry/<versionLabel>/` and the manifest [`geometry-index-<versionLabel>.json`](geometry-index-26.1.2.json).

## Coordinate system

- **Cuboid `from` / `to`**: axis-aligned bounds in **Minecraft model part local space** (same convention as `ModelPart.addBox` / `CubeListBuilder.addBox` before renderer-only transforms such as `LivingEntityRenderer` scale).
- Axes: **+X** right, **+Y** up, **+Z** forward (Minecraft model / `ModelPart` space used in vanilla entity models).

## Pose

- **`translation`**: three floats, translation applied in the same space as vanilla `PartPose.offset` (before child cuboids).
- **`rotationEulerRad`**: three floats **in radians**, applied in order given by **`eulerOrder`** (intrinsic / chained application matches AutoPBR `EntityParityTemplate` usage: `Mul(T, Er(x)*Er(y)*Er(z))` style compositions — document per-class if a part uses `offsetAndRotation` with a different convention).
- Default `eulerOrder` for quadruped-style rigs in committed shards: **`XYZ`** (rotate X, then Y, then Z on the composed local frame unless noted).

## UV

- **`uvOrigin`**: `[u, v]` integer texel coordinates for `texOffs(u, v)` (upper-left of the face region in the entity atlas), not normalized UV.

## extractionStatus

| Value | Meaning |
|-------|---------|
| `ok` | IR is complete for preview: either a **javap structural lift** produced a non-empty cuboid tree (`profile` notes name the mesh host class), or the **cow** hand template is kept and a successful `javap` float probe (π/2 in `createBodyLayer`) corroborates expected bytecode. |
| `partial` | Some geometry or names could not be recovered; see `extractionNotes`. |
| `skipped` | Index-only: `.class` bytes could not be read from `client.jar` (missing entry or mapping mismatch). |
| `heuristic` | Bytecode float probe found float constants in the resolved mesh factory path, but no structural lift ran or succeeded (placeholder roots, lift pattern mismatch, or authored shard skipped for lift). |

## Bytecode extraction caveats (26.x javap mesh lift)

AutoPBR.Tools.GeometryCompiler lifts **`CubeListBuilder.texOffs` / `addBox` / `PartPose`** from **`javap -c`** when the bytecode matches Mojang’s common **26.1.2-style** mesh factories. Treat lifted shards as **best-effort parity**, not a formal semantics of the JVM.

- **Pattern coverage:** Only call shapes the parser recognizes (including `addBox(FFFFFF)`, mirrored `addBox(String, FFFFFF)`, and `addBox(..., CubeDeformation)`). Other `CubeListBuilder` or `PartDefinition` patterns, or models built without this style, stay **`partial`** / **`heuristic`**.
- **Part id (`ldc` child name):** The lifter resolves `PartDefinition.addOrReplaceChild`’s first argument from the **`ldc // String …` immediately before that call** (javac’s usual layout). If that is missing, it falls back to the **`ldc` before `CubeListBuilder.create`** (older layout). Mismatches here skip the segment.
- **Mesh host resolution:** The tool tries the listed official `*Model` class first, then common companions in the **same package** (`Adult*`, `Baby*`, `Cold*`, `Warm*`). Classes whose factories live under a **different** package naming pattern may not be found unless reached by delegation (below).
- **Delegated factories:** If `createBodyLayer` only forwards to another class via **`invokestatic` returning `MeshDefinition`**, `ConcatMeshFactoryCodeDeep` **iteratively** follows those targets (and same-class helpers) until no new mesh factories appear, with island boundaries between merged blocks. Indirect chains via fields, instance helpers, or non-`MeshDefinition` returns are still not expanded.
- **Authored shards:** Shards that already look like a real part tree (non-placeholder parts or cuboids) are **not overwritten** by the lifter (e.g. hand-maintained **Cow** geometry).
- **Obfuscated jars:** Lift quality depends on `javap` comments and mapping consistency; ProGuard builds may differ from named 26.x output.
- **Cost:** Full batch regeneration runs **`javap` per candidate host** until a mesh-bearing disassembly is found; expect many subprocesses and minutes of wall time. The geometry compiler **deduplicates `javap` stdout** in a process-wide cache, reuses one **deep mesh concat** for both float probe and lift when possible, and supports **`--parallel` / `--max-parallelism`** for batch class processing (optional **`--stats`** prints invocation and cache counters to stderr). The PowerShell helper `tools/Generate-GeometryIndex.ps1` can pass **`-Parallel`**, **`-MaxParallelism`**, **`-Quiet`**, and **`-Stats`**.

## Version pins

Geometry shards are **not interchangeable** between `versionLabel` values. Regenerate when bumping pins in [`vanilla-preview-parity.md`](../vanilla-preview-parity.md).

## Geometry IR schema v2 (`schemaVersion: 2`)

Committed shards use **`liftKind`** on each cuboid instead of encoding fidelity only in **`provenance`**:

| `liftKind` | Meaning |
|------------|---------|
| `exact` | Box corners and faces match lifted bytecode (including parsed direction masks). |
| `direction_mask_full_box` | `addBox(…, Set)` present but the mask could not be parsed; `from`/`to` are still the six-float box. |
| `tex_crop_static` | texCrop-style `addBox` with explicit `uvSpan`. |
| `unknown` | Unclassified overload. |

Optional fields:

- **`faceMask`**: `north`…`down` when a direction `Set.of` was parsed; an **empty array** means zero faces (cuboid omitted at preview emit).
- **`liftWarnings`**: machine codes (`direction_mask_unparsed_set`, `cube_deformation_obf_inferred`, `unknown_fload_zeroed`, …).
- **`liftSummary`** (document): `cuboidApproxCount`, `poseApproxCount`, `delegationDepth`.

Migrate legacy v1 shards in-repo: `dotnet run --project src/AutoPBR.Tools.GeometryCompiler -- migrate-v2 [versionLabel…]`.

## Lift-quality gates (`geometry-lift-quality-*.json`)

Index-wide report from `GeometryIrLiftQualityReport` (regenerate via `AUTOPBR_WRITE_GEOMETRY_LIFT_QUALITY`; see [test guidance](../test-guidance-geometry-animation-ir.md)):

| Field | Meaning |
|-------|---------|
| `suspectedFlatNestedPartCount` | Known parent/child pairs (e.g. `body`+`left_front_leg`) that are **both** root siblings — often 4 on quadrupeds when legs should be under `body`. |
| `referenceHierarchyMatch` | **false** when IR nests legs under `body` but reference uses flat root siblings, when `suspectedFlatNestedPartCount > 0` without a documented bake, or when `extractionNotes` report unresolved `addChild` binding. **true** for vanilla flat quadrupeds when IR and reference both use flat root siblings (`UsesVanillaFlatQuadrupedLegBake`). **true** for **composed-flat** lifts (axolotl, baby donkey, rabbit, sniffer, ender dragon): IR keeps `body` + quadruped legs at root while `reference_java` nests legs — gate passes when reference contains quadruped leg parts and there is no binding gap. `referenceWorldPoseMatch` uses `GeometryIrReferenceTopologyAlign` before the composed walk. |
| `extractionBindingGap` | **true** when notes mention `missing addChild`, `unresolved addChild`, or real `addChild binding` failures — **not** the benign `"No PartDefinition … addChild binding lines found"` message used for flat `addOrReplaceChild`-only factories. |
| `assemblyGatePass` | Composite: hierarchy + no binding gap + reference cuboid/pose/mesh/world gates + javap pose oracle (pilots). |

Pilot hierarchy expectations (nested vs flat vs binding_gap): [`runtime-ir-preview-plan.md` § A.3](../runtime-ir-preview-plan.md#a3-pilot-gate-tables-short-names) (full table: [`archive/phase-1a-pilot-hierarchy-expectations.md`](../archive/phase-1a-pilot-hierarchy-expectations.md)).

## Tests

When adding or changing geometry IR tests, follow [test-guidance-geometry-animation-ir.md](../test-guidance-geometry-animation-ir.md) (tiers, allowlists, promotion). Runtime-IR preview plan: [`runtime-ir-preview-plan.md`](../runtime-ir-preview-plan.md).

## CleanRoom preview consumption

Static body-layer geometry uses the **`EntityCuboid`** record plus optional **`GeometryIrMeshEmitter`** ([`GeometryIrMeshEmitter.cs`](../../src/AutoPBR.Core/Preview/GeometryIrMeshEmitter.cs)) — see [`cleanroom-entity-cuboid.md`](../cleanroom-entity-cuboid.md). Parity catalog calls **`GeometryIrLiftPolicy`** and rejects shards whose cuboids are not `exact` (except allowlisted `tex_crop_static`). **Cod** / **Salmon** are IR-only builders; parity catalog tries generic IR emit before legacy code-built `Build*` fallbacks.
