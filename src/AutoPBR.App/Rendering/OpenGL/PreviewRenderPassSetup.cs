using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Entity emulated idle animation clock for OpenGL pass setup (extracted from pass setup partial).</summary>
internal static class PreviewRenderPassSetup
{
    internal static bool NeedsBindPoseMesh(
        bool parityCatalogCpuBindReady,
        bool meshDirty,
        bool bindPoseCommitted,
        PreviewModelSubject? blockModel) =>
        !parityCatalogCpuBindReady &&
        (meshDirty ||
         !bindPoseCommitted ||
         blockModel is not
         {
             GpuEntityBoneSkinning: true,
             VertexStrideFloats: EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex
         });

    internal static bool IsParityCatalogCpuBindReady(
        bool setupAnimMotion,
        PreviewModelSubject blockModel,
        bool bindPoseCommitted) =>
        !setupAnimMotion &&
        bindPoseCommitted &&
        !blockModel.GpuEntityBoneSkinning &&
        blockModel.EntityPreviewPlacementApplied &&
        blockModel.InterleavedVertices.Length > 0 &&
        blockModel.Indices.Length > 0;

    internal static float ResolveEntityEmulatedAnimClock(
        in GlRenderFrame frame,
        ref bool prevPauseEntityIdleAnimation,
        ref float frozenEntityIdleAnimClock,
        out bool pauseEdge)
    {
        pauseEdge = false;
        if (!frame.EntityEmulatedPreview || frame.BlockModel is null || frame.EntityRebakeCtx is null)
        {
            return 0f;
        }

        var speed = Math.Clamp(frame.Settings.EntityAnimationSpeed, 0f, 4f);
        var amp = Math.Clamp(frame.Settings.EntityAnimationAmplitude, 0f, 2f);
        var paused = frame.Settings.PauseEntityIdleAnimation;
        float clock;
        if (paused)
        {
            if (!prevPauseEntityIdleAnimation)
            {
                frozenEntityIdleAnimClock = (float)(frame.RenderTime * speed * amp);
            }

            clock = frozenEntityIdleAnimClock;
        }
        else
        {
            clock = (float)(frame.RenderTime * speed * amp);
        }

        pauseEdge = paused != prevPauseEntityIdleAnimation;
        prevPauseEntityIdleAnimation = paused;
        return clock;
    }

    internal static string BuildEntityGpuBindRebakeKey(EntityEmulatedPreviewRebakeContext ctx) =>
        $"{ctx.PackZipPath}\u001f{ctx.AssetArchivePath}\u001f{ctx.PackConverterCpuMeshFingerprint}\u001f{ctx.PreviewPoseId ?? ""}\u001f{ctx.PreviewSizeId ?? ""}\u001f{ctx.PreviewContextTypeId ?? ""}";

    /// <summary>
    /// Stable parity-catalog animation-off CPU bind key. Do not include mesh fingerprint — TryRebakeMesh updates
    /// <see cref="EntityEmulatedPreviewRebakeContext.PackConverterCpuMeshFingerprint"/> after commit, which must not
    /// invalidate the GL-committed subject on every UI re-push.
    /// </summary>
    internal static string BuildParityCatalogCpuBindCommitKey(EntityEmulatedPreviewRebakeContext ctx) =>
        $"{ctx.PackZipPath}\u001f{ctx.AssetArchivePath}\u001fparity-cpu-v{PreviewMeshGeometryFingerprint.PipelineRevision}\u001f{ctx.PreviewPoseId ?? ""}\u001f{ctx.PreviewSizeId ?? ""}\u001f{ctx.PreviewContextTypeId ?? ""}";

    internal static bool ParityCatalogCpuBindCommitKeyMatchesCurrentRevision(string? committedKey) =>
        committedKey is not null &&
        committedKey.Contains(
            $"parity-cpu-v{PreviewMeshGeometryFingerprint.PipelineRevision}",
            StringComparison.Ordinal);

    internal static bool IsParityCatalogEmulatedAsset(string? assetArchivePath)
    {
        if (string.IsNullOrWhiteSpace(assetArchivePath))
        {
            return false;
        }

        var norm = assetArchivePath.Replace('\\', '/').TrimStart('/');
        return EntityTextureParityCatalog.IsCatalogued(norm);
    }

    internal static string? ResolveEntityBindRebakeKey(
        bool setupAnimMotion,
        EntityEmulatedPreviewRebakeContext? rebakeCtx) =>
        rebakeCtx is null
            ? null
            : !setupAnimMotion && IsParityCatalogEmulatedAsset(rebakeCtx.AssetArchivePath)
                ? BuildParityCatalogCpuBindCommitKey(rebakeCtx)
                : BuildEntityGpuBindRebakeKey(rebakeCtx);
}
