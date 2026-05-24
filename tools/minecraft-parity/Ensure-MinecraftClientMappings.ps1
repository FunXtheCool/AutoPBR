#requires -Version 5.1
<#
.SYNOPSIS
  Downloads Mojang client_mappings.txt (ProGuard) beside a tracked version.json (file is gitignored if under jar path, or tracked if you place under 1.21.11/).

.DESCRIPTION
  Reads version.json downloads.client_mappings.url when present and writes client_mappings.txt (1.21.11+)
  or client.txt (older layout) next to version.json.

.PARAMETER VersionDir
  Directory containing version.json (default: 1.21.11).
#>
param(
    [string] $VersionDir = (Join-Path $PSScriptRoot '1.21.11')
)

$ErrorActionPreference = 'Stop'
$versionJson = Join-Path $VersionDir 'version.json'
if (-not (Test-Path $versionJson)) {
    throw "Missing version manifest: $versionJson"
}

$meta = Get-Content $versionJson -Raw | ConvertFrom-Json
$m = $meta.downloads.client_mappings
if (-not $m -or [string]::IsNullOrWhiteSpace($m.url)) {
    Write-Host "No downloads.client_mappings in version.json (e.g. some 26.x releases). Use a named client.jar with tools/Generate-MinecraftClientModelIndex.ps1 and omit -Mappings."
    exit 0
}

$out = Join-Path $VersionDir 'client_mappings.txt'
if (Test-Path $out) {
    Write-Host "Already present: $out"
    exit 0
}

Write-Host "Downloading client mappings..."
Invoke-WebRequest -Uri $m.url -OutFile $out
Write-Host "Wrote $out ($((Get-Item $out).Length / 1MB | ForEach-Object { '{0:N1} MB' -f $_ }))"
