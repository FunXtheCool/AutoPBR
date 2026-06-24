using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

file static class EntityEmulatedBoneFillScratch
{
    public static readonly List<Matrix4x4> BoneMatrices = new(EntityGpuSkinningLimits.MaxBones);
}

/// <summary>Rebuilds emulated entity preview geometry for a new animation clock (same pipeline as initial bake, without temp extraction).</summary>
public static class EntityEmulatedPreviewRebaker
{
    private static IDisposable? EnterPreviewPoseScope(EntityEmulatedPreviewRebakeContext rebake) =>
        string.IsNullOrWhiteSpace(rebake.PreviewPoseId)
            ? null
            : EntityPreviewBuildContext.UsePose(rebake.PreviewPoseId);

    private static IDisposable? EnterPreviewSizeScope(EntityEmulatedPreviewRebakeContext rebake) =>
        string.IsNullOrWhiteSpace(rebake.PreviewSizeId)
            ? null
            : EntityPreviewBuildContext.UseSize(rebake.PreviewSizeId);

    /// <summary>
    /// Recomputes interleaved vertices, indices, and draw batches for an emulated entity subject.
    /// </summary>
    /// <param name="rebake">Captured at initial detailed preview bake.</param>
    /// <param name="materialsInBakeOrder">Same order as <see cref="EntityEmulatedPreviewRebakeContext.OrderedTextureZipPaths"/>.</param>
    /// <param name="animationTimeSeconds">Wall-clock style seconds (speed/amplitude applied by caller).</param>
    /// <param name="interleavedVertices">Lifted vertex buffer on success.</param>
    /// <param name="indices">Index buffer on success.</param>
    /// <param name="drawBatches">Material batch ranges on success.</param>
    /// <param name="applyGeometryIrSetupAnimMotion">When false, keeps lifted geometry IR bind pose (no setupAnim overlay).</param>
    public static bool TryRebakeMesh(
        EntityEmulatedPreviewRebakeContext rebake,
        IReadOnlyList<PreviewTextureMaps> materialsInBakeOrder,
        float animationTimeSeconds,
        out float[]? interleavedVertices,
        out uint[]? indices,
        out PreviewDrawBatch[]? drawBatches,
        bool applyGeometryIrSetupAnimMotion = true)
    {
        interleavedVertices = null;
        indices = null;
        drawBatches = null;

        if (string.IsNullOrWhiteSpace(rebake.NativeRootDirectory) || !Directory.Exists(rebake.NativeRootDirectory))
        {
            return false;
        }

        if (materialsInBakeOrder.Count != rebake.OrderedTextureZipPaths.Length)
        {
            return false;
        }

        Version? parsed = null;
        if (!string.IsNullOrWhiteSpace(rebake.NativeParsedVersion))
        {
            _ = Version.TryParse(rebake.NativeParsedVersion, out parsed);
        }

        var profile = new MinecraftNativeProfile(
            rebake.NativeProfileName,
            rebake.NativeRootDirectory,
            parsed);

        var runtime = EntityModelRuntimeFactory.Create();
        using var previewPoseScope = EnterPreviewPoseScope(rebake);
        using var previewSizeScope = EnterPreviewSizeScope(rebake);
        if (!runtime.TryBuildStaticMesh(
                rebake.AssetArchivePath,
                profile,
                rebake.IdlePhase01,
                animationTimeSeconds,
                out var merged,
                out var meshProvenance,
                applyGeometryIrSetupAnimMotion))
        {
            return false;
        }

        rebake.MeshProvenance = meshProvenance;

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, rebake.ModelDefaultNamespace);
        if (ordered.Count != rebake.OrderedTextureZipPaths.Length)
        {
            return false;
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            if (!string.Equals(ordered[i], rebake.OrderedTextureZipPaths[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            pathToIdx[p] = i;
            texSizes[p] = (materialsInBakeOrder[i].Width, materialsInBakeOrder[i].Height);
        }

        var profileForParts = profile;
        EntityPreviewPlacement.TryPopulateRebakeElementPartIds(rebake, profileForParts, merged.Elements.Count);

        if (!MinecraftModelBaker.TryBake(
                merged,
                rebake.ModelDefaultNamespace,
                pathToIdx,
                texSizes,
                out var verts,
                out var idx,
                out var batchesList))
        {
            return false;
        }

        EntityPreviewPlacement.TryMeasureMergedModelPartCentroidsY(
            merged,
            rebake.ElementPartIds!,
            out var bindBodyY,
            out var bindHeadY,
            out var bindLegY);
        var placement = EntityPreviewPlacement.ApplyToPreviewVertices(
            verts,
            MinecraftModelBaker.FloatsPerVertex,
            rebake.ElementPartIds!,
            EntityPreviewPlacement.DefaultFloorY);
        rebake.LastGroundContactY = placement.GroundContactY;
        rebake.LastGroundLiftY = placement.GroundLiftY;
        var placementYOffset = placement.AnchorOffset.Y + placement.GroundLiftY;
        rebake.LastBodyCentroidY = bindBodyY != 0f ? bindBodyY + placementYOffset : placement.BodyCentroidY;
        rebake.LastHeadCentroidY = bindHeadY != 0f ? bindHeadY + placementYOffset : placement.HeadCentroidY;
        rebake.LastLegCentroidY = bindLegY != 0f ? bindLegY + placementYOffset : placement.LegCentroidY;

        interleavedVertices = verts;
        indices = idx;
        drawBatches = batchesList.ToArray();
        return true;
    }

    /// <summary>
    /// One-time CPU bake: bind-pose vertices + bone indices, plus mesh-space ground lift.
    /// Lift must transform those bind-pose vertices with <b>bind-pose</b> bone matrices (same clock as <see cref="MinecraftModelBaker.TryBakeBindPoseForGpuSkinning"/>);
    /// using an animated pose here misaligns transforms and produces a bogus vertical lift.
    /// </summary>
    public static bool TryPrepareGpuSkinnedEmulatedMesh(
        EntityEmulatedPreviewRebakeContext rebake,
        IReadOnlyList<PreviewTextureMaps> materialsInBakeOrder,
        float groundPlaneY,
        float groundClearance,
        out float[]? interleavedVertices,
        out uint[]? indices,
        out PreviewDrawBatch[]? drawBatches,
        out int boneCount,
        out float meshSpaceLiftY,
        bool applyGeometryIrSetupAnimMotion = false)
    {
        interleavedVertices = null;
        indices = null;
        drawBatches = null;
        boneCount = 0;
        meshSpaceLiftY = 0f;
        rebake.GpuBindPoseInverseLocalToParent = null;
        rebake.GpuBindPoseBonePalette = null;
        rebake.GpuBindPoseInterleavedVertices = null;

        if (string.IsNullOrWhiteSpace(rebake.NativeRootDirectory) || !Directory.Exists(rebake.NativeRootDirectory))
        {
            return false;
        }

        if (materialsInBakeOrder.Count != rebake.OrderedTextureZipPaths.Length)
        {
            return false;
        }

        Version? parsed = null;
        if (!string.IsNullOrWhiteSpace(rebake.NativeParsedVersion))
        {
            _ = Version.TryParse(rebake.NativeParsedVersion, out parsed);
        }

        var profile = new MinecraftNativeProfile(
            rebake.NativeProfileName,
            rebake.NativeRootDirectory,
            parsed);

        var runtime = EntityModelRuntimeFactory.Create();
        using var previewPoseScope = EnterPreviewPoseScope(rebake);
        using var previewSizeScope = EnterPreviewSizeScope(rebake);
        if (!runtime.TryBuildStaticMesh(
                rebake.AssetArchivePath,
                profile,
                rebake.IdlePhase01,
                0f,
                out var mergedBind,
                out var meshProvenance,
                applyGeometryIrSetupAnimMotion: false))
        {
            return false;
        }

        rebake.MeshProvenance = meshProvenance;

        EntityPreviewPlacement.TryPopulateRebakeElementPartIds(rebake, profile, mergedBind.Elements.Count);

        if (mergedBind.Elements.Count > EntityGpuSkinningLimits.MaxBones)
        {
            return false;
        }

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mergedBind, rebake.ModelDefaultNamespace);
        if (ordered.Count != rebake.OrderedTextureZipPaths.Length)
        {
            return false;
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            if (!string.Equals(ordered[i], rebake.OrderedTextureZipPaths[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            pathToIdx[p] = i;
            texSizes[p] = (materialsInBakeOrder[i].Width, materialsInBakeOrder[i].Height);
        }

        if (!MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
                mergedBind,
                rebake.ModelDefaultNamespace,
                pathToIdx,
                texSizes,
                out var verts,
                out var idx,
                out var batchesList))
        {
            return false;
        }

        boneCount = mergedBind.Elements.Count;
        var inv = new Matrix4x4[boneCount];
        for (var i = 0; i < boneCount; i++)
        {
            if (!Matrix4x4.Invert(mergedBind.Elements[i].LocalToParent, out inv[i]))
            {
                inv[i] = Matrix4x4.Identity;
            }
        }

        rebake.GpuBindPoseInverseLocalToParent = inv;
        rebake.GpuBindPoseBonePalette = EntityGpuShaderDiagnostics.BuildBindPoseBonePalette(mergedBind);
        rebake.GpuBindPoseInterleavedVertices = verts;

        var placement = EntityPreviewPlacement.ApplyToGpuBindVertices(
            verts,
            rebake.ElementPartIds,
            groundPlaneY,
            groundClearance);
        meshSpaceLiftY = placement.GroundLiftY;
        rebake.LastGroundContactY = placement.GroundContactY;
        rebake.LastGroundLiftY = placement.GroundLiftY;
        rebake.LastBodyCentroidY = placement.BodyCentroidY;
        rebake.LastHeadCentroidY = placement.HeadCentroidY;
        rebake.LastLegCentroidY = placement.LegCentroidY;

        _ = applyGeometryIrSetupAnimMotion;

        interleavedVertices = verts;
        indices = idx;
        drawBatches = batchesList.ToArray();
        return true;
    }

    /// <summary>Rebuilds only per-element model-space bone matrices for the current animation clock (no mesh tessellation).</summary>
    public static bool TryFillEmulatedEntityBoneMatrices(
        EntityEmulatedPreviewRebakeContext rebake,
        float animationTimeSeconds,
        Span<Matrix4x4> bonesOut,
        out int boneCount,
        bool applyGeometryIrSetupAnimMotion = true)
    {
        boneCount = 0;
        if (bonesOut.Length < EntityGpuSkinningLimits.MaxBones)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rebake.NativeRootDirectory) || !Directory.Exists(rebake.NativeRootDirectory))
        {
            return false;
        }

        Version? parsed = null;
        if (!string.IsNullOrWhiteSpace(rebake.NativeParsedVersion))
        {
            _ = Version.TryParse(rebake.NativeParsedVersion, out parsed);
        }

        var profile = new MinecraftNativeProfile(
            rebake.NativeProfileName,
            rebake.NativeRootDirectory,
            parsed);

        var runtime = EntityModelRuntimeFactory.Create();
        var animTime = applyGeometryIrSetupAnimMotion ? animationTimeSeconds : 0f;
        var useFullMeshExtract = EntityGpuBoneFillPolicy.RequiresFullMeshBoneExtract(rebake.AssetArchivePath);
        using var previewPoseScope = EnterPreviewPoseScope(rebake);
        using var previewSizeScope = EnterPreviewSizeScope(rebake);
        if (useFullMeshExtract)
        {
            if (!runtime.TryBuildStaticMesh(
                    rebake.AssetArchivePath,
                    profile,
                    rebake.IdlePhase01,
                    animTime,
                    out var merged,
                    applyGeometryIrSetupAnimMotion))
            {
                return false;
            }

            if (merged.Elements.Count > EntityGpuSkinningLimits.MaxBones)
            {
                return false;
            }

            boneCount = merged.Elements.Count;
            var inv = rebake.GpuBindPoseInverseLocalToParent;
            if (rebake.GpuPreparedBoneCount is > 0 &&
                (inv is null || inv.Length != boneCount))
            {
                boneCount = 0;
                return false;
            }

            if (inv is not null)
            {
                for (var i = 0; i < boneCount; i++)
                {
                    bonesOut[i] = ComposeGpuBonePaletteEntry(inv[i], merged.Elements[i].LocalToParent);
                }
            }
            else
            {
                for (var i = 0; i < boneCount; i++)
                {
                    bonesOut[i] = merged.Elements[i].LocalToParent;
                }
            }

            if (rebake.GpuPreparedBoneCount is > 0 and var expectedGpu2 && boneCount != expectedGpu2)
            {
                boneCount = 0;
                return false;
            }

            return true;
        }

        var scratch = EntityEmulatedBoneFillScratch.BoneMatrices;
        scratch.Clear();
        if (!runtime.TryFillBoneMatricesFast(
                rebake.AssetArchivePath,
                profile,
                rebake.IdlePhase01,
                animTime,
                scratch,
                out boneCount,
                rebake,
                applyGeometryIrSetupAnimMotion))
        {
            return false;
        }

        if (boneCount > bonesOut.Length)
        {
            return false;
        }

        var invFast = rebake.GpuBindPoseInverseLocalToParent;
        if (rebake.GpuPreparedBoneCount is > 0 &&
            (invFast is null || invFast.Length != boneCount))
        {
            boneCount = 0;
            return false;
        }

        if (invFast is not null)
        {
            for (var i = 0; i < boneCount; i++)
            {
                bonesOut[i] = ComposeGpuBonePaletteEntry(invFast[i], scratch[i]);
            }
        }
        else
        {
            for (var i = 0; i < boneCount; i++)
            {
                bonesOut[i] = scratch[i];
            }
        }

        if (rebake.GpuPreparedBoneCount is > 0 and var expectedGpuTail && boneCount != expectedGpuTail)
        {
            boneCount = 0;
            return false;
        }

        return true;
    }

    private static Matrix4x4 ComposeGpuBonePaletteEntry(in Matrix4x4 invBind, in Matrix4x4 anim) =>
        Matrix4x4.Multiply(invBind, anim);
}
