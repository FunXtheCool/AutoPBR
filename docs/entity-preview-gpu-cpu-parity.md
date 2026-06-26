# Entity preview: GPU vs CPU parity policy

Explore 3D preview uses two tessellation paths for emulated entities (parity-catalog geometry IR and clean-room fallbacks). **Both must produce the same preview-world silhouette** after the 2026-05-28 LER / walk-order / **ModelPart block-stack compose** fixes landed on CPU. GPU skinning is the default draw path; this document is the contract for keeping it aligned.

**Explore sign-off (2026-05-28):** Adult parity-catalog mobs (cow, pig, chicken, climate cows, etc.) no longer show texel-scale part separation once production compose follows the **ModelPart PoseStack policy** (block space, bind offset in row 4 + `Er` upper 3×3, **`local × parent`**). Confirmed via Entity Debug live A/B (anti-**`T × Er`**) and JVM horn placement (`ColdCowModel`). **Baby** attachment edge cases may still diverge on **`worldPose`-only** tests — use cluster / JVM render probes where attached parts rotate under parents.

Related: [runtime-ir-preview-plan.md](runtime-ir-preview-plan.md) (§ PartPose vs ModelPart render, § Canonical LER policy, § Explore scene placement), [vanilla-preview-parity.md](vanilla-preview-parity.md).

Ghast-family direct-lift and padded-atlas contract: [ghast-family-parity.md](ghast-family-parity.md).

---

## Shared mesh source (LER + walk + compose)

| Stage | Policy | Implementation |
|-------|--------|----------------|
| IR walk | **`local × parentWorld`** (row-vector / `Vector3.Transform`) | `GeometryIrMeshWalk.VisitPart` |
| Part compose (production) | **ModelPart block stack**: bind T **÷16**, **`Er` upper 3×3 + bind row 4**, chain **`localBlock × parentBlock`**, row 4 ×16 to texel | `TryComposePartPose` / `TryComposePartRenderLocalBlock` in `GeometryIrMeshEmitter.cs` |
| Part compose (legacy debug) | **`T × Er`** at texel scale — rotates bind offset into wrong axis | `UseLegacyTranslationTimesRotationPartPose` only |
| Reference `worldPose` bake | **`Er × T`** row convention (`PartWorldPoseMath`) — good for **lift quality / origins**, not always cuboid render | `reference-output` `worldPose.translation`; see § Render ground truth below |
| LER fold | Column-root `ApplyLivingEntityRendererColumnRootScale` once post-batch | `FinishGeometryIrMeshLivingEntityRendererBasis` / parity emit |
| Part-tree repair | Ears, head stacks, baby donkey legs — see `GeometryIrPartTreeRepair` | No flat-quadruped leg reparent except documented exceptions |
| setupAnim off | Bind pose only; no emit-time pose overrides | `applyGeometryIrSetupAnimMotion: false`, `wave=0` on emit |

CPU and GPU both call `CleanRoomEntityModelRuntime.TryBuildStaticMesh` with the same flags for a given Explore frame (`EnableEntityAnimation` → `applyGeometryIrSetupAnimMotion`).

### Baby scale contract

Dedicated parity-catalog `Baby*Model` Geometry IR shards are final baby meshes, not adult meshes waiting for the old uniform baby transformer. Resolve scale from the JVM host that `GeometryIrParityJvmResolver` selected: `BabyCowModel`, `BabyFoxModel`, `BabySheepModel`, `BabyDrownedModel`, etc. emit with unit cuboid scale (`BabyProfile.Adult`) even when the diagnostic line shows an unversioned native profile such as `profile=root parsed=?`.

Do not add preview-only origin compaction, per-family part offsets, or GPU-only scale compensation for these cases. CPU bake, GPU bind-pose bake, and Explore placement must all consume the same merged mesh. Regression coverage lives in `BabyCatalogGeometryIrPreviewTests`, `BabyFamilyAttachmentClusterTests`, and `EntityPreviewPlacementTests.Baby_rebake_records_meaningful_part_centroid_diagnostics`.

### Render ground truth (attached / rotated parts)

For **cuboid placement** parity (horns, ears, nested stacks), prefer JVM export fields when present:

- **`renderPartAffines`** — full 4×4 after bind `translateAndRotate` walk (`ModelPartRenderPoseMath.java`)
- **`renderCuboidCenters`** — model texel centroids after that walk

Do **not** assert render placement from **`worldPose.translation` alone** — it uses `PartWorldPoseMath` (Er×T origin bake) and can stay green while Explore cuboids are wrong (e.g. cold-cow horns near body Z while render centers sit on the head cluster).

### Logical geometry atlas contract

