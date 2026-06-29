using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering;

/// <summary>Diffuse alpha handling for 3D preview subjects (block models, entity rigs).</summary>
internal static class PreviewSubjectAlphaPolicy
{
    internal static int ResolveAlphaModeUniform(PreviewSceneKind sceneKind, bool entityEmulatedPreview, PreviewEntityAlphaMode entityAlphaMode)
    {
        if (entityEmulatedPreview)
        {
            return (int)entityAlphaMode;
        }

        return sceneKind is PreviewSceneKind.BlockCube or PreviewSceneKind.BlockModel
            ? (int)PreviewEntityAlphaMode.Cutout
            : 0;
    }
}
