using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal static partial class MinecraftModelBaker
{
    private static PreviewDrawLayerPolicy ResolveElementLayerPolicy(
        ModelElement element,
        string normalizedTextureZipPath,
        IReadOnlyDictionary<string, string> textures)
    {
        var kind = element.DepthLayerKind;
        var layerOrdinal = element.LayerOrdinal;
        if (kind == PreviewDepthLayerKind.Base &&
            PreviewDepthLayerResolver.TryResolveElement(element, textures, out var resolvedKind, out var resolvedOrdinal, out _))
        {
            kind = resolvedKind;
            layerOrdinal = resolvedOrdinal;
        }
        else if (kind == PreviewDepthLayerKind.Base &&
                 PreviewDepthLayerHeuristics.TryInferKind(normalizedTextureZipPath, out var inferred))
        {
            kind = inferred;
        }

        var policy = PreviewDrawLayerPolicy.ForKind(kind, layerOrdinal);
        if (element.CastsShadow && policy.ShadowMode == PreviewDrawLayerShadowMode.Skip)
        {
            policy = new PreviewDrawLayerPolicy
            {
                Kind = policy.Kind,
                DrawOrder = policy.DrawOrder,
                DepthBiasStep = policy.DepthBiasStep,
                DepthWrite = policy.DepthWrite,
                ShadowMode = PreviewDrawLayerShadowMode.Draw,
            };
        }

        return policy;
    }
}
