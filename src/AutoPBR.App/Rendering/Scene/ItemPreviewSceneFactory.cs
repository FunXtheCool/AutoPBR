using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.Scene;

public static class ItemPreviewSceneFactory
{
    public static PreviewScene Create(PreviewRenderSettings settings)
    {
        var mesh = settings.SpritePlaneCount <= 1
            ? PreviewMeshFactory.CreateItemPlane()
            : PreviewMeshFactory.CreateSpritePlanes(planeCount: settings.SpritePlaneCount);
        var lightDir = BlockPreviewSceneFactory.LightDirectionFromYawPitch(settings.LightYawDegrees,
            settings.LightPitchDegrees);
        return new PreviewScene(
            PreviewSceneKind.ItemPlane,
            [mesh],
            new PreviewCamera { Position = new System.Numerics.Vector3(0, 0, 1.35f), Target = System.Numerics.Vector3.Zero },
            new PreviewLight { Direction = lightDir });
    }
}
