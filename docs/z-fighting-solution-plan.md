# Z-fighting solution plan

**Status:** Proposed implementation plan  
**Created:** 2026-06-14  
**Scope:** Explore 3D / Genesis OpenGL preview, especially entity parity-catalog meshes with layered or nearly coplanar body parts.  
**Example:** `assets/minecraft/textures/entity/wandering_trader/wandering_trader.png` on `net.minecraft.client.model.npc.VillagerModel`, where green under-robe pixels intermittently win over the blue outer layer on the back.

---

## Problem statement

AutoPBR currently draws entity preview meshes as one uploaded index buffer with `PreviewDrawBatch` ranges keyed by material index only. In the main scene pass, each batch is drawn with depth testing enabled, `GL_LEQUAL`, and depth writes enabled. That is correct for ordinary opaque geometry, but it gives the GPU no stable answer when two surfaces land on the same or almost the same depth value.

The screenshot is the classic layered-coplanar case: a visual overlay or shell is intended to sit above a base surface, but the depth buffer sees both as equal or too close to distinguish consistently. Camera motion and small numerical differences then make the base color pop through the overlay.

The solution should not be a global "turn off depth" fix. It must preserve:

- ordinary block/item depth behavior;
- entity self-occlusion;
- cutout alpha behavior;
- shadow-map stability;
- runtime-IR / vanilla parity as the source of geometry truth.

---

## Research summary

Useful external references:

- OpenGL `glPolygonOffset` offsets polygon fragment depth before depth testing and depth writes; the OpenGL reference calls out decals and highlighted surfaces as intended uses: <https://docs.gl/gl4/glPolygonOffset>.
- The OpenGL Wiki notes that poor depth precision is strongly affected by the `zFar / zNear` ratio, especially when `zNear` is too close to zero, and that coplanar primitives commonly cause Z fighting: <https://wikis.khronos.org/opengl/Depth_Buffer_Precision>.
- LearnOpenGL describes Z fighting as two closely aligned triangles switching apparent order because depth precision cannot distinguish them, and lists three common mitigations: separate geometry, push the near plane out, and use higher precision depth buffers: <https://learnopengl.com/Advanced-OpenGL/Depth-testing>.
- Microsoft Direct3D depth-bias docs describe the same family of solutions as constant plus slope-scaled depth bias, and warn that large slope bias at grazing angles can introduce new artifacts: <https://learn.microsoft.com/en-us/windows/win32/direct3d11/d3d10-graphics-programming-guide-output-merger-stage-depth-bias>.
- NVIDIA's depth precision write-up explains why perspective depth is nonlinear and why reversed-Z plus floating-point depth is a strong general precision upgrade, but that is a renderer-wide change rather than a targeted layer fix: <https://developer.nvidia.com/blog/visualizing-depth-precision/>.

Takeaway for AutoPBR: camera/depth precision tuning is good hygiene, but the wandering-trader symptom is best treated as a layered-surface ordering problem. Use explicit layer metadata, render ordering, and narrowly scoped depth bias. Use geometry inflation only when the vanilla model or IR says the surface is truly an outer shell.

---

## Option fit

| Method | Fit for AutoPBR | Notes |
|--------|------------------|-------|
| Tighten near/far planes | Good baseline | `PreviewCamera` currently defaults to near `0.1`, far `100`, which is broad for a small preview subject. Helps precision but will not reliably solve intentional coplanar layers. |
| Higher precision depth target | Medium | Helpful if the current framebuffer is 16/24-bit in some contexts. Add diagnostics first; not sufficient for same-depth layers. |
| Reversed-Z | Long-term | Good general renderer improvement, but it touches projection, depth clear, depth funcs, shadow/godray/volumetric assumptions, and tests. Too broad for this bug. |
| `glPolygonOffset` per overlay batch | Best first fix | Directly targets decal/overlay use. Must be per batch, not global. |
| Disable depth writes for overlays | Good with bias | Draw base first, then overlays with depth test on and depth mask off. This lets base geometry occlude overlays while preventing overlay batches from polluting later depth. |
| Stencil/decal pass | Later | Useful if we add projected decals or need strict single-hit overlay rules. Likely too much machinery for vanilla entity layers. |
| Geometry expansion / shell inflation | Required fallback | Correct for true vanilla `CubeDeformation` shells, armor, wool/fur, and overlay models. Risky if applied blindly to thin sheets or parts that should remain coincident. |
| Texture/color preprocessing | Reject | Hides symptoms, breaks parity, and does not solve depth instability. |

---

## Recommended design

