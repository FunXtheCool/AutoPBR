# Naming Conventions

Authoritative naming rules for AutoPBR source, tests, scripts, and generated data. Apply these before adding files or renaming existing ones.

**Related:** [Project quality standards](project-quality-standards.md), [Large-class split plan](large-class-split-agent-plan.md)

## C# types and files

| Rule | Convention | Example |
|------|------------|---------|
| File name | PascalCase; matches the primary public type in the file | `TextureOverrides.cs` |
| Partial splits | `TypeName.Area.cs` (area = cohesive feature slice) | `OpenGlPreviewBackend.Render.PassSetup.cs` |
| One primary type per file | Prefer splitting multi-type barrels into separate files | `ZipAssetSource.cs`, `DirectoryAssetSource.cs` |
| Interfaces | `I` prefix | `IAssetSource` |
| Enums | Singular noun; file name matches enum or describes the group | `PreviewDepthLayerKinds.cs` |

## Namespaces

Namespaces mirror the folder path after the project root:

| Folder | Namespace |
|--------|-----------|
| `src/AutoPBR.Core/` | `AutoPBR.Core` |
| `src/AutoPBR.Core/Models/` | `AutoPBR.Core.Models` |
| `src/AutoPBR.Preview/` | `AutoPBR.Preview` |
| `src/AutoPBR.Preview/Preview/Entities/` | `AutoPBR.Preview.Entities` |
| `src/AutoPBR.Preview/Preview/Blocks/` | `AutoPBR.Preview.Blocks` |
| `src/AutoPBR.Preview/Preview/GeometryIr/` | `AutoPBR.Preview.GeometryIr` |
| `src/AutoPBR.Preview/Preview/Parity/` | `AutoPBR.Preview.Parity` |
| `src/AutoPBR.Preview/Preview/Generated/` | `AutoPBR.Preview.Generated` |
| `src/AutoPBR.Contracts/` | `AutoPBR.Contracts` |
| `src/AutoPBR.Contracts/GeometryIr/` | `AutoPBR.Contracts.GeometryIr` |
| `src/AutoPBR.App/Rendering/OpenGL/` | `AutoPBR.App.Rendering.OpenGL` |

Do not use a flat namespace when subfolders exist.

## Branding

- **Assemblies, namespaces, and public product name:** `AutoPBR` (capital PBR).
- Options and defaults types: `AutoPBROptions`, `AutoPBRDefaults` (not `AutoPbr*`).
- CLI flags and user-facing strings may use `autopbr` in lowercase where appropriate.

## OpenGL naming (intentional split)

| Layer | Convention | Example |
|-------|------------|---------|
| Folder | `Rendering/OpenGL/` | Full acronym |
| C# classes | `OpenGl*` prefix | `OpenGlPreviewBackend` |
| Short helpers | `Gl*` when wrapping a single GL object | `GlTexture2D` |
| GLSL shaders | `snake_case` | `material_labpbr.glsl`, `godrays.glsl` |

Do not mass-rename `OpenGl*` types to `OpenGL*`; the folder/class split is documented and stable.

## Domain abbreviations (glossary)

| Abbreviation | Meaning | Example |
|--------------|---------|---------|
| `Ir` | Intermediate representation (geometry, animation, setupAnim) | `GeometryIrDocument` |
| `Ml` | Machine learning (ONNX inference) | `MlSpecularInference` |
| `Javap` | Java bytecode disassembly tooling | `JavapFloatGeometryMeshLift` |
| `Jvm` | Java virtual machine / class metadata | `JvmBytecodeDisassembler` |
| `Gpu` | GPU mesh/bone path | `EntityGpuBoneDispatch` |
| `Uv` | Texture coordinates | `EntityUvPolicy` |
| `Pbr` | Physically based rendering (in compound names only when not the product name) | LabPBR channel semantics |

Keep `GeometryIr*` and `Javap*` prefixes; they are domain vocabulary, not clutter.

## Tests

| Rule | Convention | Example |
|------|------------|---------|
| Class name | Describes behavior under test | `PigPreviewAttachmentTests` |
| Avoid | Milestone/phase prefixes | ~~`Phase5ReferenceAlignmentTests`~~ → `GeometryIrReferenceAlignmentTests` |
| Parity suites | `*ParityTests` | `EntityTextureParityCatalogTests` |
| Lift/compiler | `*LiftTests` | `HumanoidDelegatedMeshLiftTests` |
| Preview | `*PreviewTests` | `BlockPreviewRenderingTests` |
| Ad-hoc probes | Not committed; use local `.csx` outside the repo or `scripts/` scratch | — |

## Scripts and tools

| Language | Convention | Example |
|----------|------------|---------|
| PowerShell | `Verb-Noun.ps1` | `Generate-GeometryIndex.ps1` |
| Python | `snake_case.py` | `build_entity_coverage_matrix.py` |
| One-off refactor scripts | `scripts/archive/` after the refactor lands | — |

Deprecate kebab-case script names over time when touching a script.

## Data and generated output

| Location | Purpose |
|----------|---------|
| `docs/` | Hand-written architecture, roadmaps, standards |
| `docs/generated/` | Generated reference corpus (geometry/animation IR indexes, javap dumps). **Do not hand-edit.** Regenerate via `tools/` scripts. |
| `src/AutoPBR.Preview/` (csproj `Content`) | Shipped preview runtime data: copies `docs/generated/**` and `Data/minecraft-native/**` into build output |
| `src/AutoPBR.Core/Data/minecraft-native/` | Conversion-side native manifests and policies still owned by Core |
| `src/AutoPBR.Core/Data/ONNX-AI/` | Bundled ONNX models |
| `src/AutoPBR.Core/Data/native/` | Redistributed CUDA/cuDNN/ORT native DLLs |
| `tools/MinecraftGeometryReference/reference-output/` | Java reference baker output (dev/parity; not the canonical shipped tree) |

**Canonical geometry IR shards for the app:** `docs/generated/geometry/` → copied into output via `AutoPBR.Preview` project `Content` items (not duplicated under Core).

## Entity partial filenames

`EntityModelRuntime` partials live under `src/AutoPBR.Preview/Preview/Entities/` and use the filename pattern:

```
EntityModelRuntime.<Family>.cs
```

Example: `EntityModelRuntime.Quadrupeds.cs` (class remains `EntityModelRuntime`).

## What not to rename

- GLSL shader files (`snake_case` is correct for GLSL).
- Committed `docs/generated/` JSON unless regenerating from tooling.
- Entity GPU bone slot order or parity catalog route order (behavior-sensitive).
