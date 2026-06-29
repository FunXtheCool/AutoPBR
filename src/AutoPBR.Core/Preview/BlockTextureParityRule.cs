namespace AutoPBR.Core.Preview;

/// <summary>
/// One routing row from <c>minecraft_26.1.2_block_texture_model_manifest.json</c> (vanilla 26.1.2 block diffuse parity).
/// </summary>
public sealed class BlockTextureParityRule
{
    public required string PathPrefix { get; init; }

    public required string FamilyId { get; init; }

    public required BlockTextureParityPreviewShape PreviewShape { get; init; }

    /// <summary>
    /// Face name (<c>up</c>, <c>down</c>, <c>north</c>, …) → block texture stem under <c>textures/block/</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string>? TextureSlots { get; init; }

    public bool CanSynthesizePreview() =>
        PreviewShape is BlockTextureParityPreviewShape.UniformCube
            or BlockTextureParityPreviewShape.CubeDirectional
            or BlockTextureParityPreviewShape.CubeColumnY
            or BlockTextureParityPreviewShape.ThinPlate
            or BlockTextureParityPreviewShape.DoorHalf
            or BlockTextureParityPreviewShape.CakeWedge
            or BlockTextureParityPreviewShape.CakeSlice
            or BlockTextureParityPreviewShape.CactusCross
            or BlockTextureParityPreviewShape.FencePost
            or BlockTextureParityPreviewShape.FenceWithLink
            or BlockTextureParityPreviewShape.RailTrack
            or BlockTextureParityPreviewShape.StairWedge
            or BlockTextureParityPreviewShape.CrossSprite;

    public bool CanSynthesizeCubePreview() => CanSynthesizePreview();
}
