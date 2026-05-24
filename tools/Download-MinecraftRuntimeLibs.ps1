#requires -Version 5.1
<#
.SYNOPSIS
  Downloads runtime libraries from version.json (needed for Java reference bake).

.PARAMETER VersionRoot
  Directory containing version.json (default: tools/minecraft-parity/26.1.2).
#>
param(
    [string] $VersionRoot = ""
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($VersionRoot)) {
    $VersionRoot = Join-Path $here "minecraft-parity/26.1.2"
}

$VersionRoot = Resolve-Path -LiteralPath $VersionRoot
$versionJson = Join-Path $VersionRoot "version.json"
$libRoot = Join-Path $VersionRoot "libraries"
if (-not (Test-Path -LiteralPath $versionJson)) {
    throw "version.json not found: $versionJson"
}

New-Item -ItemType Directory -Force -Path $libRoot | Out-Null
$vj = Get-Content -LiteralPath $versionJson -Raw | ConvertFrom-Json
$new = 0
foreach ($lib in $vj.libraries) {
    if (-not $lib.downloads.artifact) {
        continue
    }

    $path = Join-Path $libRoot ($lib.downloads.artifact.path -replace '/', '\')
    if (Test-Path -LiteralPath $path) {
        continue
    }

    $dir = Split-Path -Parent $path
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Invoke-WebRequest -Uri $lib.downloads.artifact.url -OutFile $path -UseBasicParsing
    $new++
}

$total = @(Get-ChildItem -LiteralPath $libRoot -Recurse -Filter "*.jar").Count
Write-Host "Runtime libraries: $total jars ($new newly downloaded) under $libRoot"
