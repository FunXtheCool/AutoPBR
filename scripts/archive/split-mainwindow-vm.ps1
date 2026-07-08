# Mechanical split of MainWindowViewModel.cs into partial files (REF-001)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not (Test-Path (Join-Path $root 'src'))) { $root = Split-Path $PSScriptRoot -Parent }
$srcPath = Join-Path $root 'src/AutoPBR.App/ViewModels/MainWindowViewModel.cs'
$lines = [System.IO.File]::ReadAllLines($srcPath)
$vmDir = Split-Path $srcPath -Parent

function Get-Category([string[]]$block) {
    $text = ($block -join "`n")
    $first = ($block | Where-Object { $_.Trim().Length -gt 0 } | Select-Object -First 1)
    if ($null -eq $first) { return 'Core' }

    # Nested types stay with ScanConvert (progress reporters)
    if ($text -match 'private sealed class Coalescing') { return 'ScanConvert' }

    if ($text -match 'void IBackgroundTaskSink\.|BackgroundSinkBegin|BackgroundSinkReport|BackgroundSinkEnd|ResolveBackgroundTaskLabel') { return 'ScanConvert' }
    if ($text -match 'ScanArchiveAsync|ScanCurrentInputAsync|ScanBatchAsync|ExecuteBatchConvertAsync|ConvertAsync|WaitForPendingTagWork|OnConversionProgress|UpdateConversionTiming|BuildConversionOptions|BuildMergedSpecular|CoalescingConversion|CoalescingProgressReporter|CanScanArchive|CanScanCurrentInput|CanScanBatch|CanBatchConvert|CanSinglePackConvert|CanConvert|CanCancel|\bCancel\(\)') { return 'ScanConvert' }
    if ($text -match '_conversionStartUtc|_currentStage|_stageStartUtc') { return 'ScanConvert' }

    if ($text -match 'RegisterGlPreview|LogPreviewMeshProvenance|RefreshPreviewIfActive|ScheduleRefreshPreviewIfActive|RunDebouncedPreviewRefresh|UpdatePreviewAsync|Push3DPreviewStateToGpu|Apply3DPreviewIfNeeded|Push3DRenderSettingsOnly|BuildPreview3DRenderSettings|PushPreview3DCamera|EnsurePreview3DCameraPoseTimer|OnPreview3DCameraPoseTimerTick|TriggerPreviewRefreshForDebug|SetPreviewTextureAsync|SetPreviewFromSelectionAsync|IdlePreview3DMaterial') { return 'Preview' }
    if ($text -match '_previewCts|_previewRefreshDebounceCts|_glPreview|_preview3DCameraPoseTimer|_lastPreviewTextureMaps|_lastPreviewModelSubject|_lastPreviewMeshProvenance|_lastLoggedPreviewMeshProvenance|_lastPreview3DLoggedError') { return 'Preview' }
    if ($text -match 'partial void OnPreview|partial void OnPreview3D|IsPreview2D|IsPreview3D|Preview3DCameraResetKeyChoices|Preview3DEntityAlphaModeOptions') { return 'Preview' }
    if ($text -match '\[ObservableProperty\].*_preview(Archive|Texture|Image|Display|Brick|Fade)|_preview3D') { return 'Preview' }
    if ($text -match 'OnPreviewBrickProbeDebugChanged|OnPreviewDisplayModeChanged') { return 'Preview' }
    if ($text -match 'OnFastSpecularChanged|OnNormalIntensityChanged|OnHeightIntensityChanged' -and $text -match 'ScheduleRefreshPreviewIfActive') { return 'Preview' }
    if ($text -match 'OnBrickHeight|OnUseLegacyExtractorChanged|OnSmoothnessScaleChanged|OnMetallicBoostChanged|OnPorosityBiasChanged|OnPlantMaterialPorosityExtraChanged|OnMlSpecularHeuristicBlendChanged|OnSelectedMlSpecularBlend') { return 'Preview' }
    if ($text -match 'OnUseDeepBumpNormalsChanged|OnSelectedDeepBump|OnSelectedNormal|OnPreprocess|OnDeepBump|OnNormalHeightTransparent|OnSpecular|OnUseMlSpecular|OnPreferOnnx|OnMlSpecularModel|OnMlSpecularUseEdge|OnMlSpecularTransparent|OnSpecularDebug|OnGenerateAoChanged|OnAoRadius|OnAoStrength') { return 'Preview' }
    if ($text -match 'OnSelectedFoliageModeChanged') { return 'Preview' }

    if ($text -match 'NotifyTagRulesChanged|GetEffectiveTagRules|BuildExploreTagFilterOptions|BuildMaterialTagSemanticOptions|ImportCustomTagRulesFromJson|DebugSemanticMatch|ClearTagOverrides|CanClearTagOverrides|_semanticMatchDebugText|ShowSemanticDebugDialog|_materialTagSemanticMatcher|_dictionaryDefinitionProvider|_exploreTagFilterOptions') { return 'TagRules' }
    if ($text -match 'partial void OnUseSemanticMaterialTagsChanged|partial void OnMaterialTag|partial void OnDictionary') { return 'TagRules' }
    if ($text -match '\[ObservableProperty\].*_(useSemanticMaterialTags|materialTag|dictionary)') { return 'TagRules' }
    if ($text -match 'CustomTagRules|RulesetsViewModel Rulesets') { return 'TagRules' }

    if ($text -match 'GoBackExplore|GoToBreadcrumb|EnterFolder|ExploreExpandAll|ExploreCollapseAll|RebuildExploreBreadcrumb|PreloadExpandersForCurrentView|ApplyTextureTypeOverridesToExplore|ExpandAllInSubtree|HandleExploreNodePointerSelection|GetVisibleExploreNodesInDisplayOrder|ReplaceExploreSelection|ClearExploreSelection|CanGoBackExplore|ExploreViewItems|ExploreBreadcrumb|ScannedArchiveTopLevel|BuildExploreTagFilterOptions') { return 'Explore' }
    if ($text -match 'partial void OnExploreFilterChanged|partial void OnExploreTagFilterIdChanged|partial void OnScannedArchiveRootChanged|partial void OnFocusedArchiveNodeChanged') { return 'Explore' }
    if ($text -match '\[ObservableProperty\].*_(exploreFilter|exploreTagFilter|exploreTreeColumn|scannedArchiveRoot|focusedArchiveNode|selectedExploreNode)|IsBatchScanActive|HasScannedArchive|ShowExploreEmptyMessage|SelectedExploreNodes|_selectionAnchorExploreNode') { return 'Explore' }
    if ($text -match 'ClearScannedArchive|HaveScanForCurrentPack|IsPackPath|CanScanArchive') { return 'Explore' }

    if ($text -match 'SaveSettings|FlushPendingSettingsSaveAsync|ApplyCulture|ApplyColorScheme|RefreshMlSpecularBlendModeOptions|RefreshMlSpecularBlendMathOptions|RefreshColorSchemeOptions|RefreshDeepBumpOverlapOptions|RefreshDeepBumpInputModeOptions|RefreshFoliageModeOptions|RefreshNormalOperatorOptions|RefreshNormalKernelSizeOptions|RefreshNormalDerivativeOptions|RefreshUiScaleOptions|SyncSelectedUiScaleOptionToUiScale|SnapUiScaleToAllowedStep|RecomputeOutputZipPath|_settingsPersistence|_loadingSettings|UserSettingsSynchronizer') { return 'Settings' }
    if ($text -match 'partial void OnPackPathChanged|partial void OnBatchFolderPathChanged|partial void OnUseBatchFolderInputChanged|partial void OnOutputDirectoryChanged') { return 'Settings' }
    if ($text -match 'partial void OnColorSchemeChanged|partial void OnUiScaleChanged|partial void OnSelectedUiScaleOptionChanged|partial void OnSelectedColorSchemeOptionChanged|partial void OnSelectedLanguageChanged') { return 'Settings' }
    if ($text -match 'partial void OnProcessBlocksChanged|partial void OnProcessItemsChanged|partial void OnProcessArmorChanged|partial void OnProcessEntityChanged|partial void OnProcessParticlesChanged|partial void OnMaxThreadsChanged|partial void OnTempDirectoryChanged|partial void OnDebugModeChanged') { return 'Settings' }
    if ($text -match '\[ObservableProperty\].*_(packPath|outputDirectory|normalIntensity|heightIntensity|fastSpecular|foliageMode|colorScheme|uiScale|selectedLanguage|selectedFoliage|selectedDeepBump|selectedNormal|selectedColorScheme|selectedUiScale|windowBackground|cardBackground|maxThreads|tempDirectory|debugMode|processBlocks|batchFolder|useBatchFolder|outputZipPath)') { return 'Settings' }
    if ($text -match 'MlSpecularTransparentAlphaClampMaxSlider|NormalHeightTransparentAlphaClampMaxSlider|SupportedLanguages|MainWindowViewModel\(\)') { return 'Settings' }
    if ($text -match '_settings\b|FoliageModeOptions|DeepBumpOverlapOptions|ColorSchemeOptions|UiScaleOptions|UiScaleTransform|MinUiScale|MaxUiScale') { return 'Settings' }

    if ($text -match 'AppendUserLog|AddLogLine|SetStatus|UpdateStatusText|RunOnUiThread|SaveLogToFile|Dispose\(\)|LogLines|LogText|BackgroundTasks|Strings\b|conversionElapsed|conversionEta|isBusy|isConverting|statusText|progressValue') { return 'Core' }
    if ($text -match '\[ObservableProperty\].*_(isBusy|isConverting|statusText|progressValue|progressMax|conversionElapsed|conversionEta|conversionTotal|logText|exploreMlDebug|conversionScanDebug)') { return 'Core' }
    if ($text -match '_cts\b|_scanCts\b|_specularData|_statusKey|_statusFormatArgs|_lastLogWriteUtc|LogWriteInterval|MaxLogLines|ScanDebugUpdateInterval|ConversionUiProgressUpdateInterval|ScanUiProgressUpdateInterval|_lastScanDebugUpdateUtc') { return 'Core' }

    # Default conversion-related observable properties to Settings unless already tagged
    if ($text -match '\[ObservableProperty\]') { return 'Settings' }

    return 'Core'
}

