using System.Numerics;

namespace AutoPBR.Preview;

/// <summary>
/// Vanilla <c>SlimeRenderer.scale(SlimeRenderState, PoseStack)</c> (26.1.2 <c>client.jar</c>).
/// Squish-driven layer offsets stay in model setupAnim; uniform/non-uniform scale is renderer-only.
/// </summary>
public static class SlimeFamilyPreviewScale
{
    /// <summary>Preview bounce squish aligned with clean-room magma cube and geometry-IR motion passes.</summary>
    public static float ComputePreviewSquish(float animationTimeSeconds) =>
        MathF.Max(0f, MathF.Sin(animationTimeSeconds * MathF.PI * 2f * 0.8f));

    /// <summary>
    /// <c>f5 = squish / (size * 0.5 + 1)</c>; <c>f6 = 1 / (f5 + 1)</c>; scale <c>(f6 * size, size / f6, f6 * size)</c>.
    /// Omits the 0.999 uniform scale and 0.001 Y nudge (z-fighting tweaks only).
    /// </summary>
    public static Vector3 ComputeRendererScaleFactors(int size, float squish)
    {
        var sizeF = (float)size;
        var f5 = squish / (sizeF * 0.5f + 1f);
        var f6 = 1f / (f5 + 1f);
        return new Vector3(f6 * sizeF, sizeF / f6, f6 * sizeF);
    }

    internal static MergedJavaBlockModel ApplyRendererScale(MergedJavaBlockModel model, int size, float squish)
    {
        var scale = ComputeRendererScaleFactors(size, squish);
        if (MathF.Abs(scale.X - 1f) < 1e-5f &&
            MathF.Abs(scale.Y - 1f) < 1e-5f &&
            MathF.Abs(scale.Z - 1f) < 1e-5f)
        {
            return model;
        }

        var transform = Matrix4x4.CreateScale(scale);
        var transformed = new List<ModelElement>(model.Elements.Count);
        foreach (var e in model.Elements)
        {
            transformed.Add(new ModelElement
            {
                From = e.From,
                To = e.To,
                Faces = e.Faces,
                LocalToParent = Matrix4x4.Multiply(e.LocalToParent, transform),
                DepthLayerKind = e.DepthLayerKind,
                LayerOrdinal = e.LayerOrdinal,
                CastsShadow = e.CastsShadow,
                ShellInflateTexels = e.ShellInflateTexels,
                EnableParallax = e.EnableParallax,
                MirrorCuboidUv = e.MirrorCuboidUv,
            });
        }

        return new MergedJavaBlockModel
        {
            Elements = transformed,
            Textures = model.Textures,
            UsesLivingEntityRendererColumnYFlip = model.UsesLivingEntityRendererColumnYFlip,
        };
    }
}
