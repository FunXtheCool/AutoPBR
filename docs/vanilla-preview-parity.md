# Vanilla preview mesh parity (Java Edition)

This document is the **long-term reference** for aligning AutoPBR 3D block/entity preview with **Minecraft: Java Edition**. The Cursor plan *Vanilla model parity rollout* is the working checklist; this file is the **git-tracked** copy for reviewers and future sessions.

## Pinned versions (policy)

Parity checks and `javap` extractions should be pinned to the same versions the app and tests already assume:

| Role | Version | Notes |
|------|---------|--------|
| Pre–26.1 baby / model baseline | **1.21.11** (`MinecraftNativeProfile` name + `Version(1,21,11)`) | Used widely in [`MinecraftJavaModelPreviewTests`](../tests/AutoPBR.Core.Tests/MinecraftJavaModelPreviewTests.cs). |
| Post–26.1 baby split | **26.1.2** (`Version(26,1,2)`) | [`UsesPostBabyModelUpdate`](../src/AutoPBR.Core/Preview/EntityModelRuntime.cs) and baby dimension tests. |
| Bytecode / mapping spot-checks | **1.21.4** client JAR + `client.txt` | Stable LTS-ish reference for `javap -c` when a class matches 1.21.x layout; re-verify if Mojang refactors `*Model` between minors. |

**Artifacts (per pinned version)**

1. `version_manifest_v2.json` → resolve `id` → `version.json` URL.  
2. From `version.json` `downloads`: **`client.jar`**, **`client.txt`** (ProGuard mappings).  
3. Map named class → obfuscated name in `client.txt` (e.g. `net.minecraft.client.model.CowModel -> gbs`).  
4. Extract **only** the needed `.class` entry from `client.jar` with `ZipFile` (do **not** full-unpack the JAR on Windows).  
5. `javap -c -constants <Class>.class` → cuboid bounds, `texOffs`, `PartPose` / `offsetAndRotation` (Euler floats like `1.5707964f`).

## Full client model + animation class catalog (generated)

For an **exhaustive** inventory of classes under `net.minecraft.client.model.**` and `net.minecraft.client.animation.**` (JAR entry, JVM names, optional entity-manifest `path_prefix` hits, and `javap -public` text in JSON), regenerate **one pair of files per game version** — details are **not interchangeable** between versions (obfuscation vs named bytecode, class set, signatures):

| Version | Artifacts | Notes |
|---------|-------------|--------|
| **1.21.11** | `minecraft-client-model-index-1.21.11.md` / `.json` | ProGuard `client_mappings.txt`; `mappingKind` = `proguard` in JSON. |
| **26.1.2** | `minecraft-client-model-index-26.1.2.md` / `.json` | Mojang omits `downloads.client_mappings` for this release; JAR ships **named** `net/minecraft/...` classes. JSON `mappingKind` = `named_jar`; official and obfuscated name columns match. |

- [`docs/generated/README.md`](../docs/generated/README.md) — commands and per-version policy.  
- Script: [`tools/Generate-MinecraftClientModelIndex.ps1`](../tools/Generate-MinecraftClientModelIndex.ps1).  

Track A above remains the **hand-maintained parity** checklist; the generated index is the **complete class tree** reference for that pinned jar.

## Reference Java bake — composed `worldPose` and JVM render affines (26.1.2)

`tools/MinecraftGeometryReference` walks baked `ModelPart` trees and writes:

| Field | Semantics | Use in C# |
|-------|-----------|-----------|
| **`worldPose.translation`** | Parent chain × `getInitialPose()` via **`PartWorldPoseMath`** (Er×T row convention) | Lift quality `referenceWorldPoseMatch`, hierarchy drift probes |
| **`renderPartAffines`** / **`renderCuboidCenters`** | Bind PoseStack **`translateAndRotate`** walk (`ModelPartRenderPoseMath.java`) | **Cuboid render placement** — horns, ears, rotated attachments |

C# emit (`TryComposePartPose` / `GeometryIrMeshWalk`) follows the **render** path (block stack above), not texel-scale `Mul(Er, T)` alone. **`worldPose` can match while Explore cuboids are wrong** on attached rotated parts — see [runtime-ir-preview-plan.md](runtime-ir-preview-plan.md) § *PartPose vs ModelPart render*.

Rebake reference after compose changes: `pwsh -File tools/Export-GeometryReference.ps1 -AllExistingOutput -Parallel`.

## Parity-catalog geometry IR (26.1.2)

All **761** rows in `minecraft_26.1.2_entity_texture_model_manifest.json` resolve to **`RuntimeGeometryIrJson`** in preview (`ParityCatalogMeshDriverKindSurveyTests`). Resolution order: bytecode-lifted shards under `Data/minecraft-native/geometry/26.1.2/`, equipment JVM overrides (`GeometryIrParityEquipmentJvmMap`), and hand-lift catalogs for renderer-only hosts. See [`entity-cuboid-layer.md`](entity-cuboid-layer.md) §3.

## Pipeline overview

