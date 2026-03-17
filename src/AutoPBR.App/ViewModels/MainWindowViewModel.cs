using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JetBrains.Annotations;
using AutoPBR.App.Lang;
using AutoPBR.App.Models;
using AutoPBR.App.Services;
using AutoPBR.Core;
using AutoPBR.Core.Models;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _previewCts;
    private readonly ExploreTreeController _exploreController = new();
    private SpecularData? _specularData;
    private readonly UserSettings _settings;
    private readonly bool _loadingSettings;
    private DateTime _lastLogWriteUtc = DateTime.MinValue;
    private const int LogWriteIntervalMs = 250;
    private const int MaxLogLines = 600;

    private void AddLogLine(string line)
    {
        LogLines.Add(line);
        while (LogLines.Count > MaxLogLines)
            LogLines.RemoveAt(0);
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
    [ObservableProperty] private bool _fastSpecular;
    [ObservableProperty] private string _foliageMode = "Ignore All";
    [ObservableProperty] private bool _useLegacyExtractor;
    [ObservableProperty] private double _smoothnessScale = AutoPbrDefaults.DefaultSmoothnessScale;
    [ObservableProperty] private double _metallicBoost = AutoPbrDefaults.DefaultMetallicBoost;
    [ObservableProperty] private double _porosityBias = AutoPbrDefaults.DefaultPorosityBias;
    [ObservableProperty] private int _maxThreads; // 0 = auto
    [ObservableProperty] private int _maxThreadsMax = Math.Max(1, Environment.ProcessorCount);
    [ObservableProperty] private string? _tempDirectory;
    [ObservableProperty] private bool _processBlocks = true;
    [ObservableProperty] private bool _processItems = true;
    [ObservableProperty] private bool _processArmor = true;
    [ObservableProperty] private bool _processEntity = true;
    [ObservableProperty] private bool _processParticles = true;
    [ObservableProperty] private bool _useDeepBumpNormals;
    [ObservableProperty] private string _deepBumpOverlap = "Large";
    [ObservableProperty] private string _normalOperator = nameof(AutoPBR.Core.Models.NormalOperator.SobelVc);
    [ObservableProperty] private string _normalKernelSize = "3";
    [ObservableProperty] private string _normalDerivative = nameof(AutoPBR.Core.Models.NormalDerivative.Luminance);

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isConverting;
    [ObservableProperty] private string _statusText = "";

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private double _progressMax = 1;

    [ObservableProperty] private string? _outputZipPath;

    /// <summary>Search filter for the Resource Explorer tree (Explore tab). Filters nodes by path/name.</summary>
    [ObservableProperty] private string _exploreFilter = "";

    [ObservableProperty] private string _colorScheme = "Dark";
    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private FoliageModeOption? _selectedFoliageMode;
    [ObservableProperty] private FoliageModeOption? _selectedDeepBumpOverlap;
    [ObservableProperty] private FoliageModeOption? _selectedNormalOperator;
    [ObservableProperty] private FoliageModeOption? _selectedNormalKernelSize;
    [ObservableProperty] private FoliageModeOption? _selectedNormalDerivative;
    [ObservableProperty] private FoliageModeOption? _selectedColorSchemeOption;
    [ObservableProperty] private IBrush _windowBackground = Brushes.Transparent;
    [ObservableProperty] private IBrush _cardBackground = Brushes.Transparent;
    [ObservableProperty] private IBrush _cardBorderBrush = Brushes.Gray;
    [ObservableProperty] private IBrush _accentBrush = Brushes.DeepSkyBlue;
    [ObservableProperty] private IBrush _foregroundBrush = Brushes.White;

    [ObservableProperty] private string? _previewArchivePath;
    [ObservableProperty] private string? _previewTextureName;
    [ObservableProperty] private Bitmap? _previewImage;
    /// <summary>Color used for preview panel top/bottom gradient fade (matches CardBackground).</summary>
    [ObservableProperty] private Color _previewFadeColor = Color.FromRgb(0x22, 0x22, 0x2A);

    public ObservableCollection<string> LogLines { get; } = new();

    /// <summary>Persist the current in-memory log to a file (delegates to <see cref="LogService"/>).</summary>
    private void SaveLogToFile() => LogService.SaveToFile(LogLines);

    /// <summary>Localized strings for bindings; replaced when language changes.</summary>
    public LocalizedStrings Strings { get; private set; }

    /// <summary>Foliage options for the dropdown (display name from Strings, value for settings/converter).</summary>
    public ObservableCollection<FoliageModeOption> FoliageModeOptions { get; } = new();

    /// <summary>DeepBump tile overlap options (Small, Medium, Large). Matches DeepBump --color_to_normals-overlap.</summary>
    public ObservableCollection<FoliageModeOption> DeepBumpOverlapOptions { get; } = new();

    /// <summary>Normal operator options (Sobel + VC, Scharr + VC).</summary>
    public ObservableCollection<FoliageModeOption> NormalOperatorOptions { get; } = new();

    /// <summary>Normal kernel size options (3x3, 5x5, 7x7 for Sobel; 3x3,5x5 for Scharr).</summary>
    public ObservableCollection<FoliageModeOption> NormalKernelSizeOptions { get; } = new();

    /// <summary>Derivative source options: Color, Luminance, Color+Luminance Blend, Color+Luminance Max.</summary>
    public ObservableCollection<FoliageModeOption> NormalDerivativeOptions { get; } = new();

    /// <summary>Color scheme options for the Appearance dropdown (display name from Resources, value for settings).</summary>
    public ObservableCollection<FoliageModeOption> ColorSchemeOptions { get; } = new();

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

    /// <summary>Breadcrumb path for Explore (from root to current folder); click to navigate.</summary>
    public ObservableCollection<ArchiveNode> ExploreBreadcrumb { get; } = new();

    public bool CanGoBackExplore => FocusedArchiveNode != null;

    private void RebuildExploreBreadcrumb()
    {
        ExploreBreadcrumb.Clear();
        if (FocusedArchiveNode is null)
            return;
        var path = new List<ArchiveNode>();
        for (var n = FocusedArchiveNode; n != null && !string.IsNullOrEmpty(n.Name); n = n.Parent)
            path.Add(n);
        path.Reverse();
        foreach (var node in path)
            ExploreBreadcrumb.Add(node);
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
        _loadingSettings = true;

        try
        {
            UserSettingsSynchronizer.LoadInto(this, _settings);
            ApplyColorScheme();
            var lang = string.IsNullOrWhiteSpace(_settings.Language) ? "en" : _settings.Language;
            Strings = LocalizationService.ApplyCulture(lang);
            OnPropertyChanged(nameof(Strings));
            SelectedLanguage = SupportedLanguages.FirstOrDefault(x =>
                                   string.Equals(x.CultureCode, _settings.Language,
                                       StringComparison.OrdinalIgnoreCase)) ??
                               SupportedLanguages[0];
            RefreshFoliageModeOptions();
            RefreshDeepBumpOverlapOptions();
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
        RefreshFoliageModeOptions();
        RefreshDeepBumpOverlapOptions();
        RefreshNormalOperatorOptions();
        RefreshNormalKernelSizeOptions();
        RefreshNormalDerivativeOptions();
        RefreshColorSchemeOptions();
        UpdateStatusText();
    }

    private void RefreshColorSchemeOptions()
    {
        ColorSchemeOptions.Clear();
        foreach (var o in LocalizationService.GetColorSchemeOptions())
            ColorSchemeOptions.Add(o);
        SelectedColorSchemeOption = ColorSchemeOptions.FirstOrDefault(x =>
                                        string.Equals(x.Value, ColorScheme, StringComparison.OrdinalIgnoreCase))
                                    ?? ColorSchemeOptions[0];
    }

    private void RefreshDeepBumpOverlapOptions()
    {
        DeepBumpOverlapOptions.Clear();
        foreach (var o in LocalizationService.GetDeepBumpOverlapOptions(Strings))
            DeepBumpOverlapOptions.Add(o);
        SelectedDeepBumpOverlap =
            DeepBumpOverlapOptions.FirstOrDefault(x =>
                string.Equals(x.Value, DeepBumpOverlap, StringComparison.OrdinalIgnoreCase)) ??
            DeepBumpOverlapOptions[2];
    }

    private void RefreshFoliageModeOptions()
    {
        FoliageModeOptions.Clear();
        foreach (var o in LocalizationService.GetFoliageModeOptions(Strings))
            FoliageModeOptions.Add(o);
        SelectedFoliageMode =
            FoliageModeOptions.FirstOrDefault(x =>
                string.Equals(x.Value, FoliageMode, StringComparison.OrdinalIgnoreCase)) ?? FoliageModeOptions[0];
    }

    private void RefreshNormalOperatorOptions()
    {
        NormalOperatorOptions.Clear();
        foreach (var o in LocalizationService.GetNormalOperatorOptions())
            NormalOperatorOptions.Add(o);
        SelectedNormalOperator = NormalOperatorOptions.FirstOrDefault(x =>
                                     string.Equals(x.Value, NormalOperator, StringComparison.OrdinalIgnoreCase))
                                 ?? NormalOperatorOptions[0];
    }

    private void RefreshNormalKernelSizeOptions()
    {
        NormalKernelSizeOptions.Clear();
        foreach (var o in LocalizationService.GetNormalKernelSizeOptions(NormalOperator))
            NormalKernelSizeOptions.Add(o);
        SelectedNormalKernelSize = NormalKernelSizeOptions.FirstOrDefault(x =>
                                       string.Equals(x.Value, NormalKernelSize, StringComparison.OrdinalIgnoreCase))
                                   ?? NormalKernelSizeOptions[0];
    }

    private void RefreshNormalDerivativeOptions()
    {
        NormalDerivativeOptions.Clear();
        foreach (var o in LocalizationService.GetNormalDerivativeOptions())
            NormalDerivativeOptions.Add(o);
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
            return;

        _ = UpdatePreviewAsync();
    }

    partial void OnFastSpecularChanged(bool value)
    {
        _ = value;
        RecomputeOutputZipPath();
        ConvertCommand.NotifyCanExecuteChanged();
        SaveSettings();
        RefreshPreviewIfActive();
    }

    partial void OnNormalIntensityChanged(double value)
    {
        _ = value;
        SaveSettings();
        RefreshPreviewIfActive();
    }

    partial void OnHeightIntensityChanged(double value)
    {
        _ = value;
        SaveSettings();
        RefreshPreviewIfActive();
    }

    partial void OnUseLegacyExtractorChanged(bool value)
    {
        _ = value;
        SaveSettings();
        RefreshPreviewIfActive();
    }

    partial void OnSmoothnessScaleChanged(double value)
    {
        _ = value;
        SaveSettings();
        RefreshPreviewIfActive();
    }

    partial void OnMetallicBoostChanged(double value)
    {
        _ = value;
        SaveSettings();
        RefreshPreviewIfActive();
    }

    partial void OnPorosityBiasChanged(double value)
    {
        _ = value;
        SaveSettings();
        RefreshPreviewIfActive();
    }
    partial void OnMaxThreadsChanged(int value) { _ = value; SaveSettings(); }
    partial void OnTempDirectoryChanged(string? value) { _ = value; SaveSettings(); }
    partial void OnExploreFilterChanged(string value) { _ = value; _exploreController.ApplyExploreFilter(ExploreFilter); }

    partial void OnColorSchemeChanged(string value)
    {
        _ = value;
        ApplyColorScheme();
        SaveSettings();
    }

    partial void OnSelectedColorSchemeOptionChanged(FoliageModeOption? value)
    {
        if (value != null)
            ColorScheme = value.Value;
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedLanguage changes
    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_loadingSettings)
            return;
        var code = value?.CultureCode ?? "en";
        ApplyCulture(code);
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
        RefreshPreviewIfActive();
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedDeepBumpOverlap changes
    partial void OnSelectedDeepBumpOverlapChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
            return;
        DeepBumpOverlap = value?.Value ?? "Large";
        SaveSettings();
        RefreshPreviewIfActive();
    }

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedNormalOperator changes
    partial void OnSelectedNormalOperatorChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
            return;
        NormalOperator = value?.Value ?? nameof(AutoPBR.Core.Models.NormalOperator.SobelVc);
        RefreshNormalKernelSizeOptions();
        SaveSettings();
        RefreshPreviewIfActive();
    }

    partial void OnSelectedNormalKernelSizeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
            return;
        NormalKernelSize = value?.Value ?? "3";
        SaveSettings();
        RefreshPreviewIfActive();
    }

    partial void OnSelectedNormalDerivativeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
            return;
        NormalDerivative = value?.Value ?? nameof(AutoPBR.Core.Models.NormalDerivative.Luminance);
        SaveSettings();
        RefreshPreviewIfActive();
    }

    private void RecomputeOutputZipPath()
    {
        if (string.IsNullOrWhiteSpace(PackPath) || string.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputZipPath = null;
            return;
        }

        var ext = Path.GetExtension(PackPath);
        var baseName = Path.GetFileNameWithoutExtension(PackPath);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "pack";

        // Always output .zip (separate PBR layer). JAR in → ZIP out; ZIP in → ZIP with _PBR suffix.
        OutputZipPath = ext.Equals(".jar", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(OutputDirectory, baseName + ".zip")
            : Path.Combine(OutputDirectory, $"{baseName}_PBR.zip");
    }

    private void SaveSettings()
    {
        if (_loadingSettings)
            return;
        UserSettingsSynchronizer.SaveFrom(this, _settings);
    }

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
        var restored = _exploreController.ApplyTextureTypeOverridesToExplore(previousFocusPath, ProcessBlocks, ProcessItems, ProcessEntity, ProcessParticles);
        FocusedArchiveNode = restored ?? (ScannedArchiveRoot is not null ? ExploreTreeController.FindChildByName(ScannedArchiveRoot, "assets") : null);
        PreloadExpandersForCurrentView();
    }

    private static bool IsPackPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));

    private bool CanScanArchive() => !IsConverting && !IsBusy && IsPackPath(PackPath) && File.Exists(PackPath);

    private void ClearScannedArchive()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        FocusedArchiveNode = null;
        ScannedArchiveRoot = null;
        _exploreController.Clear();
    }

    private bool HaveScanForCurrentPack() => _exploreController.HaveScanForCurrentPack(PackPath);

    /// <summary>Ensure that all folders currently shown in the Explore view have their children loaded, so expand arrows are visible.</summary>
    private void PreloadExpandersForCurrentView()
    {
        if (ScannedArchiveRoot is null)
            return;
        var host = (IArchiveNodeHost)_exploreController;
        var roots = FocusedArchiveNode?.Children ?? ScannedArchiveRoot.Children;
        foreach (var node in roots)
        {
            if (node.IsFolder)
                host.EnsureChildrenLoaded(node);
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBackExplore))]
    private void GoBackExplore()
    {
        if (FocusedArchiveNode is null)
            return;
        var parent = FocusedArchiveNode.Parent;
        if (parent is null || string.IsNullOrEmpty(parent.Name))
            FocusedArchiveNode = null;
        else
            FocusedArchiveNode = parent;
    }

    [RelayCommand]
    private void GoToBreadcrumb(ArchiveNode? node)
    {
        if (node != null)
            FocusedArchiveNode = node;
    }

    [RelayCommand]
    private void EnterFolder(ArchiveNode? node)
    {
        if (node is { IsFolder: true })
            FocusedArchiveNode = node;
    }

    private void ExpandAllInSubtree(ArchiveNode node, bool expand)
    {
        node.IsExpanded = expand;
        foreach (var c in node.Children)
            ExpandAllInSubtree(c, expand);
    }

    [RelayCommand]
    private void ExploreExpandAll()
    {
        if (ScannedArchiveRoot is null)
            return;
        var root = FocusedArchiveNode ?? ScannedArchiveRoot;
        ExpandAllInSubtree(root, true);
    }

    [RelayCommand]
    private void ExploreCollapseAll()
    {
        if (ScannedArchiveRoot is null)
            return;
        var root = FocusedArchiveNode ?? ScannedArchiveRoot;
        ExpandAllInSubtree(root, false);
    }

    private async Task UpdatePreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(PreviewArchivePath) || !IsPackPath(PackPath) || !File.Exists(PackPath))
            return;

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
                _specularData = SpecularData.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json"));
            }

            var options = BuildConversionOptions(new HashSet<string>(StringComparer.OrdinalIgnoreCase), null);
            var pngBytes = await PreviewService.RenderPreviewAsync(PackPath!, PreviewArchivePath!, options, ct)
                .ConfigureAwait(false);

            if (ct.IsCancellationRequested)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                using var ms = new MemoryStream(pngBytes);
                PreviewImage = new Bitmap(ms);
            });
        }
        catch (OperationCanceledException)
        {
            // ignore
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
            return;
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

        var scanProgress = new Progress<(int completed, int total)>(p =>
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
            var data = await Task.Run(() => PackScannerService.BuildArchiveIndex(PackPath!, scanProgress), scanToken).ConfigureAwait(false);
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
            await Task.Run(() => _exploreController.PrewarmFolderVisibilityCache(scanToken), scanToken).ConfigureAwait(false);
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
                ConvertCommand.NotifyCanExecuteChanged();
            });
        }
    }

    partial void OnScannedArchiveRootChanged(ArchiveNode? value)
    {
        _ = value; // Partial method signature is generated; parameter not needed for this handler.
        OnPropertyChanged(nameof(HasScannedArchive));
        OnPropertyChanged(nameof(ShowExploreEmptyMessage));
        OnPropertyChanged(nameof(ScannedArchiveTopLevel));
    }

    partial void OnFocusedArchiveNodeChanged(ArchiveNode? value)
    {
        if (value is { IsFolder: true })
            ((IArchiveNodeHost)_exploreController).EnsureChildrenLoaded(value);
        PreloadExpandersForCurrentView();
        OnPropertyChanged(nameof(ExploreViewItems));
        OnPropertyChanged(nameof(CanGoBackExplore));
        RebuildExploreBreadcrumb();
        GoBackExploreCommand.NotifyCanExecuteChanged();
    }

        [ObservableProperty] private ArchiveNode? _selectedExploreNode;

        [RelayCommand]
        private async Task SetPreviewTextureAsync(ArchiveNode? node)
        {
            if (node is null || node.IsFolder)
                return;

            PreviewArchivePath = node.FullPath;
            PreviewTextureName = node.FullPath;
            await UpdatePreviewAsync().ConfigureAwait(false);
        }

        [RelayCommand]
        private Task SetPreviewFromSelectionAsync() =>
            SetPreviewTextureAsync(SelectedExploreNode);

    [UsedImplicitly] // Invoked by CommunityToolkit.Mvvm source generator when SelectedFoliageMode changes
    partial void OnSelectedFoliageModeChanged(FoliageModeOption? value)
    {
        if (_loadingSettings)
            return;
        FoliageMode = value?.Value ?? "Ignore All";
        SaveSettings();
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    public async Task ConvertAsync()
    {
        if (!IsPackPath(PackPath) || !File.Exists(PackPath))
            return;
        if (string.IsNullOrWhiteSpace(OutputZipPath))
            return;

        IsConverting = true;
        IsBusy = true;
        ProgressValue = 0;
        ProgressMax = 1;
        _lastLogWriteUtc = DateTime.MinValue;
        _conversionStartUtc = DateTime.UtcNow;
        _currentStage = null;
        CancelCommand.NotifyCanExecuteChanged();

        _cts = new CancellationTokenSource();

        try
        {
            SetStatus("Status_LoadingSpecularData");
            _specularData ??=
                SpecularData.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json"));

            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (FoliageMode == "Ignore All")
            {
                foreach (var p in AutoPbrDefaults.PlantTextureKeys)
                    ignore.Add(p);
            }

            _exploreController.ApplyExploreOverridesToIgnoreSet(ignore);

            if (!HaveScanForCurrentPack())
            {
                AddLogLine(Resources.GetString("Log_ScanningPackForExtraction"));
                var scanProg = new Progress<(int completed, int total)>(p =>
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

            var options = BuildConversionOptions(ignore, entriesToExtractOnly);
            var prog = CreateConversionProgressReporter();

            AddLogLine(Resources.GetStatusString("Log_Converting", OutputZipPath ?? ""));
            await ConversionCoordinator.ConvertAsync(PackPath!, OutputZipPath!, options, prog, _cts.Token);
            AddLogLine(Resources.GetString("Log_Done"));
        }
        catch (OperationCanceledException)
        {
            SetStatus("Status_Cancelled");
            AddLogLine(Resources.GetString("Log_Cancelled"));
            var totalSec = (DateTime.UtcNow - _conversionStartUtc).TotalSeconds;
            AddLogLine(Resources.GetStatusString("Log_TotalTime", totalSec));
        }
        catch (Exception ex)
        {
            SetStatus("Status_ConversionFailed");
            AddLogLine(ex.ToString());
            var totalSec = (DateTime.UtcNow - _conversionStartUtc).TotalSeconds;
            AddLogLine(Resources.GetStatusString("Log_TotalTime", totalSec));
        }
        finally
        {
            // Persist the log for this conversion run.
            SaveLogToFile();

            _cts?.Dispose();
            _cts = null;
            IsConverting = false;
            IsBusy = false;
            ClearScannedArchive();
            ConvertCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            ScanArchiveCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanConvert() =>
        !IsConverting &&
        !IsBusy &&
        IsPackPath(PackPath) &&
        File.Exists(PackPath) &&
        !string.IsNullOrWhiteSpace(OutputZipPath);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsConverting;

    /// <summary>Build converter options from current VM state and scan data.</summary>
    private AutoPbrOptions BuildConversionOptions(HashSet<string> ignore, IReadOnlyList<string>? entriesToExtractOnly)
    {
        var model = new ConversionSettingsModel
        {
            NormalIntensity = NormalIntensity,
            HeightIntensity = HeightIntensity,
            FastSpecular = FastSpecular,
            UseLegacyExtractor = UseLegacyExtractor,
            SmoothnessScale = SmoothnessScale,
            MetallicBoost = MetallicBoost,
            PorosityBias = PorosityBias,
            MaxThreads = MaxThreads,
            TempDirectory = TempDirectory,
            ProcessBlocks = ProcessBlocks,
            ProcessItems = ProcessItems,
            ProcessArmor = ProcessArmor,
            ProcessEntity = ProcessEntity,
            ProcessParticles = ProcessParticles,
            FoliageMode = FoliageMode,
            UseDeepBumpNormals = UseDeepBumpNormals,
            DeepBumpOverlap = DeepBumpOverlap,
            NormalOperator = NormalOperator,
            NormalKernelSize = NormalKernelSize,
            NormalDerivative = NormalDerivative
        };
        return model.ToAutoPbrOptions(_specularData, ignore, entriesToExtractOnly);
    }

    /// <summary>Progress reporter that marshals conversion progress to the UI thread and updates status/log.</summary>
    private IProgress<ConversionProgress> CreateConversionProgressReporter() =>
        new Progress<ConversionProgress>(OnConversionProgress);

    private void OnConversionProgress(ConversionProgress p)
    {
        Dispatcher.UIThread.Post(() =>
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
            if (!string.IsNullOrEmpty(p.InfoMessage))
                AddLogLine(p.InfoMessage);
            if (p.Stage == ConversionStage.Extracting && p is { Completed: 0, Total: > 0 })
                AddLogLine(Resources.GetString("Log_Extracting"));
            if (p.Stage == ConversionStage.Packing && p is { Completed: 0, Total: > 0 })
                AddLogLine(Resources.GetString("Log_Packing"));
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
                ConversionStage.Extracting => p.Total > 0
                    ? ("Status_ExtractingPackProgress", [p.Completed, p.Total])
                    : ("Status_ExtractingPack", null),
                ConversionStage.ScanningTextures => ("Status_ScanningTextures", null),
                ConversionStage.GeneratingSpecular => ("Status_SpecularCurrent",
                    [p.CurrentTextureName ?? ""]),
                ConversionStage.GeneratingNormals => ("Status_NormalsCurrent",
                    [p.CurrentTextureName ?? ""]),
                ConversionStage.Packing => p.Total > 0
                    ? ("Status_PackingOutputProgress", [p.Completed, p.Total])
                    : ("Status_PackingOutput", null),
                ConversionStage.Done => ("Status_Done", null),
                _ => (_statusKey, _statusFormatArgs)
            };
            UpdateStatusText();
        });
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
}
