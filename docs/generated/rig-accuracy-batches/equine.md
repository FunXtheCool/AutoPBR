# Rig accuracy batch: equine

**Version:** 26.1.2  
**Pilot scope:** 10 JVM(s) from 56-pilot set

Camel and equine JVMs (**10 / 56** pilots). Flat factories use root sibling legs (`flatCount: 4`); `BabyDonkeyModel` is nested (legs on body). All **4C dual** with `assemblyGatePass` and oracle pass. Cross-link: plan [SS A.3.1](../../runtime-ir-preview-plan.md#a31-pilot-4c-expansion-scan-2026-05-21).

## Status

| Model | JVM | Shard | Hierarchy | flatCount | Oracle | asmGate | refHier | refWorld | 4C | preview-delta |
|-------|-----|-------|-----------|-----------|--------|---------|---------|----------|-----|-----------------|
| `AbstractEquineModel` | `net.minecraft.client.model.animal.equine.AbstractEquineModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | no |
| `AdultCamelModel` | `net.minecraft.client.model.animal.camel.AdultCamelModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | no |
| `BabyCamelModel` | `net.minecraft.client.model.animal.camel.BabyCamelModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | no |
| `BabyDonkeyModel` | `net.minecraft.client.model.animal.equine.BabyDonkeyModel` | ok | nested | 0 | pass | yes | yes | yes | 4C dual | no |
| `BabyHorseModel` | `net.minecraft.client.model.animal.equine.BabyHorseModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | no |
| `CamelModel` | `net.minecraft.client.model.animal.camel.CamelModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | no |
| `CamelSaddleModel` | `net.minecraft.client.model.animal.camel.CamelSaddleModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | no |
| `DonkeyModel` | `net.minecraft.client.model.animal.equine.DonkeyModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | no |
| `EquineSaddleModel` | `net.minecraft.client.model.animal.equine.EquineSaddleModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | no |
| `HorseModel` | `net.minecraft.client.model.animal.equine.HorseModel` | ok | flat | 4 | pass | yes | yes | yes | 4C dual | no |

## Notes

- **Binding gap hosts:** `DonkeyModel`, `HorseModel`, `EquineSaddleModel`, `CamelModel`, `CamelSaddleModel` — oracle pass via delegate/host mesh; IR keeps composed-flat sibling legs where javap uses `createBodyMesh` on root.
- **BabyDonkeyModel:** nested hierarchy (`referenceHierarchyMatch` aligned in quality JSON).
- **Backlog:** Explore sign-off for camel/horse/donkey textures; no preview-delta overlays yet.
