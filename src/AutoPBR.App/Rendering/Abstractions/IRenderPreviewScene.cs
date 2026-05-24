namespace AutoPBR.App.Rendering.Abstractions;

public interface IRenderPreviewScene
{
    PreviewSceneKind SceneKind { get; }
    IReadOnlyList<PreviewMesh> Meshes { get; }
    PreviewCamera Camera { get; }
    PreviewLight Light { get; }
}
