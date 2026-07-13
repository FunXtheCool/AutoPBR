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
| SSBO scene/material/entity data | GL 4.3 or `GL_ARB_shader_storage_buffer_object` | Move entity matrix palettes to `std430`, later material tables and draw records | Current UBOs and scalar uniforms | Planned | Desktop shader variant renders identical entity animation; GLES keeps UBO path |
| Compute-shader render prep | GL 4.3 or `GL_ARB_compute_shader` | Compute froxel/cloud precompute, roughness prefilters, diagnostics, reductions | Current fragment/fullscreen and slice passes | Planned | Compute and fragment froxel paths produce visually equivalent output before cutover |
| GPU-driven draw submission | GL 4.3 or `GL_ARB_multi_draw_indirect` | CPU-filled indirect commands for entity/material/layer batches, then scene and shadow pass reuse | Current `DrawRange` loops | Planned | Draw count/API-call reduction with unchanged batch ordering and alpha behavior |
| GPU culling and LOD | Compute + SSBO + indirect draws | Frustum, distance, layer, and shadow-cascade culling into compact command buffers | CPU-side filtering and current draw lists | Planned | Culling can be disabled by capability fallback and never drops visible preview geometry |
| Image load/store pipelines | GL 4.2 or `GL_ARB_shader_image_load_store` | Froxel writes, masks, histograms, reductions, GPU picking, material analysis | FBO/texture pass equivalents | Planned | Image path validates against FBO path on fixed test scenes |
| Atomic counters / shader atomics | GL 4.2+ atomics, preferably with SSBO | Append visible draws, counters, compact lists, diagnostics | CPU counters and fixed buffers | Planned | Counter overflow is bounded and logged; fallback path stays deterministic |
| Texture arrays and bindless-style binding | GL 3.x arrays; optional `GL_ARB_bindless_texture` | Material table + texture arrays first; optional bindless to reduce texture-unit pressure | Current texture unit binding | Planned | Texture selection remains identical for block, item, entity, and ground materials |
| Async readback and profiling | PBO/fence support, `GL_ARB_timer_query` | Pass timing HUD, scoped GPU timings, stronger async readback | Existing sync readback and async PBO sidecar path | Planned | Timings are optional and do not force stalls when unavailable |
| SPIR-V / separable shader pipeline | GL 4.6 / `GL_ARB_gl_spirv`, GL 4.1 / `GL_ARB_separate_shader_objects` | Future shared shader tooling and specialization | Current GLSL + program binary cache | Deferred | Toolchain can be enabled without removing GLSL source path |

## Milestones

- [x] P1.0: Add this shared tracking document.
- [x] P1.1: Add runtime GL capability detection and diagnostics.
- [x] P1.2: Add persistent mapped upload buffer infrastructure with safe fallback.
- [x] P1.3: Route entity bone UBO uploads through the persistent transport when supported.
- [ ] P2: Add desktop-only SSBO entity matrix palette variant while preserving UBO fallback.
- [ ] P3: Add compute shader compile/cache support and a compute froxel inject prototype.
- [ ] P4: Add CPU-filled multi-draw indirect command buffers for entity/material batches.
- [ ] P5: Add compute culling and LOD command compaction.
- [ ] P6: Add image load/store and atomic-counter diagnostics/reductions.
- [ ] P7: Add texture-array material tables and evaluate bindless texture support.
- [ ] P8: Add timer query pass scopes and profiling HUD integration.
- [ ] P9: Evaluate SPIR-V and separable program pipeline once desktop infrastructure is stable.

## Implementation notes

The first implementation batch is intentionally narrow. Entity bone data still uses the same UBO binding points and uniform blocks as the existing shader ABI. The persistent upload path only changes the transport used to update those buffers on capable desktop GL. If persistent mapping is unavailable, or if the preview runs on GLES/ANGLE, uploads use the existing `glBufferSubData` path.

Future SSBO work should keep entity scalar uniforms in place for the first desktop variant. Move only the matrix palettes to `std430` first, validate parity, then decide whether material and draw metadata should move into larger SSBO tables.
