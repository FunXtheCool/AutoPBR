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
- [Live GL pixel rendering harness](pixel-rendering-harness.md)

## Feature tracker

| System | GL requirement | AutoPBR use | GLES/ANGLE fallback | Status | Acceptance |
|--------|----------------|-------------|---------------------|--------|------------|
| Persistent mapped upload rings | GL 4.4 or `GL_ARB_buffer_storage` | Stream entity bone UBOs first, then overlay/debug dynamic VBOs and optional PBO staging | Existing `BufferData` / `BufferSubData` uploads | P1 infra | WGL logs persistent upload support and entity bone UBO uploads still match current shader ABI |
| SSBO scene/material/entity data | GL 4.3 or `GL_ARB_shader_storage_buffer_object` | Move entity matrix palettes, material flags, and draw records to `std430`; later full material/texture tables | Current UBOs and scalar uniforms | P2 entity palettes + draw records | Desktop shader variant renders identical entity animation; GLES keeps UBO/uniform path |
| Compute-shader render prep | GL 4.3 or `GL_ARB_compute_shader` plus image store for froxel writes | Compute froxel/cloud precompute, roughness prefilters, diagnostics, reductions | Current fragment/fullscreen and slice passes | P3 prototype + fixed-scene parity | Compute and fragment froxel paths produce visually equivalent output before wider cutover |
| GPU-driven draw submission | GL 4.3 or `GL_ARB_multi_draw_indirect`; grouped path also needs `GL_ARB_shader_draw_parameters` | CPU-filled indirect commands for entity/material/layer batches, then scene and shadow pass reuse | Current `DrawRange` loops | P4.1 grouped multi-draw | Draw count/API-call reduction with unchanged batch ordering and alpha behavior |
| GPU culling and LOD | Compute + SSBO + indirect draws; indirect-count submission needs GL 4.6 or `GL_ARB_indirect_parameters` | Frustum, distance, layer, and shadow-cascade culling into compact command buffers | CPU-side filtering and current draw lists | P5.2 complete | Culling can be disabled by capability fallback and never drops visible preview geometry |
| Image load/store pipelines | GL 4.2 or `GL_ARB_shader_image_load_store` | Froxel writes, masks, histograms, reductions, GPU picking, material analysis | FBO/texture pass equivalents | P6.1 image histogram consumer | Image path validates against FBO path on fixed test scenes |
| Atomic counters / shader atomics | GL 4.2+ atomics, preferably with SSBO | Append visible draws, counters, compact lists, diagnostics | CPU counters and fixed buffers | P6 complete | Counter overflow is bounded and logged; fallback path stays deterministic |
| Texture arrays and bindless-style binding | Desktop texture arrays plus material/draw SSBOs for layer selection; optional `GL_ARB_bindless_texture` | Material table + texture arrays first; optional bindless to reduce texture-unit pressure | Current texture unit binding | P7 complete | Same-size block/entity slot materials bind once as arrays; mixed-size, item, ground, tessellated, GLES/ANGLE, or compile-fallback paths keep existing samplers |
| Async readback and profiling | PBO/fence support, `GL_ARB_timer_query` | Pass timing HUD, scoped GPU timings, stronger async readback | Existing sync readback and async PBO sidecar path | P8 complete for pass timers | Timings are optional and do not force stalls when unavailable |
| SPIR-V / separable shader pipeline | GL 4.6 / `GL_ARB_gl_spirv`, GL 4.1 / `GL_ARB_separate_shader_objects` | Future shared shader tooling and specialization | Current GLSL + program binary cache | P9 complete for evaluation/staging | Toolchain can be enabled without removing GLSL source path |

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
- [x] P6.0: Add optional bounded SSBO atomic diagnostics/reductions to GPU command compaction with live overflow validation.
- [x] P6.1: Add the first image-backed histogram/reduction consumer with an FBO/readback fallback.
- [x] P7: Add texture-array material tables and evaluate bindless texture support.
- [x] P8: Add timer query pass scopes and profiling HUD integration.
- [x] P9: Evaluate SPIR-V and separable program pipeline once desktop infrastructure is stable.

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

