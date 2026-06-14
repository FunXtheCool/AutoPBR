# Dolphin Explore 3D preview: dislocated fins (parity-catalog upload path)

**Labels:** `bug`, `preview`, `geometry-ir`, `parity-catalog`, `needs-investigation`

**Minecraft version:** 26.1.2 (`net.minecraft.client.model.animal.dolphin.DolphinModel`)

**Texture:** `assets/minecraft/textures/entity/dolphin/dolphin.png`

---

## Summary

In Explore → 3D preview, dolphin **pectorals/dorsal fin render detached** from the body (three axis-aligned boxes floating above the hull). This persisted across many App-layer fix attempts. Investigation shows **26.1.2 geometry IR and CPU rebake geometry are correct**; the live bug is in the **OpenGL mesh upload / subject lifecycle**, not IR lift for this tier.

---

## Expected behavior

- Bind-pose dolphin mesh: body + `back_fin`, `left_fin`, `right_fin` attached with javap column compose `parent × (T × Er)`.
- Explore parity-catalog path (animation off): one `TryRebakeMesh` commit → GPU VBO with 192 verts / 288 indices.
- Log should **not** show pack-converter or unit-cube fallback uploads after commit.

## Actual behavior (pre-fix)

- Body/head on grid; three fin cuboids floating at wrong world positions.
- Log often showed:
  - `Parity-catalog CPU bind-pose mesh: verts=192, indices=288` (rebake succeeded)
  - Then **`Fallback mesh upload used (BlockModel)`** or silent pack upload (no log)
- `Draw ready: ... frame.Scene=BlockModel` is **normal** (scene kind enum) — not the fallback indicator.

---

## Investigation findings (verified in repo)

### Geometry IR / javap (26.1.2) — **not the root cause**

| Part | Translation | Rotation (rad) |
|------|-------------|----------------|
| `back_fin` | (0,0,0) | Rx π/3 |
| `left_fin` | (2,-2,4) | Rx π/3, Rz 2π/3 |
| `right_fin` | (-2,-2,4) | Rx π/3, Rz -2π/3 |

- Shard: `docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.dolphin.DolphinModel.json`
- Reference: `tools/MinecraftGeometryReference/reference-output/.../DolphinModel.json`
- Policy: `GeometryIrEmitPolicy.UsesColumnTranslationTimesRotationPartPoseJvm` (DolphinModel / BabyDolphinModel)
- Hand builder: `CleanRoomEntityAquatic.Build.DolphinAxolotlFrog.cs`

**Tests passing (Core):** `DolphinPreviewAttachmentTests` (8), `AttachedPartWorldMatrixParityTests` including catalog mesh vs reference_java with column compose.

**Contrast:** 1.21.11 dolphin shard shows real lift corruption (rotation → translation). User runs 26.1.2 where shard is clean.

### Live preview bug — **pack mesh clobbering rebaked VBO**

1. `GlRenderPassSetup` reset `UploadedLiveEntityAnim = false` every frame.
2. When parity-catalog CPU bind was committed, `TryCommit` was skipped but `meshDirty` could still trigger **generic fallback**.
3. **Silent `cpuPlaced` branch** uploaded pack-converter CPU mesh when `EntityPreviewPlacementApplied=true` (no diagnostic).
4. **`TryCommitParityCatalogCpuBindPoseMesh`** could fall back to incoming pack mesh if `TryRebakeMesh` failed, while still logging success.

Pack-converter mesh can match rebake in unit tests, but uploading the wrong buffer / racing with indices produces the “floating fin boxes” symptom.

### Fixes landed on `main` (needs live verification)

- `OpenGlPreviewBackend.Render.PassSetup.cs`: block entity-emulated fallback; remove pack fallback in `TryCommit`; re-sync committed CPU VBO when `meshDirty`.
- `OpenGlPreviewBackend.cs`: parity-catalog subject reuse; stable `BuildParityCatalogCpuBindCommitKey`.
- `GlPbrPreviewControl.cs`: empty scene mesh for emulated entities (no pack geometry in `frame.Scene.Meshes`).
- `OpenGlPreviewBackend.Lifecycle.cs`: `uEntityPreviewSpaceVerts=1` for CPU placed meshes.

---

## How to reproduce

1. Open AutoPBR Explore, load `minecraft-26.1.2-client.jar`.
2. Select `assets/minecraft/textures/entity/dolphin/dolphin.png`.
3. 3D preview, animation off, Entity Debug optional.

## Success criteria after fix

Log should show:

```
[3D preview] Parity-catalog CPU bind-pose mesh: verts=192, indices=288
[3D preview] Draw ready: indexCount=288, subject=parity-cpu-rebake, frame.Scene=BlockModel, ...
```

Must **not** show:

- `Fallback mesh upload used (BlockModel)`
- `Mesh upload: pack-converter CPU subject`
- `Entity mesh upload deferred` (persisting every frame)
- `Parity-catalog CPU bind-pose rebake failed`

---

## Key files

| Area | Path |
|------|------|
| Render pass / fallback | `src/AutoPBR.App/Rendering/OpenGL/OpenGlPreviewBackend.Render.PassSetup.cs` |
| Subject reuse | `src/AutoPBR.App/Rendering/OpenGL/OpenGlPreviewBackend.cs` |
| Shader uniforms | `src/AutoPBR.App/Rendering/OpenGL/OpenGlPreviewBackend.Lifecycle.cs` |
| Rebake | `src/AutoPBR.Core/Preview/EntityEmulatedPreviewRebaker.cs` |
| IR shard | `docs/generated/geometry/26.1.2/...DolphinModel.json` |
| Tests | `tests/AutoPBR.Core.Tests/DolphinPreviewAttachmentTests.cs` |

---

## Suggested next steps for reviewer (ChatGPT / human)

1. **Confirm live fix:** rebuild App, restart, verify fins attached + log criteria above.
2. If still broken with `subject=parity-cpu-rebake`: capture Entity Debug uniforms (`uEntityPreviewSpaceVerts`, `uEntityBindMesh`, `uEntityGpuSkinning`).
3. **Do not** chase 26.1.2 IR lift unless tests regress — focus App upload invariants.
4. Optional: App-level test simulating `SetBlockModelPreview` re-push + render pass to lock “no pack/unit-cube clobber”.
5. If rebake fails in App but passes in Core tests: diff `EntityEmulatedPreviewRebakeContext` (native root dir, materials count, `OrderedTextureZipPaths`).

---

## Commands

```bash
dotnet test tests/AutoPBR.Core.Tests/AutoPBR.Core.Tests.csproj --filter "FullyQualifiedName~DolphinPreview"
dotnet build src/AutoPBR.App/AutoPBR.App.csproj
```
