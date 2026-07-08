using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Preview;

internal sealed class MergedJavaBlockModel
{
    public required List<ModelElement> Elements { get; init; }
    public required Dictionary<string, string> Textures { get; init; }

    /// <summary>
    /// True when ApplyLivingEntityRendererColumnRootScale (or equivalent LER <c>scale(-1,-1,1)</c>)
    /// was folded into element <see cref="ModelElement.LocalToParent"/> poses. Drives vertical face-plane UV routing at bake.
    /// </summary>
    public bool UsesLivingEntityRendererColumnYFlip { get; init; }
}