Entity texture upload dimensions are not always the UV atlas dimensions used by the Java model. Geometry-IR baking resolves logical dimensions through `EntityGeometryIrTextureAtlas` from the parity manifest and uses them consistently for initial CPU bake, animated CPU rebake, and GPU bind-pose bake. Keep the physical `PreviewTextureMaps.Width/Height` for OpenGL upload.

The ghast family is the regression case: model UVs use `64x32` or `64x64`, while the 26.1.2 jar PNGs are padded to `128x64` or `128x128`. Using physical PNG dimensions during mesh bake can pass pose tests yet render only partial face regions.

---

## CPU path (reference)

1. `MinecraftModelBaker.TryBake(merged, …)` — per-vertex **`W(LocalToParent · r)`** where `W(x) = x/16 − ½`.
2. `EntityPreviewPlacement.ApplyToPreviewVertices` — leg-aware ground contact, XZ anchor, lift **baked into vertex Y**.
3. Draw 12-float interleaved buffer; vertex shader uses positions as-is (no entity branch).

Bind pose and animated pose both rebake the full CPU mesh (`EntityEmulatedPreviewRebaker.TryRebakeMesh`). CPU rebake is the **fallback** when GPU bind prep fails — not the primary anim-on or anim-off display path for parity-catalog entities.

---

## GPU path (must match CPU)

### Bind-pose VBO (once per texture / bind-pose key)

1. `TryBuildStaticMesh(…, animationTimeSeconds: 0, applyGeometryIrSetupAnimMotion: false)` — same bind merged model as CPU bind.
2. `MinecraftModelBaker.TryBakeBindPoseForGpuSkinning` — stores **`LocalToParent · r`** in texel model space (**no `W()`**), plus bit-cast bone index per element.
3. `GpuBindPoseInverseLocalToParent[i] = M_bind[i]⁻¹`.
4. `EntityPreviewPlacement.ApplyToGpuBindVertices` — XZ anchor in mesh space; **`EntityGpuMeshSpaceLiftY`** holds vertical lift (shader applies it after `W()`).

### Vertex shader (`genesis.vert`)

When **`uEntityBindMesh > 0`** (13-float texel bind VBO):

1. If **`uEntityGpuSkinning != 0`**, decode bone index with **`floatBitsToInt(aBoneIndexBits)`**, then `entityPos = bone[bi] × aPos`.
2. Always: **`entityPos.xyz = entityPos.xyz / 16.0 - 0.5`**, then **`entityPos.y += uEntityMeshLiftY`**.

Bone indices are stored as IEEE bit-cast floats in the VBO and read with **`VertexAttribPointer`** (not `VertexAttribIPointer`) so ANGLE/GLES reliably delivers the element slot to the shader.

When **`uEntityPreviewSpaceVerts > 0`**:

- Positions are already in preview space; shader uses `aPos` as-is.

Raw texel coordinates (~16× too large) were the root cause of “exploded” parts when **`uEntityBindMesh`** was not set.

### Animation off (parity-catalog default)

- **`uEntityGpuSkinning = 0`**, **`uEntityBindMesh = 1`**, **`uEntityBoneCount = GpuPreparedBoneCount`**, **`uEntityMeshLiftY`** as plain GLSL uniforms.
- Bind VBO stores **`M_bind·r`**; shader applies **`W()` + lift** without bone multiply.
- **`GpuEntityBoneSkinning = true`**, 13-float VBO — do **not** strip to 12-float or bypass the entity shader branch.

### Animation on

- Same 13-float bind VBO; bones updated each frame via `TryFillEmulatedEntityBoneMatrices` → **`bone[i] = invBind[i] · M_anim[i]`**.
- **`uEntityBindMesh = 1`**, **`uEntityGpuSkinning = 1`** when bone palette uploaded; bone UBO uploaded before draw.
- Live foot lift: `ComputeLiveGpuLiftY` on bind vertices + current bones; lift applied via **`uEntityMeshLiftY`** (not per-frame mesh rebake).

---

## Explore runtime wiring

| Component | Role |
|-----------|------|
| `ResourcePackConverter` | Initial CPU bake + rebake context + CPU placement diagnostics |
| `OpenGlPreviewBackend.Render.PassSetup` | GPU bind VBO prep, animated bone fill, UBO matrix upload |
| `ApplyEntityBoneSkinningUniforms` | Sets `uEntityBindMesh` / `uEntityGpuSkinning` / `uEntityBoneCount` / `uEntityMeshLiftY` on active program |
| `MainWindowViewModel.ApplyExploreParityCatalogPreviewDefaults` | Animation off for catalog textures on load |