| Track | Vanilla source | Core code |
|-------|------------------|-----------|
| **Blocks / items** | `assets/<ns>/models/**.json` — elements `from` / `to` / `faces`, optional **`rotation`** object (`origin`, `axis`, `angle`, optional `rescale`) | [`MinecraftModelMerger`](../src/AutoPBR.Core/Preview/MinecraftModelMerger.cs) → `ModelElement.LocalToParent` |
| **Entities** | `net.minecraft.client.model.*Model` (`createBodyLayer`, cuboids + poses) | [`EntityModelRuntime`](../src/AutoPBR.Core/Preview/EntityModelRuntime.cs) → geometry IR emit + `RigBuilder` (error placeholder only for IR miss) |
| **Bake** | — | [`MinecraftModelBaker`](../src/AutoPBR.Core/Preview/MinecraftModelBaker.cs): apply `LocalToParent` in **model texel space**, then `W()` preview scaling; normals/tangents transformed with the same matrix. |

## Track A — Entity checklist (rolling)

Use one row per model class; mark **done** when `javap` notes exist, geometry IR shard is `ok`, and tests/visual pass.

**Catalog policy (2026-07):** all 761 parity-catalog entity diffuse paths emit `RuntimeGeometryIrJson` only. Hand `Build*` templates are removed; uncatalogued textures show `ErrorPlaceholder`.

### Structured geometry / preview deltas (pilot)

Machine-readable parity lives under [`docs/generated/`](../docs/generated/README.md) (schemas, `geometry/**`, `preview-deltas/**`, `geometry-index-*.json`). **Pilot rows** (GEO = geometry IR shard, DELTA = preview overlay):

| Model | Geometry IR | Preview delta |
|-------|----------------|---------------|
| CowModel | [`geometry/26.1.2/net.minecraft.client.model.animal.cow.CowModel.json`](../docs/generated/geometry/26.1.2/net.minecraft.client.model.animal.cow.CowModel.json) | [`preview-deltas/26.1.2/net.minecraft.client.model.animal.cow.CowModel.json`](../docs/generated/preview-deltas/26.1.2/net.minecraft.client.model.animal.cow.CowModel.json) |
| ChickenModel | [`geometry/26.1.2/net.minecraft.client.model.ChickenModel.json`](../docs/generated/geometry/26.1.2/net.minecraft.client.model.ChickenModel.json) (partial placeholder) | [`preview-deltas/26.1.2/net.minecraft.client.model.ChickenModel.json`](../docs/generated/preview-deltas/26.1.2/net.minecraft.client.model.ChickenModel.json) |
| BlazeModel | [`geometry/26.1.2/net.minecraft.client.model.BlazeModel.json`](../docs/generated/geometry/26.1.2/net.minecraft.client.model.BlazeModel.json) (partial placeholder) | [`preview-deltas/26.1.2/net.minecraft.client.model.BlazeModel.json`](../docs/generated/preview-deltas/26.1.2/net.minecraft.client.model.BlazeModel.json) |

**1.21.11 ProGuard cow path:** `net.minecraft.client.model.animal.cow.CowModel` → [`geometry/1.21.11/net.minecraft.client.model.animal.cow.CowModel.json`](../docs/generated/geometry/1.21.11/net.minecraft.client.model.animal.cow.CowModel.json) (merge bytecode fields with [`tools/Generate-GeometryIndex.ps1`](../tools/Generate-GeometryIndex.ps1) + `client_mappings.txt`).

**Animation tables:** mob `*Animation` `<clinit>` remains in `minecraft-client-model-index-*-animation-init/*.javapc.txt`; use `AutoPBR.Tools.GeometryCompiler --print-animation-summary <path>` for a short stdout probe (see [`docs/generated/README.md`](../docs/generated/README.md)).

**Batching:** run [`tools/entity-parity-manifest-by-folder.ps1`](../tools/entity-parity-manifest-by-folder.ps1) to list 26.1.2 manifest `path_prefix` rules by first folder under `textures/entity/` (optional `-Json`).

