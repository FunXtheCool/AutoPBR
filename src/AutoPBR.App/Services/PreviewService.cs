using AutoPBR.Core;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Services;

/// <summary>Renders a single-texture PBR preview from a pack entry. Delegates to <see cref="ResourcePackConverter.RenderPreviewAsync"/>.</summary>
internal static class PreviewService
{
    /// <summary>Build a 2D composite preview (diffuse, normal, specular, height) for a single texture. Returns PNG bytes.</summary>
    public static Task<byte[]> RenderPreviewAsync(
        string inputZipPath,
        string archivePath,
        AutoPbrOptions options,
        CancellationToken cancellationToken = default) =>
        new ResourcePackConverter().RenderPreviewAsync(inputZipPath, archivePath, options, cancellationToken);
}
