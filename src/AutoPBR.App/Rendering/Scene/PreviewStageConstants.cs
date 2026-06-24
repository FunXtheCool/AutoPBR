namespace AutoPBR.App.Rendering.Scene;

/// <summary>Shared layout for preview grid, ground plane, and orbit framing.</summary>
public static class PreviewStageConstants
{
    public const float GridHalfExtent = 14f;
    public const float GridStep = 0.5f;

    /// <summary>XZ plane where background grid lines sit.</summary>
    public const float GridWorldY = -0.56f;

    /// <summary>Horizontal grass plane slightly below <see cref="GridWorldY"/> so lines read above the turf.</summary>
    public const float GroundPlaneWorldY = -0.572f;

    /// <summary>One full 16×16 grass tile per world unit (matches unit cube / block scale).</summary>
    public const float MetersPerGrassTile = 1f;

    /// <summary>Half extent of the 1×1 item/sprite preview card in world units.</summary>
    public const float SpritePlaneHalfSize = 0.5f;

    /// <summary>Minimum sprite cuboid depth (0 = single-sided plane).</summary>
    public const double SpriteThicknessMin = 0.0;

    /// <summary>Maximum sprite cuboid depth (~25% of the 1×1 face width; at max, voxel depth matches texel size).</summary>
    public const double SpriteThicknessMax = 0.25;

    /// <summary>UI step for the sprite thickness slider and numeric field.</summary>
    public const double SpriteThicknessStep = 0.002;

    /// <summary>Debounce before rebuilding per-texel sprite voxel meshes after slider drags.</summary>
    public const int SpriteThicknessMeshDebounceMs = 200;

    /// <summary>Base Y for the volumetric cloud layer before user height offset.</summary>
    public const float CloudLayerBaseY = 18f;

    public static float CloudLayerBaseWorldY(float layerHeightOffset) => CloudLayerBaseY + layerHeightOffset;

    /// <summary>World-anchored ground mist slab height above <see cref="GroundPlaneWorldY"/>.</summary>
    public const float GroundFogSlabHeight = 4f;
}
