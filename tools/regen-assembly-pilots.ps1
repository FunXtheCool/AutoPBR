#requires -Version 7.0
<#
.SYNOPSIS
  Orchestrates 56-pilot assembly-parity refresh (lift, quality, reference export, summary).

.PARAMETER ClientJar
  Pinned 26.1.2 client.jar (default: tools/minecraft-parity/26.1.2/client.jar).

.PARAMETER PilotList
  Official JVM names, one per line (default: docs/generated/geometry-assembly-parity-pilots-26.1.2.txt).

.PARAMETER JavaHome
  JDK 25+ for reference export (class file 69). Defaults to %USERPROFILE%\.autopbr\jdk-25 when present.

.PARAMETER KeepRevert
  After lift, revert shards whose lift decision score did not improve vs pre-run backup.

.PARAMETER SkipLift, -SkipQuality, -SkipReference, -SkipIndex
  Skip individual pipeline steps.

.EXAMPLE
  pwsh -File tools/regen-assembly-pilots.ps1

.EXAMPLE
  pwsh -File tools/regen-assembly-pilots.ps1 -KeepRevert -SkipReference
#>
param(
    [string] $ClientJar = "",
    [string] $PilotList = "",
    [string] $VersionLabel = "26.1.2",
    [string] $JavaHome = "",
    [switch] $SkipLift,
    [switch] $SkipQuality,
    [switch] $SkipReference,
    [switch] $SkipIndex,
    [switch] $KeepRevert
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $here "..")).Path
$proj = Join-Path $repoRoot "src/AutoPBR.Tools.GeometryCompiler/AutoPBR.Tools.GeometryCompiler.csproj"
$outDir = Join-Path $repoRoot "docs/generated"
$geometryDir = Join-Path $outDir "geometry/$VersionLabel"
$tmpBuild = Join-Path $repoRoot ".tmpbuild"
$classesList = Join-Path $tmpBuild "assembly-pilots.classes.txt"
$decisionsCsv = Join-Path $tmpBuild "assembly-pilot-lift-decisions.csv"
$refLog = Join-Path $tmpBuild "reference-export-pilots.log"
$qualityPath = Join-Path $outDir "geometry-lift-quality-$VersionLabel.json"

if ([string]::IsNullOrWhiteSpace($ClientJar)) {
    $ClientJar = Join-Path $repoRoot "tools/minecraft-parity/26.1.2/client.jar"
}
if ([string]::IsNullOrWhiteSpace($PilotList)) {
    $PilotList = Join-Path $repoRoot "docs/generated/geometry-assembly-parity-pilots-$VersionLabel.txt"
}

$ClientJar = (Resolve-Path -LiteralPath $ClientJar).Path
$PilotList = (Resolve-Path -LiteralPath $PilotList).Path
New-Item -ItemType Directory -Force -Path $tmpBuild | Out-Null

$pilots = Get-Content -LiteralPath $PilotList |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -and -not $_.StartsWith("#") }

$classLines = $pilots | ForEach-Object { ($_ -replace '\.', '/') + ".class" }
Set-Content -LiteralPath $classesList -Value $classLines -Encoding utf8
Write-Host "Pilot JVMs: $($pilots.Count); class list: $classesList"

function Get-ShardPath([string] $jvm) {
    Join-Path $geometryDir "$jvm.json"
}

function Get-LiftScore([string] $jvm, [string] $shardPath, [string] $status = "ok") {
    $args = @(
        "run", "--project", $proj, "--no-build",
        "--", "score-lift-shard",
        "--jvm", $jvm,
        "--shard", $shardPath,
        "--status", $status,
        "--version-label", $VersionLabel
    )
    $line = & dotnet @args 2>&1 | Select-Object -Last 1
    if (-not $line) { return $null }
    $parts = $line -split "`t"
    [pscustomobject]@{
        Score = [int]$parts[0]
        AssemblyGatePass = [bool]::Parse($parts[1])
        ReferenceWorldPoseMatch = if ($parts[2] -eq "True") { $true } elseif ($parts[2] -eq "False") { $false } else { $null }
        JavapPoseOracleMatch = if ($parts[3] -eq "True") { $true } elseif ($parts[3] -eq "False") { $false } else { $null }
        ExtractionBindingGap = [bool]::Parse($parts[4])
        FlatNested = [int]$parts[5]
    }
}

