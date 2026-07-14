# Desktop GL 4.6 performance roadmap

AutoPBR now has a native WGL desktop OpenGL path that can exceed the GLES 3.0 / ANGLE feature envelope. This roadmap tracks the performance systems we can add from desktop GL while keeping GLES/ANGLE as the baseline fallback and a possible future mobile path.

## Runtime policy

- GLES/ANGLE remains the compatibility path. Desktop-only accelerators must be capability-gated and must leave the GLES shader and upload paths working.
- WGL does not automatically mean GL 4.6. Runtime code must distinguish WGL 3.3, 4.0, 4.3, 4.4, and 4.6 capabilities.
- Accelerators auto-enable from detected capabilities. In this phase there are no per-feature UI settings; decisions are reported through preview diagnostics.
- Existing GLSL source preparation and program binary caching stay in place. SPIR-V is a later toolchain lane.

Related docs:

- [GLES / ANGLE shader guide](gles-angle-shader-guide.md)
- [Volumetric froxels / unified medium](volumetric-froxels.md)
- [Entity preview GPU vs CPU parity policy](entity-preview-gpu-cpu-parity.md)

## Feature tracker

| System | GL requirement | AutoPBR use | GLES/ANGLE fallback | Status | Acceptance |
|--------|----------------|-------------|---------------------|--------|------------|
| Persistent mapped upload rings | GL 4.4 or `GL_ARB_buffer_storage` | Stream entity bone UBOs first, then overlay/debug dynamic VBOs and optional PBO staging | Existing `BufferData` / `BufferSubData` uploads | P1 infra | WGL logs persistent upload support and entity bone UBO uploads still match current shader ABI |
| SSBO scene/material/entity data | GL 4.3 or `GL_ARB_shader_storage_buffer_object` | Move entity matrix palettes, material flags, and draw records to `std430`; later full material/texture tables | Current UBOs and scalar uniforms | P2 entity palettes + draw records | Desktop shader variant renders identical entity animation; GLES keeps UBO/uniform path |
| Compute-shader render prep | GL 4.3 or `GL_ARB_compute_shader` plus image store for froxel writes | Compute froxel/cloud precompute, roughness prefilters, diagnostics, reductions | Current fragment/fullscreen and slice passes | P3 prototype + fixed-scene parity | Compute and fragment froxel paths produce visually equivalent output before wider cutover |
| GPU-driven draw submission | GL 4.3 or `GL_ARB_multi_draw_indirect`; grouped path also needs `GL_ARB_shader_draw_parameters` | CPU-filled indirect commands for entity/material/layer batches, then scene and shadow pass reuse | Current `DrawRange` loops | P4.1 grouped multi-draw | Draw count/API-call reduction with unchanged batch ordering and alpha behavior |
| GPU culling and LOD | Compute + SSBO + indirect draws; indirect-count submission needs GL 4.6 or `GL_ARB_indirect_parameters` | Frustum, distance, layer, and shadow-cascade culling into compact command buffers | CPU-side filtering and current draw lists | P5.2 complete | Culling can be disabled by capability fallback and never drops visible preview geometry |
| Image load/store pipelines | GL 4.2 or `GL_ARB_shader_image_load_store` | Froxel writes, masks, histograms, reductions, GPU picking, material analysis | FBO/texture pass equivalents | P3 froxel producer prototype | Image path validates against FBO path on fixed test scenes |
| Atomic counters / shader atomics | GL 4.2+ atomics, preferably with SSBO | Append visible draws, counters, compact lists, diagnostics | CPU counters and fixed buffers | Planned | Counter overflow is bounded and logged; fallback path stays deterministic |
| Texture arrays and bindless-style binding | GL 3.x arrays; optional `GL_ARB_bindless_texture` | Material table + texture arrays first; optional bindless to reduce texture-unit pressure | Current texture unit binding | Planned | Texture selection remains identical for block, item, entity, and ground materials |
| Async readback and profiling | PBO/fence support, `GL_ARB_timer_query` | Pass timing HUD, scoped GPU timings, stronger async readback | Existing sync readback and async PBO sidecar path | Planned | Timings are optional and do not force stalls when unavailable |
| SPIR-V / separable shader pipeline | GL 4.6 / `GL_ARB_gl_spirv`, GL 4.1 / `GL_ARB_separate_shader_objects` | Future shared shader tooling and specialization | Current GLSL + program binary cache | Deferred | Toolchain can be enabled without removing GLSL source path |

