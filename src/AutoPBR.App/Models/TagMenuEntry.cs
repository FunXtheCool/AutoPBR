using Avalonia.Media.Imaging;
using AutoPBR.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPBR.App.Models;

/// <summary>Context menu row for tag add/remove on a file node: icon, label, checkbox.</summary>
public sealed class TagMenuEntry(ArchiveNode node, string tagId, string displayName, TagRuleKind kind, bool isApplied) : ObservableObject
{
    private bool _isApplied = isApplied;

    public string TagId { get; } = tagId;
    public string DisplayName { get; } = displayName;
    public TagRuleKind Kind { get; } = kind;
    public Bitmap? TagIcon { get; } = MaterialTagGlyphs.BitmapForTag(tagId, kind);
    public string IconGlyph { get; } = MaterialTagGlyphs.ForTagId(tagId, kind);
    public bool HasTagIcon => TagIcon is not null;

    public bool IsApplied
    {
        get => _isApplied;
        set
        {
            if (!SetProperty(ref _isApplied, value))
            {
                return;
            }

            node.ApplyTagMenuToggle(TagId, value);
        }
    }
}
