namespace AutoPBR.Core.Models;

/// <summary>Which pipeline produced the 3D preview mesh for the current texture selection.</summary>
public enum PreviewMeshDriverKind
{
    None = 0,

    /// <summary>Block/item model JSON from the resource pack (<c>assets/.../models/</c>).</summary>
    PackModelJson,

    /// <summary>Bytecode-lifted geometry IR JSON under bundled native data.</summary>
    RuntimeGeometryIrJson,

    /// <summary>Hand-written clean-room entity rig code (catalog, family, or specific routes).</summary>
    CleanRoom,
}

/// <summary>Human-readable mesh source for preview log / 3D debug overlay.</summary>
public readonly record struct PreviewMeshProvenance(PreviewMeshDriverKind Kind, string? Detail = null)
{
    public string ToLogLine() => Kind switch
    {
        PreviewMeshDriverKind.PackModelJson =>
            string.IsNullOrWhiteSpace(Detail)
                ? "[Preview] Mesh: pack model JSON"
                : $"[Preview] Mesh: pack model JSON ({Detail})",
        PreviewMeshDriverKind.RuntimeGeometryIrJson =>
            string.IsNullOrWhiteSpace(Detail)
                ? "[Preview] Mesh: runtime JSON (geometry IR)"
                : $"[Preview] Mesh: runtime JSON (geometry IR · {Detail})",
        PreviewMeshDriverKind.CleanRoom =>
            string.IsNullOrWhiteSpace(Detail)
                ? "[Preview] Mesh: CleanRoom"
                : $"[Preview] Mesh: CleanRoom ({Detail})",
        _ => "[Preview] Mesh: (none — 2D maps only)"
    };

    /// <summary>Compact line for the 3D preview camera overlay (no log prefix).</summary>
    public string ToOverlayLine() => Kind switch
    {
        PreviewMeshDriverKind.PackModelJson =>
            string.IsNullOrWhiteSpace(Detail)
                ? "Mesh: pack model JSON"
                : $"Mesh: pack model JSON ({Detail})",
        PreviewMeshDriverKind.RuntimeGeometryIrJson =>
            string.IsNullOrWhiteSpace(Detail)
                ? "Mesh: runtime JSON (geometry IR)"
                : $"Mesh: runtime JSON (geometry IR · {Detail})",
        PreviewMeshDriverKind.CleanRoom =>
            string.IsNullOrWhiteSpace(Detail)
                ? "Mesh: CleanRoom"
                : $"Mesh: CleanRoom ({Detail})",
        _ => "Mesh: (none)"
    };
}
