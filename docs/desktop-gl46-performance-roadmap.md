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
| Compute-shader render prep | GL 4.3 or `GL_ARB_compute_shader` plus image store for froxel writes | Compute froxel/cloud precompute, roughness prefilters, diagnostics, reductions | Current fragment/fullscreen and slice passes | P3 prototype | Compute and fragment froxel paths produce visually equivalent output before wider cutover |
| GPU-driven draw submission | GL 4.3 or `GL_ARB_multi_draw_indirect` | CPU-filled indirect commands for entity/material/layer batches, then scene and shadow pass reuse | Current `DrawRange` loops | Planned | Draw count/API-call reduction with unchanged batch ordering and alpha behavior |
| GPU culling and LOD | Compute + SSBO + indirect draws | Frustum, distance, layer, and shadow-cascade culling into compact command buffers | CPU-side filtering and current draw lists | Planned | Culling can be disabled by capability fallback and never drops visible preview geometry |
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
- [ ] P4: Add CPU-filled multi-draw indirect command buffers for entity/material batches.
- [ ] P5: Add compute culling and LOD command compaction.
- [ ] P6: Add image load/store and atomic-counter diagnostics/reductions.
- [ ] P7: Add texture-array material tables and evaluate bindless texture support.
- [ ] P8: Add timer query pass scopes and profiling HUD integration.
- [ ] P9: Evaluate SPIR-V and separable program pipeline once desktop infrastructure is stable.

## Implementation notes

The first implementation batch is intentionally narrow. P1 kept entity bone data on the same UBO binding points and uniform blocks as the existing shader ABI. P2.0 adds a desktop-only shader variant that reads the three entity matrix palettes from `std430` SSBOs at bindings 5, 6, and 7 while scalar entity uniforms stay unchanged. P2.2 adds a second desktop-only `std430` table at binding 8 for per-batch material/draw metadata such as atlas scale, parallax flags, material-map presence, height texture size, tessellation eligibility, and entity alpha mode. If SSBOs are unavailable, if shader compilation rejects either variant, or if the preview runs on GLES/ANGLE, the renderer keeps the UBO binding points and the existing scalar uniform path.

P3 adds desktop compute shader compile/cache support through the same prepared-source and program-binary cache used by vertex/fragment/tessellation programs. The first consumer is `genesis_volume_inject.comp`, a GL 4.3+ compute froxel producer that writes the existing froxel color and occupancy 2D texture arrays through image load/store, then issues an image/texture memory barrier before the existing integration shader samples the textures. The compute path requires desktop GL, compute shaders, and image load/store. GLES/ANGLE, lite volume shaders, compile failures, or missing image bindings keep the fragment-slice froxel injector as the fallback.

Future SSBO work should move from metadata tables to larger texture/material binding systems only after a live WGL/ANGLE smoke pass confirms parity.

## P2.3 smoke evidence

Completed on July 14, 2026. The durable run artifact is `artifacts/p23-live-gl-smoke.txt`.

- Hidden WGL smoke created a real desktop context and reported `desktop GL 4.6`, `persistentUpload=on`, `entitySsbo=on`, `materialDrawSsbo=on`, `computeFroxels=on`, `multiDrawIndirect=yes`, and `spirv=yes`.
- The same live context compiled the desktop Genesis shader variant with entity/material SSBO defines enabled.
- The same live context compiled `genesis_volume_inject.comp` when compute shaders and image load/store were available.
- ANGLE/GLES fallback is covered by `PreviewGlCapabilitiesTests` and `PreviewGlslEsAdaptTests`: GLES reports desktop accelerators off and adapted GLES shader sources do not include SSBO, compute, image-store, or desktop-only defines.
- Full visual/hash parity for fragment vs compute froxel injection remains a P3/P4 validation item before broader compute usage.

## Immediate next work

- Add a fixed-scene visual/hash comparison for fragment froxel inject vs compute froxel inject before broadening compute usage.
- Begin P4 with CPU-filled indirect draw command buffers for entity/material batches, keeping the existing `DrawRange` loop as fallback.
