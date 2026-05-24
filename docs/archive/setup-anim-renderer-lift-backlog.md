# Renderer / RenderState lift backlog (P6)

## Goal

Replace `PreviewRenderStateSynthesis` with bytecode-lifted renderer code that populates the same render-state field bag consumed by `VanillaSetupAnimRuntime`.

## Current contract

`PreviewRenderStateSynthesis` maps CleanRoom preview timing (`animationTimeSeconds`, `idlePhase01`, `wave`) to fields such as:

- `walkAnimationPos`, `walkAnimationSpeed`
- `xRot`, `yRot` (head look degrees)
- `ageInTicks`
- entity-specific flags (`flap`, `flapSpeed`, animation state times for `apply` playback steps)

Lifted setupAnim IR references these names in expression `state` nodes and `playbackSteps`.

## Path to B

1. Lift `LivingEntityRenderer` / mob renderer `extractRenderState` + `setupRotations` slices for catalog entities.
2. Emit `renderer-state/<version>/<Renderer>.json` mapping preview inputs → state fields.
3. Run renderer lift output through the same evaluator; shrink synthesis to a thin fallback for non-lifted renderers.
4. Gate strict parity on `renderer-state` index `ok` rows aligned with `setup-anim-index` coverage.

## Non-goals (until P6)

- Duplicating setupAnim pose math in C# (`Compute*` helpers remain deleted).
- Merging setupAnim IR into animation clinit shards (separate schemas).
