# Rig accuracy batch: hostile

**Version:** 26.1.2  
**Pilot scope:** 5 JVM(s) from 56-pilot set

Hostile / boss-scale pilots: creeper (canary), hoglin (default LER), ravager, ender dragon. Hoglin uses **default** LER (`worldRoot x LocalToParent`); creeper/ravager flat quadruped. Cross-link: plan [SS A.3.1](../../runtime-ir-preview-plan.md#a31-pilot-4c-expansion-scan-2026-05-21) and quadruped LER table in [SS A.4](../../runtime-ir-preview-plan.md#a4-manual-explore-checklist-canary).

## Status

| Model | JVM | Shard | Hierarchy | flatCount | Oracle | asmGate | refHier | refWorld | 4C | preview-delta |
|-------|-----|-------|-----------|-----------|--------|---------|---------|----------|-----|-----------------|
| `BabyHoglinModel` | `net.minecraft.client.model.monster.hoglin.BabyHoglinModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | yes |
| `CreeperModel` | `net.minecraft.client.model.monster.creeper.CreeperModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | yes |
| `EnderDragonModel` | `net.minecraft.client.model.monster.dragon.EnderDragonModel` | ok | nested | 0 | pass | yes | yes | yes | 4C dual | no |
| `HoglinModel` | `net.minecraft.client.model.monster.hoglin.HoglinModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | yes |
| `RavagerModel` | `net.minecraft.client.model.monster.ravager.RavagerModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | no |

## Notes

- **CreeperModel:** Geometry regression canary; preview-delta documents flat quadruped + cow-class LER.
- **HoglinModel / BabyHoglinModel:** preview-delta documents **default** LER (not cow-class).
- **EnderDragonModel / RavagerModel:** nested (`flatCount: 0`); 4C batch 5; manual Explore pending.
