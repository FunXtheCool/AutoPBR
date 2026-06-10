# GLES / ANGLE shader guide (Genesis preview)

AutoPBR’s 3D preview runs on **OpenGL ES 3.0 via ANGLE** on Windows (Avalonia). Desktop GLSL 330 sources are flattened and adapted before compile. New shader code must satisfy **both** desktop GL and this ES path.

On Windows, ANGLE typically translates **GLSL ES → HLSL → Direct3D 11**. Shaders that are legal GLSL ES may still fail or miscompile when the D3D backend cannot express them. Treat **ANGLE + ES** as the real target, not “desktop GL with a different `#version` line.”

**Primary consumers:** `src/AutoPBR.App/Rendering/Shaders/**` and `OpenGlPreviewBackend.*.cs`.

---

## Official references

| Resource | URL | Use for |
|----------|-----|---------|
| GLSL ES 3.00 spec | [Khronos GLSL_ES_Specification_3.00.pdf](https://registry.khronos.org/OpenGL/specs/es/3.0/GLSL_ES_Specification_3.00.pdf) | Precision, samplers, loops, fragment outputs |
| ANGLE source (translator) | [angle/angle on Google Git](https://chromium.googlesource.com/angle/angle/) | `ParseContext.cpp`, `ShaderLang.h`, `Framebuffer.cpp` |
| ANGLE compile options | [ShaderLang.h](https://chromium.googlesource.com/angle/angle/+/refs/heads/main/include/GLSLANG/ShaderLang.h) | Loop simplification, swizzle workarounds |
| WebGL feedback loops | [Khronos wiki — Framebuffer feedback](https://www.khronos.org/opengl/wiki/Framebuffer_Object#Feedback_Loops) | Same-FBO read/write rules |
| WebGL dev list (depth sampling) | [Sampling depth while attached](https://groups.google.com/g/webgl-dev-list/c/Fhpra9Euryc) | Why ANGLE forbids depth-on-same-FBO |
| Derivatives in branches | [A Clockwork Berry — shader derivatives](http://www.aclockworkberry.com/shader-derivative-functions/) | `texture()` + `if` / dynamic loops |

---

## Build pipeline (read this first)

1. **Entry shader only** carries `#version 330 core` (e.g. `genesis_volume_inject.frag`).
2. **`GlslIncludeResolver`** flattens `//!include "…"` into one translation unit. Included files must **not** repeat `#version`.
3. **`GlslSourceAdapter`** strips `#version 330 core` and prepends `#version 300 es` plus precision qualifiers (`GlslSourceAdapter.cs`).
4. Shaders compile on the **OpenGL thread** inside `GlShaderProgram`.

When debugging compile failures, dump the **adapted** ES source (see tests below), not just the `.frag` on disk. ANGLE often reports a **syntax error on the line after** the real problem (e.g. `return;` causing failure on the next `FragColor = …`).

---

## Fragment shader rules (ANGLE / D3D pitfalls)

Rules below are ordered by how often they bit us in volumetric / god-ray work.

| Rule | Do | Don’t |
|------|----|-------|
| **No early `return` in `main()`** | Use `if / else`; assign `FragColor` on every path | `return;` after partial output setup |
| **Single write to `out` color** | `FragColor = vec4(a, b, c, d);` once | `FragColor = vec4(...); FragColor.a = …;` |
| **`discard` is OK** | Use for “no contribution” pixels (`alpha` too low, sky gate, etc.) | Rely on `discard` if you later need `GL_ANGLE_shader_pixel_local_storage` (forbids both `discard` and `return`) |
| **Swizzles in `vec4()` constructors** | Extract scalars first: `float g = sunLit.x; FragColor = vec4(density, g, b, 1.0);` | `vec4(density, sunLit.x, sunLit.y, occ)` — ANGLE can syntax-error on the closing `}` |
| **Empty froxel voxels** | `if (density <= GEN_EPS) discard;` then one `FragColor` write | `if/else` with separate `FragColor` writes in each branch |
| **Constants** | Prefer `GEN_EPS` from `common.glsl` | Scattered magic literals |
| **Locals before `out`** | Build `vec4 packed = …; FragColor = packed;` if packing is complex | Multi-step writes to `out` variables |

**Why `return` fails on ANGLE (practical):** GLSL ES allows `return` from `main()`, but ANGLE’s HLSL backend and some extensions (e.g. pixel-local storage) treat flow after `return` as invalid. We standardize on **no `return` in fragment `main()`** for all Genesis shaders.

**Safe inject pattern:**

```glsl
if (density <= GEN_EPS)
{
    FragColor = vec4(0.0);
}
else
{
    vec3 sunLit = uLightColor * density * shadowT * 0.85;
    float occ = step(GEN_EPS, density);
    FragColor = vec4(density, sunLit.x, sunLit.y, occ);
}
```

**`discard` vs `return`:** `discard` drops the fragment and is fine for god-ray / cloud gates. `return` leaves prior `out` writes defined; on ES/ANGLE the combination with later statements is what breaks.

---

## Precision qualifiers (mandatory on ES)

Per [GLSL ES 3.00 §4.5.3](https://registry.khronos.org/OpenGL/specs/es/3.0/GLSL_ES_Specification_3.00.pdf): **fragment** shaders have **no default precision for `float`**. Most **sampler types have no default precision** either (revision 2+ removed defaults for ES 3.0 sampler types).

Only these samplers have defaults in ES 3.0/3.1 (both default to **`lowp`**):

- `sampler2D`
- `samplerCube`

**All other sampler types** need either a default precision statement or per-variable precision, e.g.:

```glsl
precision highp sampler3D;
// or
uniform highp sampler3D uCloudNoise;
```

`GlslSourceAdapter` currently injects for fragment shaders:

- `precision highp float;`
- `precision highp int;`
- `precision highp sampler2D;`
- `precision highp sampler2DArray;`
- `precision highp sampler2DShadow;`
- `precision highp sampler3D;`

**When adding a new sampler**, extend the adapter **and** `PreviewGlslEsAdaptTests`:

| Sampler (ES 3.0) | Default precision? | Action if used |
|------------------|-------------------|----------------|
| `sampler2D` | `lowp` (we override to `highp`) | Already covered |
| `samplerCube` | `lowp` | Add `precision highp samplerCube;` if used |
| `sampler3D` | **none** | Required — see cloud noise |
| `sampler2DArray` | **none** | Required — froxel volume |
| `sampler2DShadow` | **none** | Required — shadow maps |
| `samplerCubeShadow` | **none** | Add if used |
| `sampler2DArrayShadow` | **none** | Add if used |
| `isampler*` / `usampler*` | **none** | Add matching precision line |

Typical compile error: `'sampler3D' : No precision specified`.

**Vertex shaders:** If you omit precision, ES treats floats as `highp` in vertex stages; fragment **must** declare `float` precision explicitly (adapter handles this).

---

## Sampler semantics (GLSL ES)

From GLSL ES 3.00 §4.1.7:

- Samplers are **opaque**; not l-values; cannot use `out` / `inout` on sampler parameters.
- Sampler **arrays** may only be indexed with **constant integral** expressions.
- Samplers are set from the **API only** (uniforms), never declared with initializers in GLSL.

Prefer **separate uniforms** (`uShadowMap`, `uShadowMapNear`) over sampler arrays unless indices are compile-time constants.

---

## Loops, branches, and `texture()`

ANGLE maps GLSL ES to HLSL. Important constraints:

| Topic | Guidance |
|-------|----------|
| **Loop bounds** | Prefer `const int MAX_STEPS = 48; for (int i = 0; i < MAX_STEPS; ++i)` with `break` |
| **Dynamic loop count from uniform** | May compile slowly or fail on D3D; prefer fixed upper bound + `break` |
| **`texture()` inside dynamic `if` / variable-count loops** | Gradients (`dFdx`/`dFdy`) are **undefined** in non-uniform control flow ([GLSL ES / desktop §8.10.1](http://www.aclockworkberry.com/shader-derivative-functions/)). ANGLE/HLSL may reject or miscompile. |
| **Long loops + `texture()`** | Can cause extreme HLSL compile times ([ANGLE mailing list](https://groups.google.com/g/angleproject/c/0zfeMdGzzyQ)). Keep march steps modest (we use 28–64). |
| **ES 1.00 loop limits** | WebGL enforces Appendix A limits via `validateLoopIndexing`; ES 3.0 is looser but ANGLE still simplifies loops (`simplifyLoopConditions` in `ShaderLang.h`). |

**Genesis convention:** All march loops use **fixed `const int`** step counts (`GR_SAMPLES`, `VM_STEPS`, `CLOUD_STEPS`) and quality only changes the effective bound via `break` or compile-time `steps` variable derived from `const int`.

**Bilateral / upsample shaders:** Avoid `fwidth`, `dFdx`, `dFdy` inside branches. If needed, compute unconditionally, then `mix` with a mask.

---

## Interpolation qualifiers (`flat`)

GLSL ES 3.00: fragment shader inputs that are **`int` / `uint` / integer vectors** must use the **`flat`** qualifier.

```glsl
flat out int vMaterialId;   // vertex
flat in int vMaterialId;    // fragment
```

Booleans cannot be passed as varyings. Use `int` + `flat` if you ever need discrete per-primitive IDs.

---

## Include graph / “lite” shaders

GLES **lite** paths must not pull in features the lite C# path cannot bind.

| Concern | Pattern |
|---------|---------|
| `sampler3D` in froxel inject | Keep in `volumetric_clouds_density_maps.glsl`; include only from **full** inject + `genesis_clouds.frag` |
| Hash-only density | `volumetric_clouds_density.glsl` — safe for lite inject via `volumetric_medium.glsl` |
| Shadow compare | `sampler2DShadow` + `shadow.glsl` — full inject only; lite uses `genesis_volume_inject_lite.frag` |
| Froxel inject vs integrate | Inject uses `volume_froxel_math.glsl` only; integrate uses full `volume_froxel.glsl`. |
| **`sampler2DArray` froxel march** | Use **`texture(uFroxelVolume, vec3(uv, slice))`** — never `texelFetch` on GLES. **No `for` loops** over froxel samples (ANGLE allows `sampler2D` loops in god-rays but not `sampler2DArray`). Unroll 8 steps inside one `viMarch8Texture()` (no nested sub-functions). God-ray flow: early `discard`, single `FragColor` write. |
| Integrate includes | Do not pull `godray_integration.glsl` into integrate (shadow + FBM bloat); use `ray_reconstruct.glsl` for `grWorldRayDir` only. |

**Rule:** If a `.frag` compiles on desktop but fails on ES, check whether an include dragged in samplers or helpers the lite pass does not use.

**Duplicate includes:** Header guards (`#ifndef GENESIS_…`) prevent redefinition when `volumetric_medium.glsl` and an entry shader both pull in density helpers.

---

## GLSL features to avoid in shared includes

| Feature | ES / ANGLE note |
|---------|----------------|
| `out` parameters in user functions | Avoid — tests assert no `, out float` in adapted volume shaders |
| `bool` struct fields passed as `out` | Same |
| Dynamic loop bounds from uniforms only | Prefer fixed `const int` + `break` |
| Multiple `#version` lines | Only on entry file |
| `switch` on non-integer | ES 3.0: `switch` parameter must be `int` or `uint` |
| Digraphs / trigraphs | Disallowed in GLSL ES 3.00 |

---

## FBO / texture rules (C# + GL state)

Framebuffer **feedback loop**: a texture is attached to the **currently bound draw FBO** *and* bound to a texture unit that the **active program can sample**, even if the shader never executes `texture()` for it ([Khronos wiki](https://www.khronos.org/opengl/wiki/Framebuffer_Object#Feedback_Loops)).

| Pitfall | Guidance |
|---------|----------|
| **Same-FBO color/depth feedback** | Do not sample a texture **attached to the FBO you are drawing into**. Behavior is **undefined**; ANGLE/D3D11 often **hard-fails** ([Chromium issue discussion](https://groups.google.com/g/webgl-dev-list/c/Fhpra9Euryc)). |
| **Chrome conservative check** | Texture bound to a unit while also a render target may error even if unused — **unbind** (`glBindTexture(..., 0)`) or use separate textures ([Stack Overflow](https://stackoverflow.com/questions/62074822/webgl-feedback-loop-formed-between-framebuffer-and-active-texture)). |
| **Genesis clouds + god rays** | Bake/sample capture depth from a **different** FBO than the draw target (present to default, then composite). |
| **ES depth format** | `GlSceneCaptureTarget` uses `DEPTH24_STENCIL8` on ES; sample `.r` in shaders. |
| **Depth compare mode** | When reading depth as a normal texture (not PCF), set `TextureCompareMode = None`. |
| **ES color attachments** | Use `glDrawBuffers` (see `GlSceneCaptureTarget.ConfigureColorAttachment`). |
| **Shadow maps** | `sampler2DShadow` + `worldToShadowUv` / PCF in `shadow.glsl`; bind on consistent texture units. |
| **Depth writes** | If you must read depth from an attached texture, use a **second texture** filled by `glCopyTexSubImage` / blit in a separate pass — not same-target read/write. |

---

## Vertex shader notes (preview mesh)

See [`entity-preview-gpu-cpu-parity.md`](entity-preview-gpu-cpu-parity.md):

- Bone indices are stored as **float bit-cast** in the VBO and read with `glVertexAttribPointer` (not `glVertexAttribIPointer`) for reliable delivery on ANGLE/GLES.

---

## C# / backend conventions

- **`_volumeUseLiteShaders`**: When true, full froxel inject (shadows, 3D noise, coverage) is unavailable; screen-space or lite froxel only.
- **`TryLoadVolumePrograms`**: Compiles **both** inject and integrate; either failure disables froxel god rays.
- **Diagnostic line** `volume shaders or froxel target failed to init` includes the **adapted** compile log — read the line *before* the reported syntax error.
- **DLL lock**: Stop `netcoredbg` before rebuild if App DLL copy fails.
- **Texture unit hygiene**: After binding FBOs, avoid leaving capture/color textures bound on units used by the next draw if ANGLE feedback checks are triggered.

---

## Debugging failed ES compiles

1. **Reproduce in tests** — adapt the entry `.frag` with `GlslIncludeResolver` + `GlslSourceAdapter` (see `PreviewVolumeInjectShaderEsTests.ResolveAndAdapt`).
2. **Read context lines** in the driver log (`Source context:` in `EmitDiagnostic` output).
3. **Binary search includes** — remove `//!include` lines to see which header introduces the failure.
4. **Compare lite vs full** — if only full fails, suspect `sampler2DShadow`, `sampler3D`, or `shadow.glsl`.
5. **Force desktop GL** (dev only) — Chrome flag `--use-gl=desktop` bypasses D3D translation for isolation ([WebGL dev list](https://groups.google.com/g/webgl-dev-list/c/ce-eNPtj100)). Avalonia/ANGLE on Windows has no equivalent switch in-app; tests are the main preflight.
6. **File ANGLE bugs** at [anglebug.com](https://anglebug.com/) if you have a minimal repro.

---

## Testing checklist (run before merging shader work)

```bash
dotnet test tests/AutoPBR.App.Tests/AutoPBR.App.Tests.csproj --filter "PreviewGlslEsAdapt|PreviewVolumeInject"
```

| Test class | What it guards |
|------------|----------------|
| `PreviewGlslEsAdaptTests` | ES version header, sampler precisions |
| `PreviewVolumeInjectShaderEsTests` | No `out` params; inject packing; lite excludes `sampler3D` / `vcCloudDensityEx` |
| `PreviewVolumeInjectShaderEsTests` | Inject shaders contain **no `return;`** in `main()` |

**Manual:** Run the app with god rays + volumetric clouds on; log should **not** show `Screen-space god-ray fallback active` when froxel path is intended.

**Dump adapted source in a test:**

```csharp
var adapted = GlslSourceAdapter.Adapt(
    GlslIncludeResolver.Resolve("genesis_volume_inject_lite.frag", Read),
    ShaderType.FragmentShader, useOpenGlEs: true);
```

---

## Related docs

- [`volumetric-froxels.md`](volumetric-froxels.md) — froxel grid design and perf budget
- [`volumetric-effects-quality.md`](volumetric-effects-quality.md) — quality presets and pipeline overview
- [`entity-preview-gpu-cpu-parity.md`](entity-preview-gpu-cpu-parity.md) — bone indices / ANGLE vertex attrib note

---

## Quick pre-commit checklist

- [ ] Entry `.frag` / `.vert` only has `#version 330 core`
- [ ] No `return` in fragment `main()` (use `if/else` or `discard`)
- [ ] Single assignment to `FragColor` / other `out` variables
- [ ] New sampler types added to `GlslSourceAdapter` + ES adapt tests
- [ ] Lite shaders do not include `sampler3D` unless C# binds them
- [ ] No FBO feedback: don’t sample textures attached to the bound draw FBO
- [ ] March loops use fixed `const int` bounds
- [ ] Avoid `texture()` inside divergent dynamic loops when possible
- [ ] `flat` on integer varyings if added
- [ ] `PreviewGlslEsAdaptTests` and `PreviewVolumeInjectShaderEsTests` pass
