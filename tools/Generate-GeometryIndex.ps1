#requires -Version 5.1
<#
.SYNOPSIS
  Runs AutoPBR.Tools.GeometryCompiler against a pinned client.jar to merge bytecode metadata into committed geometry shards and optionally rebuild geometry-index-*.json.

.DESCRIPTION
  Wraps `dotnet run` on `src/AutoPBR.Tools.GeometryCompiler`. **Batch** mode (default class list or `-BatchList`)
  processes **every** model class: writes `docs/generated/geometry/<versionLabel>/<fqn>.json` (minimal placeholder
  shard when missing) and merges SHA-256, `javap -c` float probe, and **26.x-style structural mesh lift** when
  bytecode matches (see `docs/generated/geometry-ir-conventions.md`). Rewrites `geometry-index-<versionLabel>.json`
  with one row per class (`package-info` stubs are excluded from the default class list). Full batch is slow (many `javap` subprocesses per class).

  **Obfuscated jars (e.g. 1.21.11):** pass `-Mappings` to `client_mappings.txt`. Use `-Single` with the official FQN.
  Default **batch** class list is `minecraft_1.21.11_client_model_classes.txt` when `-VersionLabel` is `1.21.11`, else
  `minecraft_26.1.2_client_model_classes.txt` (override with `-BatchList`).

.PARAMETER ClientJar
  Path to the official client.jar for the target game version.

.PARAMETER Mappings
  Optional ProGuard mappings file (`client_mappings.txt` / `client.txt`) when the JAR is obfuscated.

.PARAMETER VersionLabel
  Output subdirectory label (e.g. 26.1.2, 1.21.11). Must match shard paths under docs/generated/geometry/<label>/.

.PARAMETER OutDir
  Root for geometry shards and indexes (default: repo docs/generated).

.PARAMETER Single
  If set, only process this official JVM class name (e.g. net.minecraft.client.model.animal.cow.CowModel).

.PARAMETER BatchList
  Path to a newline-separated list of `net/.../Foo.class` paths. Defaults to the repo's 26.1.2 model class list when omitted and `-Single` is not set.

.PARAMETER FactoryMethod
  Method name for javap float probe (default: createBodyLayer).

.PARAMETER Javap
  Optional path to javap.exe when it is not on PATH.

.PARAMETER Parallel
  Batch mode uses up to Min(8, processor count) parallel class workers unless -MaxParallelism overrides.
  Enabled by default for full batch runs (disable with -Parallel:$false).

.PARAMETER MaxParallelism
  Caps parallel batch workers. When greater than 1 without -Parallel, passes --max-parallelism only.

.PARAMETER NoParallel
  Forces sequential batch processing (slow; only for debugging).

.PARAMETER Quiet
  Suppresses per-shard progress lines in batch mode (errors still print).

.PARAMETER Stats
  After batch, prints javap/cache/timing counters to stderr.

.PARAMETER Phase4Only
  Batch only the six strict-ok Phase 4 models; merges into the existing geometry-index file (does not wipe other entries).

.EXAMPLE
  pwsh -File tools/Generate-GeometryIndex.ps1 `
    -ClientJar tools/minecraft-parity/26.1.2/client.jar `
    -VersionLabel 26.1.2 `
    -UseAsmLift `
    -Phase4Only `
    -Stats

.EXAMPLE
  pwsh -File tools/Generate-GeometryIndex.ps1 `
    -ClientJar tools/minecraft-parity/26.1.2/client.jar `
    -VersionLabel 26.1.2

.EXAMPLE
  pwsh -File tools/Generate-GeometryIndex.ps1 `
    -ClientJar tools/minecraft-parity/1.21.11/client.jar `
    -Mappings tools/minecraft-parity/1.21.11/client_mappings.txt `
    -VersionLabel 1.21.11 `
    -Single net.minecraft.client.model.animal.cow.CowModel

.EXAMPLE
  pwsh -File tools/Generate-GeometryIndex.ps1 `
    -ClientJar tools/minecraft-parity/1.21.11/client.jar `
    -Mappings tools/minecraft-parity/1.21.11/client_mappings.txt `
    -VersionLabel 1.21.11
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $ClientJar,

    [string] $Mappings = "",

    [Parameter(Mandatory = $true)]
    [string] $VersionLabel,

    [string] $OutDir = "",

    [string] $Single = "",

    [string] $BatchList = "",

    [string] $FactoryMethod = "createBodyLayer",

    [string] $Javap = "",

    [switch] $Parallel,

    [switch] $NoParallel = $false,

    [int] $MaxParallelism = 0,

    [switch] $Quiet = $false,

    [switch] $Stats = $false,

    [switch] $UseAsmLift = $(if ($VersionLabel -eq '26.1.2') { $true } else { $false }),

    [switch] $Phase4Only = $false
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $here "..")
$proj = Join-Path $repoRoot "src/AutoPBR.Tools.GeometryCompiler/AutoPBR.Tools.GeometryCompiler.csproj"
if (-not (Test-Path -LiteralPath $proj)) {
    throw "Geometry compiler project not found: $proj"
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
    "--out-dir", $OutDir,
    "--factory-method", $FactoryMethod
)

