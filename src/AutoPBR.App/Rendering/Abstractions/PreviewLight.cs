using System.Numerics;

namespace AutoPBR.App.Rendering.Abstractions;

public sealed class PreviewLight
{
    /// <summary>World-space direction toward the light (surface receives light from -Direction).</summary>
    public Vector3 Direction { get; init; } = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.35f));
    public Vector3 Color { get; init; } = Vector3.One;
}
