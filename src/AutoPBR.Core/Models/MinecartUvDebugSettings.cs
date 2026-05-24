namespace AutoPBR.Core.Models;

/// <summary>
/// Runtime debug toggles for UV mapping in preview baking.
/// These are global and apply to all baked models (blocks/items/entities).
/// </summary>
public static class UvDebugSettings
{
    /// <summary>Swap U bounds (<c>u0 &lt;-&gt; u1</c>).</summary>
    public static bool FlipU { get; set; }

    /// <summary>Swap V bounds (<c>v0 &lt;-&gt; v1</c>).</summary>
    public static bool FlipV { get; set; } = true;

    /// <summary>Translate UV bounds in source texture pixels along U.</summary>
    public static double OffsetUPixels { get; set; }

    /// <summary>Translate UV bounds in source texture pixels along V.</summary>
    public static double OffsetVPixels { get; set; }

    /// <summary>Additional UV rotation (degrees) applied to all faces. Valid values: 0, 90, 180, 270.</summary>
    public static int GlobalFaceRotationDegrees { get; set; }

    /// <summary>Swap semantic face routing for north/south before UV projection.</summary>
    public static bool SwapFaceNorthSouth { get; set; }

    /// <summary>Swap semantic face routing for east/west before UV projection.</summary>
    public static bool SwapFaceEastWest { get; set; }

    /// <summary>Swap semantic face routing for up/down before UV projection.</summary>
    public static bool SwapFaceUpDown { get; set; } = true;

    /// <summary>
    /// If true, preserve directional UV bounds (u0/u1, v0/v1). If false, canonicalize with min/max.
    /// </summary>
    public static bool PreserveDirectionalBounds { get; set; } = true;

    /// <summary>
    /// If true, convert from top-left texture space to GL bottom-left (Nv = 1 - py/h).
    /// If false, keep top-left orientation (Nv = py/h) for diagnostics.
    /// </summary>
    public static bool UseBottomLeftUvOrigin { get; set; }

    /// <summary>
    /// Corner order mode for UV assignment:
    /// 0=Default, 1=Rotate90, 2=Rotate180, 3=Rotate270, 4=ReverseWinding.
    /// </summary>
    public static int UvCornerOrderMode { get; set; }
}

