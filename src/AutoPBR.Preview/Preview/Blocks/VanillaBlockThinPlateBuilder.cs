namespace AutoPBR.Preview.Blocks;

internal static class VanillaBlockThinPlateBuilder
{
    internal static MergedJavaBlockModel Build(
        string textureKey,
        IReadOnlyDictionary<string, string> textures)
    {
        var key = textureKey.StartsWith('#') ? textureKey : "#" + textureKey;
        var faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase);
        foreach (var face in VanillaBlockCubeBuilder.FaceNames)
        {
            faces[face] = new ModelFace
            {
                TextureKey = key,
                Uv = [0f, 0f, 16f, 16f],
            };
        }

        return new MergedJavaBlockModel
        {
            Elements =
            [
                new ModelElement
                {
                    From = [0f, 0f, 0f],
                    To = [16f, 16f, 3f],
                    Faces = faces,
                },
            ],
            Textures = new Dictionary<string, string>(textures, StringComparer.Ordinal),
        };
    }
}
