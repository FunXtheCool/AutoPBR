using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Temporary texture-path bridge until geometry IR and clean-room builders emit explicit layer metadata.
/// Keep patterns narrow; replace with per-part policies for canary entities.
/// </summary>
internal static class PreviewDepthLayerHeuristics
{
    public static bool TryInferKind(string normalizedTextureZipPath, out PreviewDepthLayerKind kind)
    {
        var path = normalizedTextureZipPath.Replace('\\', '/');
        var file = path;
        var slash = path.LastIndexOf('/');
        if (slash >= 0)
        {
            file = path[(slash + 1)..];
        }

        if (ContainsOverlayToken(file) || ContainsOverlayToken(path))
        {
            if (ContainsEmissiveToken(file) || ContainsEmissiveToken(path))
            {
                kind = PreviewDepthLayerKind.EmissiveOverlay;
                return true;
            }

            if (ContainsCosmeticOverlayToken(file) || ContainsCosmeticOverlayToken(path))
            {
                kind = PreviewDepthLayerKind.CosmeticOverlay;
                return true;
            }

            kind = PreviewDepthLayerKind.CutoutOverlay;
            return true;
        }

        kind = PreviewDepthLayerKind.Base;
        return false;
    }

    private static bool ContainsOverlayToken(string value)
    {
        return value.Contains("/overlay", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("_overlay", StringComparison.OrdinalIgnoreCase) ||
               ContainsEmissiveToken(value) ||
               value.Contains("profession", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("profession_level", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsEmissiveToken(string value) =>
        value.Contains("emissive", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("glow", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsCosmeticOverlayToken(string value) =>
        value.Contains("_eyes", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("profession", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("profession_level", StringComparison.OrdinalIgnoreCase);
}
