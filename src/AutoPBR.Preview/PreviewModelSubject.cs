using System.Numerics;

namespace AutoPBR.Preview;

/// <summary>
/// One contiguous index range in <see cref="PreviewModelSubject.Indices"/> drawn with one material slot and depth layer policy.
/// </summary>
public readonly record struct PreviewDrawBatch(int FirstIndex, int IndexCount, int MaterialIndex)
{
    public PreviewDrawLayerPolicy LayerPolicy { get; init; } = PreviewDrawLayerPolicy.DefaultBase;

    /// <summary>Whether height-map POM/tessellated protrusion should run for this batch.</summary>
    public bool EnableParallax { get; init; } = true;
}

/// <summary>
/// Baked Java-style block/item model for 3D preview: one interleaved mesh and multiple PBR material slots
/// (e.g. door bottom + top textures).
/// </summary>
public sealed class PreviewModelSubject
{
    /// <summary>12 for standard preview meshes; 13 when <see cref="GpuEntityBoneSkinning"/> (bone index in last float).</summary>
    public int VertexStrideFloats { get; init; } = 12;

    /// <summary>When true, <see cref="InterleavedVertices"/> uses stride <see cref="VertexStrideFloats"/> and entity pose is driven by GPU bone uniforms.</summary>
    public bool GpuEntityBoneSkinning { get; init; }

    /// <summary>Preview-space Y offset applied after bone skinning (GPU shader only; CPU path bakes into vertices).</summary>
    public float EntityGpuMeshSpaceLiftY { get; init; }

    /// <summary>
    /// Bind VBO positions already include <c>W()</c> + lift (animation-off Explore path); shader uses CPU branch.
    /// </summary>
    public bool EntityGpuVerticesInPreviewSpace { get; init; }

    /// <summary>Anchor translation applied at bake (CPU) or to bind vertices (GPU XZ); shared policy with lift.</summary>
    public Vector3 EntityPreviewAnchorOffset { get; init; }

    /// <summary>When true, initial load already applied <see cref="EntityPreviewPlacement"/> (avoid double lift in UI).</summary>
    public bool EntityPreviewPlacementApplied { get; init; }

    public required float[] InterleavedVertices { get; init; }
    public required uint[] Indices { get; init; }
    public required PreviewDrawBatch[] DrawBatches { get; init; }
    public required PreviewTextureMaps[] Materials { get; init; }

    /// <summary>Parallel to <see cref="Materials"/>; archive paths for per-slot colormap tint rules.</summary>
    public string[]? MaterialArchivePaths { get; init; }

    /// <summary>Which <see cref="Materials"/> slot matches the user-selected texture (for 2D composite / flags).</summary>
    public int PrimaryMaterialIndex { get; init; }

    /// <summary>True if any material uses sprite foliage rules (mirrors primary slot flags for UI).</summary>
    public bool Sprite2DFoliageTarget { get; init; }

    /// <summary>When true, backend applies continuous render-time animation transforms (used for emulated entity rigs).</summary>
    public bool EnableRenderTimeAnimation { get; init; }

    /// <summary>Optional animation preset identifier (e.g. "entity_emulated").</summary>
    public string? AnimationPreset { get; init; }

    /// <summary>When set, the GL preview can rebuild vertex animation from the pack without a full preview refresh.</summary>
    public EntityEmulatedPreviewRebakeContext? EmulatedRebake { get; init; }

    /// <summary>How the mesh was produced (pack JSON, lifted geometry IR, or clean-room code).</summary>
    public PreviewMeshProvenance? MeshProvenance { get; init; }
}