### 1. Add explicit preview layer metadata

Extend the mesh/batch pipeline so draw ranges carry layer intent, not only material:

- Add a small layer policy model near `PreviewDrawBatch`, for example:
  - `PreviewDepthLayerKind.Base`
  - `PreviewDepthLayerKind.CutoutOverlay`
  - `PreviewDepthLayerKind.CosmeticOverlay`
  - `PreviewDepthLayerKind.EmissiveOverlay`
  - `PreviewDepthLayerKind.DebugOnly`
- Add fields such as:
  - `DrawOrder`
  - `DepthBiasStep`
  - `DepthWrite`
  - `ShadowMode`
- Keep the existing `PreviewDrawBatch(int first, int count, int material)` path source-compatible by giving new fields defaults.

Initial default policy:

| Kind | Main pass | Shadow pass |
|------|-----------|-------------|
| `Base` | depth test on, depth write on, no offset | draw normally |
| `CutoutOverlay` | draw after base, depth test on, **depth write on** (biased depth for post/volumetric occlusion), small negative polygon offset | usually ignore, unless the layer is physical |
| `CosmeticOverlay` | draw after base, depth test on, **depth write on** (biased depth), negative polygon offset by step | ignore by default |
| `EmissiveOverlay` | draw late, depth test on, **depth write on** (biased depth), bias like overlay | ignore or draw unlit only if needed later |
| `DebugOnly` | debug path only | ignore |

Why this shape: the base mesh remains the depth authority. Overlay fragments still fail when hidden behind nearer geometry, but same-surface ties are decided by the overlay pass rather than by undefined precision noise.

### 2. Preserve and propagate element-level layer intent

`MergedJavaBlockModel.Elements` is the natural place to carry element role before baking. Add optional internal metadata to `ModelElement` rather than trying to infer everything after vertices are flattened.

Suggested element metadata:

- `PartId` or existing part-id mapping when available.
- `RenderLayerKind` / `PreviewDepthLayerKind`.
- `LayerOrdinal`.
- `CastsShadow`.
- Optional `ShellInflateTexels` for geometry-space fixes.

Emit this from:

- geometry IR mesh emission (`GeometryIrMeshEmitter` and parity-catalog paths);
- hand clean-room builders where layer intent is known;
- `MergeEntityPreviewMeshes` helpers that combine multi-layer models such as Breeze;
- equipment/armor builders;
- vanilla-layer catalog entries once renderer-state lifting grows.

Use material path heuristics only as a temporary bridge:

- paths containing `/overlay`, `_overlay`, `_eyes`, `emissive`, `glow`, `profession`, or `profession_level` can become overlay candidates;
- villager / wandering trader / zombie villager robe and profession layers should get explicit part-based policy instead of relying only on names;
- base diffuse entity texture remains `Base`.

### 3. Sort and split batches by layer policy

`MinecraftModelBaker.TryBake` currently closes batches only when material changes. Change batching to close when either material or layer policy changes.

Sort draw batches into stable order before upload or before draw:

1. base opaque/cutout body;
2. physical shells that should write depth;
3. cosmetic cutout overlays, increasing `DepthBiasStep`;
4. blended overlays, if `PreviewEntityAlphaMode.Blend` is active;
5. debug overlays.

Do not reorder individual triangles inside a batch. Keep contiguous ranges by material and policy for low churn.

### 4. Apply scoped OpenGL polygon offset in the main pass

In `OpenGlPreviewBackend.Render.PassScene.cs`, wrap only overlay draw calls with polygon offset state:

- enable `GL_POLYGON_OFFSET_FILL`;
- use a small negative offset for standard OpenGL `GL_LEQUAL` depth, because smaller depth is closer to the camera;
- start conservative: step 1 = `factor -0.25`, `units -1`; step 2 = `factor -0.5`, `units -2`;
- keep depth testing enabled;
- set `DepthMask(false)` for cosmetic overlays;
- restore polygon offset, depth mask, blend, and cull state after each policy group.

Important: bias direction should be behind a named helper and diagnostic. If a backend switches to reversed-Z later, the sign flips.

### 5. Keep shadows stable

In `OpenGlPreviewBackend.Render.PassShadow.cs`, do not blindly repeat cosmetic overlay batches.

Default rule:

- `Base`: draw into shadow map.
- physical shell layers: draw only if their geometry is truly separated or marked `CastsShadow`.
- cosmetic overlays / eyes / profession paint / debug: skip in shadow pass.

This prevents the color pass from becoming stable while the shadow map still self-fights or adds a double-thick silhouette.

