using AutoPBR.App.Views;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AutoPBR.App.Controls;

public partial class MainWindowTitleBar : UserControl
{
    public MainWindowTitleBar()
    {
        InitializeComponent();
    }

    private MainWindow? Host => VisualRoot as MainWindow;

    private void TitleBarDragRegion_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        Host?.HandleTitleBarDragRegionPointerPressed(sender, e);

    private void WindowMinimize_Click(object? sender, RoutedEventArgs e) =>
        Host?.HandleWindowMinimizeClick(sender, e);

    private void WindowMaximize_Click(object? sender, RoutedEventArgs e) =>
        Host?.HandleWindowMaximizeClick(sender, e);

    private void WindowClose_Click(object? sender, RoutedEventArgs e) =>
        Host?.HandleWindowCloseClick(sender, e);
}
