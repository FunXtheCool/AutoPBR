using AutoPBR.App.ViewModels;

using Avalonia.Controls;
using Avalonia.Input;

namespace AutoPBR.App.Controls;

public partial class ResourceExplorerTab : UserControl
{
    public ResourceExplorerTab()
    {
        InitializeComponent();
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
}
