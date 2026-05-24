using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.Scene;

public static class BlockModelPreviewSceneFactory
{
    public static PreviewScene Create(PreviewRenderSettings settings, PreviewMesh bakedSubject)
    {
        var lightDir = BlockPreviewSceneFactory.LightDirectionFromYawPitch(settings.LightYawDegrees,
            settings.LightPitchDegrees);
        return new PreviewScene(
            PreviewSceneKind.BlockModel,
            [bakedSubject],
            new PreviewCamera(),
            new PreviewLight { Direction = lightDir });
    }
}
