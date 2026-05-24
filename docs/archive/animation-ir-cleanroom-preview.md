# Animation IR → CleanRoom live preview

## SetupAnim IR (procedural + playback)

Bytecode-lifted `*Model.setupAnim` shards live under `docs/generated/setup-anim/<ver>/` and copy to `Data/minecraft-native/setup-anim/<ver>/`. Index: `setup-anim-index-26.1.2.json`.

- **Compiler:** `AutoPBR.Tools.AnimationCompiler` (`--lift-setup-anim`), `tools/Generate-SetupAnimIndex.ps1`
- **Runtime:** `VanillaSetupAnimRuntime` + `PreviewRenderStateSynthesis` (preview timing only, not pose formulas)
- **Definition clips:** `DefinitionAnimationPreviewSampling` samples `AnimationDefinition` channels; `playbackSteps` on model shards call the same path as `KeyframeAnimation.apply` / `applyWalk`
- **Geometry IR motion:** `ApplySetupAnimToGeometryIrMesh` for catalog tiers with lifted shards (chicken + standard quadruped builders); `TryApplyDefinitionAnimationGeometryIrPreviewPass` after successful catalog geometry IR emit (Armadillo tail, Breeze idle wind + shoot head, baby Fox hind leg)

Hand-maintained `Compute*` setupAnim mirrors and `*VanillaKeyframes` fallbacks are removed; see `HandParityForbiddenSymbolsTests`.

## Animation definition IR (clinit keyframes)

Bytecode-lifted animation definitions ship as JSON under `docs/generated/animation/<ver>/` and are copied at build time to:

`Data/minecraft-native/animation/<ver>/*.json`

Runtime sampling is implemented in `VanillaAnimationIrPreviewSampler` and `DefinitionAnimationPreviewSampling` (`src/AutoPBR.Core/Preview/`). Sampling supports **keyframes whose per-keyframe `interpolation` is entirely `LINEAR`**. Channels marked `CATMULLROM`, `MIXED`, or with empty keyframe arrays are skipped until the compiler/lift improves.

## Wired today (26.1.2 profile)

| Animation JSON | Definition / part | Preview hook |
|----------------|-------------------|--------------|
| `BreezeAnimation` | `IDLE` `wind_mid` / `wind_top` POSITION | `BuildBreeze` wind tiers; geometry IR catalog (`breeze_wind` / main when wind parts emitted) |
| `NautilusAnimation` | `SWIMMING` `upper_mouth` ROTATION / `body` SCALE | `BuildNautilusMob` via `TrySampleNautilusSwimmingBodyScale` + upper-mouth rotation |
| `SnifferAnimation` | `SNIFFER_LONGSNIFF` `head` ROTATION | Sniffer GPU + parity `headPitch` |
| `SnifferAnimation` | `SNIFFER_WALK` `body` ROTATION (head walk is CATMULLROM) | Sniffer GPU + parity additive pitch |
| `SnifferAnimation` | `SNIFFER_WALK` `right_front_leg` / `left_front_leg` ROTATION | Sniffer + Snifflet GPU + parity front-leg pitch (`xRot`) |
| `SnifferAnimation` | `SNIFFER_WALK` `left_mid_leg` ROTATION | Sniffer + Snifflet left mid leg `Er` (walk IR has no `right_mid_leg` rotation) |
| `FrogAnimation` | `FROG_CROAK` `croaking_body` POSITION | Frog GPU + parity `croakInflate` |
| `FrogAnimation` | `FROG_WALK` `left_leg` / `right_leg` ROTATION | `BuildFrog` hind leg pitch |
| `FrogAnimation` | `FROG_WALK` `left_arm` / `right_arm` ROTATION | `BuildFrog` arm `PartPose`-order Euler (`Er`) |
| `FrogAnimation` | `FROG_WALK` `left_arm` / `right_arm` / `left_leg` / `right_leg` POSITION | `BuildFrog` additive pivots on arm/leg roots |
| `ArmadilloAnimation` | `ARMADILLO_WALK` `tail` ROTATION | `BuildArmadillo` `tailWalkPitchRad` (adult); geometry IR catalog via `TryApplyDefinitionAnimationGeometryIrPreviewPass` |
| `BabyArmadilloAnimation` | `ARMADILLO_BABY_WALK` `tail` ROTATION | Same builder when `isBaby`; geometry IR catalog pass (tail part when cuboids present) |
| `BatAnimation` | `BAT_FLYING` `left_wing` / `right_wing` ROTATION | `BuildBat` yaw |
| `CreakingAnimation` | `CREAKING_WALK` `upper_body` ROTATION | Creaking `lean` (Z) |
| `CreakingAnimation` | `CREAKING_ATTACK` `upper_body` ROTATION | Creaking `lean` additive (Y, looped) |
| `RabbitAnimation` | `IDLE_HEAD_TILT` `body` POSITION | Rabbit hop additive (adult) |
| `RabbitAnimation` | `IDLE_HEAD_TILT` `head` POSITION | `BuildRabbit` head pivot; geometry IR catalog |
| `CamelAnimation` | `CAMEL_DASH` `head` ROTATION (CATMULLROM) | `BuildCamel` neck/snout pitch; geometry IR catalog |
| `BabyRabbitAnimation` | `IDLE_HEAD_TILT` `body` POSITION | Rabbit hop additive (baby) |
| `CamelBabyAnimation` | `CAMEL_BABY_WALK` `head` POSITION | `BuildBabyCamel` head root Z |
| `RabbitAnimation` | `HOP` `frontlegs` POSITION | Adult rabbit GPU + parity hop compress additive |
| `BatAnimation` | `BAT_RESTING` `right_wing` / `left_wing` ROTATION | Bat GPU + parity wing yaw blend toward hanging pose |
| `BatAnimation` | `BAT_RESTING` `right_wing` / `left_wing` POSITION | Bat wing pivot **Z** blended (22%) with flying pose |
| `BreezeAnimation` | `SHOOT` `head` ROTATION X | Breeze GPU + parity head stack additive pitch; geometry IR catalog (`breeze` / `breeze_eyes`) |
| `BreezeAnimation` | `SHOOT` `head` POSITION | Breeze GPU + parity additive translation on head/eyes pivots; geometry IR catalog |
| `FoxBabyAnimation` | `FOX_BABY_WALK` `right_hind_leg` ROTATION | Parity `BuildFox` when `isBaby`; geometry IR catalog (`fox_baby` textures) |

