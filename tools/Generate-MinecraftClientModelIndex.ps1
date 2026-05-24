#requires -Version 5.1
<#
.SYNOPSIS
  Builds an index of Minecraft client model + animation classes from client.jar, using ProGuard mappings or named-bytecode JAR layout.

.DESCRIPTION
  Indexes classes under `net.minecraft.client.model.**` and `net.minecraft.client.animation.**`.

  For **`net.minecraft.client.animation.definitions.*Animation`** (mob clip holders), `javap -public` only lists
  `AnimationDefinition` field declarations — keyframes live in `<clinit>`. The script also runs **`javap -c`** for those
  classes and writes **`minecraft-client-model-index-<ver>-animation-init/*.javapc.txt`**; each JSON row may include
  **`javapBytecodeCRelPath`** pointing at the companion disassembly (relative to `-OutDir`).

  **Obfuscated releases (e.g. 1.21.11):** requires ProGuard `client_mappings.txt` / `client.txt`. Resolves each
  class to a JAR entry (root `*.class` repackaging, or `net/...` paths), then `javap -public`.

  **Named-bytecode releases (e.g. 26.1.2):** Mojang may omit `downloads.client_mappings`; the client JAR then
  ships readable `net/minecraft/client/model/...` paths. Omit `-Mappings` (or leave it empty); the script
  auto-detects this layout and enumerates the JAR directly.

  Committed outputs: docs/generated/minecraft-client-model-index-<label>.md|.json
  Optional local decompilation: .tmpbuild/decompiled-<label>/ (gitignored) via -DecompileOutDir + -VineflowerJar.

.PARAMETER ClientJar
  Path to the official client.jar for the target game version.

.PARAMETER Mappings
  Path to ProGuard mappings when the JAR is obfuscated. **Optional** when the JAR already contains named
  `net/minecraft/client/model/**` classes (26.1.2+); in that case the script ignores this parameter.

.PARAMETER VersionLabel
  Short label for output filenames (e.g. 1.21.11, 26.1.2).

.PARAMETER OutDir
  Directory for generated Markdown + JSON (default: repo docs/generated).

.PARAMETER ManifestJson
  Optional path to minecraft_*_entity_texture_model_manifest.json for path_prefix annotations.

.PARAMETER SkipJavap
  If set, only list classes + mapping columns (no javap; no JDK required).

.PARAMETER DecompileOutDir
  If set, run Vineflower once on the full client.jar into this directory (must also pass -VineflowerJar).

.PARAMETER VineflowerJar
  Path to vineflower standalone .jar (used when -DecompileOutDir is set).