## Milestones

- [x] P1.0: Add this shared tracking document.
- [x] P1.1: Add runtime GL capability detection and diagnostics.
- [x] P1.2: Add persistent mapped upload buffer infrastructure with safe fallback.
- [x] P1.3: Route entity bone UBO uploads through the persistent transport when supported.
- [x] P2.0: Add desktop-only SSBO entity matrix palette variant while preserving UBO fallback.
- [x] P2.1: Add automated capability, shader-define, and entity parity coverage for WGL SSBO and ANGLE/GLES fallback decisions.
- [x] P2.2: Expand SSBO coverage to material/draw records for block/entity batches while preserving scalar uniform fallback.
- [x] P2.3: Live context smoke and fallback guardrails: hidden WGL 4.6 context capability probe, desktop SSBO/compute shader compile smoke, and ANGLE/GLES fallback diagnostics/source-prep coverage.
- [x] P3: Add compute shader compile/cache support and a compute froxel inject prototype.
- [x] P3.1: Add fixed-scene live WGL parity smoke comparing fragment-slice froxel inject to compute image-store froxel inject.
- [x] P4.0: Add CPU-filled indirect draw command buffers for entity/material batches behind the desktop capability gate.
- [x] P4.1: Group compatible batches and switch those groups to `glMultiDrawElementsIndirect` where state does not change between draws.
- [x] P5.0: Add a compute SSBO indirect-command compaction producer with live WGL validation.
- [x] P5.1: Add per-batch bounds/LOD metadata and frustum/distance tests that feed the GPU compactor.
- [x] P5.2: Consume compacted command buffers in compatible main/shadow groups, including conservative animated bounds and live indirect-count execution coverage.
- [ ] P6: Add image load/store and atomic-counter diagnostics/reductions.
- [ ] P7: Add texture-array material tables and evaluate bindless texture support.
- [ ] P8: Add timer query pass scopes and profiling HUD integration.
- [ ] P9: Evaluate SPIR-V and separable program pipeline once desktop infrastructure is stable.

## Implementation notes

The first implementation batch is intentionally narrow. P1 kept entity bone data on the same UBO binding points and uniform blocks as the existing shader ABI. P2.0 adds a desktop-only shader variant that reads the three entity matrix palettes from `std430` SSBOs at bindings 5, 6, and 7 while scalar entity uniforms stay unchanged. P2.2 adds a second desktop-only `std430` table at binding 8 for per-batch material/draw metadata such as atlas scale, parallax flags, material-map presence, height texture size, tessellation eligibility, and entity alpha mode. If SSBOs are unavailable, if shader compilation rejects either variant, or if the preview runs on GLES/ANGLE, the renderer keeps the UBO binding points and the existing scalar uniform path.

P3 adds desktop compute shader compile/cache support through the same prepared-source and program-binary cache used by vertex/fragment/tessellation programs. The first consumer is `genesis_volume_inject.comp`, a GL 4.3+ compute froxel producer that writes the existing froxel color and occupancy 2D texture arrays through image load/store, then issues an image/texture memory barrier before the existing integration shader samples the textures. The compute path requires desktop GL, compute shaders, and image load/store. GLES/ANGLE, lite volume shaders, compile failures, or missing image bindings keep the fragment-slice froxel injector as the fallback.

Future SSBO work should move from metadata tables to larger texture/material binding systems only after a live WGL/ANGLE smoke pass confirms parity.

P4.0 adds a CPU-filled `DrawElementsIndirectCommand` buffer for block/entity preview batches when desktop GL reports `multiDrawIndirect` support. Scene and shadow passes keep their existing per-batch material upload, alpha/depth-layer state, draw-record index, and draw ordering, then issue `glDrawElementsIndirect` for the selected batch command. If the capability is unavailable, if the preview runs on GLES/ANGLE, or if no batch commands are valid, the renderer continues to use the existing `DrawRange` path.

