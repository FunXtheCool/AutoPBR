namespace AutoPBR.App.Models;

/// <summary>Context menu entry for tag add/remove on a file node.</summary>
public sealed class TagMenuEntry(ArchiveNode node, string tagId, bool isApplied, string menuHeader)
{
    public ArchiveNode Node { get; } = node;
    public string TagId { get; } = tagId;
    public bool IsApplied { get; } = isApplied;

    /// <summary>Menu item header (localized). Set by host when building entries.</summary>
    public string MenuHeader { get; } = menuHeader;
}
