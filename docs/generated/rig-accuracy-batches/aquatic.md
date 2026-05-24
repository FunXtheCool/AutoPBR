# Rig accuracy batch: aquatic

**Version:** 26.1.2  
**Pilot scope:** 4 JVM(s) from 56-pilot set

Sniffer and axolotl pilots (**4 / 56**): nested leg bind (javap legs on `body`; `flatCount: 0`). All on **4C dual** gate. Not fish-layer models (see `aquatic` batch name = amphibian/sniffer family in pilot set). Cross-link: plan [SS A.3.1](../../runtime-ir-preview-plan.md#a31-pilot-4c-expansion-scan-2026-05-21).

## Status

| Model | JVM | Shard | Hierarchy | flatCount | Oracle | asmGate | refHier | refWorld | 4C | preview-delta |
|-------|-----|-------|-----------|-----------|--------|---------|---------|----------|-----|-----------------|
| `AdultAxolotlModel` | `net.minecraft.client.model.animal.axolotl.AdultAxolotlModel` | ok | nested | 0 | pass | yes | yes | yes | 4C dual | no |
| `BabyAxolotlModel` | `net.minecraft.client.model.animal.axolotl.BabyAxolotlModel` | ok | nested | 0 | pass | yes | yes | yes | 4C dual | no |
| `SnifferModel` | `net.minecraft.client.model.animal.sniffer.SnifferModel` | ok | nested | 0 | pass | yes | yes | yes | 4C dual | no |
| `SniffletModel` | `net.minecraft.client.model.animal.sniffer.SniffletModel` | ok | nested | 0 | pass | yes | yes | yes | 4C dual | no |

## Notes

- **Nested legs:** Quality JSON `suspectedFlatNestedPartCount: 0` with javap legs on body; distinct from composed-flat creeper/cow pattern.
- **Sniffer / axolotl:** 4C batch 4; renderer-state pilots (Sniffer) documented in plan Part D.
- **Backlog:** Explore silhouettes; animation B.3 rows (Sniffer dig, axolotl swim).
