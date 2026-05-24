# P6 renderer-state lift — compiler blockers (26.1.2)



**Status:** Hand JSON pilots only (Breeze, Sniffer, Allay, Camel, Warden, Frog, Creaking, **Nautilus**, **CopperGolem** — 2026-05-21).  

**Canonical plan:** [`runtime-ir-preview-plan.md`](../runtime-ir-preview-plan.md) Part D.



## Why `RendererStateLift` is not a small compiler slice yet



| Blocker | Example | Impact |

|---------|---------|--------|

| **Multi-model renderers** | `WardenRenderer` bakes `WARDEN`, `WARDEN_BIOLUMINESCENT`, `WARDEN_PULSATING_SPOTS`, `WARDEN_TENDRILS`, `WARDEN_HEART` + four `LivingEntityEmissiveLayer` passes | `extractRenderState` copies `AnimationState` into one `WardenRenderState`, but preview mesh is a single layer; compiler must index which fields apply to which baked model |

| **Entity float probes** | `Warden.getTendrilAnimation(F)`, `Warden.getHeartAnimation(F)` | Not `AnimationState`; needs entity-method lift or documented preview synthesis |

| **InvokeDynamic layer wiring** | Warden ctor uses `invokedynamic` for layer alpha/texture suppliers | Javap lift of `extractRenderState` alone does not describe emissive overlay policy |

| **Renderer-only hosts** | Equipment, block entities, player split layers | Out of scope for first mob-family compiler slice |



## Landed hand pilots (2026-05-21)



| Mob | Shard | Preview driver | Notes |

|-----|-------|----------------|-------|

| Breeze | `BreezeRenderer.json` | `breeze_clip_cycle` | Multi-clip idle/shoot/slide stack |

| Sniffer | `SnifferRenderer.json` | `sniffer_clip_cycle` | Dig / stand / happy mood clips |

| Allay | `AllayRenderer.json` | `allay_hold_dance_cycle` | Hold + dance `AnimationState` |

| Camel | `CamelRenderer.json` | `camel_clip_cycle` | Sit / standup / dash / idle |

| Warden | `WardenRenderer.json` | `warden_clip_cycle` | Six mood clips + walk; `tendrilAnimation` / `heartAnimation` sinusoids |

| Frog | `FrogRenderer.json` | `frog_clip_cycle` | Jump / croak / tongue / swim-idle; walk via `applyWalk` |

| Creaking | `CreakingRenderer.json` | `creaking_clip_cycle` | Attack / invulnerable / death + walk |

| Nautilus | `NautilusRenderer.json` | `nautilus_swim_walk` | No mood `AnimationState`; `SWIMMING` via `applyWalk` on walk fields |

| Copper golem | `CopperGolemRenderer.json` | `copper_golem_clip_cycle` | Walk + idle; chest-interaction clips **not** in preview cycle (no `chest` geometry part — B.3) |



## Tractable next pilots (hand JSON + synthesis)



- *(none queued — add when a mob needs honest `AnimationState` timing beyond `ForLivingWalk`)*



## Non-goals until compiler exists



- Lifting emissive multi-pass alpha from `LivingEntityEmissiveLayer` bytecode.

- Merging renderer-state shards into setupAnim or animation clinit JSON.

