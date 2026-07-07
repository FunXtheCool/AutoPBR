namespace AutoPBR.Core.Models;

/// <summary>Which pipeline produced the 3D preview mesh for the current texture selection.</summary>
public enum PreviewMeshDriverKind
{
    None = 0,

    /// <summary>Block/item model JSON from the resource pack (<c>assets/.../models/</c>).</summary>
    PackModelJson,

    /// <summary>Bytecode-lifted geometry IR JSON under bundled native data.</summary>
    RuntimeGeometryIrJson,

    [Obsolete("Hand-built CleanRoom meshes removed; retained for saved debug state only.")]
    CleanRoom,

    /// <summary>Synthetic vanilla block cube from block texture parity catalog (multi-face slots).</summary>
    VanillaBlockParity,

    /// <summary>Visible placeholder mesh when entity geometry cannot be resolved (Source-style "!").</summary>
    ErrorPlaceholder,
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
        PreviewMeshDriverKind.VanillaBlockParity =>
            string.IsNullOrWhiteSpace(Detail)
                ? "[Preview] Mesh: vanilla block parity"
                : $"[Preview] Mesh: vanilla block parity ({Detail})",
        PreviewMeshDriverKind.ErrorPlaceholder =>
            string.IsNullOrWhiteSpace(Detail)
                ? "[Preview] Mesh: error placeholder"
                : $"[Preview] Mesh: error placeholder ({Detail})",
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
        PreviewMeshDriverKind.VanillaBlockParity =>
            string.IsNullOrWhiteSpace(Detail)
                ? "Mesh: vanilla block parity"
                : $"Mesh: vanilla block parity ({Detail})",
        PreviewMeshDriverKind.ErrorPlaceholder =>
            string.IsNullOrWhiteSpace(Detail)
                ? "Mesh: error placeholder"
                : $"Mesh: error placeholder ({Detail})",
        _ => "Mesh: (none)"
    };
}
