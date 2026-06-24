# Dynamic Runtime Geometry Roadmap

## Context

AutoPBR is approaching the end of the fully built-in, manually generated, clean-room/emulated vanilla entity model phase. The long-term goal is to preview vanilla and modded entity textures through runtime-parsed geometry and animation data instead of relying on hand-authored fallback rigs.

This document captures the planned direction for dynamic runtime geometry, including resource-pack `.zip` inputs, mod `.jar` inputs, OptiFine-style declarative models, Blockbench/GeckoLib-style assets, and static bytecode-derived model data.

The guiding rule is:

> Ship the parser, IR, converter, renderer, and compatibility logic. Prefer user-provided or locally discovered model/texture data over bundled third-party asset data.

## IP Posture

The clean-room C# model system lowers risk because AutoPBR does not directly bundle Mojang Java source, decompiled Java code, or vanilla textures. However, it is not a magic shield: exact vanilla geometry, UV layout, hierarchy, and animation behavior can still be argued to encode expressive game asset data.

The safer long-term architecture is to make AutoPBR a tool that reads user-provided assets or local Minecraft/mod assets and converts them into AutoPBR's runtime IR for preview.

Lower-risk approach:

- Do not bundle vanilla textures.
- Do not bundle extracted Mojang model JSON values as app data.
- Do not ship decompiled code or ripped asset files.
- Parse local/user-provided packs, mods, or Minecraft installations at runtime.
- Treat vanilla support as one provider in the same runtime model system used for modded content.

Higher-risk approach:

- Bundling lifted vanilla JSON/model values directly in the app/repo.
- Shipping exact extracted geometry/UV/animation data as replacement assets.
- Marketing with Mojang/Minecraft branding as a dominant product identity.

Best target:

```text
AutoPBR ships:
  - IR schema
  - parsers/importers
  - converters
  - preview renderer
  - compatibility logic

AutoPBR does not ship:
  - Mojang textures
  - copied vanilla model JSON
  - decompiled Java code
  - extracted vanilla asset payloads
```

## Runtime Geometry Goal

The preview renderer should be able to handle brand-new modded assets by resolving model data at runtime, converting it into canonical geometry IR, and feeding the existing mesh emission/bake path.

Desired flow:

```text
selected texture
  -> archive model inventory
  -> runtime model resolver
      -> declarative model importers
      -> static bytecode lift importer for .jar only
      -> bundled/local vanilla IR fallback
      -> CleanRoom fallback
  -> canonical geometry IR
  -> existing mesh emitter/baker
  -> preview renderer
```

Important constraint:

> Never execute mod code. For `.jar` inputs, treat classes as data and statically inspect bytecode only.

## Existing Codebase Anchors

The renderer already has several useful boundaries:

- `IAssetSource` can read from zip or directory archives.
- `MinecraftModelMerger` already handles standard Java model JSON inheritance/texture maps.
- `GeometryIrDocumentLoader` loads bundled geometry IR shards.
- `GeometryIrMeshEmitter` emits geometry IR into the entity/block mesh path.
- `MinecraftModelBaker` bakes `MergedJavaBlockModel` into preview vertices, indices, and draw batches.
- `PreviewMeshProvenance` already reports which mesh pipeline produced the preview.

The main missing piece is a runtime model resolver that can discover and import modded model formats before the current CleanRoom fallback path.

## Archive Classification

Add archive classification using extension and contents.

Suggested categories:

- `.zip` with `pack.mcmeta` and `assets/**/optifine/**`: resource pack with OptiFine model assets.
- `.zip` with assets/textures/models only: regular resource pack.
- `.jar` with `fabric.mod.json`, `META-INF/mods.toml`, or `quilt.mod.json`: mod jar.
- `.jar` with mostly `assets/**` and no meaningful `.class` files: resource-pack-like jar.
- `.jar` with `.class` and declarative model assets: hybrid mod jar.
- `.jar` with `.class` only or no useful model sidecars: static bytecode lift candidate.

Declarative model assets should always win over bytecode because they are cheaper, safer, and more likely user-authored.

## Model Metadata Index

`PackScannerService` currently indexes only `.png` entries for the explorer. Add a separate lightweight model metadata scan, preferably in Core, that records model-ish entries without loading entire archives into memory.

Entries to index:

