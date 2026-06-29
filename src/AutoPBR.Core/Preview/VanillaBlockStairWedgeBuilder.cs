namespace AutoPBR.Core.Preview;

internal static class VanillaBlockStairWedgeBuilder
{
    internal static MergedJavaBlockModel Build(
        string textureKey,
        IReadOnlyDictionary<string, string> textures)
    {
        var key = textureKey.StartsWith('#') ? textureKey : "#" + textureKey;
        var faces = VanillaBlockCubeBuilder.FaceNames.ToDictionary(
            face => face,
            face => new ModelFace { TextureKey = key, Uv = [0f, 0f, 16f, 16f] },
            StringComparer.OrdinalIgnoreCase);

        return new MergedJavaBlockModel
        {
            Elements =
            [
                new ModelElement
                {
                    Name = "stair_base",
                    From = [0f, 0f, 0f],
                    To = [16f, 8f, 16f],
                    Faces = faces,
                },
                new ModelElement
                {
                    Name = "stair_upper",
                    From = [0f, 8f, 8f],
                    To = [16f, 16f, 16f],
                    Faces = new Dictionary<string, ModelFace>(faces, StringComparer.OrdinalIgnoreCase),
                },
            ],
            Textures = new Dictionary<string, string>(textures, StringComparer.Ordinal),
        };
    }
}
