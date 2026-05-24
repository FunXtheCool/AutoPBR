# Phase 1A pilot hierarchy expectations (26.1.2)

Generated for **Agent C** (Phase 1A binding recovery). Classifies all **56** pilots from `geometry-assembly-parity-pilots-26.1.2.txt` using Appendix G (`docs/geometry-lift-assembly-parity-roadmap.md`), `geometry-lift-quality-26.1.2.json`, and `geometry-assembly-pilot-javap-snapshots-26.1.2.csv`, with five JVMs spot-checked against javap snapshots.

## Classification rules

| Class | Meaning |
|--------|---------|
| **nested** | Javap mesh factory binds `right_hind_leg` (and siblings) on **body** `PartDefinition` (`aload` of body slot), not root. |
| **flat** | Legs (and usually body/head) are **root siblings** via `getRoot()` receiver — creeper / vanilla flat quadruped bake. |
| **binding_gap** | Pilot-resolved factory has **0 bind lines** in snapshot CSV, delegates to host mesh, or quality notes `javap on-demand (no bindings)` / `host class`. |

### `suspectedFlatNestedPartCount` should drop?

- **No** — intentional flat quadruped; `referenceHierarchyMatch: true` with `UsesVanillaFlatQuadrupedLegBake` (e.g. Creeper, Cow, Quadruped).
- **Yes** — nested javap but IR still has flat legs at root (`referenceHierarchyMatch: false` today) or binding must be recovered first.
- **After gap** — binding_gap row: drop to 0 only once host-mesh `addOrReplaceChild` edges are lifted.

### T0 canary

CreeperModel (regression), CowModel (offset vs offsetAndRotation), QuadrupedModel (mesh host).

## Spot-check: 5 random JVMs vs snapshots

| JVM | Snapshot | Verdict |
|-----|----------|---------|
| `CreeperModel` | `CreeperModel.createBodyLayer.javap.txt` | **flat** — all parts on root `aload_2`; offset-only poses. |
| `CowModel` | `CowModel.createBodyLayer.javap.txt` | **flat** — legs on root `aload_1`; body uses `offsetAndRotation`. |
| `AbstractEquineModel` | `AbstractEquineModel.createBodyMesh.javap.txt` | **nested** — legs on body `aload_2`. |
| `DonkeyModel` | `DonkeyModel.createBodyLayer.javap.txt` | **binding_gap** — `createBodyLayer` has no mesh binds (chest on baked `body` only). |
| `AdultAxolotlModel` | `AdultAxolotlModel.createBodyLayer.javap.txt` | **nested** — ctor/`createBodyLayer` attach legs under `body`; quality `referenceHierarchyMatch: false`. |

## Full table (56 pilots)

Sources: `geometry-assembly-pilot-javap-snapshots-26.1.2.csv`, `geometry-lift-quality-26.1.2.json` (2026-05-19), Appendix G. **Current** = today's quality JSON; **post-1A depth** = expected after binding recovery / honest hierarchy.

