using System.Numerics;

namespace AutoPBR.App.Rendering.Abstractions;

public sealed class PreviewCamera
{
    /// <summary>Default eye position for block preview: pulled back so the unit cube is comfortably framed.</summary>
    public Vector3 Position { get; init; } = new(3.6f, 2.6f, 3.6f);

    public Vector3 Target { get; init; } = Vector3.Zero;
    public float FieldOfViewDegrees { get; init; } = 42f;
    public float NearPlane { get; init; } = 0.1f;
    public float FarPlane { get; init; } = 100f;
}
