using System.Numerics;

namespace AutoPBR.Preview;

public static class PreviewDrawBatchBounds
{
    public static PreviewDrawBatch[] WithComputedBounds(
        IReadOnlyList<PreviewDrawBatch> batches,
        ReadOnlySpan<float> interleavedVertices,
        ReadOnlySpan<uint> indices,
        int vertexStrideFloats,
        float lodMaxDistance = 0f)
    {
        if (batches.Count == 0)
        {
            return [];
        }

        var result = new PreviewDrawBatch[batches.Count];
        for (var i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            result[i] = TryComputeBounds(
                    batch,
                    interleavedVertices,
                    indices,
                    vertexStrideFloats,
                    out var center,
                    out var radius)
                ? batch with
                {
                    BoundsCenter = center,
                    BoundsRadius = radius,
                    LodMaxDistance = lodMaxDistance
                }
                : batch with
                {
                    BoundsCenter = Vector3.Zero,
                    BoundsRadius = -1f,
                    LodMaxDistance = lodMaxDistance
                };
        }

        return result;
    }

    public static bool TryComputeBounds(
        PreviewDrawBatch batch,
        ReadOnlySpan<float> interleavedVertices,
        ReadOnlySpan<uint> indices,
        int vertexStrideFloats,
        out Vector3 center,
        out float radius)
    {
        center = Vector3.Zero;
        radius = -1f;
        if (vertexStrideFloats < 3 ||
            interleavedVertices.Length < vertexStrideFloats ||
            batch.IndexCount <= 0 ||
            batch.FirstIndex < 0 ||
            batch.FirstIndex >= indices.Length)
        {
            return false;
        }

        var end = Math.Min(indices.Length, batch.FirstIndex + batch.IndexCount);
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        var valid = 0;
        for (var i = batch.FirstIndex; i < end; i++)
        {
            var vertexIndex = indices[i];
            if (vertexIndex > int.MaxValue / vertexStrideFloats)
            {
                continue;
            }

            var vertexOffset = (int)vertexIndex * vertexStrideFloats;
            if (vertexOffset < 0 || vertexOffset + 2 >= interleavedVertices.Length)
            {
                continue;
            }

            var p = new Vector3(
                interleavedVertices[vertexOffset],
                interleavedVertices[vertexOffset + 1],
                interleavedVertices[vertexOffset + 2]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            valid++;
        }

        if (valid == 0 || !float.IsFinite(min.X) || !float.IsFinite(max.X))
        {
            return false;
        }

        center = (min + max) * 0.5f;
        var radiusSq = 0f;
        for (var i = batch.FirstIndex; i < end; i++)
        {
            var vertexIndex = indices[i];
            if (vertexIndex > int.MaxValue / vertexStrideFloats)
            {
                continue;
            }

            var vertexOffset = (int)vertexIndex * vertexStrideFloats;
            if (vertexOffset < 0 || vertexOffset + 2 >= interleavedVertices.Length)
            {
                continue;
            }

            var p = new Vector3(
                interleavedVertices[vertexOffset],
                interleavedVertices[vertexOffset + 1],
                interleavedVertices[vertexOffset + 2]);
            radiusSq = MathF.Max(radiusSq, Vector3.DistanceSquared(center, p));
        }

        radius = MathF.Sqrt(radiusSq);
        return float.IsFinite(radius);
    }
}
