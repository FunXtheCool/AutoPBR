namespace AutoPBR.Core.Models;

/// <summary>How a block texture path is expected to resolve in 3D preview.</summary>
public enum BlockPreviewCoverageKind
{
    Unknown = 0,
    HasPackModelJson,
    ParityCatalogSynthesizable,
    PackModelJsonOnly,
}

/// <summary>Lightweight model metadata discovered in an archive scan.</summary>
public sealed record ArchiveModelInventory(
    IReadOnlySet<string> BlockModelJsonPaths,
    IReadOnlyDictionary<string, BlockPreviewCoverageKind> BlockTextureCoverageByPath)
{
    public static ArchiveModelInventory Empty { get; } = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, BlockPreviewCoverageKind>(StringComparer.OrdinalIgnoreCase));
}
