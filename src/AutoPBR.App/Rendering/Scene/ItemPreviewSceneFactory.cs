using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.Scene;

public static class ItemPreviewSceneFactory
{
    public static PreviewMesh CreateMesh(PreviewRenderSettingsSnapshot settings, PreviewMaterial? material = null)
    {
        var thickness = Math.Max(0f, settings.SpriteThickness);
        if (settings.ItemFlatSpritePreview || settings.SpritePlaneCount <= 1)
        {
            if (thickness > 1e-6f &&
                material is { Width: > 0, Height: > 0 } &&
                !material.AlbedoRgba.IsEmpty)
            {
                return SpriteVoxelMeshCache.GetOrBuild(
                    material.AlbedoRgba.Span,
                    material.Width,
                    material.Height,
                    thickness,
                    settings.AlphaCutoff);
            }

            return PreviewMeshFactory.CreateItemPlane();
        }

        return PreviewMeshFactory.CreateSpritePlanes(
            planeCount: Math.Clamp(settings.SpritePlaneCount, 2, 8));
    }

    public static PreviewScene Create(PreviewRenderSettings settings, PreviewMaterial? material = null)
    {
        // Item voxel geometry is uploaded from cached CPU meshes on the GL thread; keep scene mesh lightweight.
        var mesh = settings.ItemFlatSpritePreview
            ? PreviewMeshFactory.CreateItemPlane(name: "item_plane_pending")
            : CreateMesh(PreviewRenderSettingsSnapshot.From(settings), material);
        var lightDir = BlockPreviewSceneFactory.LightDirectionFromYawPitch(settings.LightYawDegrees,
            settings.LightPitchDegrees);
        return new PreviewScene(
            PreviewSceneKind.ItemPlane,
            [mesh],
            new PreviewCamera { Position = new System.Numerics.Vector3(0, 0, 1.35f), Target = System.Numerics.Vector3.Zero },
            new PreviewLight { Direction = lightDir });
    }
}