| Model (vanilla class) | Versions checked | Status | Notes |
|------------------------|------------------|--------|--------|
| CowModel | 1.21.4 | done | Body `offsetAndRotation(0,5,2, π/2,0,0)`; head offset `(0,4,-8)`; horns `(22,0)`; udder `(52,0)`; legs `(0,16)` + leg pivots. |
| SheepModel | 1.21.4 | done | Wool body same body pose as cow; `texOffs(28,8)`, box `(-4,-10,-7)`–`(4,6,-1)`; head group `(0,6,-8)`; head cube `(-3,-4,-6)`–`(3,2,2)`; legs `4×6×4`, UV `(0,16)`, stance 12. |
| SpiderModel | 1.21.x | done | Cephalothorax + abdomen unchanged UV; eight legs `16×2×2` at `(18,0)`, **zRot** at side hinges — not `offsetX` as leg fan. |
| AbstractEquineModel / HorseModel; DonkeyModel (ears + chests) | 1.21.4 (`gaq`/`gcn`/`gbw`) | done | Body `(0,32)` `10×10×22` @ `T(0,11,5)`; `head_parts` `T(0,4,-12)·Rx(π/6+neck)`; legs `(48,21)` @ root offsets; tail + saddle on body; tack + ears; donkey replaces ears + adds chests. |
| IllagerModel (pillager / illusioner routing) | 1.21.4 (`gcq`) + 26.1.2 manifest | done | `head` `8×10×8`, hat shell `8×12×8`, nose `2×4×2` with head-child offset, body `8×12×6` + robe `8×20×6`, folded-arms stack + arm/leg `4×12×4` volumes. |
| BeeModel | 1.21.4 (`gbf`) | done | Bone root at Y=19; body `7×7×10`; stinger `0×1×2`; antennae `1×2×3`; wings `9×0×6`; three leg strips `7×2×0` mapped as thin preview sheets. |
| BlazeModel | 1.21.11 (`BlazeModel.createBodyLayer`) | done | Head `8³` `texOffs(0,0)` `PartPose.ZERO`; twelve rods `texOffs(0,16)` `2×8×2` @ `PartPose.offset(cos(-π/4+i·π/6)·5.1, 11, sin·5.1)`; preview root `T(8,14,8)` matches legacy head anchor; `setupAnim`-style rod `xRot` sway from idle channel. |
| AllayModel | 1.21.4 (`gat`) | done | Root Y=23.5 with head `5×5×5`; body `3×4×2` + outer `3×5×2`; arms `1×4×2`; wings `0×5×8` using thin-sheet proxy and mirrored wing pose. |
| VexModel | 1.21.4 (`gem`) | done | Root Y=-2.5; head `5×5×5`; body `3×4×2` + outer `3×5×2`; arms `2×4×2`; wings `0×5×8` with mirrored flap; supports charging-state arm pose channels. |
| WitchModel | 1.21.4 (`ger`) | done | Villager-derived body/arms/legs with witch hat hierarchy: brim `10×2×10`, cone tiers `7×4×7` and `4×4×4`, tip `1×2×1` (+inflate), head `8×10×8`, nose+mole child boxes. |
| ParrotModel | 1.21.4 (`gda`) | done | Body `3×6×3` at perched tilt; head `2×3×2` with layered beak/head feathers; wings `1×5×3`; tail `3×4×1`; legs `1×2×1`. |
| PhantomModel | 1.21.4 (`gdb`) | done | Body `5×3×9`; head `7×3×5`; wing base/tip `6×2×9` + `13×1×9`; articulated tail `3×2×6` + `1×1×6` with per-segment pitch swing. |
| BatModel | 1.21.4 (`gbe`) | done | Body `3×5×2`; head `4×3×2`; ears `3×5×0`; wings `2×7×0` with `6×8×0` tips; feet `3×2×0` (zero-thickness parts baked as thin sheets). |
| GhastModel | 1.21.4 (`gch`) | done | Body `16×16×16` @ Y `17.6`; 9 tentacles on 3×3 grid with deterministic lengths from `Random(1660)` (`8..13`) and pitch-only idle sway. |
| EndermiteModel | 1.21.4 (`gcb`) | done | Segment array sizes `{4×3×2, 6×4×5, 3×3×1, 1×2×1}` with staged Z offsets and sinusoidal yaw/side wobble by segment index. |
| WolfModel | 1.21.4 (`get`) | done | Canonical split torso (`6×9×6` + `8×6×7`), head `6×6×4` with ear/nose children, legs and tail `2×8×2`; baby transform remains via model transformer. |
| RabbitModel | 1.21.4 (`gdn`) | done | Body `6×5×10`; head `5×4×5`; haunches `2×4×5` with child feet `2×1×7`; front legs `2×7×2`; ears `2×5×1`; tail `3×3×2`. |
| PigModel / ColdPigModel | 1.21.11 (`hch`/`hcg`) | done | Quadruped mesh from `QuadrupedModel` body `10×16×8` at `T(0,11,2)·Rx(π/2)`; head/snout `8×8×8` + `4×3×1`; legs `4×6×4`; cold variant adds inflated body overlay `texOffs(28,32)` with `CubeDeformation(+0.5)`; baby uses model transformer root split (`head c(0,4,4)`, body/legs `c(0,24,0).b(0.5)`). |
| FoxModel | 1.21.4 (`gcf`) | done | Head `8×6×6` + nose `4×2×3`; body `6×11×6` with rotated torso pose and child tail `2×6×2`; legs `2×6×2` with sleep/crouch pose offsets. |
| GoatModel | 1.21.4 (`gcj`) | done | Head root `T(1,14,0)` with ears `3×2×1`, nose `5×7×10` at `offsetAndRotation(0,-8,-8, 0.9599,0,0)`, horns `2×7×2`, body `9×11×16` + shell `11×14×11`, mixed front/hind leg heights (`10`/`6`). |
| LlamaModel | 1.21.4 (`gcu`) | done | Head stack at `T(0,7,-6)` with neck `8×18×6`, head `4×4×9`, ears `3×3×2`; body `12×18×10` at `Rx(π/2)`; chest slabs `8×8×3` with `Ry(π/2)`; legs `4×14×4`. |
| CamelModel | 1.21.4 (`gbn`) | done | Body root `15×12×27` at `T(0,4,9.5)`; hump and tail children; head/neck stack (`7×14×7`, `7×8×19`, `5×5×6`) at `T(0,-3,-19.5)` with ears; legs `4×17×4`; saddle/bridle/reins toggles mapped to visibility channels. |
| PandaModel | 1.21.4 (`gcz`) | done | Head at `T(0,11.5,-17)` (`13×10×9`) with nose `7×5×2` and ears `5×4×1`; body `19×26×13` at `T(0,10,0)·Rx(π/2)`; legs `6×9×6` on panda pivots. |
| PolarBearModel | 1.21.4 (`gdi`) | done | Head root `T(0,10,-16)` with head cube `7×7×7`, mouth `5×3×3`, ears `2×2×1`; body stack `14×14×11` + upper `12×12×10` at `T(-2,9,12)·Rx(π/2)`; legs split as hind `4×10×8` and front `4×10×6`. |
| MinecartModel | 1.21.11 / 26.1.2 (`hco`) | done | Floor `20×16×2` (`texOffs(0,10)`) rotated `Rx(π/2)` at root `T(0,4,0)`; four wall panels `16×8×2` (`texOffs(0,0)`) at offsets `(-9,4,0)`, `(9,4,0)`, `(0,4,-7)`, `(0,4,7)` with yaw `{3π/2, π/2, π, 0}`. |
| RavagerModel | 1.21.4 (`gdp`) | done | Neck `10×10×18` at `T(0,-7,5.5)`; head `16×20×16` with inner nose `4×8×4`, articulated mouth `16×3×16`, horns `2×14×4` via `Rx(1.0996)` pivots; body stack `14×16×20` + `12×13×18` at `T(0,1,2)·Rx(π/2)`; legs `8×37×8`. |
| ArmadilloModel | 1.21.4 (`gav`) | done | Body `8×8×12` plus inflated shell (`+0.3` deformation), head chain at `T(0,-2,-11)` with pitched `3×5×2` cube and asymmetric ear poses; tail `1×6×1` at `T(0,-3,1)` with `Rx(0.5061)`; legs `2×3×2`; roll-up cube `10×10×10` toggled by state. |
| BreezeModel | 1.21.4 (`gbm` javap) | done | `body` empty pivot; `rods` @ `T(0,8,0)` + three `2×8×2` @ full `offsetAndRotation` (Euler via `Er`); `head` @ `T(0,4,0)` fringe `10×3×4` @ `(4,24)` + `8³` @ `(0,0)`; `eyes` child duplicates both for emissive sheet; main preview wires `#eyes` → sibling `breeze_eyes`; `breeze_eyes.png` path builds eyes-only pair. Wind shells unchanged. |
| HoglinModel / ZoglinModel | 1.21.11 (`hen`) | done | Root body `16×14×26`; mane strip local-Z differs (`-7` adult / `-3` baby) before transformer; head `14×6×19` at `Rx(50°)` with mirrored ear pivots `Rz(±40°)` and horn pair `2×11×2`; route integrity checks cover both hoglin and zoglin paths. |
| SlimeModel | 1.21.4 (`gea`) | done | Outer cube `8×8×8` at Y=16 for base slime body; inner variant uses `6×6×6` core plus eye quads (`2×2×2`) and mouth voxel (`1×1×1`) at canonical offsets. |
| LavaSlimeModel (MagmaCube) | 1.21.4 (`gcs`) | done | Eight stacked `8×1×8` layers (`segment0..7`) with UV split (`u=0` middle bands, `u=32` caps) and animated per-layer Y squash; inner cube `4×4×4` at Y=18. |
| SilverfishModel | 1.21.4 (`gdw`) | done | Seven body segments with canonical size arrays `{3×2×2,4×3×2,6×4×3,3×3×3,2×2×3,2×1×2,1×1×2}` and staged Z offsets; three fin plates `10×8×3`, `6×4×3`, `6×5×2` follow segment yaw/sway channels. |
| WitherBossModel | 1.21.4 (`ges`) | done | Shoulder bar `20×3×3`; ribcage core `3×10×3` plus stacked `11×2×2` bars at `Rx(0.2042)` and tail `3×6×3` at `Rx(0.8325)`; center head `8×8×8` with side heads `6×6×6` at `(-8,4,0)` / `(10,4,0)`. |
| WardenModel | 1.21.11 (`hfr`) | done | `hfr.a`: `bone` `T(0,24,0)`; body `18×21×11` `texOffs(0,0)`; ribcages `9×21×0` (preview `dz=1` UV `9×21×1`); head `16×16×10`; jaw `12×4×16`; tendrils `16×16×0`→sheet `dz=1`; arms `8×28×8`; legs `6×13×6` on bone (not body). |
| EnderDragonModel | 1.21.11 (`hec`) | done | `hec.a`: head group `T(0,20,-62)` (upperlip `12×5×16`, upperhead `16³`, nostrils, jaw child); neck×5 / tail×12 shared `10³` @ `192,104`; body `T(0,3,8)` + hull `24×24×64` + three `2×6×12` spikes `220,53`; wings `56×8×8` + tips `56×4×4` with preview yaw on wing roots; front leg `8×24×8`→`6×24×6`→foot `8×4×16`; hind `16×32×16`→`12×32×12`→`18×6×24`. |
| ShulkerModel | 1.21.4 (`gdv`) | done | Base `16×8×16` and lid `16×12×16` both rooted at Y=24 with lid vertical sinusoid + peek roll; optional head `6×6×6` at Y=12 for dyed shulkers. |
| ShulkerBulletModel | 1.21.11 (`hhf`) | done | Three orthogonal shells `8×8×2`, `2×8×8`, `8×2×8` on `main`; `64×32` atlas; yaw/pitch on part. |
| SnowGolemModel | 1.21.11 (`hbs`) | done | All cubes `CubeDeformation(-0.5)`: head/upper/lower `7³` / `9³` / `11³` geometry with UV still `8/10/12`; arms `11×1×1` from `12×2×2` @ `(32,0)`. |
| IronGolemModel | 1.21.11 (`hbr`) | done | Head `8×10×8` + nose `2×4×2`; torso `18×12×11`; lower torso `9×5×6` with **`+0.5` inflate** → `10×6×7` mesh + UV `9×5×6`; arms `4×30×6`; legs `6×16×5`. |
| EndCrystalModel | 1.21.11 (`hgx`) | done | Base `12×4×12` @ `(0,16)`; nested glass `8³` / inner `×0.875` / core `(32,0)` `×0.765625` under outer @ `T(0,24,0)` + spin. |
| EvokerFangsModel | 1.21.11 (`hcy`) | done | Root `y=24-20·shrink`, uniform `shrink=(1-bite)/0.1` when `bite>0.9`; base `10×12×10` @ `(-5,y,-5)` with `y=24-(bite+sin(bite·2.7))·7.2`; jaws `4×14×8` @ `(40,0)` with static **yRot** `2.042035` / `4.2411504` + bite **zRot** `π∓open·0.35·π`. |
| LlamaSpitModel | 1.21.11 (`hbv`) | done | Seven `2×2×2` cubes on `main` in axis star (offsets `±4` / `2` / origin); `64×32` atlas. |
| GuardianModel | 1.21.11 (`hek`) | done | Head stack `12×12×16` + sides `2×12×12` + slabs `12×2×12`; twelve spikes `2×9×2` @ `(0,0)` with static Euler arrays + animated xyz `(f,g,h)·(1+0.01cos(L·1.5+i)−spineRetract)`; eye `2×2×1` @ `(8,0)` child `T(0,0,-8.25)`; tails `4×4×8` / `3×3×7` / `2×2×6`+`9×9×9` with swing **yRot** `sin·π·{0.05,0.1,0.15}`. Elder = same mesh + `scaling(2.35)`. |
| SquidModel | 1.21.11 (`hcs`) | done | `createBodyLayer`: body `texOffs(0,0)` `addBox(-6,-8,-6,12,16,12)` + `CubeDeformation(0.02)` under `PartPose.offset(0,8,0)`; eight tentacles `texOffs(48,0)` `2×18×2` @ `offset(5·cos(i·2π/8),15,5·sin(…))` + `yRot = π/2 − i·2π/8`. **GlowSquidRenderer** uses the same `SquidModel` twice (layers). Root **`ModelTransforms.scaling(0.5)`** is renderer-only (not folded into preview mesh, like ghast `×4`). |
| ChickenModel | 1.21.11 (`hag`) / 26.1.2 `ChickenModel` | done | Geometry unchanged; **`setupAnim`**: wings **`zRot`** `±(Mth.sin(flap)+1)·flapSpeed` (not yRot — corrected preview); legs same leg swing as `ComputeQuadrupedLegPitchRad`. Layer from `hag.e` / `createBaseChickenModel`: head `4×6×3` @ `T(0,15,-4)` + beak + `red_thing`; body `6×8×6` `T(0,16,0)·Rx(π/2)`; legs/wings offsets as before. |
| DolphinModel | 1.21.11 (`han`) | done | `han.a`: body `T(0,22,-5)` + hull `8×7×13` `(22,0)`; `back_fin` `1×4×5` @ `Rx(π/3)`; pectorals `1×4×7` @ `(48,20)` + `offsetAndRotation(±2,-2,4, π/3,0,±2π/3)` (left builder `mirror()`); tail `4×5×11` @ `(0,19)` + `T(0,-2.5,11)·Rx(−0.1047)` + child `tail_fin` `10×1×6` `T(0,0,9)`; head `8×7×6` @ `T(0,-4,-3)` + nose `2×2×4` `(0,13)`; preview tail pitch adds `swimSway`. |
| ArrowModel | 1.21.11 (`hhe`) | done | `hhe.a`: `back` `5×5×0` @ `(0,0)` + `offsetAndRotation(-11,0,0, π/4,0,0)` + uniform scale `0.8` (preview `dz=1` + `uvSize 5×5×1`); shared cross slab `16×4×0` with `CubeDeformation` path → preview `16×4×1`; `cross_1`/`cross_2` rotation-only `Rx(±π/4)` on root; `setupAnim` wobble on `back` part. |
| CreeperModel | 1.21.11 (`hcn`) | done | `hcn.a`: `head` `6×6×6` @ `(0,0)` + inflate `0.6` (preview skips inflate; `uvSize 6³`); `T(0,6,-8)`; `body` `8×16×6` @ `(28,8)` + inflate `1.75` + `T(0,5,2)·Rx(π/2)`; shared leg `4×6×4` @ `(0,16)` + inflate `0.5`; roots `T(∓3,12,±5/7)`; `setupAnim` head fuse channel on `head` part. |
| TadpoleModel | 1.21.11 (`hbj`) | done | `hbj.a`: `body` `3×2×3` @ `(0,0)` `T(0,22,-3)`; `tail` `0×2×7` @ `(0,0)` `T(0,22,0)` → preview `1×2×7` + `uvSize 1×2×7`; tail `yRot` from `setupAnim`. |
| PufferfishSmallModel | 1.21.11 (`hbb`) | done | `hbb.a`: `body` `3×2×3` @ `(0,27)` `T(0,23,0)`; `right_eye`/`left_eye` `1³` @ `(24,6)`/`(28,6)` `T(0,20,0)`; `back_fin` `3×0×3` @ `texOffs(-3,0)` (preview `u=29` wrap + `dz` proxy); side fins `1×0×2` @ `(25,0)` `T(∓1.5,22,-1.5)` → `2×2×4` proxies + left `mirror()`; catalog puff channel still scales body slightly in dispatch. |
| LeashKnotModel | 1.21.11 (`hhc`) | done | `hhc.a`: part `knot` `6×8×6` @ `texOffs(0,0)` origin `(−3,−8,−3)`; atlas `32×32`. |
| TridentModel | 1.21.11 (`hhg`) | done | `hhg.a`: `pole` `1×25×1` @ `(0,6)`; `base` `3×2×1` @ `(4,0)`; `left_spike`/`middle_spike`/`right_spike` each `1×4×1` @ `(4,3)` / `(0,0)` / mirrored `(4,3)`; atlas `32×32`. |
| SkullModel | 1.21.11 (`hhl`) | done | `hhl.a`: `head` `8³` @ `(0,0)`; `hhl.e()` adds child `hat` same logical `8³` @ `(32,0)` with `CubeDeformation(0.25)` → preview mesh `8.5³` + `uvSize 8³`; block preview basis `T(0,8,0)·Rx(pitch)`. |
| ExperienceOrb (renderer) | 1.21.11 (`hwu`) | done | No `ModelPart`; `16×16` tile UV + `1×1` XY quad, `scale(0.3³)` + billboard. Preview: **one thin north/south slab** sized `≈4.8×4.8×0.08` in entity texel space, same tile UV; `spritePick01` maps pseudo `value` `0..10`. |
| DragonFireball (renderer) | 1.21.11 (`hwh`) | done | No `ModelPart`; `1×1` quad × `scale(2,2,2)` + billboard. Preview: **one thin north/south slab** with full `64×32` UV, extents `≈32×32×0.08` in entity texel space (not a solid cube). |
| MooshroomModel | — | inherits cow | Same rig as cow texture family. |
| **Family / proxy rigs** | | | *(Removed 2026-07: catalogued paths use manifest geometry IR only; uncatalogued paths emit error placeholder.)* |
| HumanoidModel proxy (`BuildHumanoid`, `HumanoidGeneric` dispatch, equipment `humanoid` / `humanoid_leggings`) | 1.21.4 | done | Wide sheet: body `texOffs(16,16)` `8×12×4`, head `(0,0)` `8×8×8`, arms `(40,16)` `4×12×4`, legs `(0,16)` `4×12×4` — aligned with player outer-layer topology for preview. |
| `PlayerSlim` / `BuildPlayerSlim` | 1.21.4 | done | Alex arms `(32,48)` / `(40,16)` `3×12×4`; legs `(16,48)` left / `(0,16)` right. |
| Quadruped fallback (`BuildQuadruped`) | 1.21.4 (`CowModel` subset) | done | Body `(18,4)` + pose `T(0,5,2)·Rx(π/2)`, head `(0,0)`, legs `(0,16)` — **no** horns/udder vs full cow. |
| Flying fallback (`BuildFlying`) | 1.21.4 / 26.1.2 Phantom (`het`) | removed | Hand-built family fallbacks removed 2026-07; catalog paths use `PhantomModel` geometry IR. |
| Aquatic fallback (`BuildAquatic`) | 1.21.4 (`CodModel`) | done | Delegates to `BuildCod` — real fish UV/layout (`32×32`). |
| Equipment overlay fallback (`BuildEquipmentBodyOverlay`) | 1.21.4 (`AbstractEquineModel` body) | done | `texOffs(0,32)` equine torso shell for unmatched equipment diffuse paths. |
| TurtleModel | 1.21.11 (`hcu`) | done | `hcu.a`: head `6×5×6` @ `(3,0)` `T(0,19,-10)`; shell `19×20×6` + belly `11×18×3` + egg_belly `9×18×1` under shared `T(0,11,-10)·Rx(π/2)`; hind flippers `4×1×10` @ `(±3.5,22,11)`; front `13×1×5` @ `(±5,21,-4)`. |
| WindChargeModel | 1.21.11 (`hhh`) | done | `hhh.a`: `wind` child `Ry(−π/4)` + spin; slabs `8×2×8` @ `(15,20)` and `6×4×6` @ `(0,9)`; core `4³` on `wind_charge` with opposite yaw sign in `setupAnim`. |
| *(add rows as you port)* | | | |

## Track B — Block JSON

### Track B1 — Pack model JSON (existing)

- **Implemented:** [`MinecraftModelMerger`](src/AutoPBR.Core/Preview/MinecraftModelMerger.cs) maps element `rotation` → `ModelElement.LocalToParent` = `T(origin) * R(axis, angle°) * T(-origin)` (default origin `[8,8,8]`).  
- **Optional / not modeled:** `rescale: true` (vanilla UV stretch) — rotation still applies; rescale stretch is not replicated.

### Track B2 — Block texture parity catalog (synthetic cube faces)

When a resource pack contains block diffuse PNGs but no resolvable `models/block/*.json`, preview uses the **block texture parity catalog** instead of repeating one texture on all six faces of the unit-cube fallback.

| Artifact | Role |
|----------|------|
| [`minecraft_26.1.2_block_textures.json`](../src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_block_textures.json) | Inventory of vanilla `assets/minecraft/textures/block/*.png` (26.1.2) |
| [`minecraft_26.1.2_block_texture_model_manifest.json`](../src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_block_texture_model_manifest.json) | One rule per texture: `preview_shape` + `texture_slots` |
| [`BlockTextureParityCatalog`](../src/AutoPBR.Core/Preview/BlockTextureParityCatalog.cs) | Loads inventory + manifest |
| [`VanillaBlockPreviewRuntime`](../src/AutoPBR.Core/Preview/VanillaBlockPreviewRuntime.cs) | Builds synthetic `MergedJavaBlockModel` → existing `MinecraftModelBaker` path |

