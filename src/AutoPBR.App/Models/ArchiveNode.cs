using System.Collections.ObjectModel;

using Avalonia.Media.Imaging;
using AutoPBR.Core.Models;
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
    [ObservableProperty] private bool _isSelected;
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

    /// <summary>Material tag glyphs for the Explore row. Mutated by <see cref="RefreshDisplayTags"/> so ItemsControl sees collection changes.</summary>
    public ObservableCollection<DisplayTagItem> MaterialDisplayTags { get; } = new();

    /// <summary>Flag tag glyphs for the Explore row. Mutated by <see cref="RefreshDisplayTags"/> so ItemsControl sees collection changes.</summary>
    public ObservableCollection<DisplayTagItem> FlagDisplayTags { get; } = new();

    /// <summary>Effective tags for this path (menus, diagnostics). Mirrors <see cref="MaterialDisplayTags"/> and <see cref="FlagDisplayTags"/>.</summary>
    public IReadOnlyList<DisplayTagItem> DisplayTags =>
        IsFolder ? [] : MaterialDisplayTags.Concat(FlagDisplayTags).ToList();

    /// <summary>Call when host tag add/remove changed so the UI re-reads display tags and context menu rows.</summary>
    public void RefreshDisplayTags()
    {
        MaterialDisplayTags.Clear();
        FlagDisplayTags.Clear();
        if (!IsFolder && host is not null)
        {
            foreach (var t in host.GetEffectiveTags(FullPath))
            {
                var item = new DisplayTagItem
                {
                    Id = t.Id,
                    DisplayName = t.DisplayName,
                    Kind = t.Kind,
                    TagIcon = MaterialTagGlyphs.BitmapForTag(t.Id, t.Kind),
                    IconGlyph = MaterialTagGlyphs.ForTagId(t.Id, t.Kind)
                };
                if (t.Kind == TagRuleKind.Material)
                {
                    MaterialDisplayTags.Add(item);
                }
                else
                {
                    FlagDisplayTags.Add(item);
                }
            }
        }

        OnPropertyChanged(nameof(DisplayTags));
        RebuildTagMenuItems();
    }

    /// <summary>Material tag rows for the context submenu (icon, label, checkbox).</summary>
    public ObservableCollection<TagMenuEntry> MaterialTagMenuItems { get; } = new();

    /// <summary>Flag tag rows for the context submenu (icon, label, checkbox).</summary>
    public ObservableCollection<TagMenuEntry> FlagTagMenuItems { get; } = new();

    /// <summary>Syncs manual add/remove from a checkbox; host refreshes effective tags.</summary>
    internal void ApplyTagMenuToggle(string tagId, bool wantApplied)
    {
        if (host is null || IsFolder)
        {
            return;
        }

        var applied = MaterialDisplayTags.Concat(FlagDisplayTags)
            .Any(t => string.Equals(t.Id, tagId, StringComparison.OrdinalIgnoreCase));
        if (wantApplied == applied)
        {
            return;
        }

        host.ApplyManualTagToggle(FullPath, tagId, wantApplied);
    }

    private void RebuildTagMenuItems()
    {
        MaterialTagMenuItems.Clear();
        FlagTagMenuItems.Clear();
        if (IsFolder || host is null)
        {
            return;
        }

        var rules = host.GetTagRules();
        foreach (var r in rules.Where(r => r.Kind == TagRuleKind.Material))
        {
            var isApplied = MaterialDisplayTags.Any(t => string.Equals(t.Id, r.Id, StringComparison.OrdinalIgnoreCase));
            MaterialTagMenuItems.Add(new TagMenuEntry(this, r.Id, r.DisplayName, TagRuleKind.Material, isApplied));
        }

        foreach (var r in rules.Where(r => r.Kind == TagRuleKind.Flag))
        {
            var isApplied = FlagDisplayTags.Any(t => string.Equals(t.Id, r.Id, StringComparison.OrdinalIgnoreCase));
            FlagTagMenuItems.Add(new TagMenuEntry(this, r.Id, r.DisplayName, TagRuleKind.Flag, isApplied));
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

            // Pre-load one level under immediate subfolders so expand arrows exist and nested folders can open.
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