.EXAMPLE
  pwsh -File tools/Generate-MinecraftClientModelIndex.ps1 `
    -ClientJar tools/minecraft-parity/1.21.11/client.jar `
    -Mappings tools/minecraft-parity/1.21.11/client_mappings.txt `
    -VersionLabel 1.21.11 `
    -ManifestJson src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_texture_model_manifest.json

  pwsh -File tools/Generate-MinecraftClientModelIndex.ps1 `
    -ClientJar tools/minecraft-parity/26.1.2/client.jar `
    -VersionLabel 26.1.2 `
    -ManifestJson src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_texture_model_manifest.json
#>
param(
    [Parameter(Mandatory = $true)][string] $ClientJar,
    [string] $Mappings = '',
    [Parameter(Mandatory = $true)][string] $VersionLabel,
    [string] $OutDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'docs\generated'),
    [string] $ManifestJson = '',
    [switch] $SkipJavap,
    [string] $DecompileOutDir = '',
    [string] $VineflowerJar = ''
)

$ErrorActionPreference = 'Stop'

# --- Official-name prefix policy (filters ProGuard class entries; echoed into generated Markdown) ---
$OfficialNamedPrefixes = @(
    'net.minecraft.client.model.',
    'net.minecraft.client.animation.'
)

$JarSlashPrefixesForNamedLayout = @(
    'net/minecraft/client/model/',
    'net/minecraft/client/animation/'
)

function Test-JarUsesNamedBytecodeLayout([System.IO.Compression.ZipArchive] $zip) {
    return $null -ne $zip.GetEntry('net/minecraft/client/model/EntityModel.class')
}

function Get-OuterClassFqn([string] $namedClass) {
    $d = $namedClass.IndexOf('$')
    if ($d -lt 0) { return $namedClass }
    return $namedClass.Substring(0, $d)
}

function Resolve-ObfuscatedClassName([string] $namedClass, [string] $obfRhs) {
    if ($obfRhs -match '\.') { return $obfRhs }
    $outer = Get-OuterClassFqn $namedClass
    $idx = $outer.LastIndexOf('.')
    if ($idx -le 0) { return $obfRhs }
    $pkg = $outer.Substring(0, $idx)
    return "$pkg.$obfRhs"
}

function Parse-MojangMappings([string] $path) {
    $namedToObf = @{}
    $obfToNamed = @{}
    $reader = [System.IO.StreamReader]::new($path)
    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            if ($line.Length -eq 0 -or $line[0] -eq '#' -or $line[0] -eq ' ') { continue }
            $arrow = ' -> '
            $ai = $line.IndexOf($arrow)
            if ($ai -lt 0) { continue }
            $rhs = $line.Substring($ai + $arrow.Length).TrimEnd()
            if (-not $rhs.EndsWith(':')) { continue }
            $named = $line.Substring(0, $ai).Trim()
            $obfRhs = $rhs.Substring(0, $rhs.Length - 1).Trim()
            if ($named -match '^\d+:\d+:') { continue }
            $obfFqn = Resolve-ObfuscatedClassName $named $obfRhs
            $namedToObf[$named] = $obfFqn
            if (-not $obfToNamed.ContainsKey($obfFqn)) {
                $obfToNamed[$obfFqn] = $named
            }
        }
    }
    finally { $reader.Dispose() }
    return [PSCustomObject]@{ NamedToObf = $namedToObf; ObfToNamed = $obfToNamed }
}

function Find-Javap {
    $candidates = @()
    if ($env:JAVA_HOME) {
        $candidates += (Join-Path $env:JAVA_HOME 'bin\javap.exe')
        $candidates += (Join-Path $env:JAVA_HOME 'bin/javap')
    }
    $candidates += 'javap'
    foreach ($c in $candidates) {
        if ($c -eq 'javap') {
            $cmd = Get-Command javap -ErrorAction SilentlyContinue
            if ($cmd) { return $cmd.Source }
        }
        elseif (Test-Path $c) { return $c }
    }
    return $null
}

function Get-ObfuscatedSimpleBinaryName([string] $obfFqn) {
    $last = $obfFqn.LastIndexOf('.')
    if ($last -lt 0) { return $obfFqn }
    return $obfFqn.Substring($last + 1)
}

function Resolve-JarEntryForObfuscatedClass([System.IO.Compression.ZipArchive] $zip, [string] $obfFqn) {
    $simple = Get-ObfuscatedSimpleBinaryName $obfFqn
    $candidates = @(
        "$simple.class",
        (($obfFqn -replace '\.', '/') + '.class')
    )
    foreach ($rel in $candidates) {
        $ent = $zip.GetEntry($rel)
        if ($ent) { return $rel }
    }
    return $null
}

function Test-IsAnimationDefinitionsBytecodeClass([string] $officialJvmName) {
    if ([string]::IsNullOrWhiteSpace($officialJvmName)) { return $false }
    if ($officialJvmName.IndexOf('$') -ge 0) { return $false }
    if (-not $officialJvmName.StartsWith('net.minecraft.client.animation.definitions.', [StringComparison]::Ordinal)) {
        return $false
    }
    if (-not $officialJvmName.EndsWith('Animation', [StringComparison]::Ordinal)) { return $false }
    return $true
}

function Invoke-JavapBytecodeC([string] $javapExe, [string] $jar, [string] $javapClassArg) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $all = & $javapExe @(
            '-encoding', 'UTF8',
            '-c',
            '-classpath', $jar,
            $javapClassArg
        ) 2>&1 | ForEach-Object { $_.ToString() }
        $code = $LASTEXITCODE
        $text = if ($all) { ($all -join [Environment]::NewLine).TrimEnd() } else { '' }
        if ($code -ne 0) {
            return "/* javap exit ${code}: $text */"
        }
        return $text
    }
    finally {
        $ErrorActionPreference = $prev
    }
}

function Invoke-JavapPublic([string] $javapExe, [string] $jar, [string] $javapClassArg) {
    # Use call operator so -classpath survives spaces in $jar (Start-Process -ArgumentList can break on Windows).
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $all = & $javapExe @(
            '-encoding', 'UTF8',
            '-public',
            '-classpath', $jar,
            $javapClassArg
        ) 2>&1 | ForEach-Object { $_.ToString() }
        $code = $LASTEXITCODE
        $text = if ($all) { ($all -join [Environment]::NewLine).TrimEnd() } else { '' }
        if ($code -ne 0) {
            return "/* javap exit ${code}: $text */"
        }
        return $text
    }
    finally {
        $ErrorActionPreference = $prev
    }
}

if (-not (Test-Path $ClientJar)) { throw "Missing client.jar: $ClientJar" }

$javapExe = if (-not $SkipJavap) { Find-Javap } else { $null }
if (-not $SkipJavap -and -not $javapExe) {
    Write-Warning 'javap not found (set JAVA_HOME or PATH). Continuing with -SkipJavap behavior for signatures.'
    $SkipJavap = $true
}

$manifestByOfficial = @{}
if ($ManifestJson -and (Test-Path $ManifestJson)) {
    $doc = Get-Content $ManifestJson -Raw | ConvertFrom-Json
    foreach ($r in $doc.rules) {
        foreach ($prop in @('deobf_model_class', 'deobf_model_class_pre_restructure')) {
            $cn = $r.$prop
            if ([string]::IsNullOrWhiteSpace($cn)) { continue }
            if (-not $manifestByOfficial.ContainsKey($cn)) {
                $manifestByOfficial[$cn] = New-Object System.Collections.Generic.List[string]
            }
            $manifestByOfficial[$cn].Add([string]$r.path_prefix)
        }
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($ClientJar)
$namedByteCodeLayout = Test-JarUsesNamedBytecodeLayout $zip
$maps = $null
if (-not $namedByteCodeLayout) {
    if ([string]::IsNullOrWhiteSpace($Mappings) -or -not (Test-Path $Mappings)) {
        $zip.Dispose()
        throw "Obfuscated client.jar requires -Mappings (ProGuard client_mappings.txt / client.txt). Named-layout jars (e.g. 26.1.2) omit -Mappings."
    }
    $maps = Parse-MojangMappings $Mappings
}
elseif (-not [string]::IsNullOrWhiteSpace($Mappings) -and (Test-Path $Mappings)) {
    Write-Warning "Named-bytecode client.jar detected; ignoring -Mappings ($Mappings)."
}

$rows = New-Object System.Collections.Generic.List[hashtable]
try {
    if ($namedByteCodeLayout) {
        foreach ($e in $zip.Entries) {
            $name = $e.FullName.Replace('\', '/')
            if (-not $name.EndsWith('.class')) { continue }
            $inScope = $false
            foreach ($pfx in $JarSlashPrefixesForNamedLayout) {
                if ($name.StartsWith($pfx, [StringComparison]::Ordinal)) { $inScope = $true; break }
            }
            if (-not $inScope) { continue }

            $jvm = ($name.Substring(0, $name.Length - 6) -replace '/', '.')
            $javapText = ''
            if (-not $SkipJavap) {
                $javapText = Invoke-JavapPublic $javapExe $ClientJar $jvm
            }

            $mp = @()
            if ($manifestByOfficial.ContainsKey($jvm)) {
                $mp = @($manifestByOfficial[$jvm] | Select-Object -Unique)
            }

            $outerOfficial = Get-OuterClassFqn $jvm
            $parts = $outerOfficial.Split('.')
            $groupKey = if ($parts.Length -ge 5) { ($parts[0..4] -join '.') } else { $outerOfficial }

            $rows.Add(@{
                jarPath            = $name
                obfuscatedJvmName  = $jvm
                officialJvmName    = $jvm
                groupKey           = $groupKey
                manifestPrefixes   = $mp
                javapPublic        = $javapText
            })
        }
    }
    else {
        foreach ($named in $maps.NamedToObf.Keys) {
            $matchedPrefix = $false
            foreach ($pfx in $OfficialNamedPrefixes) {
                if ($named.StartsWith($pfx, [StringComparison]::Ordinal)) { $matchedPrefix = $true; break }
            }
            if (-not $matchedPrefix) { continue }

            $obfFqn = $maps.NamedToObf[$named]
            $jarRel = Resolve-JarEntryForObfuscatedClass $zip $obfFqn
            if (-not $jarRel) {
                Write-Warning "No JAR entry for mapped class: $named -> $obfFqn"
                continue
            }

            $javapArg = Get-ObfuscatedSimpleBinaryName $obfFqn
            $javapText = ''
            if (-not $SkipJavap) {
                $javapText = Invoke-JavapPublic $javapExe $ClientJar $javapArg
            }

            $mp = @()
            if ($manifestByOfficial.ContainsKey($named)) {
                $mp = @($manifestByOfficial[$named] | Select-Object -Unique)
            }

            $outerOfficial = Get-OuterClassFqn $named
            $parts = $outerOfficial.Split('.')
            $groupKey = if ($parts.Length -ge 5) { ($parts[0..4] -join '.') } else { $outerOfficial }

            $rows.Add(@{
                jarPath            = $jarRel
                obfuscatedJvmName  = $obfFqn
                officialJvmName    = $named
                groupKey           = $groupKey
                manifestPrefixes   = $mp
                javapPublic        = $javapText
            })
        }
    }
}
finally { $zip.Dispose() }

$mappingKind = if ($namedByteCodeLayout) { 'named_jar' } else { 'proguard' }

$animBytecodeRelBase = "minecraft-client-model-index-$VersionLabel-animation-init"
$animBytecodeOutDir = Join-Path $OutDir $animBytecodeRelBase
$animationBytecodeSidecarCount = 0
if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
}
if (-not $SkipJavap -and $javapExe) {
    if (Test-Path $animBytecodeOutDir) {
        Remove-Item $animBytecodeOutDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $animBytecodeOutDir | Out-Null
    foreach ($r in $rows) {
        if (-not (Test-IsAnimationDefinitionsBytecodeClass $r.officialJvmName)) { continue }
        $javapArg = if ($namedByteCodeLayout) { $r.officialJvmName } else { Get-ObfuscatedSimpleBinaryName $r.obfuscatedJvmName }
        $bytecodeText = Invoke-JavapBytecodeC $javapExe $ClientJar $javapArg
        if ([string]::IsNullOrWhiteSpace($bytecodeText) -or $bytecodeText.StartsWith('/* javap exit')) {
            Write-Warning "javap -c failed for animation definition class: $($r.officialJvmName)"
            continue
        }
        $safe = ($r.officialJvmName -replace '\.', '_') + '.javapc.txt'
        $dest = Join-Path $animBytecodeOutDir $safe
        [System.IO.File]::WriteAllText($dest, $bytecodeText, [System.Text.UTF8Encoding]::new($false))
        $r['javapBytecodeCRelPath'] = "$animBytecodeRelBase/$safe"
        $animationBytecodeSidecarCount++
    }
}

if ($DecompileOutDir) {
    if (-not $VineflowerJar -or -not (Test-Path $VineflowerJar)) {
        throw 'When -DecompileOutDir is set, -VineflowerJar must point to an existing vineflower.jar'
    }
    New-Item -ItemType Directory -Force -Path $DecompileOutDir | Out-Null
    $javaExe = if ($env:JAVA_HOME -and (Test-Path (Join-Path $env:JAVA_HOME 'bin\java.exe'))) {
        Join-Path $env:JAVA_HOME 'bin\java.exe'
    }
    else { 'java' }
    Write-Host "Running Vineflower on full client.jar -> $DecompileOutDir (local only; may take several minutes)..."
    & $javaExe '-jar' $VineflowerJar $ClientJar $DecompileOutDir
    if ($LASTEXITCODE -ne 0) {
        throw "Vineflower exited with code $LASTEXITCODE"
    }
}

$sorted = @($rows) | Sort-Object { $_.groupKey }, { $_.officialJvmName }, { $_.obfuscatedJvmName }

$mdPath = Join-Path $OutDir "minecraft-client-model-index-$VersionLabel.md"
$jsonPath = Join-Path $OutDir "minecraft-client-model-index-$VersionLabel.json"

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# Minecraft client model + animation class index ($VersionLabel)")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Auto-generated by `tools/Generate-MinecraftClientModelIndex.ps1`. Do not hand-edit; regenerate after version bumps.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## Official class name prefix policy')
[void]$sb.AppendLine('')
foreach ($jp in $OfficialNamedPrefixes) {
    [void]$sb.AppendLine("- ``$jp`` (subtree)")
}
[void]$sb.AppendLine('')
if ($namedByteCodeLayout) {
    [void]$sb.AppendLine('**Bytecode layout:** named (`net/minecraft/client/model/...` entries in the JAR). Mojang may omit `downloads.client_mappings` in `version.json` for this release line; no ProGuard file is used.')
}
else {
    [void]$sb.AppendLine('**Bytecode layout:** obfuscated. JAR entries are resolved from the ProGuard mapping (root `*.class` for repackaged clients, or `net/...` paths).')
}
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## Inputs')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("| Artifact | Path |")
[void]$sb.AppendLine("|----------|------|")
[void]$sb.AppendLine("| client.jar | ``$ClientJar`` |")
if ($namedByteCodeLayout) {
    [void]$sb.AppendLine('| ProGuard mappings | *(not used — named bytecode)* |')
}
else {
    [void]$sb.AppendLine("| mappings | ``$Mappings`` |")
}
if ($ManifestJson) {
    [void]$sb.AppendLine("| manifest (optional) | ``$ManifestJson`` |")
}
[void]$sb.AppendLine('')
[void]$sb.AppendLine("## Summary")
[void]$sb.AppendLine('')
[void]$sb.AppendLine("- Total classes listed: **$($sorted.Count)**")
[void]$sb.AppendLine("- javap public API: **$(if ($SkipJavap) { 'skipped' } else { 'included' })**")
[void]$sb.AppendLine("- Animation definition `javap -c` sidecars: **$(if ($SkipJavap) { 'skipped' } else { $animationBytecodeSidecarCount })** under ``$animBytecodeRelBase/``")
[void]$sb.AppendLine("- Mapping kind: **$mappingKind**")
[void]$sb.AppendLine('')
if ($namedByteCodeLayout) {
    [void]$sb.AppendLine('Official and obfuscated JVM name columns match: the shipped client already uses Mojang package names in archive paths.')
}
else {
    [void]$sb.AppendLine('Each row is one class under the prefix policy from ProGuard mappings; JAR paths are best-effort.')
}
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Full `javap -public` text for each class lives in the companion JSON (`javapPublic`); the Markdown tables stay small for review.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Mob **`AnimationDefinition`** keyframes are authored in static initializers; see **`javapBytecodeCRelPath`** in JSON (and the `*-animation-init/` folder) for **`javap -c`** disassembly of `net.minecraft.client.animation.definitions.*Animation` classes.')
[void]$sb.AppendLine('')

$grouped = $sorted | Group-Object groupKey | Sort-Object Name
foreach ($g in $grouped) {
    [void]$sb.AppendLine("<details>")
    [void]$sb.AppendLine("<summary><strong>$($g.Name)</strong> ($($g.Count) classes)</summary>")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('| Official JVM name | Obfuscated JVM name | JAR entry | Manifest path_prefix (sample) |')
    [void]$sb.AppendLine('| --- | --- | --- | --- |')
    foreach ($r in ($g.Group | Sort-Object officialJvmName, obfuscatedJvmName)) {
        $o = if ($r.officialJvmName) { $r.officialJvmName } else { '*(unmapped)*' }
        $mpCol = if ($r.manifestPrefixes -and $r.manifestPrefixes.Count -gt 0) {
            $sample = ($r.manifestPrefixes | Select-Object -First 3) -join '<br/>'
            if ($r.manifestPrefixes.Count -gt 3) { $sample += '<br/>…' }
            $sample
        } else { '' }
        [void]$sb.AppendLine("| ``$o`` | ``$($r.obfuscatedJvmName)`` | ``$($r.jarPath)`` | $mpCol |")
    }
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('</details>')
    [void]$sb.AppendLine('')
}

[System.IO.File]::WriteAllText($mdPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))

$jsonObj = @{
    versionLabel                  = $VersionLabel
    officialNamedPrefixes         = $OfficialNamedPrefixes
    mappingKind                   = $mappingKind
    namedByteCodeLayout           = [bool]$namedByteCodeLayout
    clientJar                     = $ClientJar
    mappings                      = $(if ($namedByteCodeLayout) { $null } else { $Mappings })
    manifestJson                  = $ManifestJson
    skipJavap                     = [bool]$SkipJavap
    animationBytecodeInitRelDir   = $(if ($SkipJavap) { $null } else { $animBytecodeRelBase })
    animationBytecodeSidecarCount = $animationBytecodeSidecarCount
    generatedUtc                  = [DateTime]::UtcNow.ToString('o')
    classes          = @(
        foreach ($r in $sorted) {
            $cls = @{
                jarPath           = $r.jarPath
                obfuscatedJvmName = $r.obfuscatedJvmName
                officialJvmName   = $r.officialJvmName
                groupKey          = $r.groupKey
                manifestPrefixes  = @($r.manifestPrefixes)
                javapPublic       = $r.javapPublic
            }
            if ($r.ContainsKey('javapBytecodeCRelPath') -and $r.javapBytecodeCRelPath) {
                $cls.javapBytecodeCRelPath = $r.javapBytecodeCRelPath
            }
            $cls
        }
    )
}
$jsonObj | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding utf8

Write-Host "Wrote $mdPath"
Write-Host "Wrote $jsonPath"
