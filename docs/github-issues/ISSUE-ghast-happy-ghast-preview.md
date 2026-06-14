# Ghast / Happy Ghast: broken mesh, UV gaps, tentacle placement in Explore 3D preview

**Labels:** `bug`, `preview`, `geometry-ir`, `geometry-compiler`, `parity-catalog`, `needs-investigation`

**JVMs:**

- `net.minecraft.client.model.monster.ghast.GhastModel`
- `net.minecraft.client.model.animal.ghast.HappyGhastModel`
- (related) `net.minecraft.client.model.animal.ghast.HappyGhastHarnessModel`

**Textures:**

- `assets/minecraft/textures/entity/ghast/ghast.png`
- `assets/minecraft/textures/entity/ghast/happy_ghast.png`
- `assets/minecraft/textures/entity/ghast/ghast_shooting.png` (variant → monster GhastModel IR)

**Minecraft version:** 26.1.2

---

## Summary

Explore 3D preview for Ghast family shows **incomplete / wrong mesh**: tentacles pointing wrong direction, missing faces, UV surfaces in wrong locations, tentacles intersecting or floating relative to body shell. Work split between **geometry compiler lift fidelity**, **preview emit semantics** (cuboid Y + UV footprint), and possibly the **same GPU upload path** issues as dolphin parity-catalog entities.

---

## Expected behavior

- Monster ghast: large body cube; nine tentacles hang **downward** (−Y) from attachment points under body (javap `BuildGhast` / `addBox(-1,0,-1,2,h,2)` with pitch at root).
- Happy ghast: body shell + nine shorter tentacles; reference omits nested `inner_body` cuboids (hoisted into `body` at bake).
- Skin atlas: single `texOffs(0,0)` unfold — body `16³`, tentacles `2×h×2` on 64×32 (monster) or 64×64 (happy).
- Tentacle idle motion: `xRot = 0.4 + 0.2*sin(age*0.3 + index)` (`GeometryIrEmitPolicy.ComputeGhastAnimateTentaclesXRot`).

## Actual behavior (user report)

- Tentacles grow **upward** or sit **inside** body volume.
- **Missing surfaces** / holes in rendered mesh.
- UV-aligned quads appear at wrong world positions.
- Happy ghast may show harness/inner-body artifacts depending on asset.

---

## Investigation findings (partial — work in progress)

### Lifted IR pose tree (26.1.2) — largely OK

- Shards: `docs/generated/geometry/26.1.2/net.minecraft.client.model.monster.ghast.GhastModel.json`, `...HappyGhastModel.json`
- Reference: `tools/MinecraftGeometryReference/reference-output/...`
- Root carries `MeshTransformer.scaling(4.5f)`; tentacles are root siblings of body.
- **Lift repair:** `GeometryIrLiftTreeRepair.CollapseInnerBodyUnderBody` — Java reference omits `inner_body` under `body` (HappyGhast); bytecode lift keeps it; repair hoists cuboids into `body`.

### Preview emit semantics — targeted fixes landed

Bytecode lift records tentacle `addBox` with local **+Y** extent (`0..h`). Vanilla preview hangs on **−Y**:

```csharp
// GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace
// Maps 0..h → -h..0 for .ghast. JVM tentacle* parts
```

UV footprint override (vanilla unfold sizes):

```csharp
// GeometryIrEmitPolicy.TryApplyGhastFamilyCuboidUvFootprint
// body 16³; tentacles 2×h×2
```

Hand builder reference: `src/AutoPBR.Core/Preview/Entities/CleanRoomEntityFlying.Build.GhastBlaze.cs`

### Core tests (pass when shard status `ok`)

`tests/AutoPBR.Core.Tests/GhastPreviewAttachmentTests.cs`:

- Tentacle Y reorient policy (monster + happy, not harness)
- Runtime mesh body/tentacle hull gap < 0.15 (bind pose)
- Monster ghast world AABB landmarks vs reference
- `ghast_shooting.png` resolves to monster `GhastModel` IR
- GPU bind mesh tentacle hang gap

**Gap:** tests validate **runtime merged model / bake**, not necessarily **live Explore GL path** (same class of bug as dolphin fallback).

