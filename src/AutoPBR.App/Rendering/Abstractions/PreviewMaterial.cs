namespace AutoPBR.App.Rendering.Abstractions;

/// <summary>CPU-side material payload for GPU upload. Maps mirror Core preview texture maps.</summary>
public sealed class PreviewMaterial
{
    public required int Width { get; init; }
    public required int Height { get; init; }

    /// <summary>Logical atlas width used during entity UV bake (0 = use <see cref="Width"/>).</summary>
    public int BakeAtlasWidth { get; init; }

    /// <summary>Logical atlas height used during entity UV bake (0 = use <see cref="Height"/>).</summary>
    public int BakeAtlasHeight { get; init; }

    public required ReadOnlyMemory<byte> AlbedoRgba { get; init; }
    public ReadOnlyMemory<byte>? NormalRgba { get; init; }
    public ReadOnlyMemory<byte>? SpecularRgba { get; init; }
    public ReadOnlyMemory<byte>? HeightRgba { get; init; }

    public bool IsPlantForNoHeight { get; init; }
    public bool Sprite2DFoliageTarget { get; init; }

    /// <summary>When true (default), RGBA rows are flipped to OpenGL bottom-first on upload.</summary>
    public bool GlUploadFlipRows { get; init; } = true;
}
