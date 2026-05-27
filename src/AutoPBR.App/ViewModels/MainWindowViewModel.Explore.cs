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

public partial class MainWindowViewModel
{
    public bool IsBatchScanActive => HasScannedArchive && _exploreController.Data?.IsBatch == true;
    /// <summary>Search filter for the Resource Explorer tree (Explore tab). Filters nodes by path/name.</summary>
    [ObservableProperty] private string _exploreFilter = "";

    /// <summary>When set, Explore tree shows only files that have this tag (and their ancestor folders). Empty = no tag filter.</summary>
    [ObservableProperty] private string _exploreTagFilterId = "";

    /// <summary>Explore tree column widths (shared by header and rows via binding). Drag splitters in the header to resize.</summary>
    [ObservableProperty] private GridLength _exploreTreeColumnResourceWidth = new(1, GridUnitType.Star);

    [ObservableProperty] private GridLength _exploreTreeColumnMaterialsWidth = new(140, GridUnitType.Pixel);
    [ObservableProperty] private GridLength _exploreTreeColumnFlagsWidth = new(140, GridUnitType.Pixel);
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
    partial void OnPackPathChanged(string? value)
    {
        _ = value;
        ClearScannedArchive();
        RecomputeOutputZipPath();
        ConvertCommand.NotifyCanExecuteChanged();
        ScanArchiveCommand.NotifyCanExecuteChanged();
        ScanCurrentInputCommand.NotifyCanExecuteChanged();
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
}
