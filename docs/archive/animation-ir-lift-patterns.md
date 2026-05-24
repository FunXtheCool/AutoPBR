# Animation IR lift: bytecode patterns

[`AnimationClinitLift`](../src/AutoPBR.Tools.AnimationCompiler/AnimationClinitLift.cs) lifts Mojang `*Animation` classes from `javap -c` `<clinit>` output. Generator: **AutoPBR.Tools.AnimationCompiler** only (not GeometryCompiler).

## Supported today

- `AnimationDefinition$Builder.withLength` + `putstatic` per definition field.
- Per-channel `addAnimation(String, AnimationChannel)` (builder style) and `addAnimation(String, AnimationChannel)` after inline `AnimationChannel.<init>(Target, Keyframe[])`.
- Keyframes: `new Keyframe` … `KeyframeAnimations.degreeVec|posVec|scaleVec` with **FFF** or **DDD** components, `Interpolations.*`, `Keyframe.<init>(F, Vector3fc, Interpolation)`.

## Previously missed (now handled)

| Pattern | Example | Symptom in IR |
|---------|---------|----------------|
| `scaleVec:(DDD)` + `dconst_*` / `ldc2_w` | Nautilus `SWIMMING` body SCALE, Sniffer clips | Channel row with `"keyframes": []` |
| Array-filled channels | `anewarray Keyframe` + `aastore` per keyframe | Whole definition `"channels": []` when only DDD failed |
| `fconst_1` before `withLength` | Nautilus loop length | Missing `lengthSeconds` |

## Still difficult / deferred

- Keyframes built only via helper methods without inline `new Keyframe` in `<clinit>`.
- Adult `FoxAnimation` is not a separate `AnimationDefinition` holder in 26.1.2 (baby fox uses `FoxBabyAnimation`). Add a row to `minecraft_*_client_animation_definition_classes.txt` only if a matching `*Animation.class` exists in the jar.
- **1.21.11 / ProGuard (2026-05-21):** pass `-Mappings` to `Generate-AnimationIndex.ps1`; [`AnimationJavapObfuscationNormalizer`](../src/AutoPBR.Tools.AnimationCompiler/AnimationJavapObfuscationNormalizer.cs) rewrites obfuscated `javap -c` before `AnimationClinitLift`. **10/10** holders in the 1.21.11 jar lift **`ok`**; six 26.1.2-only definition classes are absent from that jar, not blocked on clinit patterns.

Regenerate shards:

```powershell
pwsh -File tools/Generate-AnimationIndex.ps1 -ClientJar tools/minecraft-parity/26.1.2/client.jar -VersionLabel 26.1.2
pwsh -File tools/Generate-AnimationIndex.ps1 -ClientJar tools/minecraft-parity/1.21.11/client.jar -Mappings tools/minecraft-parity/1.21.11/client_mappings.txt -VersionLabel 1.21.11
```
