namespace AutoPBR.Preview.Blocks;

internal static class VanillaBlockCrossSpriteBuilder
{
    internal static MergedJavaBlockModel Build(
        string textureKey,
        IReadOnlyDictionary<string, string> textures)
    {
        var key = textureKey.StartsWith('#') ? textureKey : "#" + textureKey;
        var northSouth = BuildSlabFaces(key);
        var westEast = BuildSlabFaces(key);
        return new MergedJavaBlockModel
        {
            Elements =
            [
                new ModelElement
                {
                    Name = "cross_ns",
                    From = [0f, 0f, 7.5f],
                    To = [16f, 16f, 8.5f],
                    Faces = northSouth,
                },
                new ModelElement
                {
                    Name = "cross_we",
                    From = [7.5f, 0f, 0f],
                    To = [8.5f, 16f, 16f],
                    Faces = westEast,
                },
            ],
            Textures = new Dictionary<string, string>(textures, StringComparer.Ordinal),
        };
    }

    private static Dictionary<string, ModelFace> BuildSlabFaces(string textureKey) =>
        VanillaBlockCubeBuilder.FaceNames.ToDictionary(
            face => face,
            face => new ModelFace { TextureKey = textureKey, Uv = [0f, 0f, 16f, 16f] },
            StringComparer.OrdinalIgnoreCase);
}
