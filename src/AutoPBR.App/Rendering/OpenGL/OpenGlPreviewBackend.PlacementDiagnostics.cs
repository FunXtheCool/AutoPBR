using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private string? _parityPlacementDiagKey;
    private string? _entityCpuRebakeDiagKey;
    private string? _depthLayerDiagKey;
    private bool _loggedDepthBits;

    private void EmitParityCatalogPlacementDiagnostic(
        PreviewModelSubject? subject,
        EntityEmulatedPreviewRebakeContext? rebake,
        bool setupAnimMotion,
        bool gpuSkinning,
        float animClock)
    {
        if (subject is null || rebake is null)
        {
            return;
        }

        var norm = rebake.AssetArchivePath.Replace('\\', '/').TrimStart('/');
        if (!EntityTextureParityCatalog.IsCatalogued(norm))
        {
            return;
        }

        var lerBasis = subject.MeshProvenance?.Detail
            ?? rebake.MeshProvenance?.Detail
            ?? "unknown";
        var dedupeKey =
            $"{norm}|gpu={(gpuSkinning ? 1 : 0)}|motion={(setupAnimMotion ? 1 : 0)}|lift={subject.EntityGpuMeshSpaceLiftY:0.####}|ler={lerBasis}|profile={rebake.NativeProfileName}|stride={_lastMeshUploadStride}|rev={PreviewMeshGeometryFingerprint.PipelineRevision}";
        if (string.Equals(dedupeKey, _parityPlacementDiagKey, StringComparison.Ordinal))
        {
            return;
        }

        _parityPlacementDiagKey = dedupeKey;
        EmitDiagnostic(EntityPreviewPlacement.FormatExplorePlacementDiagnosticLine(
            norm,
            lerBasis,
            gpuSkinning,
            subject.EntityGpuMeshSpaceLiftY,
            animClock,
            setupAnimMotion,
            rebake.LastGroundContactY,
            rebake.LastBodyCentroidY,
            rebake.LastHeadCentroidY,
            rebake.LastLegCentroidY));
        EmitDiagnostic(
            $"[3D preview] Parity state: path={norm} profile={rebake.NativeProfileName} parsed={rebake.NativeParsedVersion ?? "?"} " +
            $"source={lerBasis} columnPose={(IsDolphinColumnPoseSource(lerBasis) ? 1 : 0)} " +
            $"legacyPose={(EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose ? 1 : 0)} lerOverride={EntityPreviewDebugSettings.LerBasisOverride} " +
            $"uploadStride={_lastMeshUploadStride} pipelineRev={PreviewMeshGeometryFingerprint.PipelineRevision} " +
            $"subjectPreview={(subject.EntityGpuVerticesInPreviewSpace ? 1 : 0)} placed={(subject.EntityPreviewPlacementApplied ? 1 : 0)}");
    }

    private static bool IsDolphinColumnPoseSource(string source) =>
        string.Equals(source, "net.minecraft.client.model.animal.dolphin.DolphinModel", StringComparison.Ordinal) ||
        string.Equals(source, "net.minecraft.client.model.animal.dolphin.BabyDolphinModel", StringComparison.Ordinal);

    private void EmitDepthLayerDiagnostic(
        PreviewModelSubject? subject,
        float nearPlane,
        float farPlane,
        GL gl)
    {
        if (subject is null || subject.DrawBatches is not { Length: > 0 } batches)
        {
            return;
        }

        var key =
            $"{subject.MeshProvenance?.Detail ?? "mesh"}|batches={batches.Length}|near={nearPlane:0.###}|far={farPlane:0.###}";
        if (string.Equals(key, _depthLayerDiagKey, StringComparison.Ordinal))
        {
            return;
        }

        _depthLayerDiagKey = key;
        if (!_loggedDepthBits)
        {
            _loggedDepthBits = true;
            if (TryQueryBoundFramebufferDepthBits(gl, out var depthBits))
            {
                EmitDiagnostic($"[3D preview] GL_DEPTH_BITS={depthBits}");
            }
        }

        EmitDiagnostic(
            $"[3D preview] Depth layers: near={nearPlane:0.###} far={farPlane:0.###} ratio={farPlane / Math.Max(nearPlane, 1e-6f):0.###}");
        EmitDiagnostic($"[3D preview] {PreviewDrawBatchDiagnostics.FormatBatchSummary(batches, subject.Materials.Length)}");
    }

    private static bool TryQueryBoundFramebufferDepthBits(GL gl, out int depthBits)
    {
        Span<int> size = stackalloc int[1];
        gl.GetFramebufferAttachmentParameter(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            FramebufferAttachmentParameterName.DepthSize,
            size);
        if (gl.GetError() != GLEnum.NoError)
        {
            depthBits = 0;
            return false;
        }

        depthBits = size[0];
        return depthBits > 0;
    }
}
