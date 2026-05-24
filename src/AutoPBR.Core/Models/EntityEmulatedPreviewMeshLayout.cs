namespace AutoPBR.Core.Models;

/// <summary>
/// Vertex layout for GPU-skinned emulated entity preview (must match <c>MinecraftModelBaker.FloatsPerSkinnedVertex</c>).
/// The last float-sized word stores the raw IEEE bits of the element bone index (see baker: <c>BitConverter.Int32BitsToSingle</c>) for OpenGL integer vertex attributes.
/// </summary>
public static class EntityEmulatedPreviewMeshLayout
{
    public const int SkinnedFloatsPerVertex = 13;
}
