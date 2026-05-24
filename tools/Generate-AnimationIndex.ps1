#requires -Version 5.1
<#
.SYNOPSIS
  Runs AutoPBR.Tools.AnimationCompiler against a pinned client.jar to emit animation IR JSON shards and animation-index-<versionLabel>.json.

.DESCRIPTION
  Wraps `dotnet run` on `src/AutoPBR.Tools.AnimationCompiler`. Batch mode (default class list or `-BatchList`)
  runs `javap -c` per `*Animation` definition class, lifts `<clinit>` into `docs/generated/animation/<versionLabel>/<fqn>.json`,
  and rewrites `animation-index-<versionLabel>.json`.

.PARAMETER ClientJar
  Path to the official client.jar for the target game version.

.PARAMETER VersionLabel
  Output subdirectory label (e.g. 26.1.2). Must match paths under docs/generated/animation/<label>/.

.PARAMETER Mappings
  ProGuard client_mappings.txt when the JAR is obfuscated (required for 1.21.11).

.PARAMETER OutDir
  Root for animation shards and indexes (default: repo docs/generated).

.PARAMETER Single
  If set, only process this official JVM class name (e.g. net.minecraft.client.animation.definitions.ArmadilloAnimation).

.PARAMETER BatchList
  Path to a newline-separated list of `net/.../Foo.class` paths. Defaults to
  `minecraft_<versionLabel>_client_animation_definition_classes.txt` under src/AutoPBR.Core/Data/minecraft-native/ when omitted.

.PARAMETER Javap
  Optional path to javap.exe when it is not on PATH.

.PARAMETER Parallel
  Batch mode: use up to Min(8, processor count) parallel workers unless -MaxParallelism overrides.

.PARAMETER MaxParallelism
  Caps parallel batch workers.

.PARAMETER Quiet
  Suppress per-shard "Wrote ..." lines in batch mode.

.PARAMETER Stats
  Print javap/cache counters to stderr after batch.

.EXAMPLE
  pwsh -File tools/Generate-AnimationIndex.ps1 `
    -ClientJar tools/minecraft-parity/26.1.2/client.jar `
    -VersionLabel 26.1.2
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $ClientJar,

    [Parameter(Mandatory = $true)]
    [string] $VersionLabel,

    [string] $Mappings = "",

    [string] $OutDir = "",

    [string] $Single = "",

    [string] $BatchList = "",

    [string] $Javap = "",

    [switch] $Parallel = $false,

    [int] $MaxParallelism = 0,

    [switch] $Quiet = $false,

    [switch] $Stats = $false
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $here "..")
$proj = Join-Path $repoRoot "src/AutoPBR.Tools.AnimationCompiler/AutoPBR.Tools.AnimationCompiler.csproj"
if (-not (Test-Path -LiteralPath $proj)) {
    throw "Animation compiler project not found: $proj"
}

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $repoRoot "docs/generated"
}

$OutDir = [System.IO.Path]::GetFullPath($OutDir)

$clientResolved = Resolve-Path -LiteralPath $ClientJar
$argsList = @(
    "run", "--project", $proj, "--",
    "--client-jar", $clientResolved.Path,
    "--version-label", $VersionLabel,
    "--out-dir", $OutDir
)

if (-not [string]::IsNullOrWhiteSpace($Mappings)) {
    $mapsResolved = Resolve-Path -LiteralPath $Mappings
    $argsList += @("--mappings", $mapsResolved.Path)
}

if (-not [string]::IsNullOrWhiteSpace($Javap)) {
    $argsList += @("--javap", $Javap)
}

if ($Parallel) {
    if ($MaxParallelism -ge 1) {
        $argsList += @("--parallel", [string]$MaxParallelism)
    }
    else {
        $argsList += "--parallel"
    }
}
elseif ($MaxParallelism -gt 1) {
    $argsList += @("--max-parallelism", [string]$MaxParallelism)
}

if ($Quiet) {
    $argsList += "--quiet"
}

if ($Stats) {
    $argsList += "--stats"
}

if (-not [string]::IsNullOrWhiteSpace($Single)) {
    $argsList += @("--single", $Single.Trim())
}
else {
    if ([string]::IsNullOrWhiteSpace($BatchList)) {
        $defaultList = "minecraft_${VersionLabel}_client_animation_definition_classes.txt"
        $BatchList = Join-Path $repoRoot "src/AutoPBR.Core/Data/minecraft-native/$defaultList"
    }
    if (-not (Test-Path -LiteralPath $BatchList)) {
        throw "Batch class list not found: $BatchList (pass -BatchList or add minecraft_*_client_animation_definition_classes.txt)."
    }
    $bl = Resolve-Path -LiteralPath $BatchList
    $argsList += @("--batch-list", $bl.Path)
}

Write-Host ("dotnet " + ($argsList -join " "))
& dotnet @argsList
exit $LASTEXITCODE
