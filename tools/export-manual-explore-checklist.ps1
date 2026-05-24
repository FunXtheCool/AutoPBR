# Exports § A.4 manual Explore checklist rows (texture paths + JVM) from runtime-ir-preview-plan.md.
# Does NOT replace manual screenshot sign-off — automation aid for pending rows only.
param(
    [string]$PlanPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "docs/runtime-ir-preview-plan.md"),
    [string]$OutDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "docs/generated"),
    [ValidateSet("csv", "md", "both")]
    [string]$Format = "both",
    [string]$Batch = ""
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $PlanPath)) {
    throw "Plan not found: $PlanPath"
}

# 4C batch IDs for export sorting (matches § A.4 / A.3.1 promotion batches).
$batchByJvm = @{
    CreeperModel            = "4C-1"
    CowModel                = "4C-1"
    PigModel                = "4C-1"
    PandaModel              = "4C-1"
    PolarBearModel          = "4C-1"
    HoglinModel             = "4C-1"
    BabyHoglinModel         = "4C-1"
    AdultCamelModel         = "4C-1"
    BabyCamelModel          = "4C-1"
    CamelModel              = "4C-1"
    CamelSaddleModel        = "4C-1"
    AbstractEquineModel     = "4C-1"
    BabyHorseModel          = "4C-1"
    DonkeyModel             = "4C-1"
    EquineSaddleModel       = "4C-1"
    HorseModel              = "4C-1"
    WolfModel               = "4C-2"
    AdultWolfModel          = "4C-2"
    BabyWolfModel           = "4C-2"
    GoatModel               = "4C-2"
    BabyGoatModel           = "4C-2"
    FoxModel                = "4C-2"
    AdultFoxModel           = "4C-2"
    BabyFoxModel            = "4C-2"
    AdultArmadilloModel     = "4C-3"
    ArmadilloModel          = "4C-3"
    BabyArmadilloModel      = "4C-3"
    AdultTurtleModel        = "4C-3"
    BabyTurtleModel         = "4C-3"
    TurtleModel             = "4C-3"
    BabyCowModel            = "4C-3"
    BabyPandaModel          = "4C-3"
    BabyPolarBearModel      = "4C-3"
    BabySheepModel          = "4C-3"
    SheepFurModel           = "4C-3"
    AdultAxolotlModel       = "4C-4"
    BabyAxolotlModel        = "4C-4"
    SnifferModel            = "4C-4"
    SniffletModel           = "4C-4"
    AdultRabbitModel        = "4C-4"
    BabyRabbitModel         = "4C-4"
    RabbitModel             = "4C-4"
    LlamaModel              = "4C-4"
    BabyLlamaModel          = "4C-4"
    AdultOcelotModel        = "4C-4"
    BabyOcelotModel         = "4C-4"
    EnderDragonModel        = "4C-5"
    RavagerModel            = "4C-5"
    BabyDonkeyModel         = "4C-5"
    BabyFelineModel         = "4C-5"
    AdultCatModel           = "partial-5"
    BabyCatModel            = "partial-5"
    AdultFelineModel        = "partial-5"
    SheepModel              = "T1-probe"
    BreezeModel             = "detection"
}

function Get-FourCBatchId {
    param([string]$Jvm, [string]$ViewportT1)
    $key = ($Jvm -replace '^\*\*|\*\*$', '').Trim()
    if ($batchByJvm.ContainsKey($key)) {
        return $batchByJvm[$key]
    }
    if ($ViewportT1 -match 'batch\s*(\d)') {
        return "4C-$($Matches[1])"
    }
    if ($ViewportT1 -match 'partial') { return "partial" }
    if ($ViewportT1 -match 'probe') { return "T1-probe" }
    return ""
}

$lines = Get-Content $PlanPath
$inTable = $false
$rows = [System.Collections.Generic.List[object]]::new()