| # | JVM | Class | Javap | post-1A maxTreeDepth | Cur depth | Cur flatCount | refHierarchy | flatCount drop? | T0 canary |
|---|-----|-------|-------|---------------------|-----------|---------------|--------------|-----------------|-----------|
| 1 | `net.minecraft.client.model.QuadrupedModel` | flat | `QuadrupedModel.createBodyMesh` — `createLegs(root)` | 1 | 1 | 4 | true | No | **Yes** |
| 2 | `…armadillo.AdultArmadilloModel` | flat | legs on root `aload_1` | 3 | 3 | 4 | true | No | No |
| 3 | `…armadillo.ArmadilloModel` | binding_gap | shares `AdultArmadilloModel` snapshot; oracle "no bindings" | 3 | 3 | 4 | true | After gap | No |
| 4 | `…armadillo.BabyArmadilloModel` | flat | `BabyArmadilloModel.createBodyLayer` — legs root | 3 | 3 | 4 | true | No | No |
| 5 | `…axolotl.AdultAxolotlModel` | nested | legs under `body` | 2 | 2 | 4 | **false** | **Yes** | No |
| 6 | `…axolotl.BabyAxolotlModel` | nested | `BabyAxolotlModel` — same pattern | 2 | 2 | 4 | **false** | **Yes** | No |
| 7 | `…camel.AdultCamelModel` | flat | legs on root; head/hump on body | 3 | 3 | 4 | true | No | No |
| 8 | `…camel.BabyCamelModel` | flat | legs root | 2 | 2 | 4 | true | No | No |
| 9 | `…camel.CamelModel` | binding_gap | shares `AdultCamelModel`; oracle "no bindings" | 3 | 3 | 4 | true | After gap | No |
| 10 | `…camel.CamelSaddleModel` | binding_gap | `createSaddleLayer` 0 binds in CSV | 3 | 3 | 4 | true | After gap | No |
| 11 | `…cow.BabyCowModel` | flat | `BabyCowModel` / cow base | 1 | 1 | 4 | true | No | No |
| 12 | `…cow.CowModel` | flat | `CowModel` — legs root | 1 | 1 | 4 | true | No | **Yes** |
| 13 | `…equine.AbstractEquineModel` | nested | `AbstractEquineModel.createBodyMesh` — legs on body | 2 | 2 | 4 | true | No | No |
| 14 | `…equine.BabyDonkeyModel` | nested | `BabyDonkeyModel` — legs on `body` | 3 | 3 | 4 | **false** | **Yes** | No |
| 15 | `…equine.BabyHorseModel` | nested | `BabyHorseModel.createBabyMesh` — legs `aload_2` | 2 | 2 | 4 | true | No | No |
| 16 | `…equine.DonkeyModel` | binding_gap | `DonkeyModel.createBodyLayer` 0 binds | 3 | 3 | 4 | true | After gap | No |
| 17 | `…equine.EquineSaddleModel` | binding_gap | `createSaddleLayer` 0 binds | 3 | 3 | 4 | true | After gap | No |
| 18 | `…equine.HorseModel` | binding_gap | shares `BabyHorseModel` mesh; oracle "host class" | 2 | 2 | 4 | true | After gap | No |
| 19 | `…feline.AbstractFelineModel` | nested | `AdultFelineModel.createBodyMesh` host | 2 | 2 | 4 | true | No | No |
| 20 | `…feline.AdultCatModel` | nested | shared feline mesh | 2 | 2 | 4 | true | No | No |
| 21 | `…feline.AdultFelineModel` | nested | legs on body `aload_2` | 2 | 2 | 4 | true | No | No |
| 22 | `…feline.AdultOcelotModel` | nested | shared feline mesh | 2 | 2 | 4 | true | No | No |
| 23 | `…feline.BabyCatModel` | nested | shared feline mesh | 2 | 2 | 4 | true | No | No |
| 24 | `…feline.BabyFelineModel` | flat | `BabyFelineModel.createBabyLayer` — legs root | 2 | 2 | 4 | true | No | No |
| 25 | `…feline.BabyOcelotModel` | nested | shared feline mesh | 2 | 2 | 4 | true | No | No |
| 26 | `…fox.AdultFoxModel` | flat | legs on root `aload_1` | 2 | 2 | 4 | true | No | No |
| 27 | `…fox.BabyFoxModel` | flat | `BabyFoxModel` | 2 | 2 | 4 | true | No | No |
| 28 | `…fox.FoxModel` | flat | shares `AdultFoxModel` | 2 | 2 | 4 | true | No | No |
| 29 | `…goat.BabyGoatModel` | flat | legs root | 2 | 2 | 4 | true | No | No |
| 30 | `…goat.GoatModel` | flat | legs root | 2 | 2 | 4 | true | No | No |
| 31 | `…llama.BabyLlamaModel` | nested | legs on body `aload_2` | 2 | 2 | 4 | true | No | No |
| 32 | `…llama.LlamaModel` | nested | legs on body | 2 | 2 | 4 | true | No | No |
| 33 | `…panda.BabyPandaModel` | flat | quadruped-style root legs | 1 | 1 | 4 | true | No | No |
| 34 | `…panda.PandaModel` | flat | `PandaModel` | 1 | 1 | 4 | true | No | No |
| 35 | `…pig.PigModel` | binding_gap | delegates `QuadrupedModel.createBodyMesh`; 1 bind in slice | 1 | 1 | 4 | true | After gap | No |
| 36 | `…polarbear.BabyPolarBearModel` | flat | root legs | 1 | 1 | 4 | true | No | No |
| 37 | `…polarbear.PolarBearModel` | flat | root legs | 1 | 1 | 4 | true | No | No |
| 38 | `…rabbit.AdultRabbitModel` | nested | legs on body sub-tree `aload_5` | 2–3 | 2 | 4 | **false** | **Yes** | No |
| 39 | `…rabbit.BabyRabbitModel` | nested | shared rabbit mesh | 2 | 2 | 4 | **false** | **Yes** | No |
| 40 | `…rabbit.RabbitModel` | nested | shares `AdultRabbitModel` | 2 | 2 | 4 | **false** | **Yes** | No |
| 41 | `…sheep.BabySheepModel` | flat | `BabySheepModel` / quadruped | 1 | 1 | 4 | true | No | No |
| 42 | `…sheep.SheepFurModel` | flat | `SheepFurModel.createFurLayer` | 1 | 1 | 4 | true | No | No |
| 43 | `…sheep.SheepModel` | binding_gap | thin overlay on quadruped; 1–2 binds | 1 | 1 | 4 | true | After gap | No |
| 44 | `…sniffer.SnifferModel` | nested | legs on body `aload_2` | 2 | 2 | 4 | **false** | **Yes** | No |
| 45 | `…sniffer.SniffletModel` | nested | shared sniffer pattern | 2 | 2 | 4 | **false** | **Yes** | No |
| 46 | `…turtle.AdultTurtleModel` | flat | legs root `aload_1` | 1 | 1 | 4 | true | No | No |
| 47 | `…turtle.BabyTurtleModel` | flat | root legs | 1 | 1 | 4 | true | No | No |
| 48 | `…turtle.TurtleModel` | flat | shares `AdultTurtleModel` | 1 | 1 | 4 | true | No | No |
| 49 | `…wolf.AdultWolfModel` | nested | legs on body `aload_2` | 2 | 2 | 4 | true | No | No |
| 50 | `…wolf.BabyWolfModel` | flat | root legs (CSV flatRootYN=Y) | 2 | 2 | 4 | true | No | No |
| 51 | `…wolf.WolfModel` | nested | shares `AdultWolfModel` | 2 | 2 | 4 | true | No | No |
| 52 | `…creeper.CreeperModel` | flat | all root siblings; offset-only | 1 | 1 | 4 | true | No | **Yes** |
| 53 | `…dragon.EnderDragonModel` | nested | legs under `body` chain | 3–4 | 2 | 4 | **false** | **Yes** | No |
| 54 | `…hoglin.BabyHoglinModel` | flat | legs root | 2 | 2 | 4 | true | No | No |
| 55 | `…hoglin.HoglinModel` | flat | legs root | 2 | 2 | 4 | true | No | No |
| 56 | `…ravager.RavagerModel` | flat | legs root; neck→head→mouth nested | 3 | 2 | 4 | true | No* | No |

\*Ravager: `referenceHierarchyMatch: true` today (flat legs match reference); flatCount may stay 4 until Phase 2B policy or leg semantics documented — not a nested-leg recovery target.

## Summary counts

| Class | Count |
|--------|------:|
| flat | 32 |
| nested | 17 |
| binding_gap | 7 |

**`suspectedFlatNestedPartCount` → Yes:** 11 (axolotl×2, baby donkey, rabbit×3, sniffer×2, ender dragon)

**T0 canaries:** 3 (Creeper, Cow, Quadruped)

## Appendix: snapshot index (44 files)

Under `tools/minecraft-parity/26.1.2/javap-snapshots/` — see `geometry-assembly-pilot-javap-snapshots-26.1.2.csv` for pilot→file mapping (shared hosts for feline, camel, fox, wolf, rabbit, etc.).

## Note on Appendix G vs CSV

Appendix G marks all pilots **flat root Y** using `suspectedFlatNestedPartCount >= 4` from an older quality run. The **CSV** (`flatRootYN`) uses javap proximity heuristics and disagrees for many rows (e.g. armadillo, camel, feline). This table uses **bytecode receiver** (root vs body for legs) as authoritative for nested vs flat.
