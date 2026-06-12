using System.Diagnostics;

using AutoPBR.App.Controls;
using AutoPBR.App.Services;
using AutoPBR.App.ViewModels;

using Avalonia;
using Avalonia.Controls;

namespace AutoPBR.App.Views;

public partial class MainWindow : Window
{
    private const int LogScrollThrottleMs = 200;
    private DateTime _lastLogScrollUtc = DateTime.MinValue;
    private UvDebugWindow? _uvDebugWindow;
    private EntityPreviewDebugWindow? _entityPreviewDebugWindow;
    private Preview3DCameraHelpWindow? _preview3DCameraHelpWindow;
    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && LogScrollViewer is { } scroll)
        {
            if (this.FindControl<GlPbrPreviewControl>("GlPbrPreview") is { } glPreview)
            {
                vm.RegisterGlPreview(glPreview);
            }

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

        WireExploreJumpToTopOnLoaded();
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

    private void OpenUvDebugWindow_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (_uvDebugWindow is { IsVisible: true })
        {
            _uvDebugWindow.Activate();
            return;
        }

        var window = new UvDebugWindow
        {
            DataContext = new UvDebugWindowViewModel(vm)
        };
        window.Closed += (_, _) => _uvDebugWindow = null;
        _uvDebugWindow = window;
        window.Show(this);
    }

    private void OpenEntityPreviewDebugWindow_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (_entityPreviewDebugWindow is { IsVisible: true })
        {
            _entityPreviewDebugWindow.Activate();
            return;
        }

        var window = new EntityPreviewDebugWindow
        {
            DataContext = new EntityPreviewDebugWindowViewModel(vm)
        };
        window.Closed += (_, _) => _entityPreviewDebugWindow = null;
        _entityPreviewDebugWindow = window;
        window.Show(this);
    }

    private void OpenPreview3DCameraHelpWindow_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (_preview3DCameraHelpWindow is { IsVisible: true })
        {
            _preview3DCameraHelpWindow.Activate();
            return;
        }

        var window = new Preview3DCameraHelpWindow
        {
            DataContext = new Preview3DCameraHelpWindowViewModel(vm)
        };
        window.Closed += (_, _) => _preview3DCameraHelpWindow = null;
        _preview3DCameraHelpWindow = window;
        window.Show(this);
    }
}