**Preview resolution order (blocks):**

1. Pack model JSON (`PreviewMeshDriverKind.PackModelJson`) — pack zip, then optional Minecraft install assets, then bundled native catalogs
2. Partial material recovery (keep JSON geometry; materialize missing sibling textures)
3. Vanilla block parity synthetic mesh (`PreviewMeshDriverKind.VanillaBlockParity`) — cubes plus complex shapes below
4. Entity runtime (only for `textures/entity/`)
5. Single-texture unit cube fallback (`PreviewSceneKind.BlockCube`)

**Catalog shapes:**

| Shape | Example families |
|-------|------------------|
| `UniformCube` | `stone`, `sand`, single-file blocks |
| `CubeDirectional` | `grass_block` (top + side + dirt bottom), `sandstone` variants |
| `CubeColumnY` | `oak_log` + `oak_log_top` (end grain on up/down) |
| `ThinPlate` | trapdoors (16×16×3 closed plate) |
| `DoorHalf` | door bottom/top textures (paired upper + lower panels in one preview) |
| `CakeWedge` | cake bites=0 inset slab (14×8×14) |
| `CactusCross` | cactus inset side panels (not a full cube) |
| `FencePost` | fence/gate center post (simplified) |
| `FenceWithLink` | bamboo fence (post + north link bar) |
| `RailTrack` | flat rail segment |
| `CrossSprite` | short grass, flowers (two crossed quads) |
| `StairWedge` | (reserved; no inventory stems yet) |
| `PackModelJsonOnly` | redstone overlays, activator rail ON state — defer to pack JSON when present |

