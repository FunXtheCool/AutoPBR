# Generated Minecraft client reference

**Generated reference corpus — do not hand-edit.** Regenerate artifacts via `tools/` scripts (see sections below). Hand-written docs live under `docs/` (not this folder).

Indexes are **per game version** (`minecraft-client-model-index-<version>.md` / `.json`). Class lists, JAR paths, and `javapPublic` signatures **differ by release** (obfuscation vs named bytecode, class count, and bytecode). Do not assume one version’s JSON applies to another.

| Game version | Mapping source | Index files |
|--------------|----------------|-------------|
| **1.21.11** | ProGuard `client_mappings.txt` (`downloads.client_mappings`) | `minecraft-client-model-index-1.21.11.*` |
| **26.1.2** | *(none — Mojang omits `client_mappings` in `version.json`; JAR ships named `net/minecraft/...` classes)* | `minecraft-client-model-index-26.1.2.*` |

## Obfuscated client (1.21.11 example)

```powershell
pwsh -File tools/minecraft-parity/Ensure-MinecraftClientJar.ps1 -VersionDir tools/minecraft-parity/1.21.11
pwsh -File tools/minecraft-parity/Ensure-MinecraftClientMappings.ps1 -VersionDir tools/minecraft-parity/1.21.11
pwsh -File tools/Generate-MinecraftClientModelIndex.ps1 `
  -ClientJar tools/minecraft-parity/1.21.11/client.jar `
  -Mappings tools/minecraft-parity/1.21.11/client_mappings.txt `
  -VersionLabel 1.21.11 `
  -ManifestJson src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_texture_model_manifest.json
```

## Named bytecode client (26.1.2)

```powershell
pwsh -File tools/minecraft-parity/Ensure-MinecraftClientJar.ps1 -VersionDir tools/minecraft-parity/26.1.2
pwsh -File tools/Generate-MinecraftClientModelIndex.ps1 `
  -ClientJar tools/minecraft-parity/26.1.2/client.jar `
  -VersionLabel 26.1.2 `
  -ManifestJson src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_texture_model_manifest.json
