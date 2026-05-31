using System.Numerics;

namespace AutoPBR.Core.Preview;

/// <summary>CPU-side simulation of <c>genesis.vert</c> entity skinning for Explore diagnostics.</summary>
public static class EntityGpuShaderDiagnostics
{
    public readonly record struct RuntimeSnapshot(
        int VertexStrideFloats,
        int VertexCount,
        int PreparedBoneCount,
        bool BoneFillOk,
        bool BonePaletteUploaded,
        int UploadedGpuSkinning,
        int UploadedBoneCount,
        float UploadedLiftY,
        int UploadedBindMesh,
        float SimBodyCentroidY,
        float SimLegCentroidY,
        float SimHeadCentroidY,
        float SimBodyLegGap,
        float SimBodyHeadGap3D,
        float SampleBodyBindY,
        float SampleLegBindY,
        bool VerticesInPreviewSpace,
        string? BoneFillFailureHint);

    /// <summary>Per-slot vertex counts from the bind VBO bone-index channel (slot 12).</summary>
    public readonly record struct BoneIndexHistogramSnapshot(
        int VertexCount,
        int DistinctBoneIndices,
        int MinBoneIndex,
        int MaxBoneIndex,
        int DominantBoneIndex,
        int VerticesOnDominantBone,
        string SlotCountsLine);

    /// <summary>Expected vs uploaded entity shader scalars immediately before an entity draw (anim on or off).</summary>
    public readonly record struct DrawContractSnapshot(
        bool GpuEntityPath,
        bool ResolveOk,
        bool SetupAnimMotion,
        int PreparedBoneCount,
        int ExpectedGpuSkinning,
        int ExpectedBoneCount,
        int UploadedGpuSkinning,
        int UploadedBoneCount,
        float UploadedLiftY,
        int UploadedBindMesh,
        int ExpectedBindMesh,
        bool BoneSnapshotValid,
        bool BonePaletteUploaded,
        bool UniformLocsComplete,
        bool EntityBoneUboReady,
        int VertexStrideFloats,
        bool VerticesInPreviewSpace);

    internal static Matrix4x4[] BuildBindPoseBonePalette(MergedJavaBlockModel mergedBind)
    {
        var n = mergedBind.Elements.Count;
        var bones = new Matrix4x4[n];
        for (var i = 0; i < n; i++)
        {
            if (!Matrix4x4.Invert(mergedBind.Elements[i].LocalToParent, out var inv))
            {
                inv = Matrix4x4.Identity;
            }

            bones[i] = Matrix4x4.Multiply(inv, mergedBind.Elements[i].LocalToParent);
        }

        return bones;
    }

    public static RuntimeSnapshot BuildRuntimeSnapshot(
        ReadOnlySpan<float> bindSkinnedVertices,
        int vertexStrideFloats,
        IReadOnlyList<string>? elementPartIds,
        int preparedBoneCount,
        bool boneFillOk,
        bool bonePaletteUploaded,
        int uploadedGpuSkinning,
        int uploadedBoneCount,
        float uploadedLiftY,
        int uploadedBindMesh,
        ReadOnlySpan<Matrix4x4> boneMatrices,
        int boneMatrixCount,
        bool verticesInPreviewSpace = false,
        string? boneFillFailureHint = null)
    {
        var vertexCount = vertexStrideFloats > 0 && bindSkinnedVertices.Length >= vertexStrideFloats
            ? bindSkinnedVertices.Length / vertexStrideFloats
            : 0;
        var applyBones = !verticesInPreviewSpace &&
                         uploadedGpuSkinning != 0 &&
                         bonePaletteUploaded &&
                         boneMatrixCount > 0;
        TryMeasureShaderPartCentroids(
            bindSkinnedVertices,
            vertexStrideFloats,
            elementPartIds,
            boneMatrices,
            boneMatrixCount,
            uploadedLiftY,
            applyBones,
            verticesInPreviewSpace,
            out var bodyCenter,
            out var legCenter,
            out var headCenter,
            out var sampleBodyBindY,
            out var sampleLegBindY);
        var bodyLegGap = bodyCenter.HasValue && legCenter.HasValue
            ? MathF.Abs(bodyCenter.Value.Y - legCenter.Value.Y)
            : 0f;
        var bodyHeadGap3D = bodyCenter.HasValue && headCenter.HasValue
            ? Vector3.Distance(bodyCenter.Value, headCenter.Value)
            : 0f;
        return new RuntimeSnapshot(
            vertexStrideFloats,
            vertexCount,
            preparedBoneCount,
            boneFillOk,
            bonePaletteUploaded,
            uploadedGpuSkinning,
            uploadedBoneCount,
            uploadedLiftY,
            uploadedBindMesh,
            bodyCenter?.Y ?? 0f,
            legCenter?.Y ?? 0f,
            headCenter?.Y ?? 0f,
            bodyLegGap,
            bodyHeadGap3D,
            sampleBodyBindY,
            sampleLegBindY,
            verticesInPreviewSpace,
            boneFillFailureHint);
    }

