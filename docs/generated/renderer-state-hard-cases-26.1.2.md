# Renderer-State Hard Cases (26.1.2)

This backlog keeps P6 renderer-state waivers explicit. These rows should not block idle-preview pilots or the narrow `RendererStateLift` MVP.

| Category | Examples | Current policy |
|---|---|---|
| Layered / multi-model renderers | `WardenRenderer`, `EnderDragonRenderer` | Keep hand preview synthesis or waiver until renderer-state lift can map fields to baked model layers and render passes. |
| Entity-method scalar probes | `Warden.getTendrilAnimation(float)`, `Warden.getHeartAnimation(float)` | Use documented preview sinusoids; do not treat arbitrary entity methods as lifted state yet. |
| InvokeDynamic layer wiring | emissive layer alpha / texture suppliers | Out of scope for compiler MVP. |
| Pose / enum renderer state | `ParrotRenderer` pose semantics | Requires pose/enum modeling before strict promotion. |
| Renderer-only or non-mob hosts | equipment, block entities, player split layers | Out of scope for first mob-family compiler slice. |
| Geometry-absent interaction clips | `CopperGolemRenderer` chest interaction states | Waived in shard via `waivedSetupAnimStateFields`; preview cycles walk + idle only. |

Promotion can proceed for a renderer-state shard when all non-living-walk setupAnim fields are either represented by the shard or listed as an explicit waiver here and in the shard.