```

Prerequisites:

1. **client.jar** — `tools/minecraft-parity/Ensure-MinecraftClientJar.ps1` with `-VersionDir` pointing at the folder that contains `version.json`.
2. **client_mappings.txt** — only when `version.json` includes `downloads.client_mappings`; use `Ensure-MinecraftClientMappings.ps1`. For **26.1.2**, that entry is absent; the generator **auto-detects** named bytecode and does not need a mappings file.

The generator walks **official** class names under `net.minecraft.client.model.` and `net.minecraft.client.animation.` — from ProGuard mappings on obfuscated jars, or from JAR paths when bytecode is already named.

For **`net.minecraft.client.animation.definitions.*Animation`** (mob `AnimationDefinition` tables), **`javap -public` is not enough** (it only shows `public static final AnimationDefinition …;`). The script also runs **`javap -c`** and writes **`minecraft-client-model-index-<ver>-animation-init/*.javapc.txt`**, and each JSON class row may include **`javapBytecodeCRelPath`** (relative to `docs/generated/`).

Optional **local** Vineflower decompile (gitignored under `.tmpbuild/decompiled-<label>/`):

```powershell
pwsh -File tools/Generate-MinecraftClientModelIndex.ps1 `
  ... `
  -DecompileOutDir .tmpbuild/decompiled-26.1.2 `
  -VineflowerJar path\to\vineflower.jar
```

Do not commit full Mojang decompiler output; only the Markdown summary + JSON index in this folder belong in git.

## Geometry IR and preview deltas (structured parity)

**Policy:** commit **geometry shards**, **preview-delta overlays**, **geometry-index-*.json**, and **JSON Schema** under `docs/generated/` for pinned versions. Batch mode emits **one shard per model class** from the class list (hundreds of files for 26.1.2); shards merge **jar metadata**, optional **float probe** results, and **structural mesh IR** when the 26.x-style extractor profile matches bytecode (otherwise placeholders or prior hand edits remain). Do **not** commit Mojang `client.jar`.

| Artifact | Purpose |
|----------|---------|
| `schema/geometry-ir.schema.json` | Validates vanilla-oriented `createBodyLayer`-style part trees (see `geometry-ir-conventions.md`). |
| `schema/preview-delta.schema.json` | Validates AutoPBR-only interpretation notes (renderer-only scale, skipped inflates, and similar). |
| `schema/geometry-index.schema.json` | Validates per-version manifests listing classes, shard paths, and `extractionStatus`. |
| `geometry/<versionLabel>/<official.jvm.Name>.json` | Per-class geometry IR (hand-maintained or merged by the tool). |
| `preview-deltas/<versionLabel>/<official.jvm.Name>.json` | Preview interpretation deltas keyed by the same official JVM name. |
| `geometry-index-<versionLabel>.json` | Version-level manifest: one row per batched class. See `geometry-ir-conventions.md` for `extractionStatus` (`ok` includes successful javap mesh lift; `heuristic` = floats only; `partial` / `skipped` otherwise). |

**Regenerate / merge** (SHA-256 of `.class`, `javap -c` float probe, and **26.x-style structural mesh lift** when bytecode matches) for **every** class in the batch list (default: `minecraft_*_client_model_classes.txt`). Missing shards are synthesized, then merged. Full batch can take **several minutes** (many `javap` subprocesses; alternate mesh hosts may add extra disassemblies per class).

**Caveats (mesh lift):** The compiler only understands common **`CubeListBuilder` / `PartPose` / `PartDefinition.addOrReplaceChild`** patterns (including `CubeDeformation` `addBox` overloads). It resolves **same-package** `Adult*` / `Baby*` / `Cold*` / `Warm*` mesh hosts and follows **one** `invokestatic` hop from `createBodyLayer` to another class’s `MeshDefinition` factory when the entry class has no `addBox` itself. It does **not** overwrite shards that already look **hand-authored**. Obfuscated jars depend on mappings/`javap` fidelity. Details: [`geometry-ir-conventions.md`](geometry-ir-conventions.md#bytecode-extraction-caveats-26x-javap-mesh-lift).

```powershell
pwsh -File tools/Generate-GeometryIndex.ps1 `
  -ClientJar tools/minecraft-parity/26.1.2/client.jar `
  -VersionLabel 26.1.2
```

Obfuscated **full batch** (requires `client.jar` + `client_mappings.txt`; class list: `minecraft_1.21.11_client_model_classes.txt`, regenerated from `minecraft-client-model-index-1.21.11.json` via `tools/build_client_model_class_list_from_model_index_json.py` when the index changes):

```powershell
pwsh -File tools/Generate-GeometryIndex.ps1 `
  -ClientJar tools/minecraft-parity/1.21.11/client.jar `
  -Mappings tools/minecraft-parity/1.21.11/client_mappings.txt `
  -VersionLabel 1.21.11
```

Single-class example (cow package + mappings):

```powershell
pwsh -File tools/Generate-GeometryIndex.ps1 `
  -ClientJar tools/minecraft-parity/1.21.11/client.jar `
  -Mappings tools/minecraft-parity/1.21.11/client_mappings.txt `
  -VersionLabel 1.21.11 `
  -Single net.minecraft.client.model.animal.cow.CowModel
```

**CI drift:** the `AutoPBR.GeometryCompiler.Tests` project validates committed JSON against the schemas on every build. Optional local diff: re-run the script after a jar bump and inspect `git diff docs/generated/geometry*` / `geometry-index*.json`.

**Lift-quality report:** Regenerate `geometry-lift-quality-26.1.2.json` via `AUTOPBR_WRITE_GEOMETRY_LIFT_QUALITY` (see [test guidance](../test-guidance-geometry-animation-ir.md#running-diagnostic-tests)); assembly/world-pose lift backlog and phases are in [`geometry-lift-assembly-parity-roadmap.md`](../geometry-lift-assembly-parity-roadmap.md).

**Assembly-parity pilot refresh (56 JVMs only — not full ~761-class index):**

```powershell
pwsh -File tools/regen-assembly-pilots.ps1
# Optional: -KeepRevert to score each shard vs pre-run backup; -SkipReference if JDK 25 unavailable
pwsh -File tools/regen-assembly-pilots.ps1 -KeepRevert -JavaHome $env:USERPROFILE\.autopbr\jdk-25
```

Pilot list: [`geometry-assembly-parity-pilots-26.1.2.txt`](geometry-assembly-parity-pilots-26.1.2.txt). Manual Explore sign-off: [`assembly-pilot-explore-checklist.md`](assembly-pilot-explore-checklist.md).

**Animation `<clinit>` prototype:** the geometry compiler CLI can print a one-line summary from an existing `javap -c` sidecar (same layout as the model index generator):

```powershell
dotnet run --project src/AutoPBR.Tools.GeometryCompiler -- --print-animation-summary minecraft-client-model-index-26.1.2-animation-init/net_minecraft_client_animation_definitions_ArmadilloAnimation.javapc.txt
```
