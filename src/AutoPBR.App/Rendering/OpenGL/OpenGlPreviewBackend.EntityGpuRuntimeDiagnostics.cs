using System.Numerics;

using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private string? _entityGpuRuntimeDiagKey;
    private string? _entityDrawContractDiagKey;
    private string? _entityBoneIndexDiagKey;
    private string? _forceEntityCpuSkinningDiagKey;
    private int _lastUploadedGpuSkinning = -1;
    private int _lastUploadedBoneCount = -1;
    private float _lastUploadedLiftY;
    private int _lastUploadedPreviewSpaceVerts = -1;
    private int _lastUploadedBindMesh = -1;

    private void ResetEntityGpuRuntimeDiagState()
    {
        _entityGpuRuntimeDiagKey = null;
        _entityDrawContractDiagKey = null;
        _entityBindPosePrepDiagKey = null;
        _entityBoneIndexDiagKey = null;
        _forceEntityCpuSkinningDiagKey = null;
        _lastUploadedGpuSkinning = -1;
        _lastUploadedBoneCount = -1;
        _lastUploadedLiftY = 0f;
        _lastUploadedPreviewSpaceVerts = -1;
        _lastUploadedBindMesh = -1;
    }

    private void EmitEntityGpuRuntimeDiagnostic(
        PreviewModelSubject? subject,
        EntityEmulatedPreviewRebakeContext? rebake,
        bool setupAnimMotion,
        float animClock,
        bool boneFillOk,
        bool bonePaletteUploaded,
        string? boneFillHint)
    {
        if (subject is null || rebake is null || !subject.GpuEntityBoneSkinning)
        {
            return;
        }

        var norm = rebake.AssetArchivePath.Replace('\\', '/').TrimStart('/');
        if (!EntityTextureParityCatalog.IsCatalogued(norm))
        {
            return;
        }

        var stride = subject.VertexStrideFloats > 0
            ? subject.VertexStrideFloats
            : EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex;
        var prepared = rebake.GpuPreparedBoneCount ?? 0;
        var paletteCount = bonePaletteUploaded ? Math.Min(_lastEntityBoneSnapshotCount, _entityBoneScratch.Length) : 0;
        var snap = EntityGpuShaderDiagnostics.BuildRuntimeSnapshot(
            subject.InterleavedVertices,
            stride,
            rebake.ElementPartIds,
            prepared,
            boneFillOk,
            bonePaletteUploaded,
            _lastUploadedGpuSkinning,
            _lastUploadedBoneCount,
            _lastUploadedLiftY,
            _lastUploadedBindMesh,
            bonePaletteUploaded ? _entityBoneScratch.AsSpan(0, paletteCount) : ReadOnlySpan<Matrix4x4>.Empty,
            paletteCount,
            subject.EntityGpuVerticesInPreviewSpace || _lastUploadedPreviewSpaceVerts != 0,
            boneFillHint);

        var key =
            $"{norm}|anim={(setupAnimMotion ? 1 : 0)}|preview={snap.VerticesInPreviewSpace}|bind={snap.UploadedBindMesh}|skin={snap.UploadedGpuSkinning}|bones={snap.UploadedBoneCount}|stride={snap.VertexStrideFloats}|fill={(boneFillOk ? 1 : 0)}|hint={boneFillHint ?? ""}";
        if (!EntityPreviewDebugSettings.LogDrawContractEveryFrame &&
            string.Equals(key, _entityGpuRuntimeDiagKey, StringComparison.Ordinal))
        {
            return;
        }

        _entityGpuRuntimeDiagKey = key;
        EmitDiagnostic(EntityGpuShaderDiagnostics.FormatExploreGpuRuntimeLine(norm, setupAnimMotion, animClock, snap));
        var warn = EntityGpuShaderDiagnostics.FormatExploreGpuRuntimeWarningLine(norm, snap);
        if (!string.IsNullOrEmpty(warn))
        {
            EmitDiagnostic(warn);
        }

        EmitEntityBoneIndexHistogramDiagnostic(rebake, subject, prepared);
    }

    private void EmitEntityBoneIndexHistogramDiagnostic(
        EntityEmulatedPreviewRebakeContext rebake,
        PreviewModelSubject subject,
        int preparedBoneCount)
    {
        var norm = rebake.AssetArchivePath.Replace('\\', '/').TrimStart('/');
        if (!EntityTextureParityCatalog.IsCatalogued(norm))
        {
            return;
        }

        var bindVerts = rebake.GpuBindPoseInterleavedVertices;
        if (bindVerts is not { Length: > 0 })
        {
            if (subject is { EntityGpuVerticesInPreviewSpace: false, VertexStrideFloats: > 0 })
            {
                bindVerts = subject.InterleavedVertices;
            }
            else
            {
                return;
            }
        }

        var stride = EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex;
        var histogram = EntityGpuShaderDiagnostics.BuildBoneIndexHistogram(bindVerts, stride, preparedBoneCount);
        var histKey = $"{norm}|verts={histogram.VertexCount}|distinct={histogram.DistinctBoneIndices}|dom={histogram.DominantBoneIndex}";
        if (!EntityPreviewDebugSettings.LogDrawContractEveryFrame &&
            string.Equals(histKey, _entityBoneIndexDiagKey, StringComparison.Ordinal))
        {
            return;
        }

        _entityBoneIndexDiagKey = histKey;
        EmitDiagnostic(EntityGpuShaderDiagnostics.FormatBoneIndexHistogramLine(norm, histogram, preparedBoneCount));
        var histWarn = EntityGpuShaderDiagnostics.FormatBoneIndexHistogramWarningLine(norm, histogram, preparedBoneCount);
        if (!string.IsNullOrEmpty(histWarn))
        {
            EmitDiagnostic(histWarn);
        }
    }

    private void EmitEntityDrawContractDiagnostic(
        string passLabel,
        EntitySkinningUniformLocs locs,
        PreviewModelSubject? subject,
        bool setupAnimMotion,
        bool boneSnapshotValid,
        int boneSnapshotCount,
        bool bonePaletteUploaded,
        bool resolveOk)
    {
        if (subject is not { GpuEntityBoneSkinning: true, EmulatedRebake: { } rebake })
        {
            return;
        }

        var norm = rebake.AssetArchivePath.Replace('\\', '/').TrimStart('/');
        if (!EntityTextureParityCatalog.IsCatalogued(norm))
        {
            return;
        }

        var prepared = rebake.GpuPreparedBoneCount ?? 0;
        var stride = subject.VertexStrideFloats > 0
            ? subject.VertexStrideFloats
            : EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex;
        var contract = EntityGpuShaderDiagnostics.BuildDrawContractSnapshot(
            subject.GpuEntityBoneSkinning,
            prepared,
            stride,
            setupAnimMotion,
            boneSnapshotValid,
            boneSnapshotCount,
            bonePaletteUploaded,
            resolveOk,
            _lastUploadedGpuSkinning,
            _lastUploadedBoneCount,
            _lastUploadedLiftY,
            _lastUploadedBindMesh,
            locs.IsComplete,
            _entityBoneUbo != 0,
            subject.EntityGpuVerticesInPreviewSpace || _lastUploadedPreviewSpaceVerts != 0);

        var key =
            $"{passLabel}|{norm}|anim={(setupAnimMotion ? 1 : 0)}|resolve={(resolveOk ? 1 : 0)}|" +
            $"skin={contract.UploadedGpuSkinning}|bones={contract.UploadedBoneCount}|expectSkin={contract.ExpectedGpuSkinning}|expectBones={contract.ExpectedBoneCount}|" +
            $"palette={(bonePaletteUploaded ? 1 : 0)}|locs={(locs.IsComplete ? 1 : 0)}|uploadStride={_lastMeshUploadStride}|stride={stride}";
        var warn = EntityGpuShaderDiagnostics.FormatEntityDrawContractWarningLine(norm, contract, _lastMeshUploadStride);
        if (EntityPreviewDebugSettings.LogDrawContractEveryFrame)
        {
            EmitDiagnostic(EntityGpuShaderDiagnostics.FormatEntityDrawContractLine(passLabel, norm, contract));
            if (!string.IsNullOrEmpty(warn))
            {
                EmitDiagnostic(warn);
            }

            return;
        }

        if (string.IsNullOrEmpty(warn))
        {
            return;
        }

        if (string.Equals(key, _entityDrawContractDiagKey, StringComparison.Ordinal))
        {
            return;
        }

        _entityDrawContractDiagKey = key;
        EmitDiagnostic(EntityGpuShaderDiagnostics.FormatEntityDrawContractLine(passLabel, norm, contract));
        EmitDiagnostic(warn);
    }

    private int _lastEntityBoneSnapshotCount;

    private void EmitEntityBindPosePrepDiagnostic(
        string? rebakeKey,
        int boneCount,
        int vertexCount,
        int indexCount,
        float liftY,
        bool setupAnimMotion)
    {
        if (rebakeKey is null)
        {
            return;
        }

        var key = $"{rebakeKey}|anim={(setupAnimMotion ? 1 : 0)}|bones={boneCount}|verts={vertexCount}|lift={liftY:0.####}";
        if (string.Equals(key, _entityBindPosePrepDiagKey, StringComparison.Ordinal))
        {
            return;
        }

        _entityBindPosePrepDiagKey = key;
        EmitDiagnostic(
            setupAnimMotion
                ? $"[3D preview] Emulated entity GPU skinned mesh prepared: bones={boneCount}, verts={vertexCount}, indices={indexCount}."
                : $"[3D preview] Emulated entity GPU bind-pose mesh prepared (shader W()+lift): bones={boneCount}, verts={vertexCount}, indices={indexCount}, liftY={liftY:0.####}.");
    }
}