if (-not [string]::IsNullOrWhiteSpace($Mappings)) {
    $m = Resolve-Path -LiteralPath $Mappings
    $argsList += @("--mappings", $m.Path)
}

if (-not [string]::IsNullOrWhiteSpace($Javap)) {
    $argsList += @("--javap", $Javap)
}

$batchMode = [string]::IsNullOrWhiteSpace($Single)
$useParallel = $false
if ($batchMode -and -not $NoParallel) {
    if ($Parallel -or $MaxParallelism -gt 1) {
        $useParallel = $true
    }
    elseif (-not $PSBoundParameters.ContainsKey('Parallel')) {
        $useParallel = $true
    }
}

if ($useParallel) {
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

if ($UseAsmLift) {
    $argsList += "--use-asm-lift"
}

if (-not [string]::IsNullOrWhiteSpace($Single)) {
    $argsList += @("--single", $Single.Trim())
}
else {
    if ($Phase4Only) {
        $BatchList = Join-Path $repoRoot "src/AutoPBR.Core/Data/minecraft-native/phase4_strict_ok_model_classes.txt"
    }
    elseif ([string]::IsNullOrWhiteSpace($BatchList)) {
        $defaultList = switch -Regex ($VersionLabel) {
            '^1\.21\.11$' { 'minecraft_1.21.11_client_model_classes.txt' }
            default { 'minecraft_26.1.2_client_model_classes.txt' }
        }
        $BatchList = Join-Path $repoRoot "src/AutoPBR.Core/Data/minecraft-native/$defaultList"
    }
    if (-not (Test-Path -LiteralPath $BatchList)) {
        throw "Batch class list not found: $BatchList (pass -BatchList or generate via python tools/build_minecraft_client_model_class_index.py)."
    }
    $bl = Resolve-Path -LiteralPath $BatchList
    $argsList += @("--batch-list", $bl.Path)
}

Write-Host ("dotnet " + ($argsList -join " ")) -ForegroundColor Cyan
if ($batchMode) {
    $classCount = 0
    if (-not [string]::IsNullOrWhiteSpace($BatchList) -and (Test-Path -LiteralPath $BatchList)) {
        $classCount = @(Get-Content -LiteralPath $BatchList | Where-Object {
                $t = $_.Trim()
                $t.Length -gt 0 -and $t.EndsWith('.class')
            }).Count
    }
    $parHint = if ($useParallel) { "parallel workers enabled" } else { "sequential (-NoParallel)" }
    Write-Host "Geometry index batch: ~$classCount model classes, $parHint. Progress lines go to stderr every class." -ForegroundColor Yellow
    Write-Host "Do not pipe this command (e.g. Select-Object -Last N); piping buffers all output until the process exits." -ForegroundColor Yellow
}

$env:DOTNET_CLI_UI_LANGUAGE = "en"
$previousDotNetWatch = $env:DOTNET_WATCH
$env:DOTNET_WATCH = "0"
try {
    & dotnet @argsList
    exit $LASTEXITCODE
}
finally {
    if ($null -ne $previousDotNetWatch) {
        $env:DOTNET_WATCH = $previousDotNetWatch
    }
    else {
        Remove-Item Env:DOTNET_WATCH -ErrorAction SilentlyContinue
    }
}
