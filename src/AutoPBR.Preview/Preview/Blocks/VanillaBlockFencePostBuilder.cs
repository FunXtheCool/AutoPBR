namespace AutoPBR.Preview.Blocks;

internal static class VanillaBlockFencePostBuilder
{
    internal static MergedJavaBlockModel BuildPostOnly(
        string textureKey,
        IReadOnlyDictionary<string, string> textures) =>
        Build(textureKey, textures, includeNorthLink: false);

    internal static MergedJavaBlockModel BuildWithNorthLink(
        string textureKey,
        IReadOnlyDictionary<string, string> textures) =>
        Build(textureKey, textures, includeNorthLink: true);

    private static MergedJavaBlockModel Build(
        string textureKey,
        IReadOnlyDictionary<string, string> textures,
        bool includeNorthLink)
    {
        var key = textureKey.StartsWith('#') ? textureKey : "#" + textureKey;
        var faces = VanillaBlockCubeBuilder.FaceNames.ToDictionary(
            face => face,
            face => new ModelFace { TextureKey = key, Uv = [0f, 0f, 16f, 16f] },
            StringComparer.OrdinalIgnoreCase);

        var elements = new List<ModelElement>
        {
            new()
            {
                Name = "fence_post",
                From = [6f, 0f, 6f],
                To = [10f, 16f, 10f],
                Faces = faces,
            },
        };

        if (includeNorthLink)
        {
            elements.Add(new ModelElement
            {
                Name = "fence_link_north",
                From = [7f, 12f, 0f],
                To = [9f, 15f, 6f],
                Faces = new Dictionary<string, ModelFace>(faces, StringComparer.OrdinalIgnoreCase),
            });
        }

        return new MergedJavaBlockModel
        {
            Elements = elements,
            Textures = new Dictionary<string, string>(textures, StringComparer.Ordinal),
        };
    }
}
