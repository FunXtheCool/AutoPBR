namespace AutoPBR.Core.Preview;

internal static class VanillaBlockRailTrackBuilder
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
                    From = [0f, 0f, 0f],
                    To = [16f, 1f, 16f],
                    Faces = faces,
                },
            ],
            Textures = new Dictionary<string, string>(textures, StringComparer.Ordinal),
        };
    }
}
