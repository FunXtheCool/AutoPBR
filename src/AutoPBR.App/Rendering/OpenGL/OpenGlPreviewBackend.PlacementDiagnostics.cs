using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private string? _parityPlacementDiagKey;
    private string? _entityCpuRebakeDiagKey;

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
            $"{norm}|gpu={(gpuSkinning ? 1 : 0)}|motion={(setupAnimMotion ? 1 : 0)}|lift={subject.EntityGpuMeshSpaceLiftY:0.####}|ler={lerBasis}";
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
    }
}
