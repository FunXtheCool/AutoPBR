using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed class MergedJavaBlockModel
{
    public required List<ModelElement> Elements { get; init; }
    public required Dictionary<string, string> Textures { get; init; }

    /// <summary>
    /// True when ApplyLivingEntityRendererColumnRootScale (or equivalent LER <c>scale(-1,-1,1)</c>)
    /// was folded into element <see cref="ModelElement.LocalToParent"/> poses. Drives vertical face-plane UV routing at bake.
    /// </summary>
    public bool UsesLivingEntityRendererColumnYFlip { get; init; }
}

internal sealed class ModelElement
{
    public required float[] From { get; init; }
    public required float[] To { get; init; }
    public required Dictionary<string, ModelFace> Faces { get; init; }

    /// <summary>Maps this element's axis-aligned <see cref="From"/>/<see cref="To"/> space into preview model space
    /// (Minecraft entity <c>PartPose</c> / cuboid hierarchy). Identity for block models and axis-aligned rigs.
    /// </summary>
    public Matrix4x4 LocalToParent { get; init; } = Matrix4x4.Identity;

    /// <summary>Explicit preview depth layer; <see cref="PreviewDepthLayerKind.Base"/> uses texture-path heuristics at bake.</summary>
    public PreviewDepthLayerKind DepthLayerKind { get; init; } = PreviewDepthLayerKind.Base;

    /// <summary>Ordinal within overlay layers of the same kind (draw order and polygon-offset step).</summary>
    public int LayerOrdinal { get; init; }

    /// <summary>When true, element casts into shadow maps even if overlay kind normally skips.</summary>
    public bool CastsShadow { get; init; }

    /// <summary>Optional shell inflation in entity texel space for parity geometry shells.</summary>
    public float ShellInflateTexels { get; init; }

    /// <summary>
    /// True when the source Java cuboid used <c>CubeListBuilder.mirror()</c>. Java mirrors by swapping X endpoints before
    /// polygon construction and reversing polygon vertices after UV remap; it is not just a UV U-bound swap.
    /// </summary>
    public bool MirrorCuboidUv { get; init; }
}

internal sealed class ModelFace
{
    public required string TextureKey { get; init; }
    public float[]? Uv { get; init; }
    public int RotationDegrees { get; init; }
}
