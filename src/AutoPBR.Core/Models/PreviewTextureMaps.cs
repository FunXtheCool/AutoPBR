namespace AutoPBR.Core.Models;

/// <summary>
/// Raw RGBA8 maps for GPU preview (same interpretation as on-disk outputs).
/// Normal RGB = tangent-space normal; alpha = packed height when present.
/// Specular follows LabPBR _s semantics (see docs/ml-specular-labpbr-contract.md).
/// </summary>
public sealed class PreviewTextureMaps
{
    public required int Width { get; init; }
    public required int Height { get; init; }

    public required byte[] DiffuseRgba { get; init; }

    /// <summary>Full normal map including alpha (height); null if normals were not written.</summary>
    public byte[]? NormalRgba { get; init; }

    public byte[]? SpecularRgba { get; init; }

    /// <summary>Grayscale height in RGB, opaque A; derived from normal alpha when height is packed; null if not applicable.</summary>
    public byte[]? HeightRgba { get; init; }

    public bool IsPlantForNoHeight { get; init; }
    public bool Sprite2DFoliageTarget { get; init; }
}

/// <summary>2D composite PNG plus structured maps from a single preview pipeline run.</summary>
/// <param name="ModelSubject">When non-null, 3D preview uses baked multi-material geometry; <see cref="Maps"/> remains the primary-slot maps for 2D UI.</param>
public sealed record PreviewDetailedResult(
    byte[] PngBytes,
    PreviewTextureMaps Maps,
    string? BrickProbeDebugText,
    PreviewModelSubject? ModelSubject = null,
    PreviewMeshProvenance? MeshProvenance = null);
