using System.Collections.ObjectModel;
using System.Globalization;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JetBrains.Annotations;

using AutoPBR.App.Lang;
using AutoPBR.App.Models;
using AutoPBR.App.Services;
using AutoPBR.App.ViewModels.Rulesets;
using AutoPBR.Core;
using AutoPBR.Core.Embeddings;
using AutoPBR.Core.Models;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IBackgroundTaskSink, IDisposable
{
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _previewRefreshDebounceCts;
    private readonly SettingsPersistenceCoordinator _settingsPersistence;
    private readonly ExploreTreeController _exploreController = new();
    private MaterialTagSemanticMatcher? _materialTagSemanticMatcher;
    private SpecularData? _specularData;
    private readonly UserSettings _settings;

    /// <summary>During construction, true while <see cref="UserSettingsSynchronizer.LoadInto"/> runs so handlers skip <see cref="SaveSettings"/>.</summary>
    private readonly bool _loadingSettings;
    private DateTime _lastLogWriteUtc = DateTime.MinValue;
    private const int LogWriteIntervalMs = 250;
    private const int MaxLogLines = 600;
    private static readonly TimeSpan ScanDebugUpdateInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ConversionUiProgressUpdateInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ScanUiProgressUpdateInterval = TimeSpan.FromMilliseconds(50);
    private DateTime _lastScanDebugUpdateUtc = DateTime.MinValue;

    /// <summary>Append a line to the log (e.g. from view code-behind).</summary>
    public void AppendUserLog(string line) => AddLogLine(line);

    private void AddLogLine(string line)
    {
        void Core()
        {
            LogLines.Add(line);
            var trimmed = false;
            while (LogLines.Count > MaxLogLines)
            {
                LogLines.RemoveAt(0);
                trimmed = true;
            }

            if (trimmed)
            {
                LogText = string.Join(Environment.NewLine, LogLines);
                return;
            }

            LogText = string.IsNullOrEmpty(LogText) ? line : $"{LogText}{Environment.NewLine}{line}";
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    private static string ResolveBackgroundTaskLabel(string taskId) => taskId switch
    {
        BackgroundTaskIds.MaterialTags => LocalizedStrings.BackgroundTaskMaterialTags,
        BackgroundTaskIds.ExploreCache => LocalizedStrings.BackgroundTaskExploreCache,
        _ => taskId
    };

    void IBackgroundTaskSink.BeginTask(string taskId) => BackgroundSinkBegin(taskId);

    void IBackgroundTaskSink.ReportTask(string taskId, double? fraction) => BackgroundSinkReport(taskId, fraction);

    void IBackgroundTaskSink.EndTask(string taskId) => BackgroundSinkEnd(taskId);

    private void BackgroundSinkBegin(string taskId)
    {
        void Core()
        {
            var existing = BackgroundTasks.FirstOrDefault(x => x.Id == taskId);
            if (existing is not null)
            {
                existing.Label = ResolveBackgroundTaskLabel(taskId);
                existing.IsIndeterminate = true;
                return;
            }

            BackgroundTasks.Add(new BackgroundTaskItem(taskId, ResolveBackgroundTaskLabel(taskId)));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    private void BackgroundSinkReport(string taskId, double? fraction)
    {
        void Core()
        {
            var item = BackgroundTasks.FirstOrDefault(x => x.Id == taskId);
            if (item is null)
            {
                return;
            }

            if (fraction is { } f)
            {
                item.IsIndeterminate = false;
                item.Progress = Math.Clamp(f, 0, 1);
            }
            else
            {
                item.IsIndeterminate = true;
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    private void BackgroundSinkEnd(string taskId)
    {
        void Core()
        {
            var item = BackgroundTasks.FirstOrDefault(x => x.Id == taskId);
            if (item is null)
            {
                return;
            }

            BackgroundTasks.Remove(item);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    private DateTime _conversionStartUtc;
    private ConversionStage? _currentStage;
    private DateTime _stageStartUtc;
    private string? _statusKey;
    private object[]? _statusFormatArgs;

    [ObservableProperty] private string? _packPath;
    [ObservableProperty] private string? _outputDirectory;

    [ObservableProperty] private double _normalIntensity = AutoPbrDefaults.DefaultNormalIntensity;
    [ObservableProperty] private double _heightIntensity = AutoPbrDefaults.DefaultHeightIntensity;
    [ObservableProperty] private bool _brickHeightMapPostProcessEnabled = AutoPbrDefaults.DefaultBrickHeightMapPostProcessEnabled;
    [ObservableProperty] private double _brickHeightMinStructuralConfidence = AutoPbrDefaults.DefaultBrickHeightMinStructuralConfidence;
    [ObservableProperty] private double _brickHeightInvertDeltaThreshold = AutoPbrDefaults.DefaultBrickHeightInvertDeltaThreshold;
    [ObservableProperty] private double _brickLightGroutDiffuseDeltaMin = AutoPbrDefaults.DefaultBrickLightGroutDiffuseDeltaMin;
    /// <summary>When true, preview refresh captures brick probe metrics into <see cref="PreviewBrickProbeDebugText"/>.</summary>
    [ObservableProperty] private bool _previewBrickProbeDebug = AutoPbrDefaults.DefaultBrickProbePreviewDebug;
    [ObservableProperty] private string? _previewBrickProbeDebugText;
    [ObservableProperty] private bool _fastSpecular;
    [ObservableProperty] private string _foliageMode = "No Height";
    [ObservableProperty] private bool _useLegacyExtractor;
    [ObservableProperty] private double _smoothnessScale = AutoPbrDefaults.DefaultSmoothnessScale;
    [ObservableProperty] private double _metallicBoost = AutoPbrDefaults.DefaultMetallicBoost;
    [ObservableProperty] private double _porosityBias = AutoPbrDefaults.DefaultPorosityBias;

    /// <summary>Extra B-channel bias for plant-tagged textures (added to <see cref="PorosityBias"/>).</summary>
    [ObservableProperty] private double _plantMaterialPorosityExtra = AutoPbrDefaults.DefaultPlantMaterialPorosityExtra;

    /// <summary>0 = heuristic specular when ML ran; 1 = full ML contribution from the heuristic/AI blend slider.</summary>
    [ObservableProperty] private double _mlSpecularHeuristicBlend = AutoPbrDefaults.DefaultMlSpecularHeuristicBlend;

    /// <summary>Enum name (SmoothnessOnly, AiMetalAndEmissive, or Full); kept in sync with <see cref="SelectedMlSpecularBlendModeOption"/>.</summary>
    [ObservableProperty] private string _mlSpecularHeuristicBlendMode =
        nameof(AutoPBR.Core.Models.MlSpecularHeuristicBlendMode.SmoothnessOnly);

    /// <summary>AI Tuning channel-mode dropdown selection.</summary>
    [ObservableProperty] private FoliageModeOption? _selectedMlSpecularBlendModeOption;

    /// <summary>Blend math enum name (Linear, Additive, Multiplicative); kept in sync with <see cref="SelectedMlSpecularBlendMathOption"/>.</summary>
    [ObservableProperty] private string _mlSpecularBlendMath =
        nameof(AutoPBR.Core.Models.MlSpecularBlendMath.Linear);

    /// <summary>AI Tuning blend-math dropdown selection.</summary>
    [ObservableProperty] private FoliageModeOption? _selectedMlSpecularBlendMathOption;

    [ObservableProperty] private int _maxThreads; // 0 = auto
    [ObservableProperty] private int _maxThreadsMax = Math.Max(1, Environment.ProcessorCount);
    [ObservableProperty] private string? _tempDirectory;
    [ObservableProperty] private bool _debugMode;
    [ObservableProperty] private bool _processBlocks = true;
    [ObservableProperty] private bool _processItems = true;
    [ObservableProperty] private bool _processArmor = true;
    [ObservableProperty] private bool _processEntity = true;
    [ObservableProperty] private bool _processParticles = true;
    [ObservableProperty] private bool _useDeepBumpNormals;
    [ObservableProperty] private string _deepBumpOverlap = "Large";
    [ObservableProperty] private string _deepBumpInputMode = nameof(AutoPBR.Core.Models.DeepBumpInputMode.Auto);
    [ObservableProperty] private bool _deepBumpForceBlue255;
    [ObservableProperty] private double _deepBumpNormalIntensity = AutoPbrDefaults.DefaultNormalIntensity;
    [ObservableProperty] private double _deepBumpNormalSoftClamp;
    [ObservableProperty] private bool _deepBumpEdgeGuidedEnhance;
    [ObservableProperty] private double _deepBumpEdgeGuidedStrength = 1.0;
    [ObservableProperty] private double _deepBumpEdgeGuidedGamma = 1.0;
    [ObservableProperty] private double _deepBumpEdgeGuidedDirectionMix = 0.35;
    [ObservableProperty] private int _normalHeightTransparentAlphaClampMax;
    [ObservableProperty] private string _normalOperator = nameof(AutoPBR.Core.Models.NormalOperator.SobelVc);
    [ObservableProperty] private string _normalKernelSize = "3";
    [ObservableProperty] private string _normalDerivative = nameof(AutoPBR.Core.Models.NormalDerivative.Luminance);

    [ObservableProperty] private bool _preprocessLinearize;
    [ObservableProperty] private int _preprocessDenoiseRadius;
    [ObservableProperty] private double _preprocessDenoiseBlend = 0.5;
    [ObservableProperty] private bool _preprocessFrequencySplit;
    [ObservableProperty] private int _preprocessFrequencyRadius = 2;
    [ObservableProperty] private double _preprocessFrequencyDetailStrength = 1.0;

    [ObservableProperty] private bool _specularUsePercentileRemap = true;
    [ObservableProperty] private double _specularRemapLowPercentile = 0.02;
    [ObservableProperty] private double _specularRemapHighPercentile = 0.98;
    [ObservableProperty] private bool _useMlSpecularPredictor;
    [ObservableProperty] private string? _mlSpecularModelPath;
    [ObservableProperty] private string? _mlSpecularModelPath16;
    [ObservableProperty] private string? _mlSpecularModelPath32;
    [ObservableProperty] private string? _mlSpecularModelPath64;
    [ObservableProperty] private string? _mlSpecularModelPath128;
    [ObservableProperty] private string? _mlSpecularModelPath256;
    [ObservableProperty] private bool _mlSpecularUseEdgeChannel = true;
    [ObservableProperty] private int _mlSpecularTransparentAlphaClampMax;
    [ObservableProperty] private bool _specularDebugDisableHeuristicSpecular;
    [ObservableProperty] private bool _specularDebugSkipSpecularRemap;
    [ObservableProperty] private bool _specularDebugVerboseSpecularMl;

    /// <summary>When true, ONNX Runtime uses TensorRT EP for GPU models (CUDA fallback). Default false = CUDA only.</summary>
    [ObservableProperty] private bool _preferOnnxTensorRtExecutionProvider;

    /// <summary>When true, Explore suggests extra material tags via MiniLM (requires Data/ONNX-AI/all-MiniLM-L6-v2-onnx/model.onnx).</summary>
    [ObservableProperty] private bool _useSemanticMaterialTags = true;

    [ObservableProperty] private double _materialTagMinSimilarity = 0.25;
    [ObservableProperty] private double _materialTagCertaintyThreshold = 0.35;
    [ObservableProperty] private int _materialTagMaxCount = 3;
    [ObservableProperty] private bool _dictionaryEvidenceEnabled;
    [ObservableProperty] private double _dictionaryEvidenceWeight = 0.35;
    [ObservableProperty] private double _dictionaryMinEvidenceScore = 0.18;
    [ObservableProperty] private int _dictionaryRequestTimeoutMs = 900;
    private readonly IDictionaryDefinitionProvider _dictionaryDefinitionProvider = new FreeDictionaryDefinitionProvider();

    public RulesetsViewModel Rulesets { get; }

    /// <summary>Compatibility proxy for settings sync and existing bindings.</summary>
    public ObservableCollection<CustomTagRuleEntry> CustomTagRules => Rulesets.CustomTagRules;

    public int MlSpecularTransparentAlphaClampMaxSlider
    {
        get => Math.Clamp(MlSpecularTransparentAlphaClampMax, 0, 16);
        set => MlSpecularTransparentAlphaClampMax = Math.Clamp(value, 0, 255);
    }

    public int NormalHeightTransparentAlphaClampMaxSlider
    {
        get => Math.Clamp(NormalHeightTransparentAlphaClampMax, 0, 16);
        set => NormalHeightTransparentAlphaClampMax = Math.Clamp(value, 0, 255);
    }

    [ObservableProperty] private bool _generateAo;
    [ObservableProperty] private int _aoRadius = 4;
    [ObservableProperty] private double _aoStrength = 1.0;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isConverting;
    [ObservableProperty] private string _statusText = "";

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private double _progressMax = 1;
    [ObservableProperty] private string _conversionElapsedText = "";
    [ObservableProperty] private string _conversionEtaText = "";
    [ObservableProperty] private string _conversionTotalText = "";

    [ObservableProperty] private string? _outputZipPath;

    /// <summary>Folder containing multiple .zip/.jar packs for batch scan / batch convert.</summary>
    [ObservableProperty] private string? _batchFolderPath;

    /// <summary>When true, the input bar targets a batch folder and the main Convert runs batch conversion.</summary>
    [ObservableProperty] private bool _useBatchFolderInput;

    /// <summary>True when the explorer is showing a merged batch index (folder of packs).</summary>
    public bool IsBatchScanActive => HasScannedArchive && _exploreController.Data?.IsBatch == true;

    /// <summary>Search filter for the Resource Explorer tree (Explore tab). Filters nodes by path/name.</summary>
    [ObservableProperty] private string _exploreFilter = "";

    /// <summary>When set, Explore tree shows only files that have this tag (and their ancestor folders). Empty = no tag filter.</summary>
    [ObservableProperty] private string _exploreTagFilterId = "";

    /// <summary>Explore tree column widths (shared by header and rows via binding). Drag splitters in the header to resize.</summary>
    [ObservableProperty] private GridLength _exploreTreeColumnResourceWidth = new(1, GridUnitType.Star);

    [ObservableProperty] private GridLength _exploreTreeColumnMaterialsWidth = new(140, GridUnitType.Pixel);
    [ObservableProperty] private GridLength _exploreTreeColumnFlagsWidth = new(140, GridUnitType.Pixel);

    [ObservableProperty] private string _colorScheme = "Dark";
    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private FoliageModeOption? _selectedFoliageMode;
    [ObservableProperty] private FoliageModeOption? _selectedDeepBumpOverlap;
    [ObservableProperty] private FoliageModeOption? _selectedDeepBumpInputMode;
    [ObservableProperty] private FoliageModeOption? _selectedNormalOperator;
    [ObservableProperty] private FoliageModeOption? _selectedNormalKernelSize;
    [ObservableProperty] private FoliageModeOption? _selectedNormalDerivative;
    [ObservableProperty] private FoliageModeOption? _selectedColorSchemeOption;

    /// <summary>Minimum UI scale (75%).</summary>
    public const double MinUiScale = 0.75;

    /// <summary>Maximum UI scale (100%).</summary>
    public const double MaxUiScale = 1.0;

    /// <summary>Dropdown entries: 75%–100% in 5% steps.</summary>
    public ObservableCollection<FoliageModeOption> UiScaleOptions { get; } = new();

    [ObservableProperty] private FoliageModeOption? _selectedUiScaleOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UiScaleTransform))]
    private double _uiScale = 1.0;

    /// <summary>Uniform scale for the main window chrome (fonts, spacing, controls) for accessibility.</summary>
    public ScaleTransform UiScaleTransform => new(UiScale, UiScale);

    [ObservableProperty] private IBrush _windowBackground = Brushes.Transparent;
    [ObservableProperty] private IBrush _cardBackground = Brushes.Transparent;
    [ObservableProperty] private IBrush _cardBorderBrush = Brushes.Gray;
    [ObservableProperty] private IBrush _accentBrush = Brushes.DeepSkyBlue;
    [ObservableProperty] private IBrush _foregroundBrush = Brushes.White;

    [ObservableProperty] private string? _previewArchivePath;
    [ObservableProperty] private string? _previewTextureName;
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private string _exploreMlDebugText = "";
    [ObservableProperty] private string _conversionScanDebugText = "";

    /// <summary>Color used for preview panel top/bottom gradient fade (matches CardBackground).</summary>
    [ObservableProperty] private Color _previewFadeColor = Color.FromRgb(0x22, 0x22, 0x2A);

    public ObservableCollection<string> LogLines { get; } = new();

    /// <summary>Mini progress rows (tab strip, right); updated from background threads via <see cref="IBackgroundTaskSink"/>.</summary>
    public ObservableCollection<BackgroundTaskItem> BackgroundTasks { get; } = new();

    /// <summary>Persist the current in-memory log to a file (delegates to <see cref="LogService"/>).</summary>
    private void SaveLogToFile() => LogService.SaveToFile(LogLines);

    /// <summary>Localized strings for bindings; replaced when language changes.</summary>
    public LocalizedStrings Strings { get; private set; }

    /// <summary>Foliage options for the dropdown (display name from Strings, value for settings/converter).</summary>
    public ObservableCollection<FoliageModeOption> FoliageModeOptions { get; } = new();

    /// <summary>DeepBump tile overlap options (Small, Medium, Large). Matches DeepBump --color_to_normals-overlap.</summary>
    public ObservableCollection<FoliageModeOption> DeepBumpOverlapOptions { get; } = new();

    /// <summary>DeepBump input mode options (Auto, Grayscale, RGB).</summary>
    public ObservableCollection<FoliageModeOption> DeepBumpInputModeOptions { get; } = new();

    /// <summary>Normal operator options (Sobel + VC, Scharr + VC).</summary>
    public ObservableCollection<FoliageModeOption> NormalOperatorOptions { get; } = new();

    /// <summary>Normal kernel size options (3x3, 5x5, 7x7 for Sobel; 3x3,5x5 for Scharr).</summary>
    public ObservableCollection<FoliageModeOption> NormalKernelSizeOptions { get; } = new();

    /// <summary>Derivative source options: Color, Luminance, Color+Luminance Blend, Color+Luminance Max.</summary>
    public ObservableCollection<FoliageModeOption> NormalDerivativeOptions { get; } = new();

    /// <summary>Color scheme options for the Appearance dropdown (display name from Resources, value for settings).</summary>
    public ObservableCollection<FoliageModeOption> ColorSchemeOptions { get; } = new();

    /// <summary>Specular ML channel mode options (which channels keep heuristic contribution).</summary>
    public ObservableCollection<FoliageModeOption> MlSpecularBlendModeOptions { get; } = new();
    /// <summary>Specular ML blend math options (how heuristic and ML are combined).</summary>
    public ObservableCollection<FoliageModeOption> MlSpecularBlendMathOptions { get; } = new();

    /// <summary>Root of the scanned archive tree for the Explore tab. Null until user clicks Scan or when cleared.</summary>
    [ObservableProperty] private ArchiveNode? _scannedArchiveRoot;

    public bool HasScannedArchive => ScannedArchiveRoot != null;
    public bool ShowExploreEmptyMessage => !HasScannedArchive;

    private static readonly ObservableCollection<ArchiveNode> EmptyArchiveNodes = new();

    public ObservableCollection<ArchiveNode> ScannedArchiveTopLevel =>
        ScannedArchiveRoot?.Children ?? EmptyArchiveNodes;

    /// <summary>Folder we're currently viewing in Explore; null = root. After scan, defaults to "assets" if present.</summary>
    [ObservableProperty] private ArchiveNode? _focusedArchiveNode;

    /// <summary>Items to show in Explore tree: children of focused folder, or root's children when no focus.</summary>
    public ObservableCollection<ArchiveNode> ExploreViewItems => FocusedArchiveNode?.Children ?? ScannedArchiveTopLevel;

    private IReadOnlyList<ExploreTagFilterOption>? _exploreTagFilterOptions;

    /// <summary>Options for "Show tag" dropdown in Explore: All plus each effective tag rule (cached so the ComboBox does not rebuild the list on every binding pass).</summary>
    public IReadOnlyList<ExploreTagFilterOption> ExploreTagFilterOptions =>
        _exploreTagFilterOptions ??= BuildExploreTagFilterOptions();

    private IReadOnlyList<ExploreTagFilterOption> BuildExploreTagFilterOptions() =>
        [new ExploreTagFilterOption { Id = "", DisplayName = LocalizedStrings.ExploreTagFilterAll }, .. GetEffectiveTagRules().Select(r => new ExploreTagFilterOption { Id = r.Id, DisplayName = r.DisplayName })];

    /// <summary>Built-in + custom tag rules for conversion and explore. Disabled custom rules are excluded.</summary>
    public IReadOnlyList<TagRule> GetEffectiveTagRules() => Rulesets.GetEffectiveTagRules();

    /// <summary>Call when CustomTagRules or effective rules change so legend and explore use new rules.</summary>
    private void NotifyTagRulesChanged()
    {
        Rulesets.RefreshPresentation();
        _materialTagSemanticMatcher?.Dispose();
        _materialTagSemanticMatcher = null;
        _exploreTagFilterOptions = null;
        OnPropertyChanged(nameof(ExploreTagFilterOptions));
        _exploreController.RefreshAllDisplayTags();
        if (!_loadingSettings)
        {
            SaveSettings();
        }
    }

    partial void OnUseSemanticMaterialTagsChanged(bool value)
    {
        if (!value)
        {
            _materialTagSemanticMatcher?.Dispose();
            _materialTagSemanticMatcher = null;
        }

        if (!_loadingSettings)
        {
            _exploreController.RefreshAllDisplayTags();
        }
    }

    partial void OnMaterialTagMinSimilarityChanged(double value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    partial void OnMaterialTagMaxCountChanged(int value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    partial void OnMaterialTagCertaintyThresholdChanged(double value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    partial void OnDictionaryEvidenceEnabledChanged(bool value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.RefreshAllDisplayTags();
        }
    }

    partial void OnDictionaryEvidenceWeightChanged(double value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    partial void OnDictionaryMinEvidenceScoreChanged(double value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    partial void OnDictionaryRequestTimeoutMsChanged(int value)
    {
        _ = value;
        SaveSettings();
        if (!_loadingSettings)
        {
            _exploreController.ScheduleRefreshAllDisplayTags();
        }
    }

    private MaterialTagSemanticOptions? BuildMaterialTagSemanticOptions()
    {
        if (!UseSemanticMaterialTags)
        {
            return null;
        }

        _materialTagSemanticMatcher ??= MaterialTagSemanticMatcher.TryCreate();
        if (_materialTagSemanticMatcher is null)
        {
            return null;
        }

        return new MaterialTagSemanticOptions
        {
            Enabled = true,
            MinSimilarity = MaterialTagMinSimilarity,
            CertaintyThreshold = MaterialTagCertaintyThreshold,
            MaxTags = Math.Clamp(MaterialTagMaxCount, 1, 16),
            Matcher = _materialTagSemanticMatcher,
            DictionaryEvidenceEnabled = DictionaryEvidenceEnabled,
            DictionaryEvidenceWeight = Math.Clamp(DictionaryEvidenceWeight, 0.0, 1.0),
            DictionaryMinEvidenceScore = Math.Clamp(DictionaryMinEvidenceScore, -1.0, 1.0),
            DictionaryRequestTimeoutMs = Math.Clamp(DictionaryRequestTimeoutMs, 100, 5000),
            DictionaryProvider = _dictionaryDefinitionProvider
        };
    }

    /// <summary>Breadcrumb path for Explore (from root to current folder); click to navigate.</summary>
    public ObservableCollection<ArchiveNode> ExploreBreadcrumb { get; } = new();

    public bool CanGoBackExplore => FocusedArchiveNode != null;

    private void RebuildExploreBreadcrumb()
    {
        ExploreBreadcrumb.Clear();
        if (FocusedArchiveNode is null)
        {
            return;
        }


        var path = new List<ArchiveNode>();
        for (var n = FocusedArchiveNode; n != null && !string.IsNullOrEmpty(n.Name); n = n.Parent)
        {
            path.Add(n);
        }


        path.Reverse();
        foreach (var node in path)
        {
            ExploreBreadcrumb.Add(node);
        }
    }

    /// <summary>Languages shown in the Language dropdown (display name, culture code). Top 10 most spoken worldwide.</summary>
    public ObservableCollection<LanguageOption> SupportedLanguages { get; } = new(
    [
        new LanguageOption("English", "en"),
        new LanguageOption("中文 (简体)", "zh-Hans"),
        new LanguageOption("Español", "es"),
        new LanguageOption("हिन्दी", "hi"),
        new LanguageOption("Français", "fr"),
        new LanguageOption("العربية", "ar"),
        new LanguageOption("Português", "pt"),
        new LanguageOption("Русский", "ru"),
        new LanguageOption("Deutsch", "de"),
        new LanguageOption("日本語", "ja"),
    ]);

    public MainWindowViewModel()
    {
        _settings = UserSettings.Load();
        Rulesets = new RulesetsViewModel(NotifyTagRulesChanged);
        _settingsPersistence = new SettingsPersistenceCoordinator(
            Dispatcher.UIThread,
            TimeSpan.FromMilliseconds(250),
            () => UserSettingsSynchronizer.SaveFrom(this, _settings));
        _loadingSettings = true;

        try
        {
            RefreshUiScaleOptions();
            UserSettingsSynchronizer.LoadInto(this, _settings);
            SyncSelectedUiScaleOptionToUiScale();
            _exploreController.SetTagRulesProvider(GetEffectiveTagRules);
            _exploreController.SetMaterialTagSemanticOptionsProvider(BuildMaterialTagSemanticOptions);
            ApplyColorScheme();
            var lang = string.IsNullOrWhiteSpace(_settings.Language) ? "en" : _settings.Language;
            Strings = LocalizationService.ApplyCulture(lang);
            OnPropertyChanged(nameof(Strings));
            _exploreController.SetBackgroundTaskSink(this);
            _exploreController.SetDebugSink(msg => Dispatcher.UIThread.Post(() => { ExploreMlDebugText = msg; }));
            SelectedLanguage = SupportedLanguages.FirstOrDefault(x =>
                                   string.Equals(x.CultureCode, _settings.Language,
                                       StringComparison.OrdinalIgnoreCase)) ??
                               SupportedLanguages[0];
            RefreshFoliageModeOptions();
            RefreshMlSpecularBlendModeOptions();
            RefreshMlSpecularBlendMathOptions();
            RefreshDeepBumpOverlapOptions();
            RefreshDeepBumpInputModeOptions();
            RefreshNormalOperatorOptions();
            RefreshNormalKernelSizeOptions();
            RefreshNormalDerivativeOptions();
            RefreshColorSchemeOptions();
            SetStatus("Status_SelectPack");
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private void ApplyCulture(string cultureCode)
    {
        Strings = LocalizationService.ApplyCulture(cultureCode);
        OnPropertyChanged(nameof(Strings));
        foreach (var t in BackgroundTasks)
        {
            t.Label = ResolveBackgroundTaskLabel(t.Id);
        }

        RefreshFoliageModeOptions();
        RefreshMlSpecularBlendModeOptions();
        RefreshMlSpecularBlendMathOptions();
        RefreshDeepBumpOverlapOptions();
        RefreshDeepBumpInputModeOptions();
        RefreshNormalOperatorOptions();
        RefreshNormalKernelSizeOptions();
        RefreshNormalDerivativeOptions();
        RefreshColorSchemeOptions();
        UpdateStatusText();
    }

    private void RefreshMlSpecularBlendModeOptions()
    {
        MlSpecularBlendModeOptions.Clear();
        MlSpecularBlendModeOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendModeSmoothnessOnly,
            nameof(AutoPBR.Core.Models.MlSpecularHeuristicBlendMode.SmoothnessOnly)));
        MlSpecularBlendModeOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendModeAiMetalAndEmissive,
            nameof(AutoPBR.Core.Models.MlSpecularHeuristicBlendMode.AiMetalAndEmissive)));
        MlSpecularBlendModeOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendModeFull,
            nameof(AutoPBR.Core.Models.MlSpecularHeuristicBlendMode.Full)));

        SelectedMlSpecularBlendModeOption =
            MlSpecularBlendModeOptions.FirstOrDefault(x =>
                string.Equals(x.Value, MlSpecularHeuristicBlendMode, StringComparison.OrdinalIgnoreCase)) ??
            MlSpecularBlendModeOptions[0];
    }

    private void RefreshMlSpecularBlendMathOptions()
    {
        MlSpecularBlendMathOptions.Clear();
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathLinear,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.Linear)));
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathSoftLight,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.SoftLight)));
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathOverlay,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.Overlay)));
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathScreen,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.Screen)));
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathBiasGain,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.BiasGain)));
        MlSpecularBlendMathOptions.Add(new FoliageModeOption(LocalizedStrings.MlSpecularBlendMathSigmoidCrossfade,
            nameof(AutoPBR.Core.Models.MlSpecularBlendMath.SigmoidCrossfade)));

        SelectedMlSpecularBlendMathOption =
            MlSpecularBlendMathOptions.FirstOrDefault(x =>
                string.Equals(x.Value, MlSpecularBlendMath, StringComparison.OrdinalIgnoreCase)) ??
            MlSpecularBlendMathOptions[0];
    }

    private void RefreshColorSchemeOptions()
    {
        ColorSchemeOptions.Clear();
        foreach (var o in LocalizationService.GetColorSchemeOptions())
        {
            ColorSchemeOptions.Add(o);
        }


        SelectedColorSchemeOption = ColorSchemeOptions.FirstOrDefault(x =>
                                        string.Equals(x.Value, ColorScheme, StringComparison.OrdinalIgnoreCase))
                                    ?? ColorSchemeOptions[0];
    }

    private void RefreshDeepBumpOverlapOptions()
    {
        DeepBumpOverlapOptions.Clear();
        foreach (var o in LocalizationService.GetDeepBumpOverlapOptions(Strings))
        {
            DeepBumpOverlapOptions.Add(o);
        }

        SelectedDeepBumpOverlap =
            DeepBumpOverlapOptions.FirstOrDefault(x =>
                string.Equals(x.Value, DeepBumpOverlap, StringComparison.OrdinalIgnoreCase)) ??
            DeepBumpOverlapOptions[2];
    }

    private void RefreshDeepBumpInputModeOptions()
    {
        DeepBumpInputModeOptions.Clear();
        foreach (var o in LocalizationService.GetDeepBumpInputModeOptions(Strings))
        {
            DeepBumpInputModeOptions.Add(o);
        }

        SelectedDeepBumpInputMode =
            DeepBumpInputModeOptions.FirstOrDefault(x =>
                string.Equals(x.Value, DeepBumpInputMode, StringComparison.OrdinalIgnoreCase)) ??
            DeepBumpInputModeOptions[0];
    }

    private void RefreshFoliageModeOptions()
    {
        FoliageModeOptions.Clear();
        foreach (var o in LocalizationService.GetFoliageModeOptions(Strings))
        {
            FoliageModeOptions.Add(o);
        }


        SelectedFoliageMode =
            FoliageModeOptions.FirstOrDefault(x =>
                string.Equals(x.Value, FoliageMode, StringComparison.OrdinalIgnoreCase)) ?? FoliageModeOptions[0];
    }

    private void RefreshNormalOperatorOptions()
    {
        NormalOperatorOptions.Clear();
        foreach (var o in LocalizationService.GetNormalOperatorOptions())
        {
            NormalOperatorOptions.Add(o);
        }


        SelectedNormalOperator = NormalOperatorOptions.FirstOrDefault(x =>
                                     string.Equals(x.Value, NormalOperator, StringComparison.OrdinalIgnoreCase))
                                 ?? NormalOperatorOptions[0];
    }

    private void RefreshNormalKernelSizeOptions()
    {
        NormalKernelSizeOptions.Clear();
        foreach (var o in LocalizationService.GetNormalKernelSizeOptions(NormalOperator))
        {
            NormalKernelSizeOptions.Add(o);
        }


        SelectedNormalKernelSize = NormalKernelSizeOptions.FirstOrDefault(x =>
                                       string.Equals(x.Value, NormalKernelSize, StringComparison.OrdinalIgnoreCase))
                                   ?? NormalKernelSizeOptions[0];
    }

    private void RefreshNormalDerivativeOptions()
    {
        NormalDerivativeOptions.Clear();
        foreach (var o in LocalizationService.GetNormalDerivativeOptions())
        {
            NormalDerivativeOptions.Add(o);
        }


        SelectedNormalDerivative = NormalDerivativeOptions.FirstOrDefault(x =>
                                       string.Equals(x.Value, NormalDerivative, StringComparison.OrdinalIgnoreCase))
                                   ?? NormalDerivativeOptions[0];
    }

    private void SetStatus(string key, params object[] args)
    {
        _statusKey = key;
        _statusFormatArgs = args.Length > 0 ? args : null;
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (_statusKey is null)
        {
            StatusText = Resources.GetString("Status_SelectPack");
            return;
        }

        StatusText = _statusFormatArgs is null || _statusFormatArgs.Length == 0
            ? Resources.GetString(_statusKey)
            : Resources.GetStatusString(_statusKey, _statusFormatArgs);
    }

    partial void OnPackPathChanged(string? value)
    {
        _ = value;
        ClearScannedArchive();
        RecomputeOutputZipPath();
        ConvertCommand.NotifyCanExecuteChanged();
        ScanArchiveCommand.NotifyCanExecuteChanged();
        ScanCurrentInputCommand.NotifyCanExecuteChanged();
    }

    partial void OnBatchFolderPathChanged(string? value)
    {
        _ = value;
        ScanBatchCommand.NotifyCanExecuteChanged();
        ScanCurrentInputCommand.NotifyCanExecuteChanged();
        ConvertCommand.NotifyCanExecuteChanged();
        if (_loadingSettings)
        {
            return;
        }

        SaveSettings();
    }

    partial void OnUseBatchFolderInputChanged(bool value)
    {
        _ = value;
        ScanArchiveCommand.NotifyCanExecuteChanged();
        ScanBatchCommand.NotifyCanExecuteChanged();
        ScanCurrentInputCommand.NotifyCanExecuteChanged();
        ConvertCommand.NotifyCanExecuteChanged();
        if (_loadingSettings)
        {
            return;
        }

        SaveSettings();
    }

    partial void OnOutputDirectoryChanged(string? value)
    {
        _ = value;
        RecomputeOutputZipPath();
        ConvertCommand.NotifyCanExecuteChanged();
        SaveSettings();
    }

    private void RefreshPreviewIfActive()
    {
        if (string.IsNullOrWhiteSpace(PreviewArchivePath))
        {
            return;
        }


        _ = UpdatePreviewAsync();
    }

    /// <summary>
    /// Coalesces rapid slider-driven setting changes (same idea as <see cref="ExploreTreeController.ScheduleRefreshAllDisplayTags"/>):
    /// waits <paramref name="delayMilliseconds"/> after the last call, then runs <see cref="RefreshPreviewIfActive"/>.
    /// </summary>
    private void ScheduleRefreshPreviewIfActive(int delayMilliseconds = 200)
    {
        _previewRefreshDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewRefreshDebounceCts = cts;
        _ = RunDebouncedPreviewRefreshAsync(cts, delayMilliseconds);
    }

    private async Task RunDebouncedPreviewRefreshAsync(CancellationTokenSource debounceCts, int delayMs)
    {
        try
        {
            await Task.Delay(delayMs, debounceCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ReferenceEquals(_previewRefreshDebounceCts, debounceCts))
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!ReferenceEquals(_previewRefreshDebounceCts, debounceCts))
            {
                return;
            }

            RefreshPreviewIfActive();
        });
    }

    partial void OnFastSpecularChanged(bool value)
    {
        _ = value;
        RecomputeOutputZipPath();
        ConvertCommand.NotifyCanExecuteChanged();
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnNormalIntensityChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnHeightIntensityChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreviewBrickProbeDebugChanged(bool value)
    {
        _ = value;
        if (!_loadingSettings)
        {
            SaveSettings();
        }

        ScheduleRefreshPreviewIfActive();
    }

    partial void OnBrickHeightMapPostProcessEnabledChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnBrickHeightMinStructuralConfidenceChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnBrickHeightInvertDeltaThresholdChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnBrickLightGroutDiffuseDeltaMinChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnUseLegacyExtractorChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSmoothnessScaleChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMetallicBoostChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPorosityBiasChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPlantMaterialPorosityExtraChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularHeuristicBlendChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSelectedMlSpecularBlendModeOptionChanged(FoliageModeOption? value)
    {
        if (value is null)
        {
            return;
        }

        if (string.Equals(MlSpecularHeuristicBlendMode, value.Value, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MlSpecularHeuristicBlendMode = value.Value;
        if (!_loadingSettings)
        {
            SaveSettings();
            ScheduleRefreshPreviewIfActive();
        }
    }

    partial void OnSelectedMlSpecularBlendMathOptionChanged(FoliageModeOption? value)
    {
        if (value is null)
        {
            return;
        }

        if (string.Equals(MlSpecularBlendMath, value.Value, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MlSpecularBlendMath = value.Value;
        if (!_loadingSettings)
        {
            SaveSettings();
            ScheduleRefreshPreviewIfActive();
        }
    }

    partial void OnMaxThreadsChanged(int value)
    {
        _ = value;
        SaveSettings();
    }

    partial void OnTempDirectoryChanged(string? value)
    {
        _ = value;
        SaveSettings();
    }

    partial void OnDebugModeChanged(bool value)
    {
        _ = value;
        SaveSettings();
    }

    partial void OnExploreFilterChanged(string value)
    {
        _ = value;
        _exploreController.ApplyExploreFilter(ExploreFilter, string.IsNullOrEmpty(ExploreTagFilterId) ? null : ExploreTagFilterId);
    }

    partial void OnExploreTagFilterIdChanged(string value)
    {
        _ = value;
        _exploreController.ApplyExploreFilter(ExploreFilter, string.IsNullOrEmpty(value) ? null : value);
    }

    partial void OnColorSchemeChanged(string value)
    {
        _ = value;
        ApplyColorScheme();
        SaveSettings();
    }

    /// <summary>Maps legacy or out-of-range values onto allowed 5% steps between <see cref="MinUiScale"/> and <see cref="MaxUiScale"/>.</summary>
    public static double SnapUiScaleToAllowedStep(double value)
    {
        var v = Math.Clamp(value, MinUiScale, MaxUiScale);
        var snapped = Math.Round(v * 20.0, MidpointRounding.AwayFromZero) / 20.0;
        return Math.Clamp(snapped, MinUiScale, MaxUiScale);
    }

    private void RefreshUiScaleOptions()
    {
        UiScaleOptions.Clear();
        for (var p = 75; p <= 100; p += 5)
        {
            var scale = p / 100.0;
            UiScaleOptions.Add(new FoliageModeOption($"{p}%", scale.ToString("0.00", CultureInfo.InvariantCulture)));
        }
    }

    private void SyncSelectedUiScaleOptionToUiScale()
    {
        var target = UiScaleOptions.FirstOrDefault(o =>
                         Math.Abs(double.Parse(o.Value, CultureInfo.InvariantCulture) - UiScale) < 0.001)
                     ?? UiScaleOptions.LastOrDefault();
        if (target is null)
        {
            return;
        }

        if (string.Equals(SelectedUiScaleOption?.Value, target.Value, StringComparison.Ordinal))
        {
            return;
        }

        SelectedUiScaleOption = target;
    }

    partial void OnUiScaleChanged(double value)
    {
        var c = SnapUiScaleToAllowedStep(value);
        if (Math.Abs(c - value) > 1e-9)
        {
            UiScale = c;
            return;
        }

        if (_loadingSettings)
        {
            return;
        }

        SaveSettings();
    }

    partial void OnSelectedUiScaleOptionChanged(FoliageModeOption? value)
    {
        if (value is null || _loadingSettings)
        {
            return;
        }

        if (!double.TryParse(value.Value, CultureInfo.InvariantCulture, out var newScale))
        {
            return;
        }

        newScale = SnapUiScaleToAllowedStep(newScale);
        if (Math.Abs(newScale - UiScale) > 1e-9)
        {
            UiScale = newScale;
        }
    }

    partial void OnSelectedColorSchemeOptionChanged(FoliageModeOption? value)
    {
        if (value != null)
        {
            ColorScheme = value.Value;
        }
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedLanguage changes
    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        var code = value?.CultureCode ?? "en";
        ApplyCulture(code);
        _exploreTagFilterOptions = null;
        OnPropertyChanged(nameof(ExploreTagFilterOptions));
        _settings.Language = code;
        _settings.Save();
    }

    partial void OnProcessBlocksChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ApplyTextureTypeOverridesToExplore();
    }

    partial void OnProcessItemsChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ApplyTextureTypeOverridesToExplore();
    }

    partial void OnProcessArmorChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ApplyTextureTypeOverridesToExplore();
    }

    partial void OnProcessEntityChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ApplyTextureTypeOverridesToExplore();
    }

    partial void OnProcessParticlesChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ApplyTextureTypeOverridesToExplore();
    }

    partial void OnUseDeepBumpNormalsChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedDeepBumpOverlap changes
    partial void OnSelectedDeepBumpOverlapChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        DeepBumpOverlap = value?.Value ?? "Large";
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedDeepBumpInputMode changes
    partial void OnSelectedDeepBumpInputModeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }

        DeepBumpInputMode = value?.Value ?? nameof(AutoPBR.Core.Models.DeepBumpInputMode.Auto);
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedNormalOperator changes
    partial void OnSelectedNormalOperatorChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        NormalOperator = value?.Value ?? nameof(AutoPBR.Core.Models.NormalOperator.SobelVc);
        RefreshNormalKernelSizeOptions();
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSelectedNormalKernelSizeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        NormalKernelSize = value?.Value ?? "3";
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSelectedNormalDerivativeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        NormalDerivative = value?.Value ?? nameof(AutoPBR.Core.Models.NormalDerivative.Luminance);
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessLinearizeChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessDenoiseRadiusChanged(int value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessDenoiseBlendChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessFrequencySplitChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessFrequencyRadiusChanged(int value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreprocessFrequencyDetailStrengthChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpForceBlue255Changed(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpNormalIntensityChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpNormalSoftClampChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpEdgeGuidedEnhanceChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpEdgeGuidedStrengthChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpEdgeGuidedGammaChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnDeepBumpEdgeGuidedDirectionMixChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnNormalHeightTransparentAlphaClampMaxChanged(int value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
        OnPropertyChanged(nameof(NormalHeightTransparentAlphaClampMaxSlider));
    }

    partial void OnSpecularUsePercentileRemapChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSpecularRemapLowPercentileChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSpecularRemapHighPercentileChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnUseMlSpecularPredictorChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreferOnnxTensorRtExecutionProviderChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPathChanged(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPath16Changed(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPath32Changed(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPath64Changed(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPath128Changed(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularModelPath256Changed(string? value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularUseEdgeChannelChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnMlSpecularTransparentAlphaClampMaxChanged(int value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
        OnPropertyChanged(nameof(MlSpecularTransparentAlphaClampMaxSlider));
    }

    partial void OnSpecularDebugDisableHeuristicSpecularChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSpecularDebugSkipSpecularRemapChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnSpecularDebugVerboseSpecularMlChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnGenerateAoChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnAoRadiusChanged(int value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnAoStrengthChanged(double value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    private void RecomputeOutputZipPath()
    {
        if (string.IsNullOrWhiteSpace(PackPath) || string.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputZipPath = null;
            return;
        }

        var baseName = Path.GetFileNameWithoutExtension(PackPath);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "pack";
        }

        // Always output a .zip PBR layer; same _PBR suffix for .zip and .jar (matches batch convert).
        OutputZipPath = Path.Combine(OutputDirectory, $"{baseName}_PBR.zip");
    }

    private void SaveSettings()
    {
        if (_loadingSettings)
        {
            return;
        }

        _settingsPersistence.RequestSave();
    }

    private async Task FlushPendingSettingsSaveAsync()
        => await _settingsPersistence.FlushAsync().ConfigureAwait(false);

    private void ApplyColorScheme()
    {
        var palette = AppearanceService.GetPalette(ColorScheme);
        WindowBackground = palette.WindowBackground;
        CardBackground = palette.CardBackground;
        PreviewFadeColor = palette.PreviewFadeColor;
        CardBorderBrush = palette.CardBorderBrush;
        AccentBrush = palette.AccentBrush;
        ForegroundBrush = palette.ForegroundBrush;
    }

    private void ApplyTextureTypeOverridesToExplore()
    {
        var previousFocusPath = FocusedArchiveNode?.FullPath;
        var restored = _exploreController.ApplyTextureTypeOverridesToExplore(previousFocusPath, ProcessBlocks,
            ProcessItems, ProcessEntity, ProcessParticles);
        FocusedArchiveNode = restored ?? (ScannedArchiveRoot is not null
            ? ExploreTreeController.FindChildByName(ScannedArchiveRoot, "assets")
            : null);
        PreloadExpandersForCurrentView();
    }

    private static bool IsPackPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));

    private bool CanScanArchive() => !IsConverting && !IsBusy && IsPackPath(PackPath) && File.Exists(PackPath);

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private void ClearScannedArchive()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        FocusedArchiveNode = null;
        ScannedArchiveRoot = null;
        _exploreController.Clear();
        OnPropertyChanged(nameof(IsBatchScanActive));
        ScanCurrentInputCommand.NotifyCanExecuteChanged();
        ConvertCommand.NotifyCanExecuteChanged();
    }

    private bool HaveScanForCurrentPack() => _exploreController.HaveScanForCurrentPack(PackPath);

    /// <summary>Ensure that all folders currently shown in the Explore view have their children loaded, so expand arrows are visible.</summary>
    private void PreloadExpandersForCurrentView()
    {
        if (ScannedArchiveRoot is null)
        {
            return;
        }


        var host = (IArchiveNodeHost)_exploreController;
        var roots = FocusedArchiveNode?.Children ?? ScannedArchiveRoot.Children;
        foreach (var node in roots)
        {
            if (node.IsFolder)
            {
                host.EnsureChildrenLoaded(node);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBackExplore))]
    private void GoBackExplore()
    {
        if (FocusedArchiveNode is null)
        {
            return;
        }


        var parent = FocusedArchiveNode.Parent;
        if (parent is null || string.IsNullOrEmpty(parent.Name))
        {
            FocusedArchiveNode = null;
        }
        else
        {
            FocusedArchiveNode = parent;
        }
    }

    [RelayCommand]
    private void GoToBreadcrumb(ArchiveNode? node)
    {
        if (node != null)
        {
            FocusedArchiveNode = node;
        }
    }

    [RelayCommand]
    private void EnterFolder(ArchiveNode? node)
    {
        if (node is { IsFolder: true })
        {
            FocusedArchiveNode = node;
        }
    }

    private static void ExpandAllInSubtree(ArchiveNode node, bool expand)
    {
        node.IsExpanded = expand;
        foreach (var c in node.Children)
        {
            ExpandAllInSubtree(c, expand);
        }
    }

    [RelayCommand]
    private void ExploreExpandAll()
    {
        if (ScannedArchiveRoot is null)
        {
            return;
        }


        var root = FocusedArchiveNode ?? ScannedArchiveRoot;
        ExpandAllInSubtree(root, true);
    }

    [RelayCommand]
    private void ExploreCollapseAll()
    {
        if (ScannedArchiveRoot is null)
        {
            return;
        }


        var root = FocusedArchiveNode ?? ScannedArchiveRoot;
        ExpandAllInSubtree(root, false);
    }

    [RelayCommand(CanExecute = nameof(CanClearTagOverrides))]
    private void ClearTagOverrides()
    {
        _exploreController.ClearTagOverridesForCurrentPack();
    }

    private bool CanClearTagOverrides() => HasScannedArchive;

    /// <summary>Replace custom tag rules from JSON (file picker calls this). Persists settings.</summary>
    public string? ImportCustomTagRulesFromJson(string json)
    {
        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<CustomTagRuleEntry>>(json);
            if (list is null)
            {
                return "Invalid or empty tag rules file.";
            }

            Rulesets.ReplaceCustomRules(list);

            SaveSettings();
            NotifyTagRulesChanged();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Debug report text for the MiniLM semantic match dialog. Bound to the dialog TextBlock.</summary>
    [ObservableProperty] private string _semanticMatchDebugText = "";

    /// <summary>Set by the view so the command can open a dialog.</summary>
    internal Action<string>? ShowSemanticDebugDialog { get; set; }

    [RelayCommand]
    private void DebugSemanticMatch(ArchiveNode? node)
    {
        if (node is null || node.IsFolder || !UseSemanticMaterialTags)
        {
            return;
        }

        var debugText = _exploreController.GetSemanticMatchDebugText(node.FullPath);
        if (string.IsNullOrWhiteSpace(debugText))
        {
            SemanticMatchDebugText = "MiniLM is not enabled or the file is not a texture.";
            ShowSemanticDebugDialog?.Invoke(SemanticMatchDebugText);
            return;
        }

        SemanticMatchDebugText = debugText;
        ShowSemanticDebugDialog?.Invoke(SemanticMatchDebugText);
    }

    private async Task UpdatePreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(PreviewArchivePath))
        {
            return;
        }

        if (!_exploreController.TryGetDiskPackAndEntryPath(PreviewArchivePath, out var diskPack, out var entryPath) ||
            string.IsNullOrEmpty(diskPack) || !File.Exists(diskPack))
        {
            return;
        }

        if (_previewCts is { } oldCts)
        {
            await oldCts.CancelAsync().ConfigureAwait(false);
            oldCts.Dispose();
        }

        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;

        try
        {
            if (_specularData is null)
            {
                SetStatus("Status_LoadingSpecularData");
                _specularData =
                    SpecularData.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json"));
            }

            IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>? manualPreview = null;
            if (IsBatchScanActive && PreviewArchivePath!.IndexOf('/') is > 0 and var slash)
            {
                var root = PreviewArchivePath[..slash];
                if (_exploreController.Data?.BatchPackRootToPath?.ContainsKey(root) == true)
                {
                    manualPreview = _exploreController.GetManualTagOverridesForBatchPackRoot(root);
                }
            }

            var options = BuildConversionOptions(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                null,
                manualPreview,
                PreviewBrickProbeDebug);
            var previewResult = await PreviewService.RenderPreviewAsync(diskPack, entryPath, options, ct)
                .ConfigureAwait(false);

            if (ct.IsCancellationRequested)
            {
                return;
            }


            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                using var ms = new MemoryStream(previewResult.PngBytes);
                PreviewImage = new Bitmap(ms);
                PreviewBrickProbeDebugText = PreviewBrickProbeDebug ? previewResult.BrickProbeDebugText : null;
            });
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("No previewable textures found after extraction.", StringComparison.Ordinal))
        {
            await Dispatcher.UIThread.InvokeAsync(() => { PreviewImage = null; });
            AddLogLine("Preview skipped: no previewable textures were found for this selection.");
        }
        catch (Exception ex)
        {
            AddLogLine(ex.ToString());
        }
    }

    [RelayCommand(CanExecute = nameof(CanScanArchive))]
    public async Task ScanArchiveAsync()
    {
        if (!IsPackPath(PackPath) || !File.Exists(PackPath))
        {
            return;
        }


        IsBusy = true;
        ScanArchiveCommand.NotifyCanExecuteChanged();
        ProgressValue = 0;
        ProgressMax = 1;
        SetStatus("Status_ScanningTexturesInPack");
        AddLogLine(Resources.GetStatusString("Log_ScanningArchive", PackPath ?? ""));

        if (_scanCts is { } oldScanCts)
        {
            await oldScanCts.CancelAsync().ConfigureAwait(false);
            oldScanCts.Dispose();
        }

        _scanCts = new CancellationTokenSource();
        var scanToken = _scanCts.Token;

        var scanProgress = CreateScanProgressReporter(p =>
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressMax = Math.Max(1, p.total);
                ProgressValue = p.completed;
                SetStatus("Status_ScanningPackProgress", p.completed, p.total);
            });
        });
        try
        {
            var data = await Task.Run(() => PackScannerService.BuildArchiveIndex(PackPath!, scanProgress), scanToken)
                .ConfigureAwait(false);
            scanToken.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ScannedArchiveRoot = _exploreController.SetData(data, PackPath!);
                ApplyTextureTypeOverridesToExplore();
                FocusedArchiveNode ??= ExploreTreeController.FindChildByName(ScannedArchiveRoot!, "assets");
                PreloadExpandersForCurrentView();
                SetStatus("Status_LoadedTextures", data.FileCount);
                AddLogLine(Resources.GetStatusString("Log_ArchiveContentsLoaded", data.FileCount));
            });
            ((IBackgroundTaskSink)this).BeginTask(BackgroundTaskIds.ExploreCache);
            try
            {
                await Task.Run(() => _exploreController.PrewarmFolderVisibilityCache(scanToken), scanToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ((IBackgroundTaskSink)this).EndTask(BackgroundTaskIds.ExploreCache);
            }
        }
        catch (OperationCanceledException)
        {
            // User cleared archive or started new scan; no error message
        }
        catch (Exception ex)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetStatus("Status_FailedToScan");
                AddLogLine(ex.ToString());
            });
        }
        finally
        {
            // ReSharper disable once MethodHasAsyncOverload
            _scanCts?.Dispose();
            _scanCts = null;
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
                ScanArchiveCommand.NotifyCanExecuteChanged();
                ScanBatchCommand.NotifyCanExecuteChanged();
                ScanCurrentInputCommand.NotifyCanExecuteChanged();
                ConvertCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private bool CanScanCurrentInput() =>
        UseBatchFolderInput ? CanScanBatch() : CanScanArchive();

    [RelayCommand(CanExecute = nameof(CanScanCurrentInput))]
    public async Task ScanCurrentInputAsync()
    {
        if (UseBatchFolderInput)
        {
            await ScanBatchAsync().ConfigureAwait(false);
        }
        else
        {
            await ScanArchiveAsync().ConfigureAwait(false);
        }
    }

    private bool CanScanBatch() =>
        !IsConverting &&
        !IsBusy &&
        !string.IsNullOrWhiteSpace(BatchFolderPath) &&
        Directory.Exists(BatchFolderPath);

    [RelayCommand(CanExecute = nameof(CanScanBatch))]
    public async Task ScanBatchAsync()
    {
        if (string.IsNullOrWhiteSpace(BatchFolderPath) || !Directory.Exists(BatchFolderPath))
        {
            return;
        }

        IsBusy = true;
        ScanBatchCommand.NotifyCanExecuteChanged();
        ScanArchiveCommand.NotifyCanExecuteChanged();
        ProgressValue = 0;
        ProgressMax = 1;
        SetStatus("Status_ScanningBatchFolder");
        AddLogLine(Resources.GetStatusString("Log_ScanningBatchFolder", BatchFolderPath));

        if (_scanCts is { } oldBatchScan)
        {
            await oldBatchScan.CancelAsync().ConfigureAwait(false);
            oldBatchScan.Dispose();
        }

        _scanCts = new CancellationTokenSource();
        var scanToken = _scanCts.Token;

        var scanProgress = CreateScanProgressReporter(p =>
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressMax = Math.Max(1, p.total);
                ProgressValue = p.completed;
                SetStatus("Status_ScanningPackProgress", p.completed, p.total);
            });
        });
        try
        {
            var data = await Task.Run(() => PackScannerService.BuildBatchArchiveIndex(BatchFolderPath!, scanProgress), scanToken)
                .ConfigureAwait(false);
            scanToken.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ScannedArchiveRoot = _exploreController.SetData(data, Path.GetFullPath(BatchFolderPath!));
                ApplyTextureTypeOverridesToExplore();
                FocusedArchiveNode = null;
                PreloadExpandersForCurrentView();
                var packCount = data.BatchPackRootToPath?.Count ?? 0;
                SetStatus("Status_BatchScanDone", packCount, data.FileCount);
                AddLogLine(Resources.GetStatusString("Log_BatchScanDone", packCount, data.FileCount));
            });
            ((IBackgroundTaskSink)this).BeginTask(BackgroundTaskIds.ExploreCache);
            try
            {
                await Task.Run(() => _exploreController.PrewarmFolderVisibilityCache(scanToken), scanToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ((IBackgroundTaskSink)this).EndTask(BackgroundTaskIds.ExploreCache);
            }
        }
        catch (OperationCanceledException)
        {
            // cancelled
        }
        catch (Exception ex)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetStatus("Status_FailedToScan");
                AddLogLine(ex.ToString());
            });
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBusy = false;
                ScanBatchCommand.NotifyCanExecuteChanged();
                ScanArchiveCommand.NotifyCanExecuteChanged();
                ScanCurrentInputCommand.NotifyCanExecuteChanged();
                ConvertCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private bool CanBatchConvert() =>
        !IsConverting &&
        !IsBusy &&
        !string.IsNullOrWhiteSpace(BatchFolderPath) &&
        Directory.Exists(BatchFolderPath) &&
        !string.IsNullOrWhiteSpace(OutputDirectory) &&
        _exploreController.HaveBatchScanForFolder(BatchFolderPath) &&
        _exploreController.Data?.BatchPackRootToPath is { Count: > 0 };

    private async Task WaitForPendingTagWorkWithProgressAsync(CancellationToken token)
    {
        var initialPending = _exploreController.GetPendingTagWorkCount();
        if (initialPending <= 0)
        {
            await _exploreController.WaitForPendingTagWorkAsync(cancellationToken: token).ConfigureAwait(false);
            return;
        }

        var progressTotal = Math.Max(1, initialPending);
        void UpdatePending(int pending)
        {
            var remaining = Math.Clamp(pending, 0, progressTotal);
            var completed = progressTotal - remaining;
            Dispatcher.UIThread.Post(() =>
            {
                ProgressMax = progressTotal;
                ProgressValue = completed;
                SetStatus("Status_TagWorkDrainProgress", completed, progressTotal);
            });
        }

        UpdatePending(initialPending);
        await _exploreController.WaitForPendingTagWorkAsync(UpdatePending, token).ConfigureAwait(false);
        Dispatcher.UIThread.Post(() =>
        {
            ProgressMax = 1;
            ProgressValue = 0;
        });
    }

    private async Task ExecuteBatchConvertAsync()
    {
        if (!CanBatchConvert())
        {
            return;
        }

        var packs = _exploreController.Data!.BatchPackRootToPath!;
        IsConverting = true;
        IsBusy = true;
        ProgressValue = 0;
        ProgressMax = 1;
        _lastLogWriteUtc = DateTime.MinValue;
        _conversionStartUtc = DateTime.UtcNow;
        _currentStage = null;
        ConversionElapsedText = "Elapsed: 00:00";
        ConversionEtaText = "ETA: --:--";
        ConversionTotalText = "";
        CancelCommand.NotifyCanExecuteChanged();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await FlushPendingSettingsSaveAsync().ConfigureAwait(false);
            SetStatus("Status_LoadingSpecularData");
            _specularData ??=
                SpecularData.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json"));

            using var prog = CreateConversionProgressReporter();
            await WaitForPendingTagWorkWithProgressAsync(token).ConfigureAwait(false);
            _exploreController.FlushTagOverridesToDisk();
            var packIndex = 0;
            foreach (var kv in packs)
            {
                packIndex++;
                var packRoot = kv.Key;
                string diskPackPath = kv.Value;
                token.ThrowIfCancellationRequested();

                var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                _exploreController.ApplyExploreOverridesToIgnoreSetForBatchPack(ignore, packRoot);
                var innerPaths = _exploreController.GetFilePathsUnderBatchPackRoot(packRoot);
                var entries = innerPaths.ToList();
                entries.Add("pack.mcmeta");
                var manual = _exploreController.GetManualTagOverridesForBatchPackRoot(packRoot);
                var options = BuildConversionOptions(ignore, entries, manual);
                var baseName = Path.GetFileNameWithoutExtension(diskPackPath);
                var outZip = Path.Combine(OutputDirectory!, baseName + "_PBR.zip");

                AddLogLine(Resources.GetStatusString("Log_BatchConvertingPack", packIndex, packs.Count, diskPackPath, outZip));
                await ConversionCoordinator.ConvertAsync(diskPackPath, outZip, options, prog, token);
            }

            AddLogLine(Resources.GetString("Log_Done"));
        }
        catch (OperationCanceledException)
        {
            RunOnUiThread(() =>
            {
                SetStatus("Status_Cancelled");
                AddLogLine(Resources.GetString("Log_Cancelled"));
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                SetStatus("Status_ConversionFailed");
                AddLogLine(ex.ToString());
            });
        }
        finally
        {
            SaveLogToFile();
            RunOnUiThread(() =>
            {
                if (string.IsNullOrWhiteSpace(ConversionTotalText))
                {
                    var total = DateTime.UtcNow - _conversionStartUtc;
                    if (total < TimeSpan.Zero)
                    {
                        total = TimeSpan.Zero;
                    }

                    ConversionTotalText = $"Total: {FormatDuration(total)}";
                }
            });
            _cts?.Dispose();
            _cts = null;
            RunOnUiThread(() =>
            {
                IsConverting = false;
                IsBusy = false;
                CancelCommand.NotifyCanExecuteChanged();
                ConvertCommand.NotifyCanExecuteChanged();
                ScanCurrentInputCommand.NotifyCanExecuteChanged();
            });
        }
    }

    partial void OnScannedArchiveRootChanged(ArchiveNode? value)
    {
        _ = value; // Partial method signature is generated; parameter not needed for this handler.
        RunOnUiThread(() =>
        {
            ClearExploreSelection();
            OnPropertyChanged(nameof(HasScannedArchive));
            OnPropertyChanged(nameof(ShowExploreEmptyMessage));
            OnPropertyChanged(nameof(ScannedArchiveTopLevel));
            OnPropertyChanged(nameof(ExploreViewItems));
            OnPropertyChanged(nameof(IsBatchScanActive));
            ScanCurrentInputCommand.NotifyCanExecuteChanged();
            ConvertCommand.NotifyCanExecuteChanged();
            ClearTagOverridesCommand.NotifyCanExecuteChanged();
        });
    }

    partial void OnFocusedArchiveNodeChanged(ArchiveNode? value)
    {
        if (value is { IsFolder: true })
        {
            ((IArchiveNodeHost)_exploreController).EnsureChildrenLoaded(value);
        }
        PreloadExpandersForCurrentView();
        OnPropertyChanged(nameof(ExploreViewItems));
        OnPropertyChanged(nameof(CanGoBackExplore));
        RebuildExploreBreadcrumb();
        GoBackExploreCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty] private ArchiveNode? _selectedExploreNode;
    public ObservableCollection<ArchiveNode> SelectedExploreNodes { get; } = new();
    private ArchiveNode? _selectionAnchorExploreNode;

    [UsedImplicitly] // Called from view pointer-selection handler.
    public void HandleExploreNodePointerSelection(ArchiveNode node, KeyModifiers modifiers)
    {
        var visible = GetVisibleExploreNodesInDisplayOrder();
        if ((modifiers & KeyModifiers.Shift) != 0 &&
            _selectionAnchorExploreNode is not null &&
            visible.Count > 0)
        {
            var a = visible.IndexOf(_selectionAnchorExploreNode);
            var b = visible.IndexOf(node);
            if (a >= 0 && b >= 0)
            {
                if (a > b)
                {
                    (a, b) = (b, a);
                }

                ReplaceExploreSelection(visible.Skip(a).Take(b - a + 1));
                SelectedExploreNode = node;
                return;
            }
        }

        if ((modifiers & KeyModifiers.Control) != 0)
        {
            if (node.IsSelected)
            {
                node.IsSelected = false;
                SelectedExploreNodes.Remove(node);
                if (ReferenceEquals(SelectedExploreNode, node))
                {
                    SelectedExploreNode = SelectedExploreNodes.LastOrDefault();
                }
            }
            else
            {
                node.IsSelected = true;
                if (!SelectedExploreNodes.Contains(node))
                {
                    SelectedExploreNodes.Add(node);
                }
                SelectedExploreNode = node;
            }

            _selectionAnchorExploreNode = node;
            return;
        }

        ReplaceExploreSelection([node]);
        SelectedExploreNode = node;
    }

    private List<ArchiveNode> GetVisibleExploreNodesInDisplayOrder()
    {
        var ordered = new List<ArchiveNode>();

        void Walk(ArchiveNode n)
        {
            if (!n.IsVisibleByFilter)
            {
                return;
            }

            ordered.Add(n);
            if (n is { IsFolder: true, IsExpanded: true })
            {
                foreach (var ch in n.Children)
                {
                    Walk(ch);
                }
            }
        }

        foreach (var n in ExploreViewItems)
        {
            Walk(n);
        }

        return ordered;
    }

    private void ReplaceExploreSelection(IEnumerable<ArchiveNode> nodes)
    {
        var desired = nodes.Distinct().ToList();
        foreach (var n in SelectedExploreNodes.ToList())
        {
            if (!desired.Contains(n))
            {
                n.IsSelected = false;
                SelectedExploreNodes.Remove(n);
            }
        }

        foreach (var n in desired)
        {
            if (!n.IsSelected)
            {
                n.IsSelected = true;
            }

            if (!SelectedExploreNodes.Contains(n))
            {
                SelectedExploreNodes.Add(n);
            }
        }

        _selectionAnchorExploreNode = desired.LastOrDefault() ?? _selectionAnchorExploreNode;
    }

    private void ClearExploreSelection()
    {
        foreach (var n in SelectedExploreNodes)
        {
            n.IsSelected = false;
        }

        SelectedExploreNodes.Clear();
        SelectedExploreNode = null;
        _selectionAnchorExploreNode = null;
    }

    [RelayCommand]
    private async Task SetPreviewTextureAsync(ArchiveNode? node)
    {
        if (node is null || node.IsFolder)
        {
            return;
        }

        PreviewArchivePath = node.FullPath;
        PreviewTextureName = node.FullPath;
        if (_previewRefreshDebounceCts is { } oldDebounce)
        {
            await oldDebounce.CancelAsync().ConfigureAwait(false);
            oldDebounce.Dispose();
        }
        _previewRefreshDebounceCts = null;
        await UpdatePreviewAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private Task SetPreviewFromSelectionAsync() =>
        SetPreviewTextureAsync(SelectedExploreNode);

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedFoliageMode changes
    partial void OnSelectedFoliageModeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
        {
            return;
        }
        FoliageMode = value?.Value ?? "Ignore All";
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    public async Task ConvertAsync()
    {
        if (UseBatchFolderInput)
        {
            await ExecuteBatchConvertAsync().ConfigureAwait(false);
            return;
        }

        if (!IsPackPath(PackPath) || !File.Exists(PackPath))
        {
            return;
        }


        if (string.IsNullOrWhiteSpace(OutputZipPath))
        {
            return;
        }


        IsConverting = true;
        IsBusy = true;
        ProgressValue = 0;
        ProgressMax = 1;
        _lastLogWriteUtc = DateTime.MinValue;
        _conversionStartUtc = DateTime.UtcNow;
        _currentStage = null;
        ConversionElapsedText = "Elapsed: 00:00";
        ConversionEtaText = "ETA: --:--";
        ConversionTotalText = "";
        CancelCommand.NotifyCanExecuteChanged();

        _cts = new CancellationTokenSource();

        try
        {
            SetStatus("Status_LoadingSpecularData");
            _specularData ??=
                SpecularData.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json"));

            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            _exploreController.ApplyExploreOverridesToIgnoreSet(ignore);

            if (!HaveScanForCurrentPack())
            {
                AddLogLine(Resources.GetString("Log_ScanningPackForExtraction"));
                var scanProg = CreateScanProgressReporter(p =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        ProgressMax = Math.Max(1, p.total);
                        ProgressValue = p.completed;
                        SetStatus("Status_ScanningPackProgress", p.completed, p.total);
                    }));
                var scanData = await Task.Run(() => PackScannerService.BuildArchiveIndex(PackPath!, scanProg));
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _exploreController.SetData(scanData, PackPath!);
                    ScannedArchiveRoot = _exploreController.Root;
                });
                AddLogLine(Resources.GetStatusString("Log_IndexedPngEntries", scanData.FileCount));
            }

            IReadOnlyList<string>? entriesToExtractOnly = null;
            if (_exploreController.Data is not null)
            {
                var list = _exploreController.Data.EnumerateAllFilePaths().ToList();
                list.Add("pack.mcmeta");
                entriesToExtractOnly = list;
                AddLogLine(Resources.GetString("Log_ExtractingOnlyPng"));
            }

            await WaitForPendingTagWorkWithProgressAsync(_cts.Token).ConfigureAwait(false);
            _exploreController.FlushTagOverridesToDisk();

            var options = BuildConversionOptions(ignore, entriesToExtractOnly);
            using var prog = CreateConversionProgressReporter();

            AddLogLine(Resources.GetStatusString("Log_Converting", OutputZipPath ?? ""));
            await ConversionCoordinator.ConvertAsync(PackPath!, OutputZipPath!, options, prog, _cts.Token);
            AddLogLine(Resources.GetString("Log_Done"));
        }
        catch (OperationCanceledException)
        {
            RunOnUiThread(() =>
            {
                SetStatus("Status_Cancelled");
                AddLogLine(Resources.GetString("Log_Cancelled"));
                var totalSec = (DateTime.UtcNow - _conversionStartUtc).TotalSeconds;
                AddLogLine(Resources.GetStatusString("Log_TotalTime", totalSec));
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() =>
            {
                SetStatus("Status_ConversionFailed");
                AddLogLine(ex.ToString());
                var totalSec = (DateTime.UtcNow - _conversionStartUtc).TotalSeconds;
                AddLogLine(Resources.GetStatusString("Log_TotalTime", totalSec));
            });
        }
        finally
        {
            // Persist the log for this conversion run.
            SaveLogToFile();
            RunOnUiThread(() =>
            {
                if (string.IsNullOrWhiteSpace(ConversionTotalText))
                {
                    var total = DateTime.UtcNow - _conversionStartUtc;
                    if (total < TimeSpan.Zero)
                    {
                        total = TimeSpan.Zero;
                    }

                    ConversionTotalText = $"Total: {FormatDuration(total)}";
                }
            });

            _cts?.Dispose();
            _cts = null;
            RunOnUiThread(() =>
            {
                IsConverting = false;
                IsBusy = false;
                ClearScannedArchive();
                ConvertCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
                ScanArchiveCommand.NotifyCanExecuteChanged();
                ScanCurrentInputCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private bool CanSinglePackConvert() =>
        !IsBatchScanActive &&
        IsPackPath(PackPath) &&
        File.Exists(PackPath) &&
        !string.IsNullOrWhiteSpace(OutputZipPath);

    private bool CanConvert() =>
        !IsConverting &&
        !IsBusy &&
        (UseBatchFolderInput ? CanBatchConvert() : CanSinglePackConvert());

    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsConverting;

    /// <summary>
    /// Per-resolution paths: explicit text box overrides bundled <c>Data/ONNX-AI/SpecLab</c> models when empty.
    /// </summary>
    private Dictionary<int, string>? BuildMergedSpecularResolutionMap()
    {
        var d = new Dictionary<int, string>();
        var baseDir = AppContext.BaseDirectory;
        void Put(int res, string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                d[res] = explicitPath.Trim();
                return;
            }

            var bundled = MlSpecularBundledModelPaths.TryResolveExistingBundledPath(res, baseDir);
            if (bundled is not null)
            {
                d[res] = bundled;
            }
        }

        Put(16, MlSpecularModelPath16);
        Put(32, MlSpecularModelPath32);
        Put(64, MlSpecularModelPath64);
        Put(128, MlSpecularModelPath128);
        Put(256, MlSpecularModelPath256);
        return d.Count > 0 ? d : null;
    }

    /// <summary>Build converter options from current VM state and scan data.</summary>
    private AutoPbrOptions BuildConversionOptions(
        HashSet<string> ignore,
        IReadOnlyList<string>? entriesToExtractOnly,
        IReadOnlyDictionary<string, (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)>? manualTagOverrides = null,
        bool brickProbePreviewDebug = false)
    {
        var model = new ConversionSettingsModel
        {
            NormalIntensity = NormalIntensity,
            HeightIntensity = HeightIntensity,
            BrickHeightMapPostProcessEnabled = BrickHeightMapPostProcessEnabled,
            BrickHeightMinStructuralConfidence = BrickHeightMinStructuralConfidence,
            BrickHeightInvertDeltaThreshold = BrickHeightInvertDeltaThreshold,
            BrickLightGroutDiffuseDeltaMin = BrickLightGroutDiffuseDeltaMin,
            BrickProbePreviewDebug = brickProbePreviewDebug,
            FastSpecular = FastSpecular,
            UseLegacyExtractor = UseLegacyExtractor,
            SmoothnessScale = SmoothnessScale,
            MetallicBoost = MetallicBoost,
            PorosityBias = PorosityBias,
            PlantMaterialPorosityExtra = PlantMaterialPorosityExtra,
            SpecularUsePercentileRemap = SpecularUsePercentileRemap,
            SpecularRemapLowPercentile = SpecularRemapLowPercentile,
            SpecularRemapHighPercentile = SpecularRemapHighPercentile,
            UseMlSpecularPredictor = UseMlSpecularPredictor,
            MlSpecularModelPath = MlSpecularModelPath,
            MlSpecularModelPathsByResolution = BuildMergedSpecularResolutionMap(),
            MlSpecularHeuristicBlend = MlSpecularHeuristicBlend,
            MlSpecularHeuristicBlendMode = MlSpecularHeuristicBlendMode,
            MlSpecularBlendMath = MlSpecularBlendMath,
            MlSpecularUseEdgeChannel = MlSpecularUseEdgeChannel,
            MlSpecularTransparentAlphaClampMax = MlSpecularTransparentAlphaClampMax,
            SpecularDebugDisableHeuristicSpecular = SpecularDebugDisableHeuristicSpecular,
            SpecularDebugSkipSpecularRemap = SpecularDebugSkipSpecularRemap,
            SpecularDebugVerboseSpecularMl = SpecularDebugVerboseSpecularMl,
            MaxThreads = MaxThreads,
            TempDirectory = TempDirectory,
            ProcessBlocks = ProcessBlocks,
            ProcessItems = ProcessItems,
            ProcessArmor = ProcessArmor,
            ProcessEntity = ProcessEntity,
            ProcessParticles = ProcessParticles,
            GenerateAo = GenerateAo,
            AoRadius = AoRadius,
            AoStrength = AoStrength,
            FoliageMode = FoliageMode,
            UseDeepBumpNormals = UseDeepBumpNormals,
            DeepBumpOverlap = DeepBumpOverlap,
            DeepBumpInputMode = DeepBumpInputMode,
            DeepBumpForceBlue255 = DeepBumpForceBlue255,
            DeepBumpNormalIntensity = DeepBumpNormalIntensity,
            DeepBumpNormalSoftClamp = DeepBumpNormalSoftClamp,
            DeepBumpEdgeGuidedEnhance = DeepBumpEdgeGuidedEnhance,
            DeepBumpEdgeGuidedStrength = DeepBumpEdgeGuidedStrength,
            DeepBumpEdgeGuidedGamma = DeepBumpEdgeGuidedGamma,
            DeepBumpEdgeGuidedDirectionMix = DeepBumpEdgeGuidedDirectionMix,
            NormalHeightTransparentAlphaClampMax = NormalHeightTransparentAlphaClampMax,
            NormalOperator = NormalOperator,
            NormalKernelSize = NormalKernelSize,
            NormalDerivative = NormalDerivative,
            PreprocessLinearize = PreprocessLinearize,
            PreprocessDenoiseRadius = PreprocessDenoiseRadius,
            PreprocessDenoiseBlend = PreprocessDenoiseBlend,
            PreprocessFrequencySplit = PreprocessFrequencySplit,
            PreprocessFrequencyRadius = PreprocessFrequencyRadius,
            PreprocessFrequencyDetailStrength = PreprocessFrequencyDetailStrength,
            PreferOnnxTensorRtExecutionProvider = PreferOnnxTensorRtExecutionProvider
        };
        return model.ToAutoPbrOptions(_specularData, ignore, entriesToExtractOnly,
            manualTagOverrides ?? _exploreController.GetManualTagOverrides(), GetEffectiveTagRules(),
            BuildMaterialTagSemanticOptions());
    }

    /// <summary>Progress reporter that coalesces high-frequency updates before they hit the UI thread.</summary>
    private CoalescingConversionProgressReporter CreateConversionProgressReporter() =>
        new(OnConversionProgress, ConversionUiProgressUpdateInterval);

    private static CoalescingProgressReporter<(int completed, int total)> CreateScanProgressReporter(
        Action<(int completed, int total)> sink) =>
        new(
            sink,
            ScanUiProgressUpdateInterval,
            p => p.completed >= p.total);

    private void OnConversionProgress(ConversionProgress p)
    {
        void ApplyProgress()
        {
            var now = DateTime.UtcNow;
            if (_currentStage.HasValue && _currentStage.Value != p.Stage)
            {
                var elapsed = (now - _stageStartUtc).TotalSeconds;
                var stageName = GetStageDisplayName(_currentStage.Value);
                AddLogLine(Resources.GetStatusString("Log_StageCompleted", stageName, elapsed));
            }

            if (p.Stage != ConversionStage.Done)
            {
                _currentStage = p.Stage;
                _stageStartUtc = now;
            }
            else
            {
                var totalSec = (now - _conversionStartUtc).TotalSeconds;
                AddLogLine(Resources.GetStatusString("Log_TotalTime", totalSec));
            }

            ProgressMax = Math.Max(1, p.Total);
            ProgressValue = p.Completed;
            UpdateConversionTiming(now, p);
            if (!string.IsNullOrEmpty(p.InfoMessage))
            {
                AddLogLine(p.InfoMessage);
            }

            if (p.Stage == ConversionStage.ScanningTextures)
            {
                var shouldUpdateScanDebug =
                    !string.IsNullOrWhiteSpace(p.InfoMessage) ||
                    now - _lastScanDebugUpdateUtc >= ScanDebugUpdateInterval ||
                    p.Completed == p.Total;
                if (shouldUpdateScanDebug)
                {
                    ConversionScanDebugText = p.Total > 0
                        ? $"Scanning debug: {p.Completed}/{p.Total} {(string.IsNullOrWhiteSpace(p.CurrentTextureName) ? "" : p.CurrentTextureName)}"
                        : "Scanning debug: phase started";
                    if (!string.IsNullOrWhiteSpace(p.InfoMessage))
                    {
                        ConversionScanDebugText = $"{ConversionScanDebugText} | {p.InfoMessage}";
                    }

                    _lastScanDebugUpdateUtc = now;
                }
            }


            if (p.Stage == ConversionStage.Extracting && p is { Completed: 0, Total: > 0 })
            {
                AddLogLine(Resources.GetString("Log_Extracting"));
            }


            if (p.Stage == ConversionStage.Packing && p is { Completed: 0, Total: > 0 })
            {
                AddLogLine(Resources.GetString("Log_Packing"));
            }


            if (!string.IsNullOrEmpty(p.CurrentTextureName))
            {
                if ((now - _lastLogWriteUtc).TotalMilliseconds >= LogWriteIntervalMs)
                {
                    _lastLogWriteUtc = now;
                    var stageLabel = GetStageDisplayName(p.Stage);
                    AddLogLine(Resources.GetStatusString("Log_StageCurrent", stageLabel, p.CurrentTextureName));
                }
            }

            (_statusKey, _statusFormatArgs) = p.Stage switch
            {
                ConversionStage.Extracting => ("Status_ExtractingPack", null),
                ConversionStage.ScanningTextures => ("Status_ScanningTextures", null),
                ConversionStage.GeneratingSpecular => ("Status_StageGeneratingSpecular", null),
                ConversionStage.GeneratingNormals => ("Status_StageGeneratingNormals", null),
                ConversionStage.Packing => ("Status_PackingOutput", null),
                ConversionStage.Done => ("Status_Done", null),
                _ => (_statusKey, _statusFormatArgs)
            };
            UpdateStatusText();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyProgress();
        }
        else
        {
            Dispatcher.UIThread.Post(ApplyProgress);
        }
    }

    private void UpdateConversionTiming(DateTime now, ConversionProgress p)
    {
        var elapsed = now - _conversionStartUtc;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        ConversionElapsedText = $"Elapsed: {FormatDuration(elapsed)}";
        if (p.Stage == ConversionStage.Done)
        {
            ConversionEtaText = "ETA: 00:00";
            ConversionTotalText = $"Total: {FormatDuration(elapsed)}";
            return;
        }

        ConversionTotalText = "";
        if (p.Total > 0 && p.Completed > 0 && p.Completed < p.Total)
        {
            var remaining = p.Total - p.Completed;
            var etaSeconds = elapsed.TotalSeconds * (remaining / (double)p.Completed);
            etaSeconds = Math.Max(0, Math.Min(etaSeconds, 365 * 24 * 3600));
            ConversionEtaText = $"ETA: {FormatDuration(TimeSpan.FromSeconds(etaSeconds))}";
        }
        else
        {
            ConversionEtaText = "ETA: --:--";
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);
        }

        return duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private static string GetStageDisplayName(ConversionStage stage)
    {
        var key = stage switch
        {
            ConversionStage.Extracting => "Log_StageExtracting",
            ConversionStage.ScanningTextures => "Log_StageScanning",
            ConversionStage.GeneratingSpecular => "Log_StageSpecular",
            ConversionStage.GeneratingNormals => "Log_StageNormals",
            ConversionStage.Packing => "Log_StagePacking",
            _ => null
        };
        return key != null ? Resources.GetString(key) : stage.ToString();
    }

    private sealed class CoalescingConversionProgressReporter : IProgress<ConversionProgress>, IDisposable
    {
        private readonly Action<ConversionProgress> _sink;
        private readonly TimeSpan _minInterval;
        private readonly object _gate = new();
        private ConversionProgress? _pending;
        private DateTime _lastDispatchUtc = DateTime.MinValue;
        private ConversionStage? _lastDispatchedStage;
        private bool _drainScheduled;
        private bool _disposed;

        public CoalescingConversionProgressReporter(Action<ConversionProgress> sink, TimeSpan minInterval)
        {
            _sink = sink;
            _minInterval = minInterval;
        }

        public void Report(ConversionProgress value)
        {
            ConversionProgress? dispatchNow = null;
            TimeSpan delay = default;
            var scheduleDrain = false;

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                var forceImmediate = value.Stage == ConversionStage.Done ||
                                     (_lastDispatchedStage.HasValue && value.Stage != _lastDispatchedStage.Value);
                var elapsed = _lastDispatchUtc == DateTime.MinValue
                    ? _minInterval
                    : now - _lastDispatchUtc;
                if (forceImmediate || elapsed >= _minInterval)
                {
                    dispatchNow = value;
                    _lastDispatchUtc = now;
                    _lastDispatchedStage = value.Stage;
                    _pending = null;
                }
                else
                {
                    _pending = value;
                    if (!_drainScheduled)
                    {
                        _drainScheduled = true;
                        delay = _minInterval - elapsed;
                        scheduleDrain = true;
                    }
                }
            }

            if (dispatchNow is not null)
            {
                _sink(dispatchNow);
            }

            if (scheduleDrain)
            {
                ScheduleDrain(delay);
            }
        }

        private void ScheduleDrain(TimeSpan delay)
        {
            _ = Task.Delay(delay).ContinueWith(_ => DrainPending(), TaskScheduler.Default);
        }

        private void DrainPending()
        {
            ConversionProgress? dispatch = null;
            TimeSpan delay = default;
            var scheduleAgain = false;

            lock (_gate)
            {
                _drainScheduled = false;
                if (_disposed || _pending is null)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                var elapsed = _lastDispatchUtc == DateTime.MinValue
                    ? _minInterval
                    : now - _lastDispatchUtc;
                if (elapsed < _minInterval)
                {
                    _drainScheduled = true;
                    delay = _minInterval - elapsed;
                    scheduleAgain = true;
                }
                else
                {
                    dispatch = _pending;
                    _pending = null;
                    _lastDispatchUtc = now;
                    _lastDispatchedStage = dispatch.Stage;
                }
            }

            if (dispatch is not null)
            {
                _sink(dispatch);
            }

            if (scheduleAgain)
            {
                ScheduleDrain(delay);
            }
        }

        public void Dispose()
        {
            ConversionProgress? lastPending;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                lastPending = _pending;
                _pending = null;
            }

            if (lastPending is not null)
            {
                _sink(lastPending);
            }
        }
    }

    private sealed class CoalescingProgressReporter<T> : IProgress<T>
    {
        private readonly Action<T> _sink;
        private readonly TimeSpan _minInterval;
        private readonly Func<T, bool> _shouldFlushImmediately;
        private readonly object _gate = new();
        private T _pending = default!;
        private bool _hasPending;
        private DateTime _lastDispatchUtc = DateTime.MinValue;
        private bool _drainScheduled;

        public CoalescingProgressReporter(Action<T> sink, TimeSpan minInterval, Func<T, bool>? shouldFlushImmediately = null)
        {
            _sink = sink;
            _minInterval = minInterval;
            _shouldFlushImmediately = shouldFlushImmediately ?? (_ => false);
        }

        public void Report(T value)
        {
            T dispatchNow = default!;
            var hasDispatchNow = false;
            TimeSpan delay = default;
            var scheduleDrain = false;

            lock (_gate)
            {
                var now = DateTime.UtcNow;
                var forceImmediate = _shouldFlushImmediately(value);
                var elapsed = _lastDispatchUtc == DateTime.MinValue
                    ? _minInterval
                    : now - _lastDispatchUtc;
                if (forceImmediate || elapsed >= _minInterval)
                {
                    dispatchNow = value;
                    hasDispatchNow = true;
                    _lastDispatchUtc = now;
                    _hasPending = false;
                }
                else
                {
                    _pending = value;
                    _hasPending = true;
                    if (!_drainScheduled)
                    {
                        _drainScheduled = true;
                        delay = _minInterval - elapsed;
                        scheduleDrain = true;
                    }
                }
            }

            if (hasDispatchNow)
            {
                _sink(dispatchNow);
            }

            if (scheduleDrain)
            {
                ScheduleDrain(delay);
            }
        }

        private void ScheduleDrain(TimeSpan delay)
        {
            _ = Task.Delay(delay).ContinueWith(_ => DrainPending(), TaskScheduler.Default);
        }

        private void DrainPending()
        {
            T dispatch = default!;
            var hasDispatch = false;
            TimeSpan delay = default;
            var scheduleAgain = false;

            lock (_gate)
            {
                _drainScheduled = false;
                if (!_hasPending)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                var elapsed = _lastDispatchUtc == DateTime.MinValue
                    ? _minInterval
                    : now - _lastDispatchUtc;
                if (elapsed < _minInterval)
                {
                    _drainScheduled = true;
                    delay = _minInterval - elapsed;
                    scheduleAgain = true;
                }
                else
                {
                    dispatch = _pending;
                    _hasPending = false;
                    hasDispatch = true;
                    _lastDispatchUtc = now;
                }
            }

            if (hasDispatch)
            {
                _sink(dispatch);
            }

            if (scheduleAgain)
            {
                ScheduleDrain(delay);
            }
        }

    }

    public void Dispose()
    {
        _cts?.Cancel();
        _scanCts?.Cancel();
        _previewCts?.Cancel();
        _previewRefreshDebounceCts?.Cancel();
        _cts?.Dispose();
        _scanCts?.Dispose();
        _previewCts?.Dispose();
        _previewRefreshDebounceCts?.Dispose();
        _materialTagSemanticMatcher?.Dispose();
        _exploreController.Dispose();
        GC.SuppressFinalize(this);
    }
}
