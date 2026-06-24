
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
    /// Equine rigs apply LER mirror as <c>LocalToParent * S</c> via a dedicated path. Chicken forces full mesh extract as an A/B path.
    /// When <c>GpuBindPoseInverseLocalToParent</c> is present, the default path is fast pose capture composed as
    /// <c>invBind · M_anim</c> (see <see cref="EntityEmulatedPreviewRebaker.TryFillEmulatedEntityBoneMatrices"/>).
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
    /// When true, fast pose-captured bones still need the vanilla LER <c>scale(-1,-1,1)</c> folded per
    /// <see cref="CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis"/> (same policy as catalog emit).
    /// Parity-catalog geometry IR meshes already include LER — see <see cref="EntityGpuBoneDispatchKind.ParityCatalog"/>.
    /// </summary>
    public static bool ShouldApplyStandardLivingPreviewBasis(string normalizedAssetPath, string stemLower)
    {
        if (RequiresFullMeshBoneExtract(normalizedAssetPath))
        {
            return false;
        }

        return CleanRoomEntityModelRuntime.ResolveGeometryIrLerBasis(
            officialJvmName: null,
            stemLower,
            normalizedAssetPath) is CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot
            or CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.RightComposeLocalChain;
    }
}