---

## Open questions / likely remaining bugs

1. **Live preview upload:** parity-catalog entities may still hit pack-converter GPU upload before rebake commit (see dolphin issue). Ghast is parity-catalog — verify `subject=parity-cpu-rebake` in log.
2. **HappyGhast `inner_body`:** lift repair vs runtime `BuildHappyGhast` — are hoisted cuboids double-counted or missing faces after emit?
3. **UV footprint:** are all six faces emitted per cuboid after reorient? Missing surfaces may be winding/cull or wrong `uw/uh/ud` after Y flip.
4. **LER / root scale:** ghast family skips some LER mirror paths — confirm `GeometryIrMeshEmitter` and `PreviewRenderStateSynthesis` agree for 4.5× root.
5. **Harness / ropes / equipment** textures (`HappyGhastHarnessModel`, `happy_ghast_ropes`) — separate dispatch; may need own emit rules.
6. **setupAnim / tentacle sway** at `animationTime>0` — attachment tests mostly bind pose; animated tentacles may detach.

---

## How to reproduce

1. AutoPBR Explore, `minecraft-26.1.2-client.jar`.
2. Select `ghast.png` or `happy_ghast.png`.
3. 3D preview; compare to in-game / reference JSON landmarks.

## Diagnostic log targets

Same as dolphin parity-catalog:

- Good: `Parity-catalog CPU bind-pose mesh`, `subject=parity-cpu-rebake`
- Bad: `Fallback mesh upload used`, `pack-converter CPU subject`, persistent `Entity mesh upload deferred`

---

## Key files

| Area | Path |
|------|------|
| Emit policy (tentacle Y, UV) | `src/AutoPBR.Core/Preview/GeometryIrEmitPolicy.cs` |
| Cuboid emit | `src/AutoPBR.Core/Preview/GeometryIrMeshEmitter.cs` |
| Hand builder | `src/AutoPBR.Core/Preview/Entities/CleanRoomEntityFlying.Build.GhastBlaze.cs` |
| Lift repair (inner_body) | `src/AutoPBR.Tools.GeometryCompiler/GeometryIrLiftTreeRepair.cs` |
| Part tree repair | `src/AutoPBR.Core/Preview/GeometryIrPartTreeRepair.cs` |
| Dispatch | `CleanRoomEntityDispatch.SpecificSlots.S71-90.cs` |
| GPU upload | `OpenGlPreviewBackend.Render.PassSetup.cs` |
| Tests | `tests/AutoPBR.Core.Tests/GhastPreviewAttachmentTests.cs` |
| IR shards | `docs/generated/geometry/26.1.2/net.minecraft.client.model.*.ghast.*.json` |

---

## Suggested next steps for reviewer (ChatGPT / human)

1. **javap** `GhastModel.createBodyLayer` + `HappyGhastModel.createBodyLayer` (26.1.2): confirm tentacle `addBox` Y ranges, `texOffs`, and whether `inner_body` is a separate Part in Java bake.
2. Compare **reference_java JSON** cuboid corners vs runtime `TryBuildStaticMesh` output per tentacle index (not just hull gap).
3. Trace **face emission** after `TryReorientGhastFamilyTentacleCuboidYForModelSpace` — enumerate triangle count per part vs reference bake.
4. Live App: confirm rebake commit path; if Core tests pass but preview fails → App upload bug (dolphin playbook).
5. Happy ghast: validate `CollapseInnerBodyUnderBody` + `BuildHappyGhast` produce same visible shell thickness.
6. Add **Explore integration test** or screenshot regression for ghast + happy_ghast bind pose.

---

## Commands

```bash
dotnet test tests/AutoPBR.Core.Tests/AutoPBR.Core.Tests.csproj --filter "FullyQualifiedName~Ghast"
# Re-lift shards if investigating compiler:
# dotnet run --project src/AutoPBR.Tools.GeometryCompiler -- ...
```

---

## Related

- Dolphin fin dislocation issue (same App upload class): `docs/github-issues/ISSUE-dolphin-fin-dislocation.md`
- Prior transcript: Ghast tentacle +Y vs hang-down emit fix; Happy ghast UV/surface incomplete screenshot follow-up.
