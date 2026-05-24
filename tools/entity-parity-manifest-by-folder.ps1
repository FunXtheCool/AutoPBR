# Requires PowerShell 5+
# Groups minecraft_26.1.2_entity_texture_model_manifest.json rules by the first
# path segment under assets/minecraft/textures/entity/ (batch parity by folder).
# Usage: pwsh -File tools/entity-parity-manifest-by-folder.ps1
# Optional: -Json to emit compact JSON for dashboards.

param([switch]$Json)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifest = Join-Path $root 'src\AutoPBR.Core\Data\minecraft-native\minecraft_26.1.2_entity_texture_model_manifest.json'
if (-not (Test-Path $manifest)) { throw "Manifest not found: $manifest" }

$doc = Get-Content $manifest -Raw | ConvertFrom-Json
$rows = foreach ($r in $doc.rules) {
    $rel = $r.path_prefix -replace '^assets/minecraft/textures/entity/', ''
    $folder = ($rel -split '/')[0]
    [PSCustomObject]@{ folder = $folder; builder = $r.builder_method }
}
$grouped = $rows | Group-Object folder | Sort-Object Name
if ($Json) {
    $out = foreach ($g in $grouped) {
        $builders = $g.Group | ForEach-Object { $_.builder } | Sort-Object -Unique
        [ordered]@{
            folder = $g.Name
            ruleCount = $g.Count
            builders = @($builders)
        }
    }
    $out | ConvertTo-Json -Depth 5 -Compress
    exit 0
}

foreach ($g in $grouped) {
    $builders = ($g.Group | ForEach-Object { $_.builder } | Sort-Object -Unique) -join '; '
    '{0,-22} rules={1,4}  {2}' -f $g.Name, $g.Count, $builders
}
