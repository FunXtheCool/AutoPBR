using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.Scene;

public static class BlockPreviewSceneFactory
{
    public static PreviewScene Create(PreviewRenderSettings settings)
    {
        var mesh = PreviewMeshFactory.CreateUnitCube();
        var lightDir = LightDirectionFromYawPitch(settings.LightYawDegrees, settings.LightPitchDegrees);
        return new PreviewScene(
            PreviewSceneKind.BlockCube,
            [mesh],
            new PreviewCamera(),
            new PreviewLight { Direction = lightDir });
    }

    public static Vector3 LightDirectionFromYawPitch(float yawDeg, float pitchDeg)
    {
        var yaw = yawDeg * (MathF.PI / 180f);
        var pitch = pitchDeg * (MathF.PI / 180f);
        var x = MathF.Cos(pitch) * MathF.Sin(yaw);
        var y = MathF.Sin(pitch);
        var z = MathF.Cos(pitch) * MathF.Cos(yaw);
        var v = new Vector3(x, y, z);
        return v.LengthSquared() > 1e-8f ? Vector3.Normalize(v) : new Vector3(-0.35f, -0.85f, -0.4f);
    }
}
