#requires -Version 5.1
<#
.SYNOPSIS
  Bakes Java ModelPart reference JSON for pilot models (Phase 4).

.PARAMETER JavaHome
  JDK 25+ for the pinned 26.1.2 client.jar (class file 69). Defaults to %USERPROFILE%\.autopbr\jdk-25 when present.

.PARAMETER ModelsFromFile
  Text file of official JVM names (one per line; # comments). Overrides -Models when set.
  Example: docs/generated/geometry-assembly-parity-pilots-26.1.2.txt (56 assembly-parity pilots).

.EXAMPLE
  pwsh -File tools/Export-GeometryReference.ps1

.EXAMPLE
  pwsh -File tools/Export-GeometryReference.ps1 -ModelsFromFile docs/generated/geometry-assembly-parity-pilots-26.1.2.txt

.EXAMPLE
  pwsh -File tools/Export-GeometryReference.ps1 -Models @(
    'net.minecraft.client.model.monster.creeper.CreeperModel',
    'net.minecraft.client.model.animal.cow.CowModel',
    'net.minecraft.client.model.QuadrupedModel'
  )
#>
param(
    [string[]] $Models = @(
        'net.minecraft.client.model.animal.fish.CodModel',
        'net.minecraft.client.model.animal.fish.SalmonModel',
        'net.minecraft.client.model.animal.chicken.ChickenModel',
        'net.minecraft.client.model.animal.cow.CowModel',
        'net.minecraft.client.model.animal.pig.PigModel',
        'net.minecraft.client.model.ambient.BatModel',
        'net.minecraft.client.model.monster.creeper.CreeperModel',
        'net.minecraft.client.model.monster.blaze.BlazeModel',
        'net.minecraft.client.model.monster.guardian.GuardianModel',
        'net.minecraft.client.model.animal.squid.BabySquidModel',
        'net.minecraft.client.model.player.PlayerModel',
        'net.minecraft.client.model.HumanoidModel',
        'net.minecraft.client.model.animal.axolotl.AdultAxolotlModel',
        'net.minecraft.client.model.animal.axolotl.BabyAxolotlModel',
        'net.minecraft.client.model.animal.frog.FrogModel',
        'net.minecraft.client.model.animal.allay.AllayModel',
        'net.minecraft.client.model.animal.sniffer.SnifferModel',
        'net.minecraft.client.model.monster.warden.WardenModel',
        'net.minecraft.client.model.monster.vex.VexModel',
        'net.minecraft.client.model.player.PlayerCapeModel',
        # Layer A static rig pilots (feline / fox / armadillo / breeze + baby JVMs)
        'net.minecraft.client.model.animal.feline.AdultCatModel',
        'net.minecraft.client.model.animal.feline.BabyCatModel',
        'net.minecraft.client.model.animal.feline.AdultFelineModel',
        'net.minecraft.client.model.animal.feline.BabyFelineModel',
        'net.minecraft.client.model.animal.fox.AdultFoxModel',
        'net.minecraft.client.model.animal.fox.BabyFoxModel',
        'net.minecraft.client.model.animal.fox.FoxModel',
        'net.minecraft.client.model.animal.armadillo.ArmadilloModel',
        'net.minecraft.client.model.animal.armadillo.AdultArmadilloModel',
        'net.minecraft.client.model.animal.armadillo.BabyArmadilloModel',
        'net.minecraft.client.model.monster.breeze.BreezeModel',
        # Lift-quality batch 1 (geometry-lift-quality-26.1.2 referenceCuboidsMatch)
        'net.minecraft.client.model.QuadrupedModel',
        'net.minecraft.client.model.animal.bee.AdultBeeModel',
        'net.minecraft.client.model.animal.bee.BabyBeeModel',
        'net.minecraft.client.model.animal.bee.BeeModel',
        'net.minecraft.client.model.animal.bee.BeeStingerModel',
        'net.minecraft.client.model.animal.camel.AdultCamelModel',
        'net.minecraft.client.model.animal.camel.BabyCamelModel',
        'net.minecraft.client.model.animal.camel.CamelModel',
        'net.minecraft.client.model.animal.camel.CamelSaddleModel',
        'net.minecraft.client.model.animal.chicken.AdultChickenModel',
        'net.minecraft.client.model.animal.chicken.BabyChickenModel',
        'net.minecraft.client.model.animal.chicken.ColdChickenModel',
        'net.minecraft.client.model.animal.cow.BabyCowModel',
        'net.minecraft.client.model.animal.cow.ColdCowModel',
        'net.minecraft.client.model.animal.cow.WarmCowModel',
        'net.minecraft.client.model.animal.dolphin.BabyDolphinModel',
        'net.minecraft.client.model.animal.dolphin.DolphinModel',
        'net.minecraft.client.model.animal.equine.AbstractEquineModel',
        'net.minecraft.client.model.animal.equine.BabyDonkeyModel',
        'net.minecraft.client.model.animal.equine.BabyHorseModel',
        'net.minecraft.client.model.animal.equine.DonkeyModel',
        'net.minecraft.client.model.animal.equine.EquineSaddleModel',
        'net.minecraft.client.model.animal.equine.HorseModel',
        'net.minecraft.client.model.animal.feline.AbstractFelineModel',
        'net.minecraft.client.model.animal.feline.AdultOcelotModel',
        'net.minecraft.client.model.animal.feline.BabyOcelotModel',
        'net.minecraft.client.model.animal.fish.PufferfishBigModel',
        'net.minecraft.client.model.animal.fish.PufferfishMidModel',
        'net.minecraft.client.model.animal.fish.PufferfishSmallModel',
        'net.minecraft.client.model.animal.fish.TropicalFishLargeModel',
        'net.minecraft.client.model.animal.fish.TropicalFishSmallModel',
        'net.minecraft.client.model.animal.frog.TadpoleModel',
        'net.minecraft.client.model.animal.ghast.HappyGhastHarnessModel'
    ),

    [string] $FactoryMethod = 'createBodyLayer',

    [string] $JavaHome = '',

    [string] $ModelsFromFile = ''
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $here

if (-not [string]::IsNullOrWhiteSpace($ModelsFromFile)) {
    $manifestPath = if ([System.IO.Path]::IsPathRooted($ModelsFromFile)) {
        $ModelsFromFile
    }
    else {
        Join-Path $repoRoot $ModelsFromFile
    }
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "ModelsFromFile not found: $manifestPath"
    }
    $Models = Get-Content -LiteralPath $manifestPath |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith('#') }
    Write-Host "Loaded $($Models.Count) models from $manifestPath"
}

