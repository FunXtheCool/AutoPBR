using System.Numerics;

namespace AutoPBR.App.Rendering.Abstractions;

public sealed class PreviewCamera
{
    /// <summary>
    /// Default orbit eye offset from the look target: front-three-quarter framing for living entities
    /// (head faces +X/+Z in preview space). Slightly biased on −Z so the view is not dead-on head-on.
    /// </summary>
    public static readonly Vector3 DefaultOrbitEyeOffsetFromTarget = new(-3.2f, 2.0f, -3.8f);

    public static float DefaultOrbitBoomArmDistance => DefaultOrbitEyeOffsetFromTarget.Length();

    /// <summary>Default eye position when the look target is at the world origin.</summary>
    public Vector3 Position { get; init; } = DefaultOrbitEyeOffsetFromTarget;

    public Vector3 Target { get; init; } = Vector3.Zero;
    public float FieldOfViewDegrees { get; init; } = 42f;
    public float NearPlane { get; init; } = 0.1f;
    public float FarPlane { get; init; } = 100f;
}