**Optional Minecraft install:** Settings → Engine → **Minecraft assets** (browse or auto-detected from `.minecraft/versions/` on first launch). Point at a version folder or extracted `assets/` root so texture-only packs can resolve vanilla `models/block/*.json` at preview time.

**Regenerate catalog:** `pwsh -File tools/generate-block-parity-manifest.ps1`  
**Rollout report:** `pwsh -File tools/block-parity-manifest-by-folder.ps1`

**Tests:** `BlockTextureParityJsonCatalogTests`, `BlockTextureParityMeshTests`, `BlockTextureParitySurveyTests`, `BlockModelMergerTests`, `BlockPreviewCoverageSurveyTests`, `BlockDoorPreviewPairingTests`, `BlockModelUvGoldenTests`, `BlockModelRescaleRotationTests`.

**Phase 2C (deferred):** full multipart `when` evaluation, stair wedge synthesis from plank textures.

**Door preview:** pack JSON from blockstate (`door_*_left` halves) is rebaked into stacked panels with vanilla-accurate thin-door UVs. Pinned goldens: `tests/AutoPBR.Core.Tests/Data/vanilla-26.1.2/` (`BlockVanillaJsonUvGoldenTests`, `BlockDoorPreviewPairingTests`).

## Shared invariants

- Entity **`RigBuilder`**: UV atlas footprint uses **integer unscaled** cuboid extents; vertex half-extents use baby scale — do not mix.  
- **Emulated entity** GL path may disable normal/parallax when tangents do not match entity UV layout ([`OpenGlPreviewBackend`](../src/AutoPBR.App/Rendering/OpenGL/OpenGlPreviewBackend.cs)).

