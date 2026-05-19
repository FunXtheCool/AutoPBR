
namespace AutoPBR.Core.Preview;

/// <summary>
/// Decides how GPU emulated-entity bone matrices are derived from clean-room rigs: fast pose capture vs full mesh extract.
/// </summary>
internal static class EntityGpuBoneFillPolicy
{
    /// <summary>
    /// Use a full <see cref="CleanRoomEntityModelRuntime.TryBuildStaticMesh"/> pass and copy
    /// <see cref="ModelElement.LocalToParent"/> into the GPU bone buffer (same source as bind-pose lift sampling),
    /// instead of <see cref="CleanRoomEntityModelRuntime.TryFillBoneMatricesFast"/> pose capture.
    /// </summary>
    /// <remarks>
    /// Equine rigs apply LER mirror as <c>LocalToParent * S</c> via a dedicated path. Quadruped cow/wolf/pig-style rigs use the same compose order when
    /// <see cref="CleanRoomEntityModelRuntime.UsesQuadrupedLerMirrorRightComposeLocalChain"/> matches. Chicken forces full mesh extract as an A/B path.
    /// </remarks>
    public static bool RequiresFullMeshBoneExtract(string normalizedAssetPath)
    {
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        if (norm.Contains("horse", StringComparison.OrdinalIgnoreCase) ||
            norm.Contains("donkey", StringComparison.OrdinalIgnoreCase) ||
            norm.Contains("mule", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Vanilla chicken family diffuse paths (cold variant + jockey sheet live under .../chicken/).
        return norm.Contains("/textures/entity/chicken/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stems whose rigs return <c>b.Build</c> without folding <see cref="CleanRoomEntityModelRuntime.ApplyLivingEntityRendererPreviewBasis"/>:
    /// captured bone matrices are already in the same space as merged <see cref="ModelElement.LocalToParent"/>.
    /// </summary>
    internal static readonly HashSet<string> StemsSkippingLivingPreviewBasis = new(StringComparer.OrdinalIgnoreCase)
    {
        "arrow",
        "banner",
        "bed",
        "bell",
        "end_crystal",
        "evoker_fangs",
        "llama_spit",
        "minecart",
        "piglin_skull",
        "shield",
        "shulker_bullet",
        "skull",
        "trident",
        "wind_charge",
    };

    internal static bool SkipsLivingEntityRendererBasis(string stemLower) =>
        StemsSkippingLivingPreviewBasis.Contains(stemLower);

    /// <summary>
    /// When true, fast pose-captured bones still need the vanilla LER <c>scale(-1,-1,1)</c> folded as <c>worldRoot * bone</c> (default), or
    /// <c>bone * worldRoot</c> for quadrupeds when <see cref="CleanRoomEntityModelRuntime.UsesQuadrupedLerMirrorRightComposeLocalChain"/> matches.
    /// </summary>
    public static bool ShouldApplyStandardLivingPreviewBasis(string normalizedAssetPath, string stemLower)
    {
        if (RequiresFullMeshBoneExtract(normalizedAssetPath))
        {
            return false;
        }

        return !StemsSkippingLivingPreviewBasis.Contains(stemLower);
    }
}
