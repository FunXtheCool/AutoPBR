using System.Diagnostics;
using System.Runtime.InteropServices;
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
    private const int LogScrollThrottleMs = 200;
    private DateTime _lastLogScrollUtc = DateTime.MinValue;

    private const double RoundedCornerRadius = 8;
    private const double JumpToTopThresholdPx = 220;
    private Border? _rootBorder;

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
                SetWindowLong(hwnd, gwlStyle, newStyle);
            }

        }
        catch
        {
            // Ignore if Win32 calls fail (e.g. handle invalid)
        }
    }

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
        }

        // Jump-to-top (Explorer) button: show only when we're in the Explore tab and the main scroll is down.
        if (MainScrollViewer is { } mainScroll && MainTabControl is { } tabs && JumpToTopButton is not null)
        {
            mainScroll.ScrollChanged += (_, _) => UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, mainScroll.Offset.Y);
            tabs.SelectionChanged += (_, _) => UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, mainScroll.Offset.Y);
            UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, mainScroll.Offset.Y);
        }
    }

    private void UpdateJumpToTopButtonVisibility(int selectedTabIndex, double scrollOffsetY)
    {
        if (JumpToTopButton is null)
        {
            return;
        }

        var isExploreTab = selectedTabIndex == 1; // Scan, Explore, Tune, Settings
        var show = isExploreTab && scrollOffsetY > JumpToTopThresholdPx;

        JumpToTopButton.IsVisible = show;
        JumpToTopButton.IsHitTestVisible = show;
        JumpToTopButton.Opacity = show ? 1 : 0;
    }

    private void JumpToTopButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MainScrollViewer is null)
        {
            return;
        }

        // Scroll the main content area back to the top so Explore filters are immediately visible again.
        MainScrollViewer.Offset = new Vector(MainScrollViewer.Offset.X, 0);
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

            var json = JsonSerializer.Serialize(vm.CustomTagRules.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
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
                ? string.Format(Lang.Resources.Log_TagRulesImportFailed, err)
                : string.Format(Lang.Resources.Log_TagRulesImported, vm.CustomTagRules.Count));
        }
        catch
        {
            // ignore
        }
    }

    private async void BrowsePack_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return;
            }


            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select resource pack (.zip or .jar)",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Zip / JAR") { Patterns = ["*.zip", "*.jar"] }
                ]
            });

            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path is null)
            {
                return;
            }


            if (DataContext is MainWindowViewModel vm)
            {
                vm.PackPath = path;
            }

        }
        catch (Exception)
        {
            // Prevent unhandled exception in async void from crashing the process
        }
    }

    private async void BrowseBatchFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
                Title = Lang.Resources.BatchFolderWatermark,
                AllowMultiple = false
            });

            var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (path is null)
            {
                return;
            }

            if (DataContext is MainWindowViewModel vm)
            {
                vm.BatchFolderPath = path;
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