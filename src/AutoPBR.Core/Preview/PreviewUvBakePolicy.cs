using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Resolved UV rules for <see cref="MinecraftModelBaker"/> face emission.
/// Production baselines are codified here; <see cref="UvDebugSettings"/> applies optional overrides only when set.
/// </summary>
internal readonly struct PreviewUvBakePolicy
{
    public bool FlipU { get; init; }
    public bool FlipV { get; init; }
    public bool SwapFaceNorthSouth { get; init; }
    public bool SwapFaceEastWest { get; init; }
    public bool SwapFaceUpDown { get; init; }
    public bool PreserveDirectionalBounds { get; init; }
    public bool UseBottomLeftUvOrigin { get; init; }
    public bool MapJavaCuboidFaceCorners { get; init; }
    /// <summary>Swap triangle index order so outward faces are CCW for GL back-face cull (block/item JSON quads).</summary>
    public bool ReverseFaceWinding { get; init; }
    public int UvCornerOrderMode { get; init; }
    public float OffsetUPixels { get; init; }
    public float OffsetVPixels { get; init; }
    public int GlobalFaceRotationDegrees { get; init; }

    /// <summary>
    /// Production policy for Java <c>ModelPart.Cube</c> entity sheets.
    /// </summary>
    public static PreviewUvBakePolicy EntityCuboidBaseline { get; } = new()
    {
        MapJavaCuboidFaceCorners = true,
        PreserveDirectionalBounds = true,
    };

    /// <summary>Block/item JSON models use bbox UV corners; quads need reversed winding vs Java corner order for GL CCW cull.</summary>
    public static PreviewUvBakePolicy BlockOrItemBaseline { get; } = new()
    {
        FlipV = true,
        PreserveDirectionalBounds = true,
        ReverseFaceWinding = true,
    };

    public static PreviewUvBakePolicy Resolve(MergedJavaBlockModel model)
    {
        var policy = UsesEntityTextures(model) ? EntityCuboidBaseline : BlockOrItemBaseline;
        return policy.WithDebugOverrides();
    }

    public PreviewUvBakePolicy WithDebugOverrides()
    {
        var policy = this;
        if (UvDebugSettings.TryGetFlipUOverride(out var flipU))
        {
            policy = policy with { FlipU = flipU };
        }

        if (UvDebugSettings.TryGetFlipVOverride(out var flipV))
        {
            policy = policy with { FlipV = flipV };
        }

        if (UvDebugSettings.TryGetSwapFaceNorthSouthOverride(out var swapNs))
        {
            policy = policy with { SwapFaceNorthSouth = swapNs };
        }

        if (UvDebugSettings.TryGetSwapFaceEastWestOverride(out var swapEw))
        {
            policy = policy with { SwapFaceEastWest = swapEw };
        }

        if (UvDebugSettings.TryGetSwapFaceUpDownOverride(out var swapUd))
        {
            policy = policy with { SwapFaceUpDown = swapUd };
        }

        if (UvDebugSettings.TryGetPreserveDirectionalBoundsOverride(out var preserve))
        {
            policy = policy with { PreserveDirectionalBounds = preserve };
        }

        if (UvDebugSettings.TryGetUseBottomLeftUvOriginOverride(out var bottomLeft))
        {
            policy = policy with { UseBottomLeftUvOrigin = bottomLeft };
        }

        if (UvDebugSettings.TryGetUvCornerOrderModeOverride(out var cornerMode))
        {
            policy = policy with { UvCornerOrderMode = cornerMode };
        }

        if (UvDebugSettings.TryGetOffsetUPixelsOverride(out var offsetU))
        {
            policy = policy with { OffsetUPixels = offsetU };
        }

        if (UvDebugSettings.TryGetOffsetVPixelsOverride(out var offsetV))
        {
            policy = policy with { OffsetVPixels = offsetV };
        }

        if (UvDebugSettings.TryGetGlobalFaceRotationDegreesOverride(out var rotation))
        {
            policy = policy with { GlobalFaceRotationDegrees = rotation };
        }

        return policy;
    }

    private static bool UsesEntityTextures(MergedJavaBlockModel model)
    {
        foreach (var tex in model.Textures.Values)
        {
            var path = tex.Replace('\\', '/').TrimStart('/');
            if (path.Contains("entity/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