- `.jem`
- `.jpm`
- `.properties`
- `.geo.json`
- `.animation.json`
- Java model JSON under `assets/*/models/**`
- AutoPBR IR sidecars, if introduced
- `.class`
- `pack.mcmeta`
- `fabric.mod.json`
- `META-INF/mods.toml`
- `quilt.mod.json`

Potential output type:

```csharp
internal sealed record ArchiveModelInventory(
    ArchiveKind Kind,
    IReadOnlyList<string> JavaModelJsonEntries,
    IReadOnlyList<string> OptifineModelEntries,
    IReadOnlyList<string> GeckoLibModelEntries,
    IReadOnlyList<string> AutoPbrIrEntries,
    IReadOnlyList<string> ClassEntries,
    IReadOnlyDictionary<string, string> MetadataEntries);
```

Keep this separate from texture scanning. Texture scanning answers "what maps should we process?" The model inventory answers "what model data can preview use?"

## Runtime Resolver Priority

For a selected texture, resolve model data in this order:

1. AutoPBR geometry IR sidecar, if present.
2. Standard Java block/item model JSON, using the existing `MinecraftModelMerger`.
3. OptiFine CEM/JEM/JPM assets.
4. GeckoLib/Blockbench `.geo.json` assets.
5. Vanilla/local/bundled geometry IR provider.
6. `.jar` static bytecode lift, only when a plausible class can be mapped.
7. CleanRoom fallback.
8. 2D-only preview.

This keeps runtime model support additive and allows CleanRoom to phase out naturally.

## Canonical Import Contract

Do not make the renderer understand every model format. Every runtime importer should convert source data into the same canonical geometry IR document shape already used by the geometry IR emitter.

Importers to add:

- `AutoPbrGeometryIrImporter`
- `JavaBlockModelImporter`
- `OptifineCemImporter`
- `GeckoLibGeoImporter`
- `JarBytecodeGeometryImporter`

All importers should output:

- root parts
- child part hierarchy
- part poses
- cuboids
- `from` / `to`
- `uvOrigin`
- optional UV spans
- optional texture keys
- optional face masks
- optional inflate/shell/layer metadata

The renderer should still receive `MergedJavaBlockModel` or `PreviewModelSubject`, not format-specific model structures.

## Split IR Sources From Bundled Vanilla Data

`GeometryIrDocumentLoader` is currently coupled to bundled paths under:

```text
Data/minecraft-native/geometry/<version>/<jvm>.json
```

Refactor toward source providers:

```csharp
internal interface IGeometryIrSource
{
    bool TryLoad(string key, out JsonElement root);
}
```

Provider types:

- bundled vanilla source
- runtime archive source via `IAssetSource`
- local Minecraft install source
- generated cache source
- test fixture source

This makes vanilla, modded, and user-supplied geometry all enter the same mesh path.

## `.zip` Resource Pack Lane

For `.zip` resource packs, assume no executable code-based model sidecars. Expect declarative assets:

- standard Java block/item model JSON
- OptiFine CEM/JEM/JPM
- texture metadata/properties
- possible Blockbench-exported assets if packed as data

Runtime behavior:

1. Scan model metadata.
2. Build texture-to-model associations.
3. Prefer explicit OptiFine/GeckoLib/AutoPBR IR sidecars.
4. Fall back to existing Java model JSON resolver.
5. Fall back to vanilla/local IR if the texture is vanilla-like.
6. Fall back to CleanRoom only when no runtime model data exists.

## `.jar` Mod Lane

For `.jar` archives, there may be:

- normal resource-pack-like assets under `assets/**`
- OptiFine models
- GeckoLib/Blockbench assets
- renderer/model classes
- no useful declarative sidecars

Runtime behavior:

1. Treat declarative assets exactly like the `.zip` lane.
2. If no declarative model is found, inspect mod metadata and class entries.
3. Try to map texture paths to model/renderer classes.
4. Static-lift candidate model classes into geometry IR.
5. Cache bytecode-lifted IR by archive hash plus class name.
6. Never load or execute mod classes.

Bytecode lifting should be bounded:

- cancellation-aware
- time-limited
- memory-bounded
- cached
- best-effort
- provenance-visible

## Bytecode Lift Refactor

The current bytecode lift logic lives primarily in `AutoPBR.Tools.GeometryCompiler`. Runtime `.jar` support should extract the reusable pieces into Core or a small shared library:

- class entry lookup
- class byte reading
- bytecode mesh resolution
- geometry IR construction
- optional mapping support
- no CLI file-output assumptions
- no dependency on shelling out to `javap` if avoidable

