using System.Numerics;
using System.Runtime.InteropServices;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Single placement policy for emulated entity preview: entity-aware ground contact, anchor, and lift
/// (CPU vertices or GPU <c>uEntityMeshLiftY</c> — never both with different inputs).
/// </summary>
public static class EntityPreviewPlacement
{
    public const float DefaultFloorY = -0.56f;

    public readonly record struct PlacementResult(
        float GroundLiftY,
        Vector3 AnchorOffset,
        float GroundContactY,
        float BodyCentroidY,
        float HeadCentroidY,
        float LegCentroidY);

    /// <summary>CPU-baked preview vertices (post-<c>W</c>); lift and anchor are baked into positions.</summary>
    public static PlacementResult ApplyToPreviewVertices(
        float[] interleavedVertices,
        int vertexStrideFloats,
        IReadOnlyList<string>? elementPartIds,
        float floorY = DefaultFloorY,
        float clearance = EntityPreviewGrounding.DefaultClearance,
        EntityPreviewAnchorMode anchorMode = EntityPreviewAnchorMode.FeetOnGroundPlane)
    {
        var boneScratch = new List<int>();
        if (vertexStrideFloats == MinecraftModelBaker.FloatsPerSkinnedVertex)
        {
            EntityPreviewGrounding.CollectBoneIndices(
                interleavedVertices,
                vertexStrideFloats,
                MinecraftModelBaker.FloatsPerSkinnedVertex - 1,
                boneScratch);
        }

        var contactY = EntityPreviewGrounding.ComputeGroundContactYPreviewSpace(
            interleavedVertices,
            vertexStrideFloats,
            CollectionsMarshal.AsSpan(boneScratch),
            elementPartIds);
        var liftY = EntityPreviewGrounding.ComputeLiftToFloor(contactY, floorY, clearance);
        var anchor = EntityPreviewGrounding.ComputeAnchorOffsetPreviewSpace(
            interleavedVertices,
            vertexStrideFloats,
            contactY + liftY,
            floorY,
            clearance,
            anchorMode);
        var totalOffset = anchor with { Y = anchor.Y + liftY };
        EntityPreviewGrounding.ApplyTranslation(interleavedVertices, totalOffset, vertexStrideFloats);

        MeasurePartCentroidsY(
            interleavedVertices,
            vertexStrideFloats,
            CollectionsMarshal.AsSpan(boneScratch),
            elementPartIds,
            out var bodyY,
            out var headY,
            out var legY);

        return new PlacementResult(0f, totalOffset, contactY + liftY, bodyY, headY, legY);
    }

    /// <summary>GPU bind-pose buffer: anchor XZ+Y in mesh space; vertical lift in preview space via shader.</summary>
    public static PlacementResult ApplyToGpuBindVertices(
        float[] interleavedSkinned,
        IReadOnlyList<string>? elementPartIds,
        float floorY = DefaultFloorY,
        float clearance = EntityPreviewGrounding.DefaultClearance,
        EntityPreviewAnchorMode anchorMode = EntityPreviewAnchorMode.FeetOnGroundPlane)
    {
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var boneScratch = new List<int>();
        EntityPreviewGrounding.CollectBoneIndices(interleavedSkinned, stride, stride - 1, boneScratch);

        var contactPreviewY = EntityPreviewGrounding.ComputeGroundContactYMeshSpace(
            interleavedSkinned,
            stride,
            CollectionsMarshal.AsSpan(boneScratch),
            elementPartIds);
        var liftY = EntityPreviewGrounding.ComputeLiftToFloor(contactPreviewY, floorY, clearance);
        var anchorMesh = EntityPreviewGrounding.ComputeAnchorOffsetMeshSpace(
            interleavedSkinned,
            stride,
            contactPreviewY + liftY,
            floorY,
            clearance,
            anchorMode);
        EntityPreviewGrounding.ApplyTranslation(interleavedSkinned, anchorMesh, stride);

        MeasureGpuPartCentroidsY(
            interleavedSkinned,
            stride,
            CollectionsMarshal.AsSpan(boneScratch),
            elementPartIds,
            liftY,
            out var bodyY,
            out var headY,
            out var legY);

        return new PlacementResult(
            liftY,
            anchorMesh,
            contactPreviewY + liftY,
            bodyY,
            headY,
            legY);
    }

