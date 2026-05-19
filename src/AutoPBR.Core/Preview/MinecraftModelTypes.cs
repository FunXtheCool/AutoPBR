using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed class MergedJavaBlockModel
{
    public required List<ModelElement> Elements { get; init; }
    public required Dictionary<string, string> Textures { get; init; }
}

internal sealed class ModelElement
{
    public required float[] From { get; init; }
    public required float[] To { get; init; }
    public required Dictionary<string, ModelFace> Faces { get; init; }

    /// <summary>
    /// Maps this element's axis-aligned <see cref="From"/>/<see cref="To"/> space into preview model space
    /// (Minecraft entity <c>PartPose</c> / cuboid hierarchy). Identity for block models and axis-aligned rigs.
    /// </summary>
    public Matrix4x4 LocalToParent { get; init; } = Matrix4x4.Identity;
}

internal sealed class ModelFace
{
    public required string TextureKey { get; init; }
    public float[]? Uv { get; init; }
    public int RotationDegrees { get; init; }
}
