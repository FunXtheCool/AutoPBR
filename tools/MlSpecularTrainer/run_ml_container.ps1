# Prefer -InstallOrtTraining / -InstallTorchOrt: Docker CLI parses "-OrtTraining" as flag "-O"
# if you mistakenly pass it to `docker compose` instead of this script.
param(
  [Parameter()]
  [Alias('OrtTraining')]
  [switch]$InstallOrtTraining,
  [Parameter()]
  [Alias('TorchOrt')]
  [switch]$InstallTorchOrt,
  [Parameter()]
  [Alias('DeepSpeed')]
  [switch]$InstallDeepSpeed,
  [switch]$SkipBasePipInstall,
  [string]$OrtTrainingSpec = "onnxruntime-training==1.19.2+cu118",
  [string]$TorchOrtSpec = "torch-ort==1.19.2",
  [string]$DeepSpeedSpec = "deepspeed",
  [string]$PipExtraIndexUrl = "",
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$Command
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$composePath = Join-Path $scriptDir "docker/docker-compose.yml"

$env:INSTALL_BASE_DEPS = "1"
$env:SKIP_BASE_PIP_INSTALL = $(if ($SkipBasePipInstall) { "1" } else { "0" })
$env:INSTALL_ORT_TRAINING = $(if ($InstallOrtTraining) { "1" } else { "0" })
$env:INSTALL_TORCH_ORT = $(if ($InstallTorchOrt) { "1" } else { "0" })
$env:INSTALL_DEEPSPEED = $(if ($InstallDeepSpeed) { "1" } else { "0" })
$env:ORT_TRAINING_PIP_SPEC = $OrtTrainingSpec
$env:TORCH_ORT_PIP_SPEC = $TorchOrtSpec
$env:DEEPSPEED_PIP_SPEC = $DeepSpeedSpec
$env:PIP_EXTRA_INDEX_URL = $PipExtraIndexUrl
$env:ORT_TRAINING_FIND_LINKS = "https://download.onnxruntime.ai/onnxruntime_stable_cu118.html"

if (-not $Command -or $Command.Count -eq 0) {
  $Command = @("bash")
}

Write-Host "[ml-container] SKIP_BASE_PIP_INSTALL=$env:SKIP_BASE_PIP_INSTALL INSTALL_ORT_TRAINING=$env:INSTALL_ORT_TRAINING INSTALL_TORCH_ORT=$env:INSTALL_TORCH_ORT INSTALL_DEEPSPEED=$env:INSTALL_DEEPSPEED"
Write-Host "[ml-container] Running: docker compose -f `"$composePath`" run --rm ml-specular $($Command -join ' ')"

docker compose -f "$composePath" run --rm ml-specular @Command
