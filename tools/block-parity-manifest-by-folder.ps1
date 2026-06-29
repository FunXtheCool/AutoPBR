# Groups minecraft_26.1.2_block_texture_model_manifest.json rules by preview_shape and family_id.
# Usage: pwsh -File tools/block-parity-manifest-by-folder.ps1
# Optional: -Json

param([switch]$Json)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifest = Join-Path $root 'src\AutoPBR.Core\Data\minecraft-native\minecraft_26.1.2_block_texture_model_manifest.json'
if (-not (Test-Path $manifest)) { throw "Manifest not found: $manifest (run generate-block-parity-manifest.ps1 first)" }

$doc = Get-Content $manifest -Raw | ConvertFrom-Json
$grouped = $doc.rules | Group-Object preview_shape | Sort-Object Name
if ($Json) {
    $out = foreach ($g in $grouped) {
        [ordered]@{
            preview_shape = $g.Name
            ruleCount = $g.Count
            sample_families = @(
                $g.Group | Select-Object -ExpandProperty family_id -Unique | Sort-Object | Select-Object -First 8
            )
        }
    }
    $out | ConvertTo-Json -Depth 5 -Compress
    exit 0
}

foreach ($g in $grouped) {
    $samples = ($g.Group | Select-Object -ExpandProperty family_id -Unique | Sort-Object | Select-Object -First 5) -join ', '
    '{0,-20} rules={1,4}  e.g. {2}' -f $g.Name, $g.Count, $samples
}
