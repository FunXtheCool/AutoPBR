using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AutoPBR.Core.Atlas;

public readonly record struct AtlasTile(int Column, int Row, Rectangle Bounds);

public static class AtlasTiling
{
    public static readonly AtlasPlan None = new(false, 0, 0, 0, AtlasDecisionReason.None, 0f);
    public const int DefaultMinTileSize = 16;

    public static AtlasPlan Decide(
        int width,
        int height,
        bool? explicitAtlasEnabled = null,
        int minTileSize = DefaultMinTileSize,
        int? preferredTileSize = null)
    {
        if (width <= 0 || height <= 0)
        {
            return None;
        }

        if (explicitAtlasEnabled == false)
        {
            return new AtlasPlan(false, 0, 0, 0, AtlasDecisionReason.ExplicitDisabled, 1f);
        }

        if (preferredTileSize is > 0)
        {
            var preferred = preferredTileSize.Value;
            if (width == preferred && height == preferred)
            {
                return None;
            }

            if (width % preferred == 0 && height % preferred == 0)
            {
                var cols = width / preferred;
                var rows = height / preferred;
                if (cols * rows > 1)
                {
                    return new AtlasPlan(true, preferred, cols, rows, AtlasDecisionReason.GeometryInferred, 0.97f);
                }
            }

            minTileSize = Math.Max(minTileSize, preferred);
        }

        var inferred = TryInferPlan(width, height, minTileSize);
        if (explicitAtlasEnabled == true)
        {
            if (inferred.IsAtlas)
            {
                return inferred with { Reason = AtlasDecisionReason.ExplicitEnabled, Confidence = 1f };
            }

            // Explicit opt-in: still return a valid plan if divisible by the smallest allowed power-of-two.
            var fallbackTile = GreatestPowerOfTwoDivisor(Math.Min(width, height));
            if (fallbackTile >= minTileSize && width % fallbackTile == 0 && height % fallbackTile == 0)
            {
                return new AtlasPlan(
                    true,
                    fallbackTile,
                    width / fallbackTile,
                    height / fallbackTile,
                    AtlasDecisionReason.ExplicitEnabled,
                    1f);
            }
        }

        return inferred;
    }

    public static AtlasPlan TryInferPlan(int width, int height, int minTileSize = DefaultMinTileSize)
    {
        if (width <= 0 || height <= 0)
        {
            return None;
        }

        var maxTile = HighestPowerOfTwoAtMost(Math.Min(width, height));
        for (var tile = maxTile; tile >= minTileSize; tile >>= 1)
        {
            if (width % tile != 0 || height % tile != 0)
            {
                continue;
            }

            var cols = width / tile;
            var rows = height / tile;
            if (cols * rows <= 1)
            {
                continue;
            }

            var nonSquareBoost = width == height ? 0f : 0.1f;
            var confidence = Math.Clamp(0.85f + nonSquareBoost, 0f, 0.99f);
            return new AtlasPlan(true, tile, cols, rows, AtlasDecisionReason.GeometryInferred, confidence);
        }

        return None;
    }

    public static IEnumerable<AtlasTile> EnumerateTiles(AtlasPlan plan)
    {
        if (!plan.IsAtlas)
        {
            yield break;
        }

        for (var row = 0; row < plan.Rows; row++)
        {
            for (var col = 0; col < plan.Columns; col++)
            {
                yield return new AtlasTile(col, row, new Rectangle(col * plan.TileSize, row * plan.TileSize, plan.TileSize, plan.TileSize));
            }
        }
    }

    public static Image<Rgba32> ExtractTile(Image<Rgba32> atlas, AtlasTile tile) =>
        atlas.Clone(ctx => ctx.Crop(tile.Bounds));

    public static void PasteTile(Image<Rgba32> destination, AtlasTile tile, Image<Rgba32> tileImage) =>
        destination.Mutate(ctx => ctx.DrawImage(tileImage, tile.Bounds.Location, 1f));

    private static int HighestPowerOfTwoAtMost(int value)
    {
        if (value < 1)
        {
            return 0;
        }

        var power = 1;
        while ((power << 1) > 0 && (power << 1) <= value)
        {
            power <<= 1;
        }

        return power;
    }

    private static int GreatestPowerOfTwoDivisor(int value)
    {
        if (value <= 0)
        {
            return 0;
        }

        return value & -value;
    }
}
