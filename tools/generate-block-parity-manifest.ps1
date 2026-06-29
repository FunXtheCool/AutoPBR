# Generates minecraft_26.1.2_block_textures.json and minecraft_26.1.2_block_texture_model_manifest.json
# from InventivetalentDev/minecraft-assets branch 26.1.2 (read-only GitHub tree API).
# Usage: pwsh -File tools/generate-block-parity-manifest.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$py = Join-Path $repoRoot 'tools/generate-block-parity-manifest.py'
if (-not (Test-Path $py)) { throw "Missing $py" }
python $py
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
