#requires -Version 5.1
<#
.SYNOPSIS
  Lifts setupAnim from *Model classes in client.jar into setup-anim IR JSON shards and setup-anim-index-<versionLabel>.json.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ClientJar,
    [string]$VersionLabel = "26.1.2",
    [string]$OutDir = "",
    [string]$Single = "",
    [string]$BatchList = "",
    [string]$Javap = "",
    [switch]$Parallel,
    [int]$MaxParallelism = 0,
    [switch]$Quiet,
    [switch]$Stats
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $repoRoot "docs\generated"
}

$proj = Join-Path $repoRoot "src\AutoPBR.Tools.AnimationCompiler\AutoPBR.Tools.AnimationCompiler.csproj"
$argsList = @(
    "run", "--project", $proj, "--",
    "--client-jar", (Resolve-Path $ClientJar).Path,
    "--version-label", $VersionLabel,
    "--out-dir", (Resolve-Path $OutDir).Path,
    "--lift-setup-anim"
)

if ($Javap) { $argsList += @("--javap", $Javap) }
if ($Single) { $argsList += @("--single", $Single) }
if ($BatchList) { $argsList += @("--batch-list", (Resolve-Path $BatchList).Path) }
if ($Parallel) { $argsList += "--parallel" }
if ($MaxParallelism -gt 0) { $argsList += @("--max-parallelism", "$MaxParallelism") }
if ($Quiet) { $argsList += "--quiet" }
if ($Stats) { $argsList += "--stats" }

Write-Host "dotnet $($argsList -join ' ')"
& dotnet @argsList
exit $LASTEXITCODE
