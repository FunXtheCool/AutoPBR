using System.Collections.ObjectModel;

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPBR.App.Models;

/// <summary>Lazy node in the scanned archive tree. Children are loaded on expand; override state is stored in the host.</summary>
public partial class ArchiveNode(
    string name,
    string fullPath,
    bool isFolder,
    ArchiveNode? parent,
    IArchiveNodeHost? host,
    bool isBatchPackRoot = false) : ObservableObject
{
    public string Name { get; } = name;
    public string FullPath { get; } = fullPath;
    public bool IsFolder { get; } = isFolder;
    public bool IsBatchPackRoot { get; } = isBatchPackRoot;
    public ArchiveNode? Parent { get; set; } = parent;
    public ObservableCollection<ArchiveNode> Children { get; } = new();

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private Bitmap? _packIcon;

    /// <summary>When true, this node is shown in the tree; when false, hidden by the Resource Explorer search filter.</summary>
    [ObservableProperty] private bool _isVisibleByFilter = true;

    /// <summary>True when this node has a custom icon (e.g. batch pack thumbnail from pack.png).</summary>
    public bool HasPackIcon => PackIcon is not null;

    /// <summary>Include/exclude override: null = use rules, true = include, false = exclude. Stored in host, not in node.</summary>
    public bool? ManualOverride
    {
        get => host?.GetOverride(FullPath);
        set
        {
            if (host is null)
            {
                return;
            }

            host.SetOverride(FullPath, value);
            OnPropertyChanged();
        }
    }

    /// <summary>Call when host overrides were updated externally so the checkbox binding re-reads ManualOverride.</summary>
    public void NotifyOverrideChanged() => OnPropertyChanged(nameof(ManualOverride));

    /// <summary>Effective tags for this path (from host). Empty for folders. Call RefreshDisplayTags when host tag overrides change.</summary>
    public IReadOnlyList<DisplayTagItem> DisplayTags => IsFolder
        ? []
        : (host?.GetEffectiveTags(FullPath) ?? []).Select(t => new DisplayTagItem { Id = t.Id, DisplayName = t.DisplayName }).ToList();

    /// <summary>Call when host tag add/remove changed so the UI re-reads DisplayTags and TagMenuItems.</summary>
    public void RefreshDisplayTags()
    {
        OnPropertyChanged(nameof(DisplayTags));
        OnPropertyChanged(nameof(TagMenuEntries));
    }

    /// <summary>For context menu: entries with Node reference for commands. Refreshes when DisplayTags changes.</summary>
    public IReadOnlyList<TagMenuEntry> TagMenuEntries
    {
        get
        {
            if (IsFolder || host is null)
            {
                return [];
            }

            var rules = host.GetTagRules();
            var displayTags = DisplayTags;
            return rules
                .Select(r =>
                {
                    var isApplied = displayTags.Any(t => string.Equals(t.Id, r.Id, StringComparison.OrdinalIgnoreCase));
                    var menuHeader = host.GetTagMenuHeader(r.DisplayName, isApplied);
                    return new TagMenuEntry(this, r.Id, isApplied, menuHeader);
                })
                .ToList();
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (!IsFolder)
        {
            return;
        }

        if (value && host is not null)
        {
            // Load this folder's children if needed.
            if (Children.Count == 0)
            {
                host.EnsureChildrenLoaded(this);
            }

            // Also pre-load one level of children for immediate subfolders
            // so their expand/collapse arrows are visible right away.
            foreach (var child in Children)
            {
                if (child.IsFolder)
                {
                    host.EnsureChildrenLoaded(child);
                }
            }
        }
    }

    partial void OnPackIconChanged(Bitmap? value)
    {
        _ = value;
        OnPropertyChanged(nameof(HasPackIcon));
    }
}
