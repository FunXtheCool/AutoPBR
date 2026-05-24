# Assembly-parity pilot Explore 3D checklist (26.1.2)

**Regenerated:** 2026-05-19 (`tools/regen-assembly-pilots.ps1`)  
**Quality:** [`geometry-lift-quality-26.1.2.json`](geometry-lift-quality-26.1.2.json) — **17/56** pilots `assemblyGatePass: true`  
**Automated viewport:** `GeometryIrAssemblyViewportSanityTests` — **SheepModel** only on T1 strict (`geometry_ir_assembly_viewport_strict_jvm.txt`)

## Canary set (Phase 4 manual sign-off)

| JVM | `assemblyGatePass` | Viewport T1 (strict) | Explore 3D manual |
|-----|-------------------|----------------------|-------------------|
| `net.minecraft.client.model.monster.creeper.CreeperModel` | yes | yes (T2 probe; right-compose LER basis for flat offset bake) | **pending** |
| `net.minecraft.client.model.animal.cow.CowModel` | yes | yes (T2 probe; flat bake + `offsetAndRotation` body; `LocalToParent * S` LER) | **pending** |
| `net.minecraft.client.model.animal.pig.PigModel` | no | no | **pending** |
| `net.minecraft.client.model.animal.sheep.SheepModel` | no | **yes** (T1 strict) | **pending** |
| `net.minecraft.client.model.monster.hoglin.HoglinModel` | yes | T2 pass (default LER; not creeper-flat) | **pending** |
| `net.minecraft.client.model.monster.hoglin.BabyHoglinModel` (baby pilot) | yes | T2 probe pass (`AUTOPBR_RUN_ASSEMBLY_VIEWPORT_PROBES=1`; default LER fold) | **pending** manual Explore |
| Other babies (e.g. `BabyCowModel`) | yes | T2 probe pass | **pending** |

## Promotion policy (4C)

- **Dual gate (pilots only):** `assemblyGatePass` **and** viewport T1 strict pass before adding a pilot to `geometry_ir_partial_to_ok_promotion_jvm.txt` or keeping it on `geometry_ir_assembly_viewport_strict_jvm.txt` for promotion purposes.
- **Today:** **0 / 56** pilots pass both T1 strict gates (SheepModel: T1 only; CreeperModel/CowModel: assembly + T2 viewport probe).
- **After viewport fixes:** Re-run 4C in **one PR** — allowlists + shards + `geometry-lift-quality-26.1.2.json` (`tools/regen-assembly-pilots.ps1` first).
- Entity-wide rows (`HumanoidModel`, `VillagerModel`, `SkullModel`) use assembly gate only — not the pilot dual gate.

## Notes

- Creeper/cow flat root siblings need `ApplyGeometryIrParityLivingEntityRendererPreviewBasis` (`LocalToParent * S`) on parity emit — default `S * LocalToParent` inverts leg/head Y (e.g. cow leg Y≈6 vs head Y≈5.1).
- Re-run this checklist after preview-delta or lifter work; update manual column when signed off in Explore.
