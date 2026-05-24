# Rig accuracy batch: flying

**Version:** 26.1.2  
**Pilot scope:** 0 JVM(s) from 56-pilot set + 4 quality-index row(s)

Flying / ambient models from quality JSON and partial->ok promotion (not in 56-pilot list except none). `BatModel` has committed preview-delta. Cross-link: plan [SS A.3.1](../../runtime-ir-preview-plan.md#a31-pilot-4c-expansion-scan-2026-05-21).

## Status

| Model | JVM | Shard | Hierarchy | flatCount | Oracle | asmGate | refHier | refWorld | 4C | preview-delta |
|-------|-----|-------|-----------|-----------|--------|---------|---------|----------|-----|-----------------|
| `AllayModel` | `net.minecraft.client.model.animal.allay.AllayModel` | ok | nested | 0 | n/a | yes | yes | yes | partial->ok | no |
| `BatModel` | `net.minecraft.client.model.ambient.BatModel` | ok | nested | 0 | n/a | yes | yes | yes | partial->ok | yes |
| `BeeModel` | `net.minecraft.client.model.animal.bee.BeeModel` | ok | nested | 0 | n/a | yes | yes | yes | - | no |
| `VexModel` | `net.minecraft.client.model.monster.vex.VexModel` | ok | nested | 0 | n/a | yes | yes | yes | partial->ok | no |

## Notes

- **BatModel:** Mob-family pilot; preview-delta committed; strict cuboid + parity mesh pass in tests.
- **BeeModel / AllayModel / VexModel:** partial->ok promotion targets; oracle varies; reference promotion backlog.
