#requires -Version 5.1
<#
.SYNOPSIS
  Regenerates geometry shards for the 20 non-pilot partial index rows (26.1.2).
#>
param(
    [string] $ClientJar = (Join-Path $PSScriptRoot "minecraft-parity/26.1.2/client.jar"),
    [string] $VersionLabel = "26.1.2"
)

$ErrorActionPreference = "Stop"
# Remaining non-pilot partial rows (2026-05-21 drain). Promoted: AbstractPiglin + piglin concretes + player cape/ears/model + Endermite.
$partials = @(
    "net.minecraft.client.model.animal.chicken.ColdChickenModel",
    "net.minecraft.client.model.animal.cow.ColdCowModel",
    "net.minecraft.client.model.animal.cow.WarmCowModel",
    "net.minecraft.client.model.animal.pig.ColdPigModel",
    "net.minecraft.client.model.monster.zombie.ZombieVillagerModel",
    "net.minecraft.client.model.object.armorstand.ArmorStandArmorModel",
    "net.minecraft.client.model.object.banner.BannerModel",
    "net.minecraft.client.model.object.boat.BoatModel",
    "net.minecraft.client.model.object.boat.RaftModel",
    "net.minecraft.client.model.object.chest.ChestModel",
    "net.minecraft.client.model.object.skull.PiglinHeadModel"
)

foreach ($jvm in $partials) {
    Write-Host "=== $jvm ==="
    & (Join-Path $PSScriptRoot "Generate-GeometryIndex.ps1") `
        -ClientJar $ClientJar `
        -VersionLabel $VersionLabel `
        -Single $jvm `
        -UseAsmLift `
        -Quiet
}

Write-Host "Done. Review docs/generated/geometry-index-$VersionLabel.json counts."