$backupDir = Join-Path $tmpBuild "assembly-pilot-shard-backup"
if ($KeepRevert) {
    if (Test-Path -LiteralPath $backupDir) {
        Remove-Item -LiteralPath $backupDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    foreach ($jvm in $pilots) {
        $shard = Get-ShardPath $jvm
        if (Test-Path -LiteralPath $shard) {
            Copy-Item -LiteralPath $shard -Destination (Join-Path $backupDir "$jvm.json") -Force
        }
    }
    Write-Host "Backed up existing pilot shards to $backupDir"
}

if (-not $SkipLift) {
    Write-Host "=== Step 2: Re-lift $($pilots.Count) pilots ==="
    dotnet build $proj -v q | Out-Null
    $liftArgs = @(
        "run", "--project", $proj, "--no-build", "--",
        "--client-jar", $ClientJar,
        "--version-label", $VersionLabel,
        "--out-dir", $outDir,
        "--use-asm-lift",
        "--batch-list", $classesList,
        "--parallel", "--max-parallelism", "4", "--stats"
    )
    & dotnet @liftArgs
    if ($LASTEXITCODE -ne 0) { throw "GeometryCompiler batch lift failed with exit $LASTEXITCODE" }
}
elseif (-not $SkipIndex) {
    Write-Host "SkipLift set; index rows still updated only when lift runs."
}

if ($KeepRevert) {
    Write-Host "=== Step 3: Keep/revert by lift score (one dotnet process per score; omit -KeepRevert for speed) ==="
    dotnet build $proj -v q | Out-Null
    $rows = @("officialJvmName,oldScore,newScore,decision,assemblyGatePass,referenceWorldPoseMatch,javapPoseOracleMatch")
    foreach ($jvm in $pilots) {
        $shard = Get-ShardPath $jvm
        $backup = Join-Path $backupDir "$jvm.json"
        $oldScore = -1
        if (Test-Path -LiteralPath $backup) {
            $old = Get-LiftScore $jvm $backup
            if ($old) { $oldScore = $old.Score }
        }
        $new = if (Test-Path -LiteralPath $shard) { Get-LiftScore $jvm $shard } else { $null }
        $newScore = if ($new) { $new.Score } else { -1 }
        $decision = if ($null -eq $new -or $newScore -lt 0) {
            if (Test-Path -LiteralPath $backup) { "revert" } else { "skip" }
        }
        elseif (-not (Test-Path -LiteralPath $backup)) { "keep" }
        elseif ($oldScore -lt 0) { "keep" }
        elseif ($newScore -gt $oldScore) { "keep" }
        elseif ($newScore -eq $oldScore) { "keep" }
        else { "revert" }
        if ($decision -eq "revert" -and (Test-Path -LiteralPath $backup)) {
            Copy-Item -LiteralPath $backup -Destination $shard -Force
        }
        $ag = if ($new) { $new.AssemblyGatePass } else { $false }
        $wp = if ($new) { $new.ReferenceWorldPoseMatch } else { $null }
        $jo = if ($new) { $new.JavapPoseOracleMatch } else { $null }
        $rows += "$jvm,$oldScore,$newScore,$decision,$ag,$wp,$jo"
    }
    Set-Content -LiteralPath $decisionsCsv -Value $rows -Encoding utf8
    Write-Host "Wrote $decisionsCsv"
}

if (-not $SkipQuality) {
    Write-Host "=== Step 4: Regenerate quality report ==="
    $env:AUTOPBR_WRITE_GEOMETRY_LIFT_QUALITY = $qualityPath
    try {
        dotnet test (Join-Path $repoRoot "tests/AutoPBR.Core.Tests/AutoPBR.Core.Tests.csproj") `
            --filter "FullyQualifiedName~Write_quality_report_when_env_set" -v q
        if ($LASTEXITCODE -ne 0) { throw "Quality report test failed" }
    }
    finally {
        Remove-Item Env:AUTOPBR_WRITE_GEOMETRY_LIFT_QUALITY -ErrorAction SilentlyContinue
    }
}

if (-not $SkipReference) {
    Write-Host "=== Step 5: Reference export (world poses) ==="
    if ([string]::IsNullOrWhiteSpace($JavaHome)) {
        $defaultJdk = Join-Path $env:USERPROFILE ".autopbr/jdk-25"
        if (Test-Path -LiteralPath $defaultJdk) { $JavaHome = $defaultJdk }
    }
    $refArgs = @(
        "-File", (Join-Path $repoRoot "tools/Export-GeometryReference.ps1"),
        "-ModelsFromFile", $PilotList
    )
    if (-not [string]::IsNullOrWhiteSpace($JavaHome)) {
        $refArgs += @("-JavaHome", $JavaHome)
    }
    & pwsh @refArgs 2>&1 | Tee-Object -FilePath $refLog
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Reference export failed (JDK 25+ required). See $refLog"
    }
}

Write-Host "=== Step 6: Post-run summary ==="
if (-not (Test-Path -LiteralPath $qualityPath)) {
    Write-Warning "Quality JSON missing: $qualityPath"
    exit 0
}

$quality = Get-Content -LiteralPath $qualityPath -Raw | ConvertFrom-Json
$byJvm = @{}
foreach ($e in $quality.entries) { $byJvm[$e.officialJvmName] = $e }

$gatePass = 0
$worldPass = 0
$oraclePass = 0
$hierPass = 0
foreach ($p in $pilots) {
    $e = $byJvm[$p]
    if (-not $e) { continue }
    if ($e.assemblyGatePass) { $gatePass++ }
    if ($e.referenceWorldPoseMatch -eq $true) { $worldPass++ }
    if ($e.javapPoseOracleMatch -eq $true) { $oraclePass++ }
    if ($e.referenceHierarchyMatch) { $hierPass++ }
}

Write-Host "generatedUtc: $($quality.generatedUtc)"
Write-Host "Pilots: $($pilots.Count) | assemblyGatePass: $gatePass | referenceWorldPoseMatch: $worldPass | javapPoseOracleMatch: $oraclePass | referenceHierarchyMatch: $hierPass"

$canaries = @(
    "net.minecraft.client.model.monster.creeper.CreeperModel",
    "net.minecraft.client.model.animal.cow.CowModel",
    "net.minecraft.client.model.animal.pig.PigModel",
    "net.minecraft.client.model.animal.sheep.SheepModel",
    "net.minecraft.client.model.animal.wolf.WolfModel"
)
Write-Host "`nCanary pilots:"
foreach ($c in $canaries) {
    $e = $byJvm[$c]
    if (-not $e) {
        Write-Host "  $c : (missing)"
        continue
    }
    Write-Host ("  {0} : gate={1} world={2} oracle={3} hier={4}" -f $c, $e.assemblyGatePass, $e.referenceWorldPoseMatch, $e.javapPoseOracleMatch, $e.referenceHierarchyMatch)
}