    public static string FormatExploreGpuRuntimeLine(
        string normalizedTexturePath,
        bool setupAnimMotion,
        float animClock,
        in RuntimeSnapshot snap) =>
        "[3D preview] GPU runtime: " +
        $"path={normalizedTexturePath} " +
        $"setupAnimMotion={(setupAnimMotion ? 1 : 0)} animClock={animClock:0.###} " +
        $"stride={snap.VertexStrideFloats} verts={snap.VertexCount} preparedBones={snap.PreparedBoneCount} " +
        $"boneFillOk={(snap.BoneFillOk ? 1 : 0)} paletteUploaded={(snap.BonePaletteUploaded ? 1 : 0)} " +
        $"uEntityGpuSkinning={snap.UploadedGpuSkinning} uEntityBoneCount={snap.UploadedBoneCount} uEntityMeshLiftY={snap.UploadedLiftY:0.####} " +
        $"uEntityBindMesh={snap.UploadedBindMesh} " +
        $"previewSpaceVerts={(snap.VerticesInPreviewSpace ? 1 : 0)} " +
        $"simBodyY={snap.SimBodyCentroidY:0.####} simLegY={snap.SimLegCentroidY:0.####} simHeadY={snap.SimHeadCentroidY:0.####} " +
        $"simBodyLegGap={snap.SimBodyLegGap:0.####} simBodyHeadGap3D={snap.SimBodyHeadGap3D:0.####} " +
        $"bindSampleBodyY={snap.SampleBodyBindY:0.####} bindSampleLegY={snap.SampleLegBindY:0.####}" +
        (string.IsNullOrEmpty(snap.BoneFillFailureHint) ? "" : $" hint={snap.BoneFillFailureHint}");