### 6. Add geometry inflation where parity requires it

Depth bias is a rendering policy, not a geometry substitute.

Use geometry inflation for:

- vanilla `CubeDeformation` and `extend` shells;
- wool, fur, armor, sleeves, pants, villager robes, profession layers, and other modeled outer surfaces;
- cases where a layer should be visibly offset from base geometry at all angles and cast a physical silhouette.

Do not use blind uniform scaling. Use local cuboid expansion or vertex normal expansion in entity texel space, with per-part constraints:

- cuboid shells: expand `From`/`To` in local cuboid space by the vanilla deformation amount;
- face-only sheets: avoid expansion unless the source has thickness policy;
- zero-depth sheets: keep existing thin-sheet proxy behavior and use draw ordering/bias instead.

For the wandering trader canary, first determine whether the blue/green conflict is:

- a missing or collapsed vanilla shell deformation;
- a profession/robe overlay being drawn as coplanar paint;
- an unintended duplicate layer merged into the same material/batch.

That diagnosis decides whether the permanent fix belongs in geometry emission, draw policy, or both.

### 7. Tighten preview depth range as hygiene

Audit `PreviewCamera.NearPlane` / `FarPlane` per scene kind:

- keep a broad fallback for fly camera and large debug scenes;
- for entity/object preview, compute near/far from subject bounds plus margin;
- target a much smaller ratio than `100 / 0.1` when orbiting a one-to-three-meter subject;
- log `GL_DEPTH_BITS` once in preview diagnostics so depth precision problems are visible.

This is not the main fix for coplanar layers, but it reduces the chance that almost-coplanar legitimate geometry flickers at oblique angles.

---

## Rollout plan

### Phase 0: Repro and diagnostics

- Add or document a manual canary set:
  - `assets/minecraft/textures/entity/wandering_trader/wandering_trader.png`
  - `assets/minecraft/textures/entity/villager/villager.png`
  - representative villager profession and profession-level textures
  - zombie villager
  - sheep/wool or goat/fleece
  - armor/equipment overlays
  - Breeze wind and eyes layers
- Add a debug log line for selected subject:
  - material count;
  - draw batch count;
  - layer kind/order/bias per batch;
  - `GL_DEPTH_BITS`;
  - near/far used for the frame.
- Add an optional "Layer Debug" mode that false-colors batches by `PreviewDepthLayerKind`.

### Phase 1: Metadata plumbing

- Introduce `PreviewDepthLayerKind` and `PreviewDrawLayerPolicy`.
- Extend `PreviewDrawBatch` with layer policy defaults.
- Add `ModelElement` preview-layer metadata.
- Update `MinecraftModelBaker.TryBake` and `TryBakeBindPoseForGpuSkinning` to split batches by material plus policy.
- Keep all existing tests passing with default `Base` policy.

### Phase 2: OpenGL draw policy

- Add helper methods for applying/restoring layer depth state.
- Use sorted policy groups in the main pass.
- Keep entity blend behavior compatible with the existing `PreviewEntityAlphaMode.Blend` path.
- Update the shadow pass to skip cosmetic overlays by default.
- Add tests that inspect draw-batch ordering and policy propagation; avoid depending only on screenshots.

### Phase 3: Entity layer classification

- Add explicit policies for known canaries:
  - wandering trader / villager robe and body/profession layers;
  - zombie villager overlays;
  - humanoid sleeves/pants/jacket shells;
  - sheep wool, goat fleece, armor/equipment overlays;
  - Breeze wind/eyes.
- Prefer source truth from geometry IR / renderer-state data.
- Use path heuristics only for temporary coverage.

### Phase 4: Geometry shell correction

- Audit whether current IR and clean-room paths preserve vanilla cube deformation for layer shells.
- Fix missing deformation before relying on bias.
- Add focused geometry tests for shell separation in entity texel space.
- Keep shadow casting only for physical shells.

### Phase 5: Camera/depth hygiene

- Add per-subject near/far calculation.
- Keep debug/fly-camera fallback.
- Validate volumetric, godray, shadow, and TAA paths after changing camera planes.
- Leave reversed-Z as a separate future renderer plan if broader depth precision remains a problem.

---

## Test and validation gates

Automated:

- Unit tests for `PreviewDrawBatch` defaults and compatibility.
- Baker tests proving batches split on layer policy as well as material.
- Entity canary tests proving known overlay elements receive non-base policy.
- Shadow-pass policy tests, at least at helper level, proving cosmetic overlays are skipped.
- Shader/source tests remain green; no `gl_FragDepth` workaround should be added for this fix.

