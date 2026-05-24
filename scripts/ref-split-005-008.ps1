# Mechanical partial splits for REF-005..008
$ErrorActionPreference = 'Stop'
$root = 'z:\Cursor Projects\AutoPBR'

function Get-Lines([string]$path) {
    [System.IO.File]::ReadAllLines($path)
}

function Write-Partial([string]$path, [string[]]$headerLines, [string[]]$bodyLines) {
    $all = $headerLines + $bodyLines + '}'
    [System.IO.File]::WriteAllLines($path, $all)
}

function Extract-Range([string[]]$lines, [int]$start1, [int]$end1) {
    $lines[($start1 - 1)..($end1 - 1)]
}

# --- REF-005 SetupAnimLift ---
$setupPath = Join-Path $root 'src\AutoPBR.Tools.AnimationCompiler\SetupAnimLift.cs'
$setupLines = Get-Lines $setupPath
$setupHeader = @(
    'using System.Text.Json.Nodes;',
    'using System.Text.RegularExpressions;',
    '',
    'namespace AutoPBR.Tools.AnimationCompiler;',
    '',
    'internal static partial class SetupAnimLift',
    '{'
)

$setupMainBody = @()
$setupMainBody += Extract-Range $setupLines 8 40   # regex + IsNonBlockingNote
$setupMainBody += Extract-Range $setupLines 41 46 # AbstractSetupAnimHoistTemplates
$setupMainBody += Extract-Range $setupLines 199 373 # TryLift + IsSupportedProperty

$setupEmitBody = Extract-Range $setupLines 48 197
$setupParseBody = Extract-Range $setupLines 375 817
$setupParseBody += Extract-Range $setupLines 2537 2553
$setupHoistBody = Extract-Range $setupLines 1299 1409
$setupExprBody = Extract-Range $setupLines 818 1293
$setupExprBody += Extract-Range $setupLines 1411 2536

Write-Partial (Join-Path $root 'src\AutoPBR.Tools.AnimationCompiler\SetupAnimLift.cs') $setupHeader $setupMainBody
Write-Partial (Join-Path $root 'src\AutoPBR.Tools.AnimationCompiler\SetupAnimLift.Emit.cs') $setupHeader $setupEmitBody
Write-Partial (Join-Path $root 'src\AutoPBR.Tools.AnimationCompiler\SetupAnimLift.Parse.cs') $setupHeader $setupParseBody
Write-Partial (Join-Path $root 'src\AutoPBR.Tools.AnimationCompiler\SetupAnimLift.Hoist.cs') $setupHeader $setupHoistBody
Write-Partial (Join-Path $root 'src\AutoPBR.Tools.AnimationCompiler\SetupAnimLift.Expressions.cs') $setupHeader $setupExprBody

Write-Host 'REF-005 done'

# --- REF-006 OpenGlPreviewBackend ---
$oglPath = Join-Path $root 'src\AutoPBR.App\Rendering\OpenGL\OpenGlPreviewBackend.cs'
$oglLines = Get-Lines $oglPath
$oglHeader = @(
    'using System.Buffers.Binary;',
    'using System.Diagnostics;',
    'using System.Numerics;',
    'using System.Runtime.InteropServices;',
    '',
    'using AutoPBR.App.Rendering.Abstractions;',
    'using AutoPBR.App.Rendering.Scene;',
    'using AutoPBR.Core.Models;',
    'using AutoPBR.Core.Preview;',
    '',
    'using Avalonia.OpenGL;',
    'using Avalonia.Platform;',
    '',
    'using Silk.NET.OpenGL;',
    '',
    'namespace AutoPBR.App.Rendering.OpenGL;',
    '',
    '/// <summary>OpenGL implementation of <see cref="IRenderPreviewBackend"/>; GPU entry points must run on the OpenGL thread (Avalonia <see cref="AutoPBR.App.Controls.GlPbrPreviewControl"/> callbacks).</summary>',
    'public sealed partial class OpenGlPreviewBackend',
    '{'
)

# Main: 1-476 (through ComposeOrbitEye), 477-1434 (GlInit/Deinit/Render), 2233-2495 (uniforms + Dispose)
$oglMainBody = Extract-Range $oglLines 22 1434
$oglMainBody += Extract-Range $oglLines 2233 2495

$oglLifecycleBody = Extract-Range $oglLines 1435 1605
$oglLifecycleBody += Extract-Range $oglLines 2284 2396

$oglSceneDrawBody = Extract-Range $oglLines 1606 1662
$oglSceneDrawBody += Extract-Range $oglLines 2125 2231

$oglLightingBody = Extract-Range $oglLines 1663 2124

# Shaders: shader compile block lives in GlInit; move TryInitLineOverlay's program to SceneDraw.
# Add Shaders partial with GlInit shader-only helpers extracted from main — use lines 502-539 as duplicate? 
# Plan wants Shaders.cs — move SetMatrixOnProgram family used by line/atmo (2245-2282) + TryInitLineOverlay shader compile is inside TryInitLineOverlay
$oglShadersBody = Extract-Range $oglLines 2245 2282