Unit checks live in `tests/AutoPBR.Core.Tests/VanillaAnimationIrPreviewSamplerTests.cs` (copies the same JSON set into test output). Geometry IR catalog motion: `GeometryIrDefinitionAnimationPreviewTests.cs`.

## Done (recent)

- **SniffletModel geometry IR** — `snifflet.png` / post–26.1 baby sniffer uses dedicated `BuildSnifflet` from `docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.sniffer.SniffletModel.json` instead of scaled adult `SnifferModel` (`CleanRoomEntityModelRuntime.BuildSniffer`).

## Remaining work (backlog)

Work below still needs **sampler interpolation upgrades** (Catmull-Rom / MIXED), **missing IR channels**, **new preview mesh parts**, or is intentionally deferred.

### Per IR file (16 total)

- **WardenAnimation** — Largely **CATMULLROM** in IR.
- **SnifferAnimation** — Walk **head** rotation uses non-pure-LINEAR keyframe values in IR; **hind** legs; **leg POSITION**; **right_mid_leg** walk rotation absent from shipped `SNIFFER_WALK` IR (only **left_mid_leg** rotation listed); dig clips (`SNIFFER_DIG`); `BABY_TRANSFORM`.
- **NautilusAnimation** — **`SWIMMING` `body` SCALE** wired (26.1.2 IR has LINEAR keyframes); **`inner_mouth` / `lower_mouth` SCALE** not applied (no split jaw mesh in cleanroom builder).
- **FrogAnimation** — Tongue / extra croak channels; `FROG_WALK` **body** if expanded later.
- **FoxBabyAnimation** — `FOX_BABY_WALK` **MIXED** legs.
- **CopperGolemAnimation** — **CATMULLROM**-heavy walk.
- **CamelAnimation** — **`CAMEL_DASH` head** CATMULLROM wired (2026-05-21); SIT/STANDUP and leg CATMULLROM remain.
- **CamelBabyAnimation** — Idle, dash, sit, legs (beyond wired baby walk head POSITION).
- **BabyAxolotlAnimation** — **CATMULLROM** / **MIXED**.
- **ArmadilloAnimation** / **BabyArmadilloAnimation** — Roll / peek / legs beyond walk tail.
- **BatAnimation** — `BAT_RESTING` **head/body** 180° flip and **wing_tip** rotations; resting **wing POSITION** X/Y not blended (only **Z** with same 22% factor as yaw).
- **RabbitAnimation** / **BabyRabbitAnimation** — Hop beyond adult `frontlegs` POSITION; baby hop `frontlegs` not all LINEAR; legs/tail hop. **`IDLE_HEAD_TILT` head POSITION** wired for adult (2026-05-21).
- **BreezeAnimation** — `SHOOT` **wind_*** rod rotations, jump/slide clips, and other defs beyond idle wind + shoot head.

### Cross-cutting

1. **Interpolation** — `VanillaAnimationIrPreviewSampler` now samples **LINEAR** and **CATMULLROM** (and per-segment **MIXED**). Wired for camel adult walk root, warden sniff body, copper golem walk body, fox baby hind leg, sniffer walk head.
2. **IR completeness** — Nautilus body scale (`extractionNotes`); sniffer walk **right_mid_leg** if compiler starts emitting it.
3. **Adult Fox** — No `FoxAnimation.json` in the 16-file set.
4. **Integration tests** — Optional golden / `Build*` smoke coverage.
5. **Sniffer** — Hind legs + leg POSITION lifts; right mid walk rotation when IR includes it.

## Related code

- [`cleanroom-entity-cuboid.md`](cleanroom-entity-cuboid.md) — static mesh `EntityCuboid` layer, planned geometry IR emitter, and codegen (orthogonal to animation IR sampling).
- `CleanRoomEntityModelRuntime.cs` / `Entities/CleanRoomEntityModelRuntime.ParityCatalogDispatch.cs` — GPU stem routes and catalog cases calling samplers.
- `EntityCleanRoomAnimationMap` — version label fallback when `MinecraftNativeProfile` is null or `"root"`.
- `docs/generated/schema/animation-ir.schema.json` — IR shape reference.