P6.0 extends the command compactor's counter SSBO with a fixed eight-word reduction ABI: examined, written, frustum-culled, distance-culled, empty, visibility-flag-culled, overflow, and maximum eligible index count. The indirect visible-count word remains at byte offset zero, so `glMultiDrawElementsIndirectCount` keeps the same ABI. Every output write is guarded by `uOutputCapacity`; excess candidates increment overflow without writing out of bounds, and indirect submission is additionally limited by `maxDrawCount`. Diagnostic atomics are opt-in and runtime rendering performs no counter readback. The GLES/ANGLE and non-compute paths remain unchanged.

P6.1 adds a bounded 64-bin luminance histogram over the RGBA8 scene-capture texture. Desktop GL uses `genesis_luminance_histogram.comp` with read-only image load and SSBO atomics; a shared integer luminance formula makes its bins exactly reproducible on the CPU. Sampling is automatically strided to at most 65,536 pixels, the SSBO includes explicit sample/overflow counters, and the shader guards capacity before every append. The existing two-second preview fingerprint diagnostic reports this GPU result when the image path is available. GLES/ANGLE, missing scene capture, missing capabilities, or shader compilation failure computes the same histogram from the existing framebuffer readback, adding no fallback readback beyond the fingerprint capture.

P7 adds `GlTexture2DArray` plus a conservative material-array planner for same-dimension block/entity slot materials. The desktop shader variant defines `GENESIS_MATERIAL_TEXTURE_ARRAYS`, binds albedo/normal/specular/height arrays once per main pass, binds the albedo array once for shadow alpha, and reads the array layer from the existing material/draw SSBO reserved field. The path requires desktop GL, texture arrays, and material/draw SSBOs; it is disabled for tessellated Genesis because the tessellation evaluation stage still samples height through the legacy `sampler2D`. Mixed slot dimensions, missing capabilities, compile failures, item/ground draws, and GLES/ANGLE keep the previous texture-unit sampler path. `GL_ARB_bindless_texture` is detected and reported, but remains optional and unused until the array path has broader scene parity.

P8 adds a desktop-only `GL_TIME_ELAPSED` query ring for render pass profiling. The backend scopes setup, shadow, scene, post, and native overlay/present-handoff work, then polls older query slots without waiting on the GPU. Completed samples update the existing FPS HUD with a compact GPU timing line and emit throttled diagnostics with per-pass milliseconds. GLES/ANGLE, missing timer-query support, query lag, or driver/query failures keep the current untimed path and clear the extra HUD line. The existing async PBO sidecar presentation path remains unchanged; broader HUD-facing readback transport can build on this non-blocking profiler later.

P9 adds an explicit shader-toolchain plan without changing production shader compilation. Runtime diagnostics now report `spirv`, `separablePrograms`, and a one-line plan whose primary path remains `GLSL source + program binary cache`. Desktop GL 4.6 marks SPIR-V support as staged until real `.spv` shader assets are present; GLES/ANGLE reports the lane off. A small SPIR-V manifest/binary validator defines the future ingestion contract, and live WGL smoke validates that the driver accepts a minimal GLSL separable vertex/fragment program pipeline. This completes the roadmap evaluation lane and preserves the existing GLSL source preparation, include flattening, ES adaptation, and program-binary cache as the correctness path.

## P2.3 smoke evidence

Updated on July 15, 2026. The durable run artifact is `artifacts/p23-live-gl-smoke.txt`.

