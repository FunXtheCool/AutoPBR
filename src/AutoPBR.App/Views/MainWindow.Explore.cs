using AutoPBR.App.Controls;

using Avalonia;
using Avalonia.Controls;

namespace AutoPBR.App.Views;

public partial class MainWindow : Window
{
    private const double JumpToTopThresholdPx = 220;

    private void WireExploreJumpToTopOnLoaded()
    {
        var exploreTree = ResourceExplorer?.ExploreTreeScrollViewer;
        if (exploreTree is not null && MainTabControl is { } tabs && JumpToTopButton is not null)
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
        if (ResourceExplorer?.ExploreTreeScrollViewer is { } tree)
        {
            tree.Offset = new Vector(tree.Offset.X, 0);
        }
    }
}
