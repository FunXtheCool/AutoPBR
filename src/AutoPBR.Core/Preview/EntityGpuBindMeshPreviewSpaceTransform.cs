using System.Numerics;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Applies the same preview-space transform as <see cref="MinecraftModelBaker"/> <c>W()</c> + shader lift on the CPU.
/// Used for parity tests and diagnostics — Explore display uses the GPU bind VBO + entity shader path.
/// </summary>
public static class EntityGpuBindMeshPreviewSpaceTransform
{
    public static void BakeIntoVertices(float[] interleavedSkinned, float meshSpaceLiftY)
    {
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        if (interleavedSkinned.Length < stride)
        {
            return;
        }

        for (var i = 0; i + stride - 1 < interleavedSkinned.Length; i += stride)
        {
            var p = new Vector3(interleavedSkinned[i], interleavedSkinned[i + 1], interleavedSkinned[i + 2]);
            p = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(p);
            p.Y += meshSpaceLiftY;
            interleavedSkinned[i] = p.X;
            interleavedSkinned[i + 1] = p.Y;
            interleavedSkinned[i + 2] = p.Z;
            var n = new Vector3(
                interleavedSkinned[i + 3],
                interleavedSkinned[i + 4],
                interleavedSkinned[i + 5]);
            n = Vector3.Normalize(n * 16f);
            interleavedSkinned[i + 3] = n.X;
            interleavedSkinned[i + 4] = n.Y;
            interleavedSkinned[i + 5] = n.Z;
        }
    }

    /// <summary>
    /// Applies <c>bone[bi] · pos</c>, then <c>W()</c> + lift, and emits 12-float preview layout (same as <c>genesis.vert</c> on CPU).
    /// </summary>
    public static float[] SkinAndBakeToPreviewLayout(
        ReadOnlySpan<float> bindSkinned,
        ReadOnlySpan<Matrix4x4> boneMatrices,
        int boneCount,
        float meshSpaceLiftY)
    {
        const int srcStride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        const int dstStride = MinecraftModelBaker.FloatsPerVertex;
        if (bindSkinned.Length < srcStride || bindSkinned.Length % srcStride != 0 || boneCount <= 0)
        {
            return Array.Empty<float>();
        }

        var vertexCount = bindSkinned.Length / srcStride;
        var dst = new float[vertexCount * dstStride];
        for (var v = 0; v < vertexCount; v++)
        {
            var si = v * srcStride;
            var di = v * dstStride;
            var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(bindSkinned[si + 12]);
            var p = new Vector3(bindSkinned[si], bindSkinned[si + 1], bindSkinned[si + 2]);
            var n = new Vector3(bindSkinned[si + 3], bindSkinned[si + 4], bindSkinned[si + 5]);
            if (bi >= 0 && bi < boneCount)
            {
                p = Vector3.Transform(p, boneMatrices[bi]);
                n = Vector3.Normalize(Vector3.TransformNormal(n, boneMatrices[bi]) * 16f);
            }
            else
            {
                n = Vector3.Normalize(n * 16f);
            }

            p = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(p);
            p.Y += meshSpaceLiftY;
            dst[di] = p.X;
            dst[di + 1] = p.Y;
            dst[di + 2] = p.Z;
            dst[di + 3] = n.X;
            dst[di + 4] = n.Y;
            dst[di + 5] = n.Z;
            bindSkinned.Slice(si + 6, 6).CopyTo(dst.AsSpan(di + 6, 6));
        }

        return dst;
    }

    /// <summary>Drop the bone-index float so the VBO uses the standard 12-float preview layout (no entity shader branch).</summary>
    public static float[] ToPreviewMeshLayout(ReadOnlySpan<float> interleavedSkinned)
    {
        const int srcStride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        const int dstStride = MinecraftModelBaker.FloatsPerVertex;
        if (interleavedSkinned.Length < srcStride || interleavedSkinned.Length % srcStride != 0)
        {
            return interleavedSkinned.ToArray();
        }

        var vertexCount = interleavedSkinned.Length / srcStride;
        var dst = new float[vertexCount * dstStride];
        for (var v = 0; v < vertexCount; v++)
        {
            interleavedSkinned.Slice(v * srcStride, dstStride).CopyTo(dst.AsSpan(v * dstStride, dstStride));
        }

        return dst;
    }
}