- Hidden WGL smoke created a real desktop context and reported `desktop GL 4.6`, `persistentUpload=on`, `entitySsbo=on`, `materialDrawSsbo=on`, `materialTextureArrays=on`, `computeFroxels=on`, `multiDrawIndirect=yes`, `drawParameters=yes`, and `spirv=yes`.
- The same live context compiled the desktop Genesis shader variant with entity/material SSBO defines enabled.
- The same live context compiled `genesis_volume_inject.comp` when compute shaders and image load/store were available.
- The same live context rendered a fixed 32x24x8 froxel scene through both the fragment-slice injector and compute image-store injector, then verified RGBA and occupancy readback within one byte of tolerance. Latest artifact hash: `rgbaHash=A2B561C5`, `occHash=163D06C5`.
- The same live context now reports `indirectDraws=on`, `multiDrawGroups=on`, and uploads/binds a two-command indirect draw buffer for P4 command transport coverage.
- The same live context compiles the base-instance Genesis draw-record variant and runs GPU indirect command compaction from four source commands to two visible commands.
- The same live context reports `gpuBatchCulling=on` and runs GPU frustum/distance culling from five source commands to two visible commands.
- The same live context reports `gpuCompactedDraws=on` and executes the GL 4.6 indirect-count submission path without CPU counter readback (four source commands compacted to three submitted draws).
- The same live context reports `gpuReductions=on`, validates categorized reductions, and intentionally limits output to one command to verify two excess candidates are counted without an out-of-bounds write.
- The same live context reports `imageHistogram=on` and verifies all 64 GPU image/atomic histogram bins exactly match the FBO/readback fallback for a fixed RGBA8 texture (`samples=128`, `overflow=0`).
- The same live context reports `materialTextureArrays=on`, compiles the desktop Genesis texture-array shader variant, uploads a two-layer RGBA8 texture array, and verifies `FramebufferTextureLayer` readback from layer 1.
- The same live context reports `gpuTimers=on` and verifies a desktop `GL_TIME_ELAPSED` query snapshot can be produced through the P8 non-blocking profiler.
- The same live context reports `spirv=yes` and `separablePrograms=yes`, records the P9 shader-toolchain plan as `primary=GLSL source + program binary cache`, `spirv=no-assets`, and `separablePrograms=available`, then validates a minimal GLSL separable vertex/fragment program pipeline.
- ANGLE/GLES fallback is covered by `PreviewGlCapabilitiesTests` and `PreviewGlslEsAdaptTests`: GLES reports desktop accelerators off and adapted GLES shader sources do not include SSBO, compute, image-store, or desktop-only defines.

## Immediate next work

- The original desktop GL 4.6 performance roadmap is now complete through P9.
- Post-roadmap pixel correctness harness: an opt-in hidden-WGL fixture now compares complete RGBA8 output for direct, per-command indirect, grouped multi-draw, GPU-compacted indirect-count, and legacy-sampler versus texture-array lanes. It records PNG/JSON evidence and detailed diff metrics while adding no work to the production frame loop.
- First post-roadmap hardening slice: material texture-array eligibility is now frame-based, so shadow and main passes both decline the array path when tessellation displacement is active. Effective shader flags are computed during setup instead of depending on shadow rendering, so tessellation/parallax program selection works even when shadows are disabled. Mixed slot dimensions, malformed optional map payloads, missing draw records, and missing block/entity slots are covered by unit tests and keep the sampler fallback path. The roadmap GL-object teardown paths now explicitly cover material arrays, material draw-record buffers, image histograms, timer queries, and indirect command buffers.
- Final hardening slice: material-array fixture coverage now uses real multi-material `PreviewModelSubject` draw batches, mapped preview materials, mixed-dimension fallback cases, and entity-emulated tessellation gating. This gives P7 a real subject-level parity guard while still preserving the GLES/ANGLE sampler fallback.
- No additional major roadmap work is required before treating this GL 4.6 performance roadmap as complete.
- Optional future lanes: P8.1 HUD-facing async readback transport, P9.1 real `.spv` asset generation/packaging, or a bindless-texture experiment after texture-array parity is broader.
