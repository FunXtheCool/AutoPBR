using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.Scene;

public sealed class PreviewScene(
    PreviewSceneKind kind,
    IReadOnlyList<PreviewMesh> meshes,
    PreviewCamera camera,
    PreviewLight light) : IRenderPreviewScene
{
    public PreviewSceneKind SceneKind { get; } = kind;
    public IReadOnlyList<PreviewMesh> Meshes { get; } = meshes;
    public PreviewCamera Camera { get; } = camera;
    public PreviewLight Light { get; } = light;
}
