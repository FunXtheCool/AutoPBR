namespace AutoPBR.Core.Preview;

using AutoPBR.Core.Models;

internal static class PreviewProvenanceFormatter
{
    internal static string Tag(string? detail, string tag) =>
        string.IsNullOrWhiteSpace(detail) ? tag : $"{detail} · {tag}";

    internal static PreviewMeshProvenance WithTag(PreviewMeshProvenance provenance, string tag) =>
        provenance with { Detail = Tag(provenance.Detail, tag) };
}
