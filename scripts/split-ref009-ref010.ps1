$ErrorActionPreference = 'Stop'
$base = Join-Path $PSScriptRoot '..\tests\AutoPBR.Core.Tests' | Resolve-Path

# --- REF-009: MinecraftJavaModelPreviewTests ---
$java = Join-Path $base 'MinecraftJavaModelPreviewTests.cs'
$lines = Get-Content -LiteralPath $java

$blockHeader = @'
using System.IO.Compression;
using System.Text;

namespace AutoPBR.Core.Tests;

public partial class MinecraftJavaModelPreviewTests
{
'@

$cleanHeader = @'
using System.Numerics;

namespace AutoPBR.Core.Tests;

public partial class MinecraftJavaModelPreviewTests
{
    // Java elytra cuboid (10x20x2 + texOffs 22,0); the mirrored wing flips U start/end on the north face.
    private static readonly float[] ElytraLeftNorthUv = [24f, 2f, 34f, 22f];
    private static readonly float[] ElytraRightNorthUv = [34f, 2f, 24f, 22f];

'@

$blockBody = $lines[13..282] -join "`n"
$addEntry = $lines[4630..4635] -join "`n"
$dictSource = $lines[4850..4869] -join "`n"
$blockFile = $blockHeader + "`n" + $blockBody + "`n`n" + $addEntry + "`n`n" + $dictSource + "`n}"
Set-Content -LiteralPath (Join-Path $base 'MinecraftJavaModelPreviewTests.BlockstateBake.cs') -Value $blockFile -Encoding utf8

$cleanBody = $lines[284..4628] -join "`n"
$cleanHelpers = $lines[4637..4849] -join "`n"
$cleanFile = $cleanHeader + $cleanBody + "`n`n" + $cleanHelpers + "`n}"
Set-Content -LiteralPath (Join-Path $base 'MinecraftJavaModelPreviewTests.CleanRoomEntity.cs') -Value $cleanFile -Encoding utf8

Remove-Item -LiteralPath $java

# --- REF-010: EntityTextureParityCatalogTests ---
$parity = Join-Path $base 'EntityTextureParityCatalogTests.cs'
$plines = Get-Content -LiteralPath $parity

$partialHeader = @'
namespace AutoPBR.Core.Tests;

public sealed partial class EntityTextureParityCatalogTests
{
'@

function Write-Partial([string]$suffix, [int]$start, [int]$end) {
    $body = $plines[($start - 1)..($end - 1)] -join "`n"
    $content = $partialHeader + "`n" + $body + "`n}"
    Set-Content -LiteralPath (Join-Path $base "EntityTextureParityCatalogTests.$suffix.cs") -Value $content -Encoding utf8
}

Write-Partial 'Humanoids' 7 402
Write-Partial 'Equipment' 404 772
Write-Partial 'Monsters' 774 1326
Write-Partial 'Quadrupeds' 1328 1692
Write-Partial 'TextureVariants' 1694 3336

$fixturesBody = $plines[3337..3425] -join "`n"
$fixturesFile = $partialHeader + "`n" + $fixturesBody + "`n}"
Set-Content -LiteralPath (Join-Path $base 'EntityTextureParityCatalogTests.Fixtures.cs') -Value $fixturesFile -Encoding utf8

Remove-Item -LiteralPath $parity

Write-Host 'Split complete.'
