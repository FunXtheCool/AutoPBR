namespace AutoPBR.Core.Models;

public sealed class TextureOverrides
{
    public float? NormalIntensity { get; set; }
    public bool InvertNormalRed { get; set; }
    public bool InvertNormalGreen { get; set; }

    public float? HeightIntensity { get; set; }
    public float? HeightBrightness { get; set; }
    /// <summary>Invert heightmap values after generation (e.g. automatic coal ore tuning).</summary>
    public bool InvertHeight { get; set; }

    public bool? FastSpecular { get; set; }
    public IReadOnlyList<SpecularRule>? CustomSpecularRules { get; set; }

    /// <summary>
    /// When true, invert the specular smoothness (R) channel after heuristic/ML composition (LabPBR R) so dark↔light swap;
    /// set automatically for the <c>brick</c> material rule to align with height, or manually for grout-style fixes.
    /// </summary>
    public bool InvertSpecular { get; set; }
}

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

    /// <summary>When true, texture has the plant material tag (or OptiFine plant/plants path) for extra porosity bias.</summary>
    public bool HasPlantMaterialTag { get; init; }

    public TextureOverrides Overrides { get; } = new();

    public string DiffusePath => FullPath;
    public string NormalPath => Path.Combine(DirectoryPath, Name + "_n" + Extension);
    public string SpecularPath => Path.Combine(DirectoryPath, Name + "_s" + Extension);
}