On texture switch, `SetBlockModelPreview` clears `GpuPreparedBoneCount` / `GpuBindPoseInverseLocalToParent`; the next frame rebuilds GPU bind state via `TryPrepareGpuSkinnedEmulatedMesh`.

---

## Diagnostics (preview log)

`ParityCatalogEntityPreviewDiagnostics.FormatExplorePlacementLine` prints:

- `gpuSkinning`, `setupAnimMotion`, `animClock`, `liftY`, `contactY`
- `bodyY`, `headY`, `legY` — **preview space** after `W()` + lift

On GL init, `Entity shader init` logs `GetUniformLocation` for `uEntityPreviewSpaceVerts`, `uEntityBindMesh`, `uEntityGpuSkinning`, `uEntityBoneCount`, `uEntityMeshLiftY` on **main and shadow** programs, plus `EntitySkinningBones` UBO block binding.

Each entity draw (main + shadow passes) emits `Entity draw contract` with **expected vs uploaded** scalars:

| Anim | `expectBindMesh` | `expectSkin` | `expectBoneCount` | Shader |
|------|------------------|--------------|-------------------|--------|
| Off | 1 | 0 | `GpuPreparedBoneCount` | `W()` + lift only |
| On | 1 | 1 | snapshot count | `bone × pos`, then `W()` + lift |

`GPU WARN` lines flag **`uEntityBindMesh` mismatch** and anim-on-only failures (palette not uploaded). Uniforms are re-applied immediately before each entity `DrawRange` so grass-pass zeros cannot leak into the mesh draw.

---

## Tests (run before Explore sign-off)

```bash
dotnet test tests/AutoPBR.Core.Tests --filter "BabyFamilyAttachmentClusterTests|EntityGpuSkinnedMatrixCpuParityTests|EntityViewportGpuCpuParityTests|EntityGpuSkinnedBoneIndexTests"
```

| Test | Guards |
|------|--------|
| `EntityGpuSkinnedMatrixCpuParityTests` | `W(v · inv · M_anim)` ≡ CPU bake per vertex |
| `BabyFamilyAttachmentClusterTests` | Body/head/leg clusters after CPU placement **and** GPU bind + `W()` |
| `BabyCatalogGeometryIrPreviewTests` | Dedicated `Baby*Model` IR hosts emit unit cuboid scale, including unversioned `root` profiles |
| `EntityPreviewPlacementTests.Baby_rebake_records_meaningful_part_centroid_diagnostics` | Explore placement diagnostics report body/head/leg centroid data for baby preview regressions |
| `EntityViewportGpuCpuParityTests` | Fast bone fill ≡ `LocalToParent` for parity-catalog IR |
| `EntityGpuSkinnedBoneIndexTests` | Bit-cast bone index decode / slot coverage |
| `GeometryIrQuadrupedReferenceWorldPoseTests` | Catalog static mesh vs Java reference **`worldPose`** (origins; may diverge from render on attached parts) |
| `ColdCowHornPreviewPlacementTests` | Horn cuboids on head cluster + JVM **`renderCenterTexel`** |
| `ModelPartTranslateAndRotateProbeTests` | Block-stack compose vs JVM **`renderPartAffines`** |

---

## Do not regress

1. **`W()` gated on `uEntityBindMesh > 0`** — not on `uEntityGpuSkinning` alone.
2. **Fast pose capture + `invBind · M_anim`** for bones when GPU bind inverse exists — reserve full mesh extract for `EntityGpuBoneFillPolicy.RequiresFullMeshBoneExtract` only (equine + chicken).
3. **`floatBitsToInt(aBoneIndexBits)`** for bone indices — not `(int)boneFloat` or `VertexAttribIPointer` alone.
4. **Identity bone upload on animation off** — bind VBO is sufficient; skinning flag stays 0.
5. **Double lift** — emulated GPU subjects skip `PreviewSubjectPlacement.LiftSubjectIfClipping`; GPU lift only via `uEntityMeshLiftY`.
6. **No CPU preview-space bake or per-frame CPU skin for display** — do not strip anim-off to 12-float, do not rebake/skin on CPU each anim frame when GPU bind prep succeeded. Keep **`GpuPreparedBoneCount`** on rebake ctx across anim toggles.
7. **Production compose = ModelPart block stack** — bind **`Er` + row-4 offset**, **`local × parent`**, block **÷16**. Do **not** revert to legacy **`T × Er`** (Entity Debug toggle only). Er×T **`worldPose`** bake remains valid for lift quality, not a substitute for render affines on horns/ears.
8. **Logical atlas dimensions for Geometry IR** — do not replace manifest atlas dimensions with padded physical PNG dimensions in initial, CPU-rebake, or GPU-bind bake paths.
