namespace AutoPBR.Core.Models;

public sealed class TextureWorkItem
{
    public required string FullPath { get; init; }          // e.g. ...\stone.png
    public required string DirectoryPath { get; init; }     // e.g. ...\block\
    public required string Name { get; init; }              // e.g. stone
    public required string Extension { get; init; }         // e.g. .png
    public required string RelativeKey { get; init; }       // e.g. \block\stone

    /// <summary>
    /// When true, only specular is generated (e.g. particles); normals and height are skipped.
    /// </summary>
    public bool SpecularOnly { get; init; }

    /// <summary>When true (FoliageMode No Height on 2D Sprite targets), normal is written but height is not packed to alpha.</summary>
    public bool IsPlantForNoHeight { get; init; }

    /// <summary>
    /// When true, Explore would show the <c>sprite_2d</c> flag — Foliage mode (Ignore All / No Height / Convert All) applies to this texture only.
    /// </summary>
    public bool Sprite2DFoliageTarget { get; init; }

    /// <summary>When true, texture has the organic material tag (or OptiFine plant/plants path) for extra porosity bias.</summary>
    public bool HasPlantMaterialTag { get; init; }

    /// <summary>When true, effective material tags include <c>brick</c> — enables structural mortar height post-processing.</summary>
    public bool HasBrickMaterialTag { get; init; }

    /// <summary>When true, effective flags include <c>uv_wrap</c>; atlas split/stitch is only allowed for these textures.</summary>
    public bool HasUvWrap { get; init; }

    /// <summary>Dominant pack texture resolution (16/32/64...), used as preferred atlas tile size.</summary>
    public int? PackBaseTileSize { get; init; }

    public TextureOverrides Overrides { get; } = new();

    /// <summary>Set during single-texture preview when <see cref="AutoPBROptions.BrickProbePreviewDebug"/> is true.</summary>
    public string? BrickProbeDebugText { get; set; }

    public string DiffusePath => FullPath;
    public string NormalPath => Path.Combine(DirectoryPath, Name + "_n" + Extension);
    public string SpecularPath => Path.Combine(DirectoryPath, Name + "_s" + Extension);
}