P4.1 adds true `glMultiDrawElementsIndirect` groups for compatible consecutive batches. The grouped path requires material/draw-record SSBOs plus `GL_ARB_shader_draw_parameters`/GL 4.6 so the shader can read the indirect command `baseInstance` as the draw-record index. Groups stay conservative: the main pass groups only same-material, same depth-layer/blend-state batches, and the shadow pass groups same-material shadow-casting batches. Tessellated Genesis draws continue through the per-batch indirect path until the draw-record index is carried through tessellation stages.

P5.0 adds a reusable compute shader producer, `genesis_indirect_compact.comp`, that consumes source indirect commands and visibility flags, atomically appends visible commands into a compact output indirect buffer, and writes a visible-command counter.

P5.1 adds static per-batch culling sphere metadata to CPU-baked preview batches and extends the compactor with GPU frustum/distance culling. Missing or invalid bounds keep a batch visible, which preserves correctness for GPU-skinned animated bind meshes until they get conservative animated bounds.

The first P5.2 slice consumes compacted buffers in opaque/cutout main-pass and shadow same-state groups through `glMultiDrawElementsIndirectCount`. The atomic visible-command counter stays GPU-resident as the indirect draw count, avoiding CPU readback. Alpha-blended main-pass groups remain on the ordered P4 path because atomic compaction does not guarantee command order. This lane requires GL 4.6 or `GL_ARB_indirect_parameters`, shader draw parameters, compatible material/depth state, at least four grouped commands, and at least one known bound. Command ranges, model-transformed culling spheres, and per-cascade frusta are supplied to the compute pass. Any missing capability, unresolved entry point, compute compile failure, tessellated draw, small group, or unknown-bounds-only group retains the P4 grouped/per-batch indirect or direct GLES-safe fallback.

P5.2 is complete with conservative GPU-skinned bounds. GPU bind-mesh preparation caches one bind-space AABB per batch/bone cluster. Each animation frame transforms only the eight corners of those cached boxes through the current bone palette, applies the same preview normalization/lift as the vertex shader, and derives a conservative batch sphere. This avoids per-frame vertex scans and does not CPU-skin the display mesh. Missing, invalid, or mismatched palettes clear the dynamic spheres back to the always-visible fallback. Render diagnostics report the first compacted group source-command count and one resulting API call; live WGL smoke executes an indirect-count draw using the compute-written counter and checks `GL_NO_ERROR`.

## P2.3 smoke evidence

Completed on July 14, 2026. The durable run artifact is `artifacts/p23-live-gl-smoke.txt`.

- Hidden WGL smoke created a real desktop context and reported `desktop GL 4.6`, `persistentUpload=on`, `entitySsbo=on`, `materialDrawSsbo=on`, `computeFroxels=on`, `multiDrawIndirect=yes`, `drawParameters=yes`, and `spirv=yes`.
- The same live context compiled the desktop Genesis shader variant with entity/material SSBO defines enabled.
- The same live context compiled `genesis_volume_inject.comp` when compute shaders and image load/store were available.
- The same live context rendered a fixed 32x24x8 froxel scene through both the fragment-slice injector and compute image-store injector, then verified RGBA and occupancy readback within one byte of tolerance. Latest artifact hash: `rgbaHash=A2B561C5`, `occHash=163D06C5`.
- The same live context now reports `indirectDraws=on`, `multiDrawGroups=on`, and uploads/binds a two-command indirect draw buffer for P4 command transport coverage.
- The same live context compiles the base-instance Genesis draw-record variant and runs GPU indirect command compaction from four source commands to two visible commands.
- The same live context reports `gpuBatchCulling=on` and runs GPU frustum/distance culling from five source commands to two visible commands.
- The same live context reports `gpuCompactedDraws=on` and executes the GL 4.6 indirect-count submission path without CPU counter readback (four source commands compacted to three submitted draws).
- ANGLE/GLES fallback is covered by `PreviewGlCapabilitiesTests` and `PreviewGlslEsAdaptTests`: GLES reports desktop accelerators off and adapted GLES shader sources do not include SSBO, compute, image-store, or desktop-only defines.

## Immediate next work

- Start P6.0 by reusing the existing compute/SSBO counter infrastructure for bounded GPU diagnostics and reduction buffers before adding new image-store consumers.
- Then add a histogram/reduction image pipeline with an FBO/readback fallback, which gives P6 a measurable first production consumer without coupling it to volumetric rendering.