Manual Explore:

- Wandering trader rear view: green should not pop through blue while orbiting.
- Villager front/back/side: robe and nose remain visible; no overlay pulls through arms/head.
- Profession textures: deterministic layering across base, biome, profession, level.
- Armor/equipment: overlays do not flicker and do not cast double shadows.
- Breeze: wind and eyes still render in the intended visual order.
- Thin sheets: no new clipping or "inflated card" artifacts on fins, wings, tendrils, arrows, and zero-depth proxies.

Regression checks:

- Blocks/items still draw with ordinary depth.
- Ground/grid/moon/sky order unchanged.
- Shadow acne does not increase.
- Entity alpha cutout still discards transparent texels in main and shadow passes.
- GPU skinning and CPU rebake paths produce the same layer policies.

---

## Risks

- Too much negative polygon offset can pull overlays through nearby unrelated geometry, especially at grazing angles.
- Global depth-mask changes can leak if state restoration is incomplete.
- Path heuristics can misclassify a texture. Keep them narrow and replace with explicit metadata.
- Blind geometry inflation can break thin sheets, UV seams, and vanilla parity.
- Reversed-Z would be attractive but broad; it should not be mixed into the first targeted fix.

---

## Dynamic layer classification (all entities / mods)

Layer intent should not depend on clean-room `Build*` templates. Use a **single resolver** (`PreviewDepthLayerResolver`) with ordered signals:

| Priority | Signal | Source | Typical result |
|----------|--------|--------|----------------|
| 1 | Explicit element metadata | `ModelElement.DepthLayerKind` from IR emit or hand builders | As tagged |
| 2 | Part id tokens | Geometry IR `partId` (`hat`, `wind_*`, `jacket`, `eyes`, …) | Cutout / cosmetic overlay |
| 3 | Supplementary `textureKey` | Lifted cuboid `textureKey` from `LayerDefinitionRetainAtlasStamp` (`#wind`, `#eyes`, not `#skin`/`#main`) | Overlay by key name |
| 4 | Physical shell | IR `inflate > 0` | `Base` + `CastsShadow` |
| 5 | Stacked cuboids | Second+ cuboid on same part without inflate | `CutoutOverlay` |
| 6 | Face texture keys | `ModelFace.TextureKey` on merged JSON models | Same as row 3 |
| 7 | Texture zip path | `PreviewDepthLayerHeuristics` on resolved PNG path (`_overlay`, `profession`, …) | Overlay by path token |
| 8 | Default | Unclassified | `Base` (safe no-op) |

**Mod / lifted-JVM path:** GeometryCompiler already stamps per-cuboid `textureKey` and `inflate` from vanilla `LayerDefinition.create` + `retainPartsAndChildren`. Any entity whose model is lifted to geometry IR gets rows 2–5 automatically — no per-mob catalog entry required.

**Pack-only / JSON block models:** `TryBuildStaticMesh` runs `EnrichMergedModel` before bake so face texture keys and zip paths apply even without IR.

**Future (optional, highest priority when available):**

- ~~Lift explicit `previewDepthLayer` on geometry IR cuboids at compile time~~ — **done:** schema + `PreviewDepthLayerIrStamp` in geometry lift pipeline; resolver honors stamped IR at emit.
- ~~P6 renderer-state lift: map vanilla supplementary `ModelLayer` factories~~ — **done (MVP):** `RendererStateLift` emits `modelLayers` (`createWindLayer` → `#wind` / `cutoutOverlay`, etc.); `EnrichMergedModel` applies via `RendererStateDocumentLoader.TryGetModelLayerByTextureKey`.
- ~~Bake-time coplanar sibling detection~~ — **done:** `ApplyCoplanarSiblingOverlay` groups elements by `LocalToParent` and tags overlapping siblings as `CutoutOverlay`.
- Gradually remove per-entity clean-room layer tags as IR lift + enrich prove sufficient (Illager outer robe manual tag removed).

**Do not:** infer layers from diffuse color, alpha preprocessing, or global depth disables.

---

## Done criteria

- Wandering trader canary is stable while orbiting in Explore 3D.
- Layer policy is visible in diagnostics and defaults to safe base behavior for unclassified meshes.
- Cosmetic overlays no longer write depth or shadow depth unless explicitly marked physical.
- Geometry shell cases use real deformation/expansion rather than render bias alone.
- Focused tests cover metadata propagation, draw ordering, and canary classification.
- Manual Explore confirms no new obvious pull-through on villager, humanoid, equipment, Breeze, and thin-sheet canaries.
