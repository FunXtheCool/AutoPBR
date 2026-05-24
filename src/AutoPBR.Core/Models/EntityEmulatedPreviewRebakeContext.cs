using System.Numerics;

using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Models;

/// <summary>
/// Data required to rebuild an emulated-entity preview mesh with a new animation clock without re-running the full preview pipeline.
/// </summary>
public sealed class EntityEmulatedPreviewRebakeContext
{
    /// <summary>Resource pack .zip path passed to the preview pipeline (must remain readable while the preview is open).</summary>
    public required string PackZipPath { get; init; }

    /// <summary>Texture path inside the pack, e.g. <c>assets/minecraft/textures/entity/...</c>.</summary>
    public required string AssetArchivePath { get; init; }

    /// <summary>Absolute root of bundled native Minecraft data (same resolution as <see cref="ResourcePackConverter"/> preview).</summary>
    public required string NativeRootDirectory { get; init; }

    public required string NativeProfileName { get; init; }

    /// <summary>Optional <see cref="System.Version"/> string from native profile resolution.</summary>
    public string? NativeParsedVersion { get; init; }

    public required string ModelDefaultNamespace { get; init; }

    /// <summary>Deterministic idle phase in 0..1 (matches initial bake).</summary>
    public float IdlePhase01 { get; init; }

    /// <summary>Texture zip paths in merged-model bake order (same as the initial preview pipeline).</summary>
    public required string[] OrderedTextureZipPaths { get; init; }

    /// <summary>
    /// When set, GPU bone fast-fill can skip large routing scans for catalog parity or family-fallback meshes.
    /// Cleared automatically when a fill fails.
    /// </summary>
    public EntityGpuBoneDispatchRoute? GpuBoneDispatchRoute { get; set; }

    /// <summary>Mesh pipeline used at initial bake (shown in preview debug log on rebake).</summary>
    public PreviewMeshProvenance? MeshProvenance { get; set; }

    /// <summary>
    /// Number of <c>ModelPart</c>-equivalent bones in the bind-pose GPU mesh (set after successful
    /// <see cref="EntityEmulatedPreviewRebaker.TryPrepareGpuSkinnedEmulatedMesh"/>). Used to reject stale or mismatched bone snapshots.
    /// </summary>
    public int? GpuPreparedBoneCount { get; set; }

    /// <summary>
    /// Per-element <see cref="System.Numerics.Matrix4x4.Invert"/> of bind-pose <c>LocalToParent</c> (animation time 0) from the same
    /// merged mesh as <see cref="GpuPreparedBoneCount"/>. Required for GPU skinning uniforms so preview vertices (stored before the
    /// <c>x/16−½</c> cuboid scale) compose as <c>v · M_bind⁻¹ · M_anim</c> (same as GLSL <c>M_anim · M_bind⁻¹ · v</c> with row-major UBO bytes), not <c>M_anim · M_bind⁻¹</c> on the wrong multiply side.
    /// </summary>
    public Matrix4x4[]? GpuBindPoseInverseLocalToParent { get; set; }
}