# Header through class opening (lines 1-28)
$headerEnd = 0
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '^public partial class MainWindowViewModel') {
        $headerEnd = $i
        break
    }
}
$header = $lines[0..$headerEnd]

# Parse member blocks at indent level 4 (class members)
$blocks = [System.Collections.Generic.List[object]]::new()
$i = $headerEnd + 1
while ($i -lt $lines.Length - 1) {
    $line = $lines[$i]
    if ($line -match '^    (public |private |protected |internal |partial void |void I|public void |\[ObservableProperty\]|\[RelayCommand|\[UsedImplicitly\]|/// )') {
        $start = $i
        $i++
        while ($i -lt $lines.Length - 1) {
            $next = $lines[$i]
            if ($next -match '^    (public |private |protected |internal |partial void |void I|public void |\[ObservableProperty\]|\[RelayCommand|\[UsedImplicitly\]|/// )' -and $i -gt $start) {
                break
            }
            $i++
        }
        $blockLines = $lines[$start..($i - 1)]
        $cat = Get-Category $blockLines
        $blocks.Add([PSCustomObject]@{ Category = $cat; Lines = $blockLines })
    }
    else {
        $i++
    }
}

# Closing brace
$footer = @('}')

$partialHeader = @'
namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
'@

$categories = @{
    Core = [System.Collections.Generic.List[string]]::new()
    Settings = [System.Collections.Generic.List[string]]::new()
    Preview = [System.Collections.Generic.List[string]]::new()
    ScanConvert = [System.Collections.Generic.List[string]]::new()
    Explore = [System.Collections.Generic.List[string]]::new()
    TagRules = [System.Collections.Generic.List[string]]::new()
}

