namespace AutoPBR.Core.Preview;

/// <summary>
/// Detects when pack-converter CPU meshes change so the OpenGL backend can drop stale GPU bind VBOs.
/// </summary>
public static class PreviewMeshGeometryFingerprint
{
    /// <summary>Bump when geometry emit / pose-compose / UV layout logic changes (invalidates cached GPU bind meshes).</summary>
    public const int PipelineRevision = 15;

    public static ulong ComputeCpuPreviewMesh(ReadOnlySpan<float> interleavedVertices, int vertexStrideFloats)
    {
        if (vertexStrideFloats <= 0 || interleavedVertices.Length < vertexStrideFloats)
        {
            return (ulong)PipelineRevision;
        }

        ulong hash = (ulong)PipelineRevision;
        hash = Mix(hash, unchecked((uint)vertexStrideFloats));
        var vertexCount = interleavedVertices.Length / vertexStrideFloats;
        for (var v = 0; v < vertexCount; v++)
        {
            var baseIndex = v * vertexStrideFloats;
            for (var c = 0; c < vertexStrideFloats; c++)
            {
                hash = Mix(hash, unchecked((uint)BitConverter.SingleToInt32Bits(interleavedVertices[baseIndex + c])));
            }
        }

        return hash;
    }

    private static ulong Mix(ulong hash, uint value) =>
        hash * 0x9E3779B97F4A7C15UL ^ value;
}
