using AutoPBR.App.ViewModels;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace AutoPBR.App.Views;

public partial class MainWindow : Window
{
    private const double JumpToTopThresholdPx = 220;

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
    private void WireExploreJumpToTopOnLoaded()
    {
        if (ExploreTreeScrollViewer is { } exploreTree && MainTabControl is { } tabs && JumpToTopButton is not null)
        {
            exploreTree.ScrollChanged += (_, _) => UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, exploreTree.Offset.Y);
            tabs.SelectionChanged += (_, _) => UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, exploreTree.Offset.Y);
            UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, exploreTree.Offset.Y);
        }
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
}
