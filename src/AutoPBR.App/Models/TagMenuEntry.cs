using Avalonia.Media.Imaging;
using AutoPBR.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPBR.App.Models;

/// <summary>Context menu row for tag add/remove on a file node: icon, label, checkbox.</summary>
public sealed class TagMenuEntry : ObservableObject
{
    private readonly ArchiveNode _node;
    private bool _isApplied;

    public TagMenuEntry(ArchiveNode node, string tagId, string displayName, TagRuleKind kind, bool isApplied)
    {
        _node = node;
        TagId = tagId;
        DisplayName = displayName;
        Kind = kind;
        _isApplied = isApplied;
        TagIcon = MaterialTagGlyphs.BitmapForTag(tagId, kind);
        IconGlyph = MaterialTagGlyphs.ForTagId(tagId, kind);
    }

    public string TagId { get; }
    public string DisplayName { get; }
    public TagRuleKind Kind { get; }
    public Bitmap? TagIcon { get; }
    public string IconGlyph { get; }
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

            _node.ApplyTagMenuToggle(TagId, value);
        }
    }
}
