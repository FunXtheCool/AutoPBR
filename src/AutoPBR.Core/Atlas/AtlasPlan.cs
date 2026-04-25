namespace AutoPBR.Core.Atlas;

public enum AtlasDecisionReason
{
    None = 0,
    ExplicitDisabled = 1,
    ExplicitEnabled = 2,
    GeometryInferred = 3
}

public readonly record struct AtlasPlan(
    bool IsAtlas,
    int TileSize,
    int Columns,
    int Rows,
    AtlasDecisionReason Reason,
    float Confidence)
{
    public int TileCount => Columns * Rows;
    public bool HasMultipleTiles => TileCount > 1;
}
