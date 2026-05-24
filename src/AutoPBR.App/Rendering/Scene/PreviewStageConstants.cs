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
}
