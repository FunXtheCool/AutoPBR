#requires -Version 5.1
<#
.SYNOPSIS
  Downloads Mojang client.jar beside a tracked version.json (jar is gitignored).

.DESCRIPTION
  Place client.jar once under tools/minecraft-parity/<version>/ and reuse for javap batches.
  Default folder matches AutoPBR parity pins (see docs/vanilla-preview-parity.md).

.PARAMETER VersionDir
  Directory containing version.json with downloads.client.url (default: 1.21.11 next to this script).
#>
param(
    [string] $VersionDir = (Join-Path $PSScriptRoot '1.21.11')
)

$ErrorActionPreference = 'Stop'
$versionJson = Join-Path $VersionDir 'version.json'
if (-not (Test-Path $versionJson)) {
    throw "Missing version manifest: $versionJson"
}

$out = Join-Path $VersionDir 'client.jar'
if (Test-Path $out) {
    Write-Host "Already present: $out"
    exit 0
}

$meta = Get-Content $versionJson -Raw | ConvertFrom-Json
$url = $meta.downloads.client.url
if ([string]::IsNullOrWhiteSpace($url)) {
    throw "version.json has no downloads.client.url"
}

Write-Host "Downloading client.jar..."
Invoke-WebRequest -Uri $url -OutFile $out
Write-Host "Wrote $out ($((Get-Item $out).Length / 1MB | ForEach-Object { '{0:N1} MB' -f $_ }))"
