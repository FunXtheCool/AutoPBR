# SetupAnim IR lift: bytecode patterns

[`SetupAnimLift`](../src/AutoPBR.Tools.AnimationCompiler/SetupAnimLift.cs) lifts `setupAnim` from Mojang `javap -c` for `*Model` classes into `docs/generated/setup-anim/<ver>/<Model>.json`.

Companion lifts:

- [`AnimationModelWiringLift`](../src/AutoPBR.Tools.AnimationCompiler/AnimationModelWiringLift.cs) — ctor `AnimationDefinition.bake` and `KeyframeAnimation.apply*` in `setupAnim`
- [`AnimationClinitLift`](../src/AutoPBR.Tools.AnimationCompiler/AnimationClinitLift.cs) — `*Animation` keyframe data (separate shards)

## Expression AST (`expr`)

| Node | JSON | Example bytecode |
|------|------|------------------|
| Constant | `{ "const": 0.6662 }` | `ldc // float 0.6662f` |
| Render state | `{ "state": "walkAnimationPos" }` | `getfield LivingEntityRenderState.walkAnimationPos:F` |
| Binary op | `{ "op": "mul", "args": […] }` | `fmul` |
| Unary | `{ "op": "cos", "args": […] }` | `invokestatic Mth.cos:(D)F` |
| Branch | `{ "when": {…}, "then": …, "else": … }` | `ifeq` / `ifne` around assignment block |

Assignments target `ModelPart` fields: `partField` (Java field on model) + `property` (`xRot`, `yRot`, `zRot`, `visible`, …).

## Inheritance

When `setupAnim` begins with `invokespecial … setupAnim`, the shard sets `inheritsSetupAnimFrom` to the resolved super FQN and lifts only **delta** assignments in the subclass method.

Template order for batch: `EntityModel`, `QuadrupedModel`, `HumanoidModel`, then leaf models.

## Playback (`playbackSteps`)

| Mode | Bytecode |
|------|----------|
| `apply` | `KeyframeAnimation.apply(AnimationState, float age)` |
| `applyWalk` | `KeyframeAnimation.applyWalk(float pos, float speed, float, float)` |
| `setVisible` | `ModelPart.visible = AnimationState.isStarted()` |

## Regenerate

```powershell
pwsh -File tools/Generate-SetupAnimIndex.ps1 -ClientJar tools/minecraft-parity/26.1.2/client.jar -VersionLabel 26.1.2 -Parallel -Stats
```

## Preview runtime

[`PreviewRenderStateSynthesis`](../src/AutoPBR.Core/Preview/PreviewRenderStateSynthesis.cs) maps preview time to render-state fields only (no pose formulas). [`VanillaSetupAnimRuntime`](../src/AutoPBR.Core/Preview/VanillaSetupAnimRuntime.cs) evaluates lifted IR.