## Reusable entity runtime parity playbook (locked)

This section is **locked process policy** for every entity parity rollout. The horse workflow established this bytecode-first/reference-first sequence; do not skip or reorder gates.

1. **Lock class source of truth first (no heuristics)**
   - Extract `createBodyLayer` / `createBodyMesh` and `setupAnim` from pinned `client.jar` with `javap -c -constants`.
   - Port **all** cuboids and `PartPose` values before making visual tweaks.
2. **Treat baby models as separate classes**
   - Do not scale adult rigs to approximate babies.
   - Port `Baby*Model.createBabyMesh` / `createBabyLayer` directly (horse + donkey required this).
   - In parity-catalog Geometry IR, use the resolved JVM host to choose scale. Dedicated `Baby*Model` shards are already baby geometry and must emit with unit cuboid scale (`BabyProfile.Adult`), including unversioned `profile=root parsed=?` previews; adult/shared hosts are the ones that may use `VanillaUniformBaby`.
3. **Apply renderer-space transforms explicitly**
   - Vanilla living entities use pre-draw pose stack scale `(-1, -1, 1)`.
   - Apply this consistently in preview mesh space or part hierarchy will appear mirrored/offset.
4. **Port setupAnim baseline writes, not only geometry**
   - Include head/tail/leg baseline placement writes and helper overrides (`getLegStandingYOffset`, `getTailXRotOffset`, etc.).
   - Keep preview deterministic (no time-varying drift between rebuilds).
5. **Model-state gates matter**
   - Donkey/mule chest crates are state-controlled (`hasChest`) and should not always render for base diffuse previews.
   - Keep equipment overlays separate from base diffuse topology assumptions.
6. **Validate in layers**
   - Run focused tests (`Horse`, `Donkey`, equipment routes), then full Core suite.
   - Use screenshots for final pose parity and only then refine UV/debug layers.
7. **GPU Explore path must match CPU preview-world policy**
   - CPU fixes (LER column-root, walk order, compose) apply to both tessellation paths via the same `TryBuildStaticMesh` source.
   - Bind pose on GPU: shader `W()`+lift with **`uEntityGpuSkinning=0`**; animated: **`invBind·M_anim`** from full mesh extract.
   - Contract: [`entity-preview-gpu-cpu-parity.md`](entity-preview-gpu-cpu-parity.md).