The CLI compiler can then call the shared runtime-safe library instead of owning the only implementation.

## Texture-To-Model Association

This is likely the hardest part.

Declarative sources usually help:

- Java model JSON has `textures`.
- OptiFine CEM/JEM/JPM can declare model and texture relationships.
- GeckoLib assets often have model/texture/animation path conventions.

Code-only jars are harder:

- model classes do not always map cleanly to texture paths.
- renderer classes may contain texture identifiers.
- entity registration can be indirect.
- obfuscation/mappings complicate class discovery.

Recommended order:

1. Implement explicit sidecar/declarative association first.
2. Add heuristic class association later.
3. Keep bytecode lift as a best-effort provider, not the first milestone.

## Preview Integration Point

The main integration point is `ResourcePackConverter.RenderPreviewDetailedAsync`.

Before the existing `JavaModelPathResolver.TryResolveModelJsonFromTexture(...)` path, call a new runtime resolver:

```text
RuntimePreviewModelResolver.TryResolve(...)
```

If it succeeds:

1. Materialize all required textures into the preview temp folder.
2. Scan generated/required textures.
3. Generate normal/specular maps for those textures.
4. Bake the merged model.
5. Return `PreviewDetailedResult` with a `PreviewModelSubject`.

If it fails, continue through the existing paths.

## Provenance And Debugging

Extend `PreviewMeshProvenance` so logs and overlays can identify runtime sources:

- `Mesh: AutoPBR runtime IR`
- `Mesh: Java model JSON`
- `Mesh: OptiFine CEM`
- `Mesh: GeckoLib geo.json`
- `Mesh: jar bytecode lift`
- `Mesh: bundled vanilla geometry IR`
- `Mesh: CleanRoom fallback`

This is important for bug reports and for validating that CleanRoom is actually being phased out.

## Test Matrix

Add fixture archives for:

- `.zip` resource pack with OptiFine CEM.
- `.jar` mod with OptiFine/asset model sidecars.
- `.jar` mod with GeckoLib/Blockbench assets.
- `.jar` with classes but no sidecar, expected bytecode candidate or graceful fallback.
- malformed model files.
- missing texture bindings.
- multiple namespaces.
- selected texture used by multiple possible models.
- vanilla texture resolved through runtime/local IR provider.
- vanilla texture falling back to CleanRoom only when runtime IR is unavailable.

## Milestones

### Milestone 1: Inventory And Resolver Skeleton

- Add archive kind detection.
- Add `ArchiveModelInventory`.
- Add `RuntimePreviewModelResolver`.
- Add provenance for runtime resolver attempts.
- Keep all existing preview behavior intact.

### Milestone 2: Declarative Runtime IR

- Add AutoPBR geometry IR sidecar loading.
- Split IR source loading from bundled vanilla paths.
- Convert loaded IR into existing mesh emission path.
- Add tests with direct IR sidecars.

### Milestone 3: OptiFine Model Import

- Parse CEM/JEM/JPM.
- Convert to canonical geometry IR.
- Resolve texture bindings.
- Add `.zip` resource-pack fixture tests.

### Milestone 4: GeckoLib/Blockbench Import

- Parse `.geo.json`.
- Convert bones/cubes/pivots/rotations to canonical geometry IR.
- Add animation handling only after static geometry is reliable.
- Add `.jar` and `.zip` fixture tests.

### Milestone 5: Runtime Vanilla Provider

- Route vanilla preview through the same runtime provider interface.
- Prefer local/user-provided Minecraft data when available.
- Keep bundled data only as a compatibility fallback if still desired.

### Milestone 6: Static `.jar` Bytecode Lift

- Extract reusable bytecode lift from compiler tooling.
- Add class discovery and texture-to-renderer/model heuristics.
- Add cache and cancellation.
- Keep bytecode lift behind strict fallback/provenance.

### Milestone 7: CleanRoom Phase-Out

- Track which preview routes still use CleanRoom.
- Replace remaining routes with runtime IR/importers.
- Keep CleanRoom only as a debug/emergency fallback until no longer needed.

## Recommended First Build Target

Start with:

```text
runtime model inventory
  + resolver skeleton
  + direct AutoPBR IR sidecars
  + OptiFine/GeckoLib importer foundations
```

Do not start with bytecode lifting. Declarative model support gives the biggest modded-resource-pack win, creates the right architecture, and keeps the risk profile lower. Bytecode lifting should come after the runtime resolver and IR provider system are already stable.
