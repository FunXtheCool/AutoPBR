using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Rendering;

public static class PreviewMaterialMapper
{
    public static PreviewMaterial FromCoreMaps(PreviewTextureMaps maps) =>
        new()
        {
            Width = maps.Width,
            Height = maps.Height,
            AlbedoRgba = maps.DiffuseRgba,
            NormalRgba = maps.NormalRgba,
            SpecularRgba = maps.SpecularRgba,
            HeightRgba = maps.HeightRgba,
            IsPlantForNoHeight = maps.IsPlantForNoHeight,
            Sprite2DFoliageTarget = maps.Sprite2DFoliageTarget
        };
}
