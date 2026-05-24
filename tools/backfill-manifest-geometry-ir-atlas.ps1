# Backfills geometry_ir_texture_width / geometry_ir_texture_height on parity manifest rows
# from packaged geometry shards (docs/generated/geometry/26.1.2) and GeometryIrParityAtlasDefaults.
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_texture_model_manifest.json"
$geometryDir = Join-Path $repoRoot "docs/generated/geometry/26.1.2"

$defaults = @{
    Horse = @(64, 64); DonkeyMuleHorse = @(64, 64); Cat = @(64, 64); Wolf = @(64, 32)
    HumanoidVillager = @(64, 64); WanderingTrader = @(64, 64); PlayerWide = @(64, 64); PlayerSlim = @(64, 64)
    Blaze = @(64, 32); Bed = @(64, 64); StandingSignEntity = @(64, 32); HangingSignEntity = @(64, 32)
    DecoratedPotEntity = @(64, 64); EquipmentHumanoidLeggings = @(64, 64)
}

function Get-ShardJvm([object]$rule) {
    if ($rule.geometry_ir_official_jvm) { return $rule.geometry_ir_official_jvm }
    if ($rule.deobf_model_class -and $rule.deobf_model_class -notmatch 'renderer') { return $rule.deobf_model_class }
    if ($rule.deobfuscated_model_class_pre_restructure) { return $rule.deobfuscated_model_class_pre_restructure }
    return $null
}

$doc = Get-Content $manifestPath -Raw | ConvertFrom-Json
$updated = 0
foreach ($rule in $doc.rules) {
    $w = $null; $h = $null
    $jvm = Get-ShardJvm $rule
    if ($jvm) {
        $shard = Join-Path $geometryDir "$jvm.json"
        if (Test-Path $shard) {
            $shardDoc = Get-Content $shard -Raw | ConvertFrom-Json
            if ($shardDoc.textureWidth -and $shardDoc.textureHeight) {
                $w = [int]$shardDoc.textureWidth
                $h = [int]$shardDoc.textureHeight
            }
        }
    }
    if (-not $w -and $rule.builder_method -and $defaults.ContainsKey($rule.builder_method)) {
        $pair = $defaults[$rule.builder_method]
        $w = $pair[0]; $h = $pair[1]
    }
    if ($w -and $h) {
        if ($rule.geometry_ir_texture_width -ne $w -or $rule.geometry_ir_texture_height -ne $h) {
            $rule | Add-Member -NotePropertyName geometry_ir_texture_width -NotePropertyValue $w -Force
            $rule | Add-Member -NotePropertyName geometry_ir_texture_height -NotePropertyValue $h -Force
            $updated++
        }
    }
}

$json = $doc | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($manifestPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "Backfilled atlas columns on $updated manifest rules -> $manifestPath"
