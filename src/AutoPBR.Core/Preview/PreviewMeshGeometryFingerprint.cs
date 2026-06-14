namespace AutoPBR.Core.Preview;

/// <summary>
/// Detects when pack-converter CPU meshes change so the OpenGL backend can drop stale GPU bind VBOs.
/// </summary>
public static class PreviewMeshGeometryFingerprint
{
    /// <summary>Bump when geometry emit / pose-compose logic changes (invalidates cached GPU bind meshes).</summary>
    public const int PipelineRevision = 3;

    public static ulong ComputeCpuPreviewMesh(ReadOnlySpan<float> interleavedVertices, int vertexStrideFloats)
    {
        if (vertexStrideFloats <= 0 || interleavedVertices.Length < vertexStrideFloats)
        {
            return (ulong)PipelineRevision;
        }

        ulong hash = (ulong)PipelineRevision;
        var vertexCount = interleavedVertices.Length / vertexStrideFloats;
        for (var v = 0; v < vertexCount; v++)
        {
            var baseIndex = v * vertexStrideFloats;
            hash = Mix(hash, unchecked((uint)BitConverter.SingleToInt32Bits(interleavedVertices[baseIndex])));
            hash = Mix(hash, unchecked((uint)BitConverter.SingleToInt32Bits(interleavedVertices[baseIndex + 1])));
            hash = Mix(hash, unchecked((uint)BitConverter.SingleToInt32Bits(interleavedVertices[baseIndex + 2])));
        }

        return hash;
    }

    private static ulong Mix(ulong hash, uint value) =>
        hash * 0x9E3779B97F4A7C15UL ^ value;
}