    private static void MeasureGpuPartCentroidsY(
        ReadOnlySpan<float> interleavedSkinned,
        int vertexStrideFloats,
        ReadOnlySpan<int> boneIndexPerVertex,
        IReadOnlyList<string>? elementPartIds,
        float meshSpaceLiftY,
        out float bodyY,
        out float headY,
        out float legY)
    {
        bodyY = headY = legY = 0f;
        if (elementPartIds is not { Count: > 0 } || boneIndexPerVertex.Length == 0)
        {
            return;
        }

        float bodySum = 0f, headSum = 0f, legSum = 0f;
        var bodyN = 0;
        var headN = 0;
        var legN = 0;
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

            var partId = elementPartIds[bi];
            var p = new Vector3(interleavedSkinned[i], interleavedSkinned[i + 1], interleavedSkinned[i + 2]);
            var previewY = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(p).Y + meshSpaceLiftY;
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += previewY;
                headN++;
            }
            else if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += previewY;
                legN++;
            }
            else if (partId.Contains("body", StringComparison.OrdinalIgnoreCase))
            {
                bodySum += previewY;
                bodyN++;
            }
        }

        if (bodyN > 0)
        {
            bodyY = bodySum / bodyN;
        }

        if (headN > 0)
        {
            headY = headSum / headN;
        }

        if (legN > 0)
        {
            legY = legSum / legN;
        }
    }

    public static float ComputeLiveGpuLiftY(
        ReadOnlySpan<float> bindSkinnedVertices,
        ReadOnlySpan<Matrix4x4> boneMatrices,
        int boneCount,
        float floorY = DefaultFloorY,
        float clearance = EntityPreviewGrounding.DefaultClearance)
    {
        var contactY = EntityPreviewGrounding.ComputeGpuPreviewSpaceMinY(bindSkinnedVertices, boneMatrices, boneCount);
        return EntityPreviewGrounding.ComputeLiftToFloor(contactY, floorY, clearance);
    }

    public static Vector3 ComputeEntityOrbitTarget(ReadOnlySpan<float> previewVertices, int vertexStrideFloats)
    {
        if (!EntityPreviewGrounding.TryComputeHorizontalBoundsPublic(
                previewVertices,
                vertexStrideFloats,
                out var minX,
                out var maxX,
                out var minY,
                out var maxY,
                out var minZ,
                out var maxZ))
        {
            return new Vector3(0f, 0.35f, 0f);
        }

        _ = (minX, maxX, minZ, maxZ);
        var chestY = minY + (maxY - minY) * 0.55f;
        return new Vector3(0f, chestY, 0f);
    }

    public static string FormatExplorePlacementDiagnosticLine(
        string normalizedTexturePath,
        string lerBasis,
        bool gpuSkinning,
        float liftY,
        float animClock,
        bool setupAnimMotion,
        float groundContactY,
        float bodyCentroidY,
        float headCentroidY,
        float legCentroidY) =>
        ParityCatalogEntityPreviewDiagnostics.FormatExplorePlacementLine(
            normalizedTexturePath,
            lerBasis,
            gpuSkinning,
            liftY,
            animClock,
            setupAnimMotion,
            groundContactY,
            bodyCentroidY,
            headCentroidY,
            legCentroidY);

    internal static void TryMeasureMergedModelPartCentroidsY(
        MergedJavaBlockModel mesh,
        IReadOnlyList<string> elementPartIds,
        out float bodyY,
        out float headY,
        out float legY)
    {
        bodyY = headY = legY = 0f;
        if (elementPartIds is not { Count: > 0 } || mesh.Elements.Count == 0)
        {
            return;
        }

        float bodySum = 0f, headSum = 0f, legSum = 0f;
        var bodyN = 0;
        var headN = 0;
        var legN = 0;
        var count = Math.Min(mesh.Elements.Count, elementPartIds.Count);
        for (var i = 0; i < count; i++)
        {
            var partId = elementPartIds[i];
            var cy = MeasureElementCornerCentroidY(mesh.Elements[i]);
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += cy;
                headN++;
            }
            else if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += cy;
                legN++;
            }
            else if (partId.Contains("body", StringComparison.OrdinalIgnoreCase))
            {
                bodySum += cy;
                bodyN++;
            }
        }

        if (bodyN > 0)
        {
            bodyY = bodySum / bodyN;
        }

        if (headN > 0)
        {
            headY = headSum / headN;
        }

        if (legN > 0)
        {
            legY = legSum / legN;
        }
    }

    private static float MeasureElementCornerCentroidY(ModelElement el)
    {
        ReadOnlySpan<(float x, float y, float z)> corners =
        [
            (el.From[0], el.From[1], el.From[2]),
            (el.To[0], el.From[1], el.From[2]),
            (el.From[0], el.To[1], el.From[2]),
            (el.To[0], el.To[1], el.From[2]),
            (el.From[0], el.From[1], el.To[2]),
            (el.To[0], el.From[1], el.To[2]),
            (el.From[0], el.To[1], el.To[2]),
            (el.To[0], el.To[1], el.To[2]),
        ];
        var wMin = new Vector3(float.PositiveInfinity);
        var wMax = new Vector3(float.NegativeInfinity);
        foreach (var (x, y, z) in corners)
        {
            var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
            wMin = Vector3.Min(wMin, w);
            wMax = Vector3.Max(wMax, w);
        }

        return (wMin.Y + wMax.Y) * 0.5f;
    }

    internal static void TryPopulateRebakeElementPartIds(
        EntityEmulatedPreviewRebakeContext rebake,
        MinecraftNativeProfile profile,
        int elementCount)
    {
        if (rebake.ElementPartIds is { Length: > 0 })
        {
            return;
        }

        if (EntityPreviewGrounding.TryResolveCatalogElementPartIds(
                rebake.AssetArchivePath,
                profile,
                elementCount,
                out var partIds))
        {
            rebake.ElementPartIds = partIds;
        }
    }

    private static void MeasurePartCentroidsY(
        ReadOnlySpan<float> interleavedVertices,
        int vertexStrideFloats,
        ReadOnlySpan<int> boneIndexPerVertex,
        IReadOnlyList<string>? elementPartIds,
        out float bodyY,
        out float headY,
        out float legY)
    {
        bodyY = headY = legY = 0f;
        if (elementPartIds is not { Count: > 0 } || boneIndexPerVertex.Length == 0)
        {
            return;
        }

        float bodySum = 0f, headSum = 0f, legSum = 0f;
        var bodyN = 0;
        var headN = 0;
        var legN = 0;
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
            var y = interleavedVertices[i];
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += y;
                headN++;
            }
            else if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += y;
                legN++;
            }
            else if (partId.Contains("body", StringComparison.OrdinalIgnoreCase))
            {
                bodySum += y;
                bodyN++;
            }
        }

        if (bodyN > 0)
        {
            bodyY = bodySum / bodyN;
        }

        if (headN > 0)
        {
            headY = headSum / headN;
        }

        if (legN > 0)
        {
            legY = legSum / legN;
        }
    }
}