foreach ($b in $blocks) {
    foreach ($ln in $b.Lines) { [void]$categories[$b.Category].Add([string]$ln) }
    [void]$categories[$b.Category].Add('')
}

# Core gets usings + class; others get minimal usings
$coreUsings = $header[0..($header.Length - 2)]  # exclude class line from header - it's in partialHeader

function Write-Partial($name, $bodyLines, $extraUsings) {
    $path = Join-Path $vmDir "MainWindowViewModel.$name.cs"
    $content = New-Object System.Collections.Generic.List[string]
    if ($extraUsings.Count -gt 0) {
        foreach ($u in $extraUsings) { $content.Add($u) }
        $content.Add('')
    }
    foreach ($h in $partialHeader) { $content.Add($h) }
    foreach ($ln in $bodyLines) { $content.Add([string]$ln) }
    $content.Add('}')
    [System.IO.File]::WriteAllLines($path, $content)
    $lc = (Get-Content $path | Measure-Object -Line).Lines
    Write-Host "$name : $lc lines"
}

$settingsUsings = @(
    'using System.Globalization;',
    'using AutoPBR.App.Lang;',
    'using AutoPBR.App.Models;',
    'using AutoPBR.App.Services;',
    'using AutoPBR.App.ViewModels.Rulesets;',
    'using AutoPBR.Core;',
    'using AutoPBR.Core.Models;',
    'using Avalonia.Media;',
    'using Avalonia.Threading;',
    'using CommunityToolkit.Mvvm.ComponentModel;',
    'using JetBrains.Annotations;'
)
$previewUsings = @(
    'using AutoPBR.App.Controls;',
    'using AutoPBR.App.Rendering;',
    'using AutoPBR.App.Rendering.Abstractions;',
    'using AutoPBR.App.Services;',
    'using AutoPBR.Core;',
    'using AutoPBR.Core.Models;',
    'using Avalonia.Media.Imaging;',
    'using Avalonia.Threading;',
    'using CommunityToolkit.Mvvm.ComponentModel;',
    'using CommunityToolkit.Mvvm.Input;'
)
$scanUsings = @(
    'using AutoPBR.App.Lang;',
    'using AutoPBR.App.Models;',
    'using AutoPBR.App.Services;',
    'using AutoPBR.Core;',
    'using AutoPBR.Core.Models;',
    'using Avalonia.Threading;',
    'using CommunityToolkit.Mvvm.Input;'
)
$exploreUsings = @(
    'using System.Collections.ObjectModel;',
    'using AutoPBR.App.Models;',
    'using AutoPBR.App.Services;',
    'using AutoPBR.Core.Models;',
    'using Avalonia.Input;',
    'using Avalonia.Threading;',
    'using CommunityToolkit.Mvvm.ComponentModel;',
    'using CommunityToolkit.Mvvm.Input;'
)
$tagUsings = @(
    'using AutoPBR.App.Models;',
    'using AutoPBR.App.Services;',
    'using AutoPBR.App.ViewModels.Rulesets;',
    'using AutoPBR.Core.Embeddings;',
    'using AutoPBR.Core.Models;',
    'using CommunityToolkit.Mvvm.ComponentModel;',
    'using CommunityToolkit.Mvvm.Input;'
)

# Core file: full original usings + members
$corePath = $srcPath
$coreContent = New-Object System.Collections.Generic.List[string]
foreach ($u in $coreUsings) { $coreContent.Add($u) }
$coreContent.Add('public partial class MainWindowViewModel : ViewModelBase, IBackgroundTaskSink, IDisposable')
$coreContent.Add('{')
foreach ($ln in $categories['Core']) { $coreContent.Add([string]$ln) }
$coreContent.Add('}')
[System.IO.File]::WriteAllLines($corePath, $coreContent)
Write-Host "Core : $((Get-Content $corePath | Measure-Object -Line).Lines) lines"

Write-Partial 'Settings' $categories['Settings'] $settingsUsings
Write-Partial 'Preview' $categories['Preview'] $previewUsings
Write-Partial 'ScanConvert' $categories['ScanConvert'] $scanUsings
Write-Partial 'Explore' $categories['Explore'] $exploreUsings
Write-Partial 'TagRules' $categories['TagRules'] $tagUsings

# Summary counts per category (blocks)
$blocks | Group-Object Category | ForEach-Object { Write-Host "Blocks $($_.Name): $($_.Count)" }
