using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using AutoPBR.App.Models;
using AutoPBR.App.Services;
using AutoPBR.App.ViewModels;

namespace AutoPBR.App.Views;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions IndentedJsonSerializerOptions = new() { WriteIndented = true };
    private static readonly CompositeFormat LogTagRulesImportFailedFormat =
        CompositeFormat.Parse(Lang.Resources.Log_TagRulesImportFailed);
    private static readonly CompositeFormat LogTagRulesImportedFormat =
        CompositeFormat.Parse(Lang.Resources.Log_TagRulesImported);

    private const int LogScrollThrottleMs = 200;
    private DateTime _lastLogScrollUtc = DateTime.MinValue;

    private const double RoundedCornerRadius = 8;
    private const double JumpToTopThresholdPx = 220;
    private Border? _rootBorder;
    private double _lastUiScaleForWindow = 1.0;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        TryEnableWindowsSnap();
        _rootBorder = this.FindControl<Border>("RootBorder");
        RestoreWindowLayout();
        if (DataContext is MainWindowViewModel vmOpen)
        {
            _lastUiScaleForWindow = vmOpen.UiScale;
            vmOpen.PropertyChanged += ViewModel_OnPropertyChanged;
        }

        UpdateCornerRadiusFromCurrentState();
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty)
            {
                UpdateCornerRadiusFromCurrentState();
            }

        };
        Resized += (_, _) => UpdateCornerRadiusFromCurrentState();
        PositionChanged += (_, _) => UpdateCornerRadiusFromCurrentState();
    }

    private void RestoreWindowLayout()
    {
        var state = WindowLayoutState.Load();
        Position = new PixelPoint((int)state.X, (int)state.Y);
        Width = state.Width;
        Height = state.Height;
        if (state.State is >= 0 and <= 2)
        {
            WindowState = (WindowState)state.State;
        }

        var contentGrid = this.FindControl<Grid>("ContentGrid");
        if (contentGrid?.ColumnDefinitions.Count >= 3)
        {
            contentGrid.ColumnDefinitions[2].Width = new GridLength(state.PreviewColumnWidth, GridUnitType.Pixel);
        }

    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vmClose)
        {
            vmClose.PropertyChanged -= ViewModel_OnPropertyChanged;
        }

        var contentGrid = this.FindControl<Grid>("ContentGrid");
        var state = new WindowLayoutState
        {
            X = Position.X,
            Y = Position.Y,
            Width = Width,
            Height = Height,
            State = (int)WindowState,
            PreviewColumnWidth = 280
        };
        if (contentGrid?.ColumnDefinitions.Count is >= 3 &&
            contentGrid.ColumnDefinitions[2].Width.IsAbsolute)
        {
            state.PreviewColumnWidth = contentGrid.ColumnDefinitions[2].Width.Value;
        }


        state.Save();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.UiScale))
        {
            return;
        }

        if (sender is not MainWindowViewModel vm)
        {
            return;
        }

        if (WindowState != WindowState.Normal)
        {
            _lastUiScaleForWindow = vm.UiScale;
            return;
        }

        var newS = vm.UiScale;
        var oldS = _lastUiScaleForWindow;
        if (Math.Abs(newS - oldS) < 1e-9)
        {
            return;
        }

        _lastUiScaleForWindow = newS;
        Width *= newS / oldS;
        Height *= newS / oldS;
    }

    private void ExploreTreeHeaderSplitter_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is GridSplitter splitter && ReferenceEquals(e.Pointer.Captured, splitter))
        {
            SyncExploreTreeColumnWidthsFromHeader();
        }
    }

    private void ExploreTreeHeaderSplitter_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        SyncExploreTreeColumnWidthsFromHeader();
    }

    private void SyncExploreTreeColumnWidthsFromHeader()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var grid = this.FindControl<Grid>("ExploreTreeHeaderGrid");
        if (grid is null || grid.ColumnDefinitions.Count < 5)
        {
            return;
        }

        vm.ExploreTreeColumnResourceWidth = grid.ColumnDefinitions[0].Width;
        vm.ExploreTreeColumnMaterialsWidth = grid.ColumnDefinitions[2].Width;
        vm.ExploreTreeColumnFlagsWidth = grid.ColumnDefinitions[4].Width;
    }

    private void UpdateCornerRadiusFromCurrentState()
    {
        if (_rootBorder is null)
        {
            return;
        }


        bool useSquare = WindowState == WindowState.Maximized || IsWindowsSnapped();
        _rootBorder.CornerRadius = useSquare ? new CornerRadius(0) : new CornerRadius(RoundedCornerRadius);
    }

    /// <summary>
    /// True when the window is in a Windows snap layout (any preset: half, thirds, quarters, sixths on ultra-wide, etc.) so we use square corners.
    /// </summary>
    private bool IsWindowsSnapped()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }


        if (TryGetPlatformHandle()?.Handle is not { } hwnd)
        {

            return false;
        }


        try
        {
            if (IsZoomed(hwnd) != 0)
            {
                return true;
            }


            if (!GetWindowRect(hwnd, out var r))
            {
                return false;
            }


            IntPtr mon = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
            if (mon == IntPtr.Zero)
            {
                return false;
            }


            var mi = new MonitorInfo { cbSize = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(mon, ref mi))
            {

                return false;
            }


            int workW = mi.rcWork.Right - mi.rcWork.Left;
            int workH = mi.rcWork.Bottom - mi.rcWork.Top;
            int winW = r.Right - r.Left;
            int winH = r.Bottom - r.Top;
            int leftOffset = r.Left - mi.rcWork.Left;
            int topOffset = r.Top - mi.rcWork.Top;
            const int tolerance = 8;

            // Check if window matches a grid cell (2D grid: columns and rows 2,3,4,6) — covers half, thirds, quarters, sixths, and quarter snaps (top-left, bottom-left, etc.)
            for (int colDiv = 2; colDiv <= 6; colDiv++)
            {
                int colW = workW / colDiv;
                if (colW <= 0)
                {
                    continue;
                }


                for (int rowDiv = 2; rowDiv <= 6; rowDiv++)
                {
                    int rowH = workH / rowDiv;
                    if (rowH <= 0)
                    {
                        continue;
                    }


                    for (int cols = 1; cols <= colDiv; cols++)
                    {
                        int spanW = cols * colW;
                        if (Math.Abs(winW - spanW) > tolerance)
                        {
                            continue;
                        }


                        for (int rows = 1; rows <= rowDiv; rows++)
                        {
                            int spanH = rows * rowH;
                            if (Math.Abs(winH - spanH) > tolerance)
                            {
                                continue;
                            }


                            for (int startCol = 0; startCol <= colDiv - cols; startCol++)
                            {
                                if (Math.Abs(leftOffset - startCol * colW) > tolerance)
                                {
                                    continue;
                                }


                                for (int startRow = 0; startRow <= rowDiv - rows; startRow++)
                                {
                                    if (Math.Abs(topOffset - startRow * rowH) <= tolerance)
                                    {

                                        return true;
                                    }

                                }
                            }
                        }
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public uint cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern int IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    /// <summary>
    /// On Windows, add WS_THICKFRAME and WS_MAXIMIZEBOX to the window style so the window
    /// participates in Aero Snap (drag to edge/corner to snap). Safe no-op on other platforms.
    /// </summary>
    private void TryEnableWindowsSnap()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }


        if (TryGetPlatformHandle()?.Handle is not { } hwnd)
        {
            return;
        }


        try
        {
            const int gwlStyle = -16;
            const int wsThickframe = 0x00040000;
            const int wsMaximizebox = 0x00010000;
            const int wsMinimizebox = 0x00020000;
            int style = GetWindowLong(hwnd, gwlStyle);
            int newStyle = style | wsThickframe | wsMaximizebox | wsMinimizebox;
            if (newStyle != style)
            {
                NativeSetLastError(0);
                var previousStyle = SetWindowLong(hwnd, gwlStyle, newStyle);
                _ = previousStyle;
            }

        }
        catch
        {
            // Ignore if Win32 calls fail (e.g. handle invalid)
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
    private static extern void NativeSetLastError(uint dwErrCode);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private void TitleBarDragRegion_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }

    }

    private void ResizeWest_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(WindowEdge.West, e);
        }

    }

    private void ResizeEast_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(WindowEdge.East, e);
        }

    }

    private void ResizeSouth_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(WindowEdge.South, e);
        }

    }

    private void ResizeSouthWest_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(WindowEdge.SouthWest, e);
        }

    }

    private void ResizeSouthEast_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(WindowEdge.SouthEast, e);
        }

    }

    private void ResizeNorthWest_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(WindowEdge.NorthWest, e);
        }

    }

    private void ResizeNorthEast_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(WindowEdge.NorthEast, e);
        }

    }

    private void WindowMinimize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void WindowMaximize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void WindowClose_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && LogScrollViewer is { } scroll)
        {
            vm.LogLines.CollectionChanged += (_, _) =>
            {
                var now = DateTime.UtcNow;
                if ((now - _lastLogScrollUtc).TotalMilliseconds >= LogScrollThrottleMs)
                {
                    _lastLogScrollUtc = now;
                    scroll.ScrollToEnd();
                }
            };

            vm.ShowSemanticDebugDialog = ShowSemanticDebugWindow;
        }

        // Jump-to-top (Explorer) button: show when Explore tab is active and the tree list is scrolled.
        if (ExploreTreeScrollViewer is { } exploreTree && MainTabControl is { } tabs && JumpToTopButton is not null)
        {
            exploreTree.ScrollChanged += (_, _) => UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, exploreTree.Offset.Y);
            tabs.SelectionChanged += (_, _) => UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, exploreTree.Offset.Y);
            UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, exploreTree.Offset.Y);
        }
    }

    private void ShowSemanticDebugWindow(string text)
    {
        var window = new Window
        {
            Title = "MiniLM Semantic Match Debug",
            Width = 620,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = new SelectableTextBlock
                {
                    Text = text,
                    FontFamily = new Avalonia.Media.FontFamily("Cascadia Mono, Consolas, Courier New, monospace"),
                    FontSize = 12,
                    Margin = new Thickness(12),
                    TextWrapping = Avalonia.Media.TextWrapping.NoWrap
                }
            }
        };

        window.Show(this);
    }

    private void UpdateJumpToTopButtonVisibility(int selectedTabIndex, double exploreTreeOffsetY)
    {
        if (JumpToTopButton is null)
        {
            return;
        }

        var isExploreTab = selectedTabIndex == 0;
        var show = isExploreTab && exploreTreeOffsetY > JumpToTopThresholdPx;

        JumpToTopButton.IsVisible = show;
        JumpToTopButton.IsHitTestVisible = show;
        JumpToTopButton.Opacity = show ? 1 : 0;
    }

    private void JumpToTopButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ExploreTreeScrollViewer is { } tree)
        {
            tree.Offset = new Vector(tree.Offset.X, 0);
        }
    }

    private void OpenLogFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(LogService.LogsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = LogService.LogsDirectory,
                UseShellExecute = true
            });
        }
        catch
        {
            // Opening the log folder should never crash the app.
        }
    }

    private async void ExportTagRules_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Lang.Resources.ExportTagRules,
                DefaultExtension = "json",
                SuggestedFileName = "auto_pbr_custom_tag_rules.json",
                FileTypeChoices =
                [
                    new FilePickerFileType("JSON") { Patterns = ["*.json"] }
                ]
            });
            var path = file?.TryGetLocalPath();
            if (path is null)
            {
                return;
            }

            var json = JsonSerializer.Serialize(vm.CustomTagRules.ToList(), IndentedJsonSerializerOptions);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(true);
            vm.AppendUserLog(Lang.Resources.Log_TagRulesExported);
        }
        catch
        {
            // ignore
        }
    }

    private async void ImportTagRules_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null || DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Lang.Resources.ImportTagRules,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("JSON") { Patterns = ["*.json"] }
                ]
            });
            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path is null)
            {
                return;
            }

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(true);
            var err = vm.ImportCustomTagRulesFromJson(json);
            vm.AppendUserLog(err is not null
                ? string.Format(CultureInfo.InvariantCulture, LogTagRulesImportFailedFormat, err)
                : string.Format(CultureInfo.InvariantCulture, LogTagRulesImportedFormat, vm.CustomTagRules.Count));
        }
        catch
        {
            // ignore
        }
    }

    private async void BrowseInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return;
            }

            if (vm.UseBatchFolderInput)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = Lang.Resources.BatchFolderWatermark,
                    AllowMultiple = false
                });

                var folderPath = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
                if (folderPath is not null)
                {
                    vm.BatchFolderPath = folderPath;
                }
            }
            else
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select resource pack (.zip or .jar)",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Zip / JAR") { Patterns = ["*.zip", "*.jar"] }
                    ]
                });

                var filePath = files.Count > 0 ? files[0].TryGetLocalPath() : null;
                if (filePath is not null)
                {
                    vm.PackPath = filePath;
                }
            }
        }
        catch (Exception)
        {
            // Prevent unhandled exception in async void from crashing the process
        }
    }

    private async void BrowseOutput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return;
            }


            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select output folder",
                AllowMultiple = false
            });

            var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (path is null)
            {
                return;
            }


            if (DataContext is MainWindowViewModel vm)
            {
                vm.OutputDirectory = path;
            }

        }
        catch (Exception)
        {
            // Prevent unhandled exception in async void from crashing the process
        }
    }
}