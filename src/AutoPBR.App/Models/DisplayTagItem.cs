using AutoPBR.Core.Models;

using Avalonia.Media.Imaging;

namespace AutoPBR.App.Models;

public sealed class DisplayTagItem
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public TagRuleKind Kind { get; init; } = TagRuleKind.Material;
    /// <summary>Minecraft-style 16×16 texture when available; otherwise <see cref="IconGlyph"/>.</summary>
    public Bitmap? TagIcon { get; init; }
    public bool HasTagIcon => TagIcon is not null;
    public string IconGlyph { get; init; } = "\u25CF";
}