    public static string FormatExploreGpuRuntimeWarningLine(
        string normalizedTexturePath,
        in RuntimeSnapshot snap)
    {
        if (snap.VertexStrideFloats != MinecraftModelBaker.FloatsPerSkinnedVertex)
        {
            return $"[3D preview] GPU WARN: path={normalizedTexturePath} stride={snap.VertexStrideFloats} expected={MinecraftModelBaker.FloatsPerSkinnedVertex} (VAO may desync from shader)";
        }

        if (snap.PreparedBoneCount <= 0)
        {
            return $"[3D preview] GPU WARN: path={normalizedTexturePath} GpuPreparedBoneCount=0 at draw (shader treats mesh as non-entity; raw texel scale)";
        }

        if (snap.PreparedBoneCount > 0 && snap.UploadedBindMesh <= 0 && !snap.VerticesInPreviewSpace)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} uEntityBindMesh=0 with preparedBones={snap.PreparedBoneCount} " +
                $"(shader skips W(); raw texel scale — same failure for anim-on and anim-off)";
        }

        if (snap.PreparedBoneCount > 0 && snap.UploadedBoneCount <= 0 && !snap.VerticesInPreviewSpace)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} uEntityBoneCount=0 with preparedBones={snap.PreparedBoneCount} " +
                $"(bone clamp / skinning disabled; W() still runs when uEntityBindMesh=1)";
        }

        if (snap.UploadedGpuSkinning != 0 && !snap.BonePaletteUploaded)
        {
            return $"[3D preview] GPU WARN: path={normalizedTexturePath} uEntityGpuSkinning=1 but bone matrices were not uploaded this frame";
        }

        if (snap.SimBodyHeadGap3D > 1.25f)
        {
            return $"[3D preview] GPU WARN: path={normalizedTexturePath} simBodyHeadGap3D={snap.SimBodyHeadGap3D:0.###} (exploded pose in X/Y/Z; check W() and bone palette)";
        }

        if (snap.SimBodyLegGap > 1.25f)
        {
            return $"[3D preview] GPU WARN: path={normalizedTexturePath} simBodyLegGap={snap.SimBodyLegGap:0.###} bindSampleBodyY={snap.SampleBodyBindY:0.###} bindSampleLegY={snap.SampleLegBindY:0.###} (exploded pose; check W() and bone palette)";
        }

        return string.Empty;
    }

    public static BoneIndexHistogramSnapshot BuildBoneIndexHistogram(
        ReadOnlySpan<float> bindSkinned,
        int vertexStrideFloats,
        int preparedBoneCount = 0)
    {
        const int stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        if (vertexStrideFloats != stride || bindSkinned.Length < stride || bindSkinned.Length % stride != 0)
        {
            return new BoneIndexHistogramSnapshot(0, 0, 0, 0, 0, 0, string.Empty);
        }

        var vertexCount = bindSkinned.Length / stride;
        var slotLimit = Math.Max(preparedBoneCount, 1);
        slotLimit = Math.Clamp(slotLimit, 1, EntityGpuSkinningLimits.MaxBones);
        Span<int> counts = stackalloc int[slotLimit];
        counts.Clear();
        var minIndex = int.MaxValue;
        var maxIndex = int.MinValue;
        var distinct = 0;
        for (var v = 0; v < vertexCount; v++)
        {
            var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(bindSkinned[(v * stride) + 12]);
            if (bi < 0)
            {
                continue;
            }

            if (bi < minIndex)
            {
                minIndex = bi;
            }

            if (bi > maxIndex)
            {
                maxIndex = bi;
            }

            if (bi < slotLimit)
            {
                if (counts[bi] == 0)
                {
                    distinct++;
                }

                counts[bi]++;
            }
        }

        if (distinct == 0)
        {
            minIndex = 0;
            maxIndex = 0;
        }

        var dominantBone = 0;
        var dominantCount = 0;
        for (var i = 0; i < slotLimit; i++)
        {
            if (counts[i] <= dominantCount)
            {
                continue;
            }

            dominantBone = i;
            dominantCount = counts[i];
        }

        var parts = new List<string>(Math.Min(distinct, 16));
        for (var i = 0; i < slotLimit && parts.Count < 16; i++)
        {
            if (counts[i] > 0)
            {
                parts.Add($"{i}:{counts[i]}");
            }
        }

        var slotLine = parts.Count > 0 ? string.Join(',', parts) : "none";
        if (distinct > parts.Count)
        {
            slotLine += ",…";
        }

        return new BoneIndexHistogramSnapshot(
            vertexCount,
            distinct,
            minIndex == int.MaxValue ? 0 : minIndex,
            maxIndex == int.MinValue ? 0 : maxIndex,
            dominantBone,
            dominantCount,
            slotLine);
    }

    public static string FormatBoneIndexHistogramLine(
        string normalizedTexturePath,
        in BoneIndexHistogramSnapshot snap,
        int preparedBoneCount) =>
        "[3D preview] Entity bone indices: " +
        $"path={normalizedTexturePath} verts={snap.VertexCount} preparedBones={preparedBoneCount} " +
        $"distinct={snap.DistinctBoneIndices} min={snap.MinBoneIndex} max={snap.MaxBoneIndex} " +
        $"dominant=bone{snap.DominantBoneIndex}:{snap.VerticesOnDominantBone} slots=[{snap.SlotCountsLine}]";

    public static string FormatBoneIndexHistogramWarningLine(
        string normalizedTexturePath,
        in BoneIndexHistogramSnapshot snap,
        int preparedBoneCount)
    {
        if (snap.VertexCount <= 0)
        {
            return string.Empty;
        }

        if (snap.DistinctBoneIndices <= 1 && snap.VerticesOnDominantBone == snap.VertexCount)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} all {snap.VertexCount} bind verts use bone {snap.DominantBoneIndex} " +
                $"(expected multiple slots for multi-part rigs — skinning collapses to one bone → exploded parts)";
        }

        if (preparedBoneCount > 0 && snap.MaxBoneIndex >= preparedBoneCount)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} bind bone index max={snap.MaxBoneIndex} >= preparedBones={preparedBoneCount} " +
                $"(out-of-range indices clamp to bone 0 in shader)";
        }

        return string.Empty;
    }

    public static string FormatEntityShaderInitLine(
        string programLabel,
        int previewSpaceLoc,
        int bindMeshLoc,
        int gpuSkinningLoc,
        int boneCountLoc,
        int liftLoc,
        bool uboBlockBound) =>
        "[3D preview] Entity shader init: " +
        $"program={programLabel} locPreviewSpace={previewSpaceLoc} locBindMesh={bindMeshLoc} locGpuSkinning={gpuSkinningLoc} " +
        $"locBoneCount={boneCountLoc} locLiftY={liftLoc} uboBlockBound={(uboBlockBound ? 1 : 0)}";

    public static DrawContractSnapshot BuildDrawContractSnapshot(
        bool gpuEntityBoneSkinning,
        int preparedBoneCount,
        int vertexStrideFloats,
        bool setupAnimMotion,
        bool boneSnapshotValid,
        int boneSnapshotCount,
        bool bonePaletteUploaded,
        bool resolveOk,
        int uploadedGpuSkinning,
        int uploadedBoneCount,
        float uploadedLiftY,
        int uploadedBindMesh,
        bool uniformLocsComplete,
        bool entityBoneUboReady,
        bool verticesInPreviewSpace)
    {
        var gpuPath = gpuEntityBoneSkinning && preparedBoneCount > 0;
        var expectedGpuSkinning = 0;
        var expectedBoneCount = 0;
        var expectedBindMesh = 0;
        if (gpuPath && !verticesInPreviewSpace)
        {
            expectedBindMesh = 1;
            expectedGpuSkinning = setupAnimMotion && boneSnapshotValid && boneSnapshotCount > 0 ? 1 : 0;
            expectedBoneCount = boneSnapshotValid && boneSnapshotCount > 0 ? boneSnapshotCount : preparedBoneCount;
        }

        return new DrawContractSnapshot(
            gpuPath,
            resolveOk,
            setupAnimMotion,
            preparedBoneCount,
            expectedGpuSkinning,
            expectedBoneCount,
            uploadedGpuSkinning,
            uploadedBoneCount,
            uploadedLiftY,
            uploadedBindMesh,
            expectedBindMesh,
            boneSnapshotValid,
            bonePaletteUploaded,
            uniformLocsComplete,
            entityBoneUboReady,
            vertexStrideFloats,
            verticesInPreviewSpace);
    }

    public static string FormatEntityDrawContractLine(
        string passLabel,
        string normalizedTexturePath,
        in DrawContractSnapshot snap) =>
        "[3D preview] Entity draw contract: " +
        $"pass={passLabel} path={normalizedTexturePath} anim={(snap.SetupAnimMotion ? 1 : 0)} " +
        $"resolveOk={(snap.ResolveOk ? 1 : 0)} stride={snap.VertexStrideFloats} preparedBones={snap.PreparedBoneCount} " +
        $"expectSkin={snap.ExpectedGpuSkinning} expectBoneCount={snap.ExpectedBoneCount} expectBindMesh={snap.ExpectedBindMesh} " +
        $"uEntityGpuSkinning={snap.UploadedGpuSkinning} uEntityBoneCount={snap.UploadedBoneCount} uEntityMeshLiftY={snap.UploadedLiftY:0.####} " +
        $"uEntityBindMesh={snap.UploadedBindMesh} " +
        $"paletteUploaded={(snap.BonePaletteUploaded ? 1 : 0)} locsOk={(snap.UniformLocsComplete ? 1 : 0)} uboReady={(snap.EntityBoneUboReady ? 1 : 0)} " +
        $"previewSpaceVerts={(snap.VerticesInPreviewSpace ? 1 : 0)}";

    public static string FormatEntityDrawContractWarningLine(
        string normalizedTexturePath,
        in DrawContractSnapshot snap,
        int uploadedMeshStrideFloats = 0)
    {
        if (!snap.GpuEntityPath)
        {
            return string.Empty;
        }

        if (!snap.UniformLocsComplete)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} entity scalar uniform locations incomplete " +
                $"(anim={(snap.SetupAnimMotion ? 1 : 0)}; W()/skinning may not run until shaders reload)";
        }

        if (!snap.EntityBoneUboReady)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} EntitySkinningBones UBO not ready " +
                $"(anim-on bone multiply cannot run; anim-off W() still needs uEntityBindMesh)";
        }

        if (!snap.ResolveOk)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} entity draw state unresolved " +
                $"(GpuEntityBoneSkinning={snap.GpuEntityPath} prepared={snap.PreparedBoneCount}; uploaded zeros — raw texel scale)";
        }

        if (snap.PreparedBoneCount > 0 && snap.ExpectedBindMesh != snap.UploadedBindMesh && !snap.VerticesInPreviewSpace)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} uEntityBindMesh mismatch " +
                $"expect={snap.ExpectedBindMesh} got={snap.UploadedBindMesh} " +
                $"(shader skips W(); exploded parts for anim-on and anim-off)";
        }

        if (snap.PreparedBoneCount > 0 && snap.UploadedBoneCount <= 0 && snap.ExpectedGpuSkinning != 0 && !snap.VerticesInPreviewSpace)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} uEntityBoneCount=0 with preparedBones={snap.PreparedBoneCount} " +
                $"(anim-on bone multiply cannot run)";
        }

        if (snap.ExpectedGpuSkinning != snap.UploadedGpuSkinning || snap.ExpectedBoneCount != snap.UploadedBoneCount)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} entity uniform mismatch " +
                $"expectSkin={snap.ExpectedGpuSkinning} got={snap.UploadedGpuSkinning} " +
                $"expectBoneCount={snap.ExpectedBoneCount} got={snap.UploadedBoneCount} anim={(snap.SetupAnimMotion ? 1 : 0)}";
        }

        if (snap.ExpectedGpuSkinning != 0 && !snap.BonePaletteUploaded)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} anim-on uEntityGpuSkinning=1 but bone palette not uploaded";
        }

        if (snap.VertexStrideFloats != MinecraftModelBaker.FloatsPerSkinnedVertex)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} stride={snap.VertexStrideFloats} expected={MinecraftModelBaker.FloatsPerSkinnedVertex} at draw";
        }

        if (uploadedMeshStrideFloats > 0 &&
            snap.GpuEntityPath &&
            snap.VertexStrideFloats != uploadedMeshStrideFloats)
        {
            return
                $"[3D preview] GPU WARN: path={normalizedTexturePath} VAO stride={uploadedMeshStrideFloats} != subject stride={snap.VertexStrideFloats} " +
                $"(13-float entity VBO uploaded as 12-float misaligns every vertex after the first — exploded parts for anim-on and anim-off)";
        }

        return string.Empty;
    }

    private static void TryMeasureShaderPartCentroids(
        ReadOnlySpan<float> interleavedSkinned,
        int vertexStrideFloats,
        IReadOnlyList<string>? elementPartIds,
        ReadOnlySpan<Matrix4x4> boneMatrices,
        int boneCount,
        float meshSpaceLiftY,
        bool applyBonePalette,
        bool verticesInPreviewSpace,
        out Vector3? bodyCenter,
        out Vector3? legCenter,
        out Vector3? headCenter,
        out float sampleBodyBindY,
        out float sampleLegBindY)
    {
        bodyCenter = legCenter = headCenter = null;
        sampleBodyBindY = sampleLegBindY = 0f;
        if (vertexStrideFloats < MinecraftModelBaker.FloatsPerSkinnedVertex ||
            interleavedSkinned.Length < vertexStrideFloats)
        {
            return;
        }

        Vector3 bodySum = Vector3.Zero, legSum = Vector3.Zero, headSum = Vector3.Zero;
        var bodyN = 0;
        var legN = 0;
        var headN = 0;
        for (var i = 0; i + vertexStrideFloats - 1 < interleavedSkinned.Length; i += vertexStrideFloats)
        {
            var bi = EntityEmulatedGpuSkinningMath.DecodeSkinnedBoneIndexFromFloat(interleavedSkinned[i + 12]);
            if (bi < 0 || (elementPartIds is { Count: > 0 } && bi >= elementPartIds.Count))
            {
                continue;
            }

            var pBind = new Vector3(interleavedSkinned[i], interleavedSkinned[i + 1], interleavedSkinned[i + 2]);
            Vector3 preview;
            if (verticesInPreviewSpace)
            {
                preview = pBind;
            }
            else
            {
                var skinned = pBind;
                if (applyBonePalette && bi < boneCount)
                {
                    skinned = Vector3.Transform(pBind, boneMatrices[bi]);
                }

                preview = EntityEmulatedGpuSkinningMath.PreviewCuboidNormalizeTexelPosition(skinned);
                preview.Y += meshSpaceLiftY;
            }

            if (elementPartIds is not { Count: > 0 })
            {
                continue;
            }

            var partId = elementPartIds[bi];
            if (partId.Contains("body", StringComparison.OrdinalIgnoreCase))
            {
                bodySum += preview;
                bodyN++;
                if (sampleBodyBindY == 0f)
                {
                    sampleBodyBindY = pBind.Y;
                }
            }
            else if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase) &&
                     !partId.Contains("head", StringComparison.OrdinalIgnoreCase))
            {
                legSum += preview;
                legN++;
                if (sampleLegBindY == 0f)
                {
                    sampleLegBindY = pBind.Y;
                }
            }
            else if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                     !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += preview;
                headN++;
            }
        }

        if (bodyN > 0)
        {
            bodyCenter = bodySum / bodyN;
        }

        if (legN > 0)
        {
            legCenter = legSum / legN;
        }

        if (headN > 0)
        {
            headCenter = headSum / headN;
        }
    }
}
