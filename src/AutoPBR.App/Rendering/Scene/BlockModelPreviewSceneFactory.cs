using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.Scene;

public static class BlockModelPreviewSceneFactory
{
    public static PreviewScene Create(
        PreviewRenderSettings settings,
        PreviewMesh bakedSubject,
        Vector3? orbitTarget = null)
    {
        var lightDir = BlockPreviewSceneFactory.LightDirectionFromYawPitch(settings.LightYawDegrees,
            settings.LightPitchDegrees);
        var target = orbitTarget ?? Vector3.Zero;
        return new PreviewScene(
            PreviewSceneKind.BlockModel,
            [bakedSubject],
            new PreviewCamera
            {
                Target = target,
                Position = target + PreviewCamera.DefaultOrbitEyeOffsetFromTarget,
            },
            new PreviewLight { Direction = lightDir });
    }
}
