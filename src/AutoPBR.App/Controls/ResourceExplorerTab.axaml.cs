using System.ComponentModel;

using AutoPBR.App.Models;
using AutoPBR.App.ViewModels;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AutoPBR.App.Controls;

public partial class ResourceExplorerTab : UserControl
{
    public ResourceExplorerTab()
    {
        InitializeComponent();
    }

    public ScrollViewer? ExploreTreeScrollViewer
    {
        get
        {
            ExploreTreeView.ApplyTemplate();
            return ExploreTreeView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        }
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

    private void ExploreList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not ListBox list)
        {
            return;
        }

        var selected = new List<ArchiveNode>();
        if (list.SelectedItems is { } items)
        {
            foreach (var item in items)
            {
                if (item is ExploreListRow row)
                {
                    selected.Add(row.Node);
                }
            }
        }

        var primary = (list.SelectedItem as ExploreListRow)?.Node;
        vm.SyncExploreSelectionFromList(selected, primary);
    }

    private void ExploreRowContextMenu_OnOpening(object? sender, CancelEventArgs e)
    {
        if (sender is not ContextMenu menu)
        {
            return;
        }

        var row = menu.DataContext as ExploreListRow
                  ?? (menu.PlacementTarget as Control)?.DataContext as ExploreListRow;
        var node = row?.Node
                   ?? menu.DataContext as ArchiveNode
                   ?? (menu.PlacementTarget as Control)?.DataContext as ArchiveNode;
        node?.EnsureTagMenuItems();
    }
}
