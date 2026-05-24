namespace AutoPBR.App.Rendering.Abstractions;

/// <summary>
/// Interleaved vertex: position (3) + normal (3) + texcoord (2) + tangent (4) = 12 floats.
/// Tangents use MikkTSpace-style W sign for bitangent reconstruction.
/// </summary>
public sealed class PreviewMesh
{
    public const int FloatsPerVertex = 12;

    public required string Name { get; init; }
    public required float[] InterleavedVertices { get; init; }
    public required uint[] Indices { get; init; }

    public int VertexCount => InterleavedVertices.Length / FloatsPerVertex;
}
