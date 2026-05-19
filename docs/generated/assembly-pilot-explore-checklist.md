# Assembly-parity pilot Explore 3D checklist (26.1.2)

**Regenerated:** 2026-05-19 (`tools/regen-assembly-pilots.ps1`)  
**Quality:** [`geometry-lift-quality-26.1.2.json`](geometry-lift-quality-26.1.2.json) — **17/56** pilots `assemblyGatePass: true`  
**Automated viewport:** `GeometryIrAssemblyViewportSanityTests` — **SheepModel** only on T1 strict (`geometry_ir_assembly_viewport_strict_jvm.txt`)

## Canary set (Phase 4 manual sign-off)

| JVM | `assemblyGatePass` | Viewport T1 (strict) | Explore 3D manual |
|-----|-------------------|----------------------|-------------------|
| `net.minecraft.client.model.monster.creeper.CreeperModel` | yes | no (legs above head in LER space) | **pending** |
| `net.minecraft.client.model.animal.cow.CowModel` | no (`javapPoseOracleMatch`) | no | **pending** |
| `net.minecraft.client.model.animal.pig.PigModel` | no | no | **pending** |
| `net.minecraft.client.model.animal.sheep.SheepModel` | no | **yes** (T1 strict) | **pending** |
| `net.minecraft.client.model.monster.hoglin.HoglinModel` | yes | not on strict list | **pending** |
| Baby variant (e.g. `BabyCowModel`, `BabyHoglinModel`) | partial | not strict | **pending** |

## Promotion policy (4C)

- **Do not** add pilot quadrupeds to `geometry_ir_partial_to_ok_promotion_jvm.txt` until **both** `assemblyGatePass` and viewport T1 pass.
- Entity-wide allowlist rows (`HumanoidModel`, `VillagerModel`, `SkullModel`) remain gated by quality JSON + existing strict lists.

## Notes

- Creeper passes full assembly gate after flat-bake hierarchy policy but still fails viewport T1 — matches known flat `PartPose.offset` factory layout vs Explore composition.
- Re-run this checklist after preview-delta or lifter work; update manual column when signed off in Explore.