Write-Partial (Join-Path $root 'src\AutoPBR.App\Rendering\OpenGL\OpenGlPreviewBackend.cs') $oglHeader $oglMainBody
Write-Partial (Join-Path $root 'src\AutoPBR.App\Rendering\OpenGL\OpenGlPreviewBackend.Lifecycle.cs') $oglHeader $oglLifecycleBody
Write-Partial (Join-Path $root 'src\AutoPBR.App\Rendering\OpenGL\OpenGlPreviewBackend.SceneDraw.cs') $oglHeader $oglSceneDrawBody
Write-Partial (Join-Path $root 'src\AutoPBR.App\Rendering\OpenGL\OpenGlPreviewBackend.Lighting.cs') $oglHeader $oglLightingBody
Write-Partial (Join-Path $root 'src\AutoPBR.App\Rendering\OpenGL\OpenGlPreviewBackend.Shaders.cs') $oglHeader $oglShadersBody

Write-Host 'REF-006 done'

# --- REF-007 NormalHeightGenerator ---
$nhPath = Join-Path $root 'src\AutoPBR.Core\NormalHeightGenerator.cs'
$nhLines = Get-Lines $nhPath
$nhHeader = @(
    'using AutoPBR.Core.Atlas;',
    'using AutoPBR.Core.HeightFromNormals;',
    'using AutoPBR.Core.Models;',
    '',
    'using Microsoft.ML.OnnxRuntime;',
    '',
    'using SixLabors.ImageSharp;',
    'using SixLabors.ImageSharp.PixelFormats;',
    'using SixLabors.ImageSharp.Processing;',
    '',
    'namespace AutoPBR.Core;',
    '',
    '/// <summary>',
    '/// Generates normal maps and encodes height information into the alpha channel.',
    '/// </summary>',
    'internal static partial class NormalHeightGenerator',
    '{',
    '    private const int VcOrientationCount = 12;',
    '    private static readonly float[] VcCos = BuildVcCos();',
    '    private static readonly float[] VcSin = BuildVcSin();'
)

$nhMainBody = Extract-Range $nhLines 22 444
$nhMainBody += Extract-Range $nhLines 1492 1515

$nhClassicalBody = Extract-Range $nhLines 446 762
$nhClassicalBody += Extract-Range $nhLines 1032 1490

$nhDeepBumpBody = Extract-Range $nhLines 764 1030

Write-Partial (Join-Path $root 'src\AutoPBR.Core\NormalHeightGenerator.cs') $nhHeader $nhMainBody
Write-Partial (Join-Path $root 'src\AutoPBR.Core\NormalHeightGenerator.Classical.cs') $nhHeader $nhClassicalBody
Write-Partial (Join-Path $root 'src\AutoPBR.Core\NormalHeightGenerator.DeepBump.cs') $nhHeader $nhDeepBumpBody

Write-Host 'REF-007 done'

# --- REF-008 JavapFloatGeometryMeshLift ---
$javPath = Join-Path $root 'src\AutoPBR.Tools.GeometryCompiler\JavapFloatGeometryMeshLift.cs'
$javLines = Get-Lines $javPath
$javHeader = @(
    'using System.Globalization;',
    'using System.Text;',
    'using System.Text.Json.Nodes;',
    'using System.Text.RegularExpressions;',
    'using static AutoPBR.Tools.GeometryCompiler.GeometryLiftCoordinateRounding;',
    '',
    'namespace AutoPBR.Tools.GeometryCompiler;',
    '',
    '/// <summary>',
    '/// Lifts a flat part tree from Mojang 26.x-style <c>javap -c</c> for static mesh factories (<c>MeshDefinition</c> and',
    '/// <c>LayerDefinition</c> static methods once concatenated): <c>CubeListBuilder.texOffs</c>,',
    '/// <c>addBox(FFFFFF)</c> / extended <c>addBox(FFFFFFL…CubeDeformation;FF)</c> / <c>addBox(String,FFFFFF)</c>,',
    '/// texCrop-style <c>addBox(String,FFFIII…)</c>, direction-mask overload via <c>java/util/Set</c> (full box approximation),',
    '/// and <c>PartPose</c> before <c>PartDefinition.addOrReplaceChild</c>.',
    '/// </summary>',
    'internal static partial class JavapFloatGeometryMeshLift',
    '{'
)

$javMainBody = Extract-Range $javLines 18 160
$javBindingBody = Extract-Range $javLines 163 2373

Write-Partial $javPath $javHeader $javMainBody
Write-Partial (Join-Path $root 'src\AutoPBR.Tools.GeometryCompiler\JavapFloatGeometryMeshLift.BindingResolve.cs') $javHeader $javBindingBody

Write-Host 'REF-008 done'
