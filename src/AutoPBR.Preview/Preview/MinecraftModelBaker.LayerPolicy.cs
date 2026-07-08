using AutoPBR.Core.Models;

namespace AutoPBR.Preview;

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
        if (element.CastsShadow)
        {
            if (policy.ShadowMode == PreviewDrawLayerShadowMode.Skip)
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
        }
        else if (policy.ShadowMode == PreviewDrawLayerShadowMode.Draw)
        {
            policy = new PreviewDrawLayerPolicy
            {
                Kind = policy.Kind,
                DrawOrder = policy.DrawOrder,
                DepthBiasStep = policy.DepthBiasStep,
                DepthWrite = policy.DepthWrite,
                ShadowMode = PreviewDrawLayerShadowMode.Skip,
            };
        }

        return policy;
    }
}