$refRoot = Join-Path $here 'MinecraftGeometryReference'
if (-not (Test-Path -LiteralPath $refRoot)) {
    throw "MinecraftGeometryReference not found: $refRoot"
}

$gradleJava = 'C:\Program Files\Eclipse Adoptium\jdk-21.0.6.7-hotspot'
if (-not (Test-Path -LiteralPath $gradleJava)) {
    $gradleJava = $env:JAVA_HOME
}

if ([string]::IsNullOrWhiteSpace($JavaHome)) {
    $JavaHome = Join-Path $env:USERPROFILE '.autopbr\jdk-25'
}

if (-not (Test-Path -LiteralPath (Join-Path $JavaHome 'bin\java.exe'))) {
    throw @"
JDK 25+ required for 26.1.2 client.jar (class file 69).
Install Temurin 25 to $JavaHome or pass -JavaHome.
See tools/MinecraftGeometryReference/README.md
"@
}

$props = Join-Path $refRoot 'gradle.properties'
$installLine = "org.gradle.java.installations.paths=$($JavaHome -replace '\\','/')"
if (Test-Path -LiteralPath $props) {
    $content = Get-Content -LiteralPath $props -Raw
    if ($content -notmatch 'org\.gradle\.java\.installations\.paths=') {
        Add-Content -LiteralPath $props -Value $installLine
    }
}
else {
    Set-Content -LiteralPath $props -Value $installLine
}

$createMeshModels = @(
    'net.minecraft.client.model.player.PlayerModel',
    'net.minecraft.client.model.HumanoidModel'
)

$createCapeLayerModels = @(
    'net.minecraft.client.model.player.PlayerCapeModel'
)

$createSaddleLayerModels = @(
    'net.minecraft.client.model.animal.camel.CamelSaddleModel',
    'net.minecraft.client.model.animal.equine.EquineSaddleModel',
    'net.minecraft.client.model.animal.nautilus.NautilusSaddleModel'
)

$createFurLayerModels = @(
    'net.minecraft.client.model.animal.sheep.SheepFurModel'
)

$createHarnessLayerModels = @(
    'net.minecraft.client.model.animal.ghast.HappyGhastHarnessModel'
)

Push-Location $refRoot
try {
    $env:JAVA_HOME = $gradleJava
    foreach ($m in $Models) {
        $method = if ($createMeshModels -contains $m) {
            'createMesh'
        }
        elseif ($createCapeLayerModels -contains $m) {
            'createCapeLayer'
        }
        elseif ($createSaddleLayerModels -contains $m) {
            'createSaddleLayer'
        }
        elseif ($createHarnessLayerModels -contains $m) {
            'createHarnessLayer'
        }
        elseif ($createFurLayerModels -contains $m) {
            'createFurLayer'
        }
        else {
            $FactoryMethod
        }
        Write-Host "Baking reference: $m ($method)"
        & .\gradlew.bat run --quiet --args="$m $method"
        if ($LASTEXITCODE -ne 0) {
            throw "Reference bake failed for $m (exit $LASTEXITCODE)"
        }
    }
}
finally {
    Pop-Location
}

Write-Host "Reference output: $refRoot\reference-output"
