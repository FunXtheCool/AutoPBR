namespace AutoPBR.Preview.Blocks;

/// <summary>
/// Vanilla grass block cube: base faces plus a cutout <c>grass_block_side_overlay</c> shell on horizontal faces.
/// </summary>
internal static class VanillaBlockGrassCubeBuilder
{
    internal const string OverlayTextureKey = "overlay";
    internal const string OverlayStem = "grass_block_side_overlay";

    internal static void AddOverlaySlot(Dictionary<string, string> slotToZipPath, string defaultNamespace)
    {
        slotToZipPath[OverlayTextureKey] =
            BlockTextureSlotResolver.StemToBlockTextureZipPath(defaultNamespace, OverlayStem);
    }

    internal static MergedJavaBlockModel Build(
        BlockTextureParityRule rule,
        IReadOnlyDictionary<string, string> slotToZipPath,
        string defaultNamespace)
    {
        var textures = BlockTextureSlotResolver.BuildTextureDictionary(rule, slotToZipPath, defaultNamespace);
        textures[OverlayTextureKey] = BlockTextureSlotResolver.ZipPathToModelTextureReference(
            slotToZipPath.TryGetValue(OverlayTextureKey, out var overlayZip)
                ? overlayZip
                : BlockTextureSlotResolver.StemToBlockTextureZipPath(defaultNamespace, OverlayStem),
            defaultNamespace);

        var faceKeys = VanillaBlockCubeBuilder.BuildFaceTextureKeys(slotToZipPath);
        var baseModel = VanillaBlockCubeBuilder.Build(faceKeys, textures);

        var overlayFaces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase);
        foreach (var face in new[] { "north", "south", "east", "west" })
        {
            overlayFaces[face] = new ModelFace
            {
                TextureKey = "#" + OverlayTextureKey,
                Uv = [0f, 0f, 16f, 16f],
            };
        }

        var elements = new List<ModelElement>(baseModel.Elements.Count + 1);
        elements.AddRange(baseModel.Elements);
        elements.Add(new ModelElement
        {
            From = [0f, 0f, 0f],
            To = [16f, 16f, 16f],
            Faces = overlayFaces,
        });

        return new MergedJavaBlockModel
        {
            Elements = elements,
            Textures = textures,
        };
    }
}
