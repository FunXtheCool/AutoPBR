namespace AutoPBR.Core.Models;

/// <summary>
/// A tag rule: when a texture's Name or RelativeKey contains any of the keywords (case-insensitive),
/// the associated overrides are applied during conversion.
/// </summary>
public sealed class TagRule
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = [];
    public TextureOverrides Overrides { get; init; } = new();
}
