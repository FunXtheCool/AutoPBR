# Minecraft geometry reference bake (Phase 4–6)

Java harness that calls vanilla `createBodyLayer()` / `createMesh()`, walks baked `ModelPart` trees, and writes JSON aligned with AutoPBR geometry IR schema v2 (`extractionStatus: reference_java`).

## Requirements

- **JDK 25+** to load the pinned `tools/minecraft-parity/26.1.2/client.jar` (class file 69)
- **JDK 21** to run Gradle itself (Gradle 8.14 does not run on JDK 25 yet)
- Install Temurin 25 under `%USERPROFILE%\.autopbr\jdk-25` (recommended) or pass a path via `gradle.properties` / `Export-GeometryReference.ps1 -JavaHome`

## Build and run

```powershell
# One pilot
$env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-21.0.6.7-hotspot"
cd tools/MinecraftGeometryReference
.\gradlew.bat run --args="net.minecraft.client.model.animal.fish.CodModel createBodyLayer"

# All Phase 4–6 pilots (quadruped + loop-pose + humanoid createMesh)
pwsh -File tools/Export-GeometryReference.ps1

# Assembly-parity pilot set (56 JVMs) — includes worldPose per part (Phase 3A)
pwsh -File tools/Export-GeometryReference.ps1 -ModelsFromFile docs/generated/geometry-assembly-parity-pilots-26.1.2.txt

# Canary subset
pwsh -File tools/Export-GeometryReference.ps1 -Models @(
  'net.minecraft.client.model.monster.creeper.CreeperModel',
  'net.minecraft.client.model.animal.cow.CowModel',
  'net.minecraft.client.model.QuadrupedModel'
)

# Humanoid pilots use createMesh (not createBodyLayer)
.\gradlew.bat run --args="net.minecraft.client.model.player.PlayerModel createMesh"
.\gradlew.bat run --args="net.minecraft.client.model.HumanoidModel createMesh"
```

Output: `tools/MinecraftGeometryReference/reference-output/<fqn>.json`

## C# diff

- `src/AutoPBR.Core/Preview/GeometryIrReferenceComparer.cs` — cuboid fingerprint compare (reference vs IR shard or parity mesh)
- `tests/AutoPBR.Core.Tests/GeometryIrReferenceBakeTests.cs` — runs when `reference_java` JSON is present (skips stubs / missing files)
