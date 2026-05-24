# Rig accuracy batch: humanoid

**Version:** 26.1.2  
**Pilot scope:** 0 JVM(s) from 56-pilot set + 3 quality-index row(s)

Humanoid / player models tracked in quality JSON but **outside** the 56-pilot assembly manifest. Included for rig-accuracy cross-family reference. Cross-link: plan [SS A.3.1](../../runtime-ir-preview-plan.md#a31-pilot-4c-expansion-scan-2026-05-21).

## Status

| Model | JVM | Shard | Hierarchy | flatCount | Oracle | asmGate | refHier | refWorld | 4C | preview-delta |
|-------|-----|-------|-----------|-----------|--------|---------|---------|----------|-----|-----------------|
| `HumanoidModel` | `net.minecraft.client.model.HumanoidModel` | ok | nested | 0 | n/a | yes | yes | yes | partial->ok | no |

## Notes

- **HumanoidModel / PlayerModel:** `javapPoseOracleMatch` n/a (abstract/interface); cuboid strict paths in allowlist.
- **PlayerCapeModel:** `partial` shard — cape cuboid lift WIP; 14 parts present.
