using JetBrains.Annotations;

namespace AutoPBR.App.Rendering.Abstractions;

public sealed class RenderPreviewInitializationOptions
{
    /// <summary>Optional MSAA samples for the preview FBO; null uses platform default.</summary>
    [UsedImplicitly]
    public int? MsaaSamples { get; init; }
}
