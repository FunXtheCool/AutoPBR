using System.Numerics;

namespace AutoPBR.Preview;

/// <summary>How emulated entity preview meshes are centered after bake and LER.</summary>
public enum EntityPreviewAnchorMode
{
    /// <summary>Lowest foot/ground contact at <see cref="EntityPreviewPlacement.DefaultFloorY"/>; horizontal center from AABB.</summary>
    FeetOnGroundPlane,

    /// <summary>World-space AABB center at origin (used for block-like previews).</summary>
    BoundingBoxCenter
}

/// <summary>
/// Entity-aware ground contact and lift for Explore / emulated entity preview (leg parts or bottom quartile).
/// </summary>
public static class EntityPreviewGrounding
{
    public const float DefaultClearance = 0.002f;

    /// <summary>Resolves cuboid-owner part ids for parity-catalog textures when geometry IR is available.</summary>
    internal static bool TryResolveCatalogElementPartIds(
        string normalizedAssetPath,
        MinecraftNativeProfile profile,
        int elementCount,
        string? previewPoseId,
        out string[] partIds)
    {
        partIds = [];
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        if (!EntityTextureParityCatalog.IsCatalogued(norm) ||
            EntityTextureParityCatalog.ResolveRule(norm, stem) is not { } rule)
        {
            return false;
        }

        var isBaby = EntityModelRuntime.LooksLikeBabyTexture(stem, norm);
        if (!GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                profile,
                rule,
                norm,
                stem,
                isBaby,
                out var jvm,
                out var geometryRoot))
        {
            return false;
        }

        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, geometryRoot);
        var atlasW = rule.GeometryIrTextureWidth is > 0 and var rw ? rw : 64;
        var atlasH = rule.GeometryIrTextureHeight is > 0 and var rh ? rh : 64;
        if (geometryRoot.TryGetProperty("textureWidth", out var tw) && tw.TryGetInt32(out var twi) && twi > 0 &&
            geometryRoot.TryGetProperty("textureHeight", out var th) && th.TryGetInt32(out var thi) && thi > 0)
        {
            atlasW = twi;
            atlasH = thi;
        }

        var options = EntityModelRuntime.CreateParityCatalogPartIdResolveEmitOptions(
            rule.BuilderMethod,
            profile,
            isBaby,
            jvm,
            atlasW,
            atlasH,
            norm,
            previewPoseId);
        var collected = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        if (collected.Count != elementCount)
        {
            return false;
        }

        partIds = collected.ToArray();
        return true;
    }

    public static float ComputeGroundContactYPreviewSpace(
        ReadOnlySpan<float> interleavedVertices,
        int vertexStrideFloats,
        ReadOnlySpan<int> boneIndexPerVertex,
        IReadOnlyList<string>? elementPartIds)
    {
        if (vertexStrideFloats < 2 ||
            interleavedVertices.Length < vertexStrideFloats ||
            interleavedVertices.Length % vertexStrideFloats != 0)
        {
            return 0f;
        }

        if (elementPartIds is { Count: > 0 } &&
            boneIndexPerVertex.Length > 0 &&
            TryMinYForLegParts(
                interleavedVertices,
                vertexStrideFloats,
                boneIndexPerVertex,
                elementPartIds,
                out var legMinY))
        {
            return legMinY;
        }

        return ComputeBottomQuartileMinYPreviewSpace(interleavedVertices, vertexStrideFloats);
    }

    public static float ComputeGroundContactYMeshSpace(
        ReadOnlySpan<float> interleavedSkinned,
        int vertexStrideFloats,
        ReadOnlySpan<int> boneIndexPerVertex,
        IReadOnlyList<string>? elementPartIds)
    {
        if (vertexStrideFloats < 3 ||
            interleavedSkinned.Length < vertexStrideFloats ||
            interleavedSkinned.Length % vertexStrideFloats != 0)
        {
            return 0f;
        }

        if (elementPartIds is { Count: > 0 } &&
            boneIndexPerVertex.Length > 0 &&
            TryMinPreviewYForLegPartsMeshSpace(
                interleavedSkinned,
                vertexStrideFloats,
                boneIndexPerVertex,
                elementPartIds,
                out var legMinY))
        {
            return legMinY;
        }

        return ComputeBottomQuartileMinYMeshSpace(interleavedSkinned, vertexStrideFloats);
    }

    public static float ComputeLiftToFloor(float contactY, float floorY, float clearance = DefaultClearance)
    {
        if (!float.IsFinite(contactY))
        {
            return 0f;
        }

        var targetMinY = floorY + MathF.Max(0f, clearance);
        return MathF.Max(0f, targetMinY - contactY);
    }

    public static Vector3 ComputeAnchorOffsetPreviewSpace(
        ReadOnlySpan<float> interleavedVertices,
        int vertexStrideFloats,
        float groundContactY,
        float floorY,
        float clearance,
        EntityPreviewAnchorMode mode)
    {
        if (!TryComputeHorizontalBounds(
                interleavedVertices,
                vertexStrideFloats,
                out var minX,
                out var maxX,
                out var minY,
                out var maxY,
                out var minZ,
                out var maxZ))
        {
            return Vector3.Zero;
        }

        var centerX = (minX + maxX) * 0.5f;
        var centerZ = (minZ + maxZ) * 0.5f;
        return mode switch
        {
            EntityPreviewAnchorMode.BoundingBoxCenter => new Vector3(
                -centerX,
                -((minY + maxY) * 0.5f),
                -centerZ),
            _ => new Vector3(
                -centerX,
                floorY + MathF.Max(0f, clearance) - groundContactY,
                -centerZ)
        };
    }

    /// <summary>GPU path: anchor XZ in mesh space (pre <c>W</c>); Y lift is applied separately in the shader.</summary>
    public static Vector3 ComputeAnchorOffsetMeshSpace(
        ReadOnlySpan<float> interleavedSkinned,
        int vertexStrideFloats,
        float groundContactPreviewY,
        float floorY,
        float clearance,
        EntityPreviewAnchorMode mode)
    {
        if (vertexStrideFloats < 3 ||
            interleavedSkinned.Length < vertexStrideFloats ||
            interleavedSkinned.Length % vertexStrideFloats != 0)
        {
            return Vector3.Zero;
        }

        if (mode == EntityPreviewAnchorMode.BoundingBoxCenter)
        {
            if (!TryComputeMeshSpaceBounds(interleavedSkinned, vertexStrideFloats, out var minP, out var maxP))
            {
                return Vector3.Zero;
            }

            return -(minP + maxP) * 0.5f;
        }

        _ = groundContactPreviewY;
        _ = floorY;
        _ = clearance;

        // Match CPU horizontal anchor: center the preview-space AABB (W(p)), then t = -center * 16.
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var minZ = float.PositiveInfinity;
        var maxZ = float.NegativeInfinity;
        for (var i = 0; i + vertexStrideFloats - 1 < interleavedSkinned.Length; i += vertexStrideFloats)
        {
            var p = new Vector3(interleavedSkinned[i], interleavedSkinned[i + 1], interleavedSkinned[i + 2]);
            var preview = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(p);
            minX = MathF.Min(minX, preview.X);
            maxX = MathF.Max(maxX, preview.X);
            minZ = MathF.Min(minZ, preview.Z);
            maxZ = MathF.Max(maxZ, preview.Z);
        }

        if (!float.IsFinite(minX))
        {
            return Vector3.Zero;
        }

        var previewCenterX = (minX + maxX) * 0.5f;
        var previewCenterZ = (minZ + maxZ) * 0.5f;
        return new Vector3(-previewCenterX * 16f, 0f, -previewCenterZ * 16f);
    }

    public static void ApplyTranslation(
        float[] interleavedVertices,
        Vector3 offset,
        int vertexStrideFloats)
    {
        if (offset == Vector3.Zero || vertexStrideFloats < 3)
        {
            return;
        }

        for (var i = 0; i + 2 < interleavedVertices.Length; i += vertexStrideFloats)
        {
            interleavedVertices[i] += offset.X;
            interleavedVertices[i + 1] += offset.Y;
            interleavedVertices[i + 2] += offset.Z;
        }
    }

    public static float ComputeGpuPreviewSpaceMinY(
        ReadOnlySpan<float> bindSkinnedVertices,
        ReadOnlySpan<Matrix4x4> boneMatrices,
        int boneCount,
        int vertexStrideFloats = MinecraftModelBaker.FloatsPerSkinnedVertex)
    {
        if (boneCount <= 0 || bindSkinnedVertices.Length < vertexStrideFloats)
        {
            return 0f;
        }

        var minY = float.PositiveInfinity;
        for (var i = 0; i + vertexStrideFloats - 1 < bindSkinnedVertices.Length; i += vertexStrideFloats)
        {
            var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(bindSkinnedVertices[i + 12]);
            if (bi < 0 || bi >= boneCount)
            {
                continue;
            }

            var p = new Vector3(bindSkinnedVertices[i], bindSkinnedVertices[i + 1], bindSkinnedVertices[i + 2]);
            var skinned = Vector3.Transform(p, boneMatrices[bi]);
            var ty = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(skinned).Y;
            minY = MathF.Min(minY, ty);
        }

        return float.IsFinite(minY) ? minY : 0f;
    }

    public static void CollectBoneIndices(
        ReadOnlySpan<float> interleavedSkinned,
        int vertexStrideFloats,
        int boneIndexFloatOffset,
        List<int> boneIndicesOut)
    {
        boneIndicesOut.Clear();
        for (var i = boneIndexFloatOffset; i < interleavedSkinned.Length; i += vertexStrideFloats)
        {
            boneIndicesOut.Add(EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(interleavedSkinned[i]));
        }
    }

    private static bool TryMinYForLegParts(
        ReadOnlySpan<float> interleavedVertices,
        int vertexStrideFloats,
        ReadOnlySpan<int> boneIndexPerVertex,
        IReadOnlyList<string> elementPartIds,
        out float minY)
    {
        minY = float.PositiveInfinity;
        var vi = 0;
        for (var i = 1; i < interleavedVertices.Length; i += vertexStrideFloats)
        {
            if (vi >= boneIndexPerVertex.Length)
            {
                break;
            }

            var bi = boneIndexPerVertex[vi++];
            if (bi < 0 || bi >= elementPartIds.Count)
            {
                continue;
            }

            var partId = elementPartIds[bi];
            if (!IsLegPartId(partId))
            {
                continue;
            }

            minY = MathF.Min(minY, interleavedVertices[i]);
        }

        return float.IsFinite(minY);
    }

    private static bool TryMinPreviewYForLegPartsMeshSpace(
        ReadOnlySpan<float> interleavedSkinned,
        int vertexStrideFloats,
        ReadOnlySpan<int> boneIndexPerVertex,
        IReadOnlyList<string> elementPartIds,
        out float minPreviewY)
    {
        minPreviewY = float.PositiveInfinity;
        var vi = 0;
        for (var i = 0; i + vertexStrideFloats - 1 < interleavedSkinned.Length; i += vertexStrideFloats)
        {
            if (vi >= boneIndexPerVertex.Length)
            {
                break;
            }

            var bi = boneIndexPerVertex[vi++];
            if (bi < 0 || bi >= elementPartIds.Count)
            {
                continue;
            }

            if (!IsLegPartId(elementPartIds[bi]))
            {
                continue;
            }

            var p = new Vector3(interleavedSkinned[i], interleavedSkinned[i + 1], interleavedSkinned[i + 2]);
            minPreviewY = MathF.Min(minPreviewY, EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(p).Y);
        }

        return float.IsFinite(minPreviewY);
    }

    private static bool IsLegPartId(string partId) =>
        partId.Contains("leg", StringComparison.OrdinalIgnoreCase) &&
        !partId.Contains("head", StringComparison.OrdinalIgnoreCase);

    private static float ComputeBottomQuartileMinYPreviewSpace(
        ReadOnlySpan<float> interleavedVertices,
        int vertexStrideFloats)
    {
        var ys = new List<float>();
        for (var i = 1; i < interleavedVertices.Length; i += vertexStrideFloats)
        {
            ys.Add(interleavedVertices[i]);
        }

        return MinOfBottomQuartile(ys);
    }

    private static float ComputeBottomQuartileMinYMeshSpace(
        ReadOnlySpan<float> interleavedSkinned,
        int vertexStrideFloats)
    {
        var ys = new List<float>();
        for (var i = 0; i + vertexStrideFloats - 1 < interleavedSkinned.Length; i += vertexStrideFloats)
        {
            var p = new Vector3(interleavedSkinned[i], interleavedSkinned[i + 1], interleavedSkinned[i + 2]);
            ys.Add(EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(p).Y);
        }

        return MinOfBottomQuartile(ys);
    }

    private static float MinOfBottomQuartile(List<float> ys)
    {
        if (ys.Count == 0)
        {
            return 0f;
        }

        ys.Sort();
        var quartileCount = Math.Max(1, (ys.Count + 3) / 4);
        var min = float.PositiveInfinity;
        for (var q = 0; q < quartileCount; q++)
        {
            min = MathF.Min(min, ys[q]);
        }

        return float.IsFinite(min) ? min : 0f;
    }

    internal static bool TryComputeHorizontalBoundsPublic(
        ReadOnlySpan<float> interleavedVertices,
        int vertexStrideFloats,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY,
        out float minZ,
        out float maxZ) =>
        TryComputeHorizontalBounds(
            interleavedVertices,
            vertexStrideFloats,
            out minX,
            out maxX,
            out minY,
            out maxY,
            out minZ,
            out maxZ);

    private static bool TryComputeHorizontalBounds(
        ReadOnlySpan<float> interleavedVertices,
        int vertexStrideFloats,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY,
        out float minZ,
        out float maxZ)
    {
        minX = minY = minZ = float.PositiveInfinity;
        maxX = maxY = maxZ = float.NegativeInfinity;
        if (vertexStrideFloats < 3 || interleavedVertices.Length < vertexStrideFloats)
        {
            return false;
        }

        for (var i = 0; i + 2 < interleavedVertices.Length; i += vertexStrideFloats)
        {
            var x = interleavedVertices[i];
            var y = interleavedVertices[i + 1];
            var z = interleavedVertices[i + 2];
            minX = MathF.Min(minX, x);
            maxX = MathF.Max(maxX, x);
            minY = MathF.Min(minY, y);
            maxY = MathF.Max(maxY, y);
            minZ = MathF.Min(minZ, z);
            maxZ = MathF.Max(maxZ, z);
        }

        return float.IsFinite(minX);
    }

    private static bool TryComputeMeshSpaceBounds(
        ReadOnlySpan<float> interleavedSkinned,
        int vertexStrideFloats,
        out Vector3 minP,
        out Vector3 maxP)
    {
        minP = new Vector3(float.PositiveInfinity);
        maxP = new Vector3(float.NegativeInfinity);
        if (vertexStrideFloats < 3)
        {
            return false;
        }

        for (var i = 0; i + 2 < interleavedSkinned.Length; i += vertexStrideFloats)
        {
            var p = new Vector3(interleavedSkinned[i], interleavedSkinned[i + 1], interleavedSkinned[i + 2]);
            minP = Vector3.Min(minP, p);
            maxP = Vector3.Max(maxP, p);
        }

        return float.IsFinite(minP.X);
    }
}