### Reusable rollout gates/checklist (required)

| Gate | Must pass before merge | Evidence |
|------|-------------------------|----------|
| **G1 — Bytecode reference lock** | Cuboid bounds, UV offsets, root/child `PartPose`, and setup math are copied from pinned `javap` notes before preview-only tweaks. | Builder comments/tests include class + mapping id and constants. |
| **G2 — Runtime template audit** | Runtime implementation follows reusable structure: geometry block, rig/setup block, setup math block, global transform audit notes. **Rigging:** vanilla `PartPose`-equivalent chains use `EntityParityTemplate` (`Mul`, `T`, `Rx`/`Ry`/`Rz`, `Er`) only — no ad-hoc duplicate local `Mul`/`T`/`Er` in refactored builders; call `AssertFinitePose` on primary root/child rig matrices where setup math is non-trivial. | Builder matches block order; grep shows no shadow `static Mul`/`Er` locals outside the template class for entities in scope of the rollout. |
| **G3 — Formula parity asserts** | At least one formula assertion validates setup math, not only cuboid size snapshots. | Dedicated helper/assert computes expected formula and compares runtime output. |
| **G4 — Ordered anchor centers** | At least one ordered anchor-center check validates front/back or mirror placement under `LocalToParent`. | Test uses ordered transformed-center helpers and checks direction/sign. |
| **G5 — Baby/adult consistency** | Baby vs adult route and/or geometry consistency is verified for the same model family. Dedicated `Baby*Model` IR hosts keep unit cuboid scale; adult/shared hosts keep explicit legacy baby-transform coverage. | Test exercises both adult and baby texture paths. For catalog IR, include `BabyCatalogGeometryIrPreviewTests.Dedicated_baby_ir_uses_unit_cuboid_scale_when_profile_is_unversioned_root`. |
| **G6 — Route integrity** | Representative texture path resolves to `SpecificMesh` (not fallback/unknown). | `ClassifyEntityTextureRoute` assertions per entity/model route. |
| **G7 — Test execution** | Targeted entity tests and filtered parity suite both pass locally. | Commands + pass results recorded in session/PR notes. |

### Runtime implementation template (geometry IR parity)

1. **Geometry block** — lifted IR cuboids + UVs in local part space (`GeometryIrMeshEmitter`).
2. **Rig/setup block** — explicit `PartPose` chain via `EntityModelRuntime` / `EntityParityTemplate` (`T`, `Er`, `Mul`, axis rotations) and child attachment order matching javap.
3. **Setup math block** — baseline `setupAnim` writes captured as pure helper formulas (definition-animation IR crosswalk via `EntityParityAnimationMap`).
4. **Global transform audit block** — verify renderer-space orientation/scale does not silently conjugate child rotations; object entities use `EntityBlockEntities` finish passes.

### Baby equine (HorseModel / DonkeyModel splits — 26.1.2)

Rolling passes — repeat whenever preview-facing regress. **Policy matches adult [Quadruped body placement regression](runtime-ir-preview-plan.md#quadruped-body-placement-regression-cow--polarbear--panda) and [Baby JVM family](runtime-ir-preview-plan.md#baby-jvm-family-same-canonical-policy-as-adults--2026-05-28) (column-root LER, IR walk order, no flat-quadruped leg reparent except `BabyDonkeyModel` nested-head case).

| Pass | Goal |
|------|------|
| **1 — Transform audit** | Same as adults: `ApplyLivingEntityRendererColumnRootScale` after model-space emit; **`local × parentWorld`** in `GeometryIrMeshWalk`; production **ModelPart block stack** in `TryComposePartPose` (not legacy **`T × Er`**). Prefer `AbstractEquineModel` / `BabyHorseModel` / `BabyDonkeyModel` IR hosts via `GeometryIrParityJvmResolver` (not mis-lifted `HorseModel`). |
| **2 — Anchor geometry** | Lock neck (`neck_r1` ordered **4×8×4**) vs tail (`tail_r1` **3×3×8**) world-Z separation after `LocalToParent` (`EquineBabyPreview_*_NeckCenterNegativeZOfTailCenter`). Ordered extents avoid leg (`3×8×3`) vs tail permutation ambiguity. **Tail:** IR hierarchy walk only — no hand absolute tail overrides on emit. |
| **3 — Part-tree repair** | `BabyDonkeyModel`: `HeadStackNestedUnderBody` → reparent flat leg siblings under `body`. `BabyHorseModel`: flat root legs — **no** reparent (`UsesVanillaFlatQuadrupedLegBake`). |
| **4 — setupAnim / idle** | Baby donkey: `ComputeBabyDonkeySetupAnimHeadPartsXRotRad` idle after forced −30° pitch (`EquineParity_BabyDonkeySetupAnimHeadPartsXRot_*`). Flat families: peer position strip + rotation deltas only on geometry IR (see runtime plan § Baby JVM family). |
| **5 — Visual** | Screenshots `donkey_baby`, `horse_*_baby`, `mule_baby` vs vanilla client; only then micro-tweak UV or preview-only calibration. |

## Done criteria (global)

- Merger applies element-level rotation; baker unchanged except bugfixes.  
- Tests cover merged JSON with rotation + `TryBake` vertex sanity.  
- Entity table above grows with each shipped mob parity PR.
