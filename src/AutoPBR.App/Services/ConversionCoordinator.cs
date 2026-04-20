using AutoPBR.Core;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Services;

/// <summary>Runs the full PBR conversion from input pack to output zip. Delegates to <see cref="ResourcePackConverter.ConvertAsync"/>.</summary>
internal static class ConversionCoordinator
{
    /// <summary>Convert the resource pack using the given options and report progress.</summary>
    public static Task ConvertAsync(
        string inputZipPath,
        string outputZipPath,
        AutoPbrOptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken = default) =>
        ResourcePackConverter.ConvertAsync(inputZipPath, outputZipPath, options, progress, cancellationToken);
}