foreach ($line in $lines) {
    if ($line -match '^\|\s*JVM\s*\|') {
        $inTable = $true
        continue
    }
    if ($inTable -and $line -match '^\|\s*[-:]') {
        continue
    }
    # automated_prereq | Auto gates | Viewport T1 | Texture | Manual Explore
    if ($inTable -and $line -match '^\|\s*([^|]+)\s*\|\s*([^|]+)\s*\|\s*([^|]+)\s*\|\s*([^|]+)\s*\|\s*`([^`]+)`\s*\|\s*([^|]+)\s*\|') {
        $jvm = ($Matches[1] -replace '^\*\*|\*\*$', '').Trim()
        $autoPrereq = $Matches[2].Trim()
        $auto = $Matches[3].Trim()
        $t1 = $Matches[4].Trim()
        $texture = $Matches[5].Trim()
        $manual = $Matches[6].Trim()
        if ($manual -notmatch 'pending') {
            continue
        }
        $rows.Add([pscustomobject]@{
                Jvm              = $jvm
                AutomatedPrereq  = $autoPrereq
                FourCBatchId     = Get-FourCBatchId -Jvm $jvm -ViewportT1 $t1
                TexturePath      = $texture
                AutoGates        = $auto
                ViewportT1       = $t1
                ManualStatus     = $manual
            })
        continue
    }
    if ($inTable -and $line -notmatch '^\|') {
        break
    }
}

if ($rows.Count -eq 0) {
    Write-Warning "No pending § A.4 rows found in $PlanPath"
    exit 0
}

$exportRows = $rows
if (-not [string]::IsNullOrWhiteSpace($Batch)) {
    $batchFilter = $Batch.Trim()
    $exportRows = [System.Collections.Generic.List[object]]::new()
    foreach ($r in $rows) {
        if ($r.FourCBatchId -eq $batchFilter) {
            $exportRows.Add($r)
        }
    }
    if ($exportRows.Count -eq 0) {
        Write-Warning "No pending § A.4 rows for batch '$batchFilter' in $PlanPath"
        exit 0
    }
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$stamp = Get-Date -Format "yyyy-MM-dd"
$batchSuffix = if ([string]::IsNullOrWhiteSpace($Batch)) { "" } else { "-$($Batch.Trim())" }
$csvPath = Join-Path $OutDir "manual-explore-checklist-pending$batchSuffix-$stamp.csv"
$mdPath = Join-Path $OutDir "manual-explore-checklist-pending$batchSuffix-$stamp.md"

if ($Format -eq "csv" -or $Format -eq "both") {
    $exportRows | Sort-Object FourCBatchId, Jvm | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
    Write-Host "Wrote $($exportRows.Count) pending rows -> $csvPath"
}

if ($Format -eq "md" -or $Format -eq "both") {
    $sb = [System.Text.StringBuilder]::new()
    $batchNote = if ([string]::IsNullOrWhiteSpace($Batch)) { "all batches" } else { "batch **$($Batch.Trim())**" }
    [void]$sb.AppendLine("# Manual Explore checklist (pending rows)")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Exported from ``runtime-ir-preview-plan.md`` § A.4 on $stamp ($batchNote). Screenshot sign-off still required in the plan table.")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Playbook: ``docs/manual-explore-playbook.md``")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| JVM | automated_prereq | 4C batch | Texture | T1 | Manual |")
    [void]$sb.AppendLine("|-----|------------------|----------|---------|----|--------|")
    foreach ($r in ($exportRows | Sort-Object FourCBatchId, Jvm)) {
        [void]$sb.AppendLine("| $($r.Jvm) | $($r.AutomatedPrereq) | $($r.FourCBatchId) | ``$($r.TexturePath)`` | $($r.ViewportT1) | $($r.ManualStatus) |")
    }
    [System.IO.File]::WriteAllText($mdPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
    Write-Host "Wrote $($exportRows.Count) pending rows -> $mdPath"
}
