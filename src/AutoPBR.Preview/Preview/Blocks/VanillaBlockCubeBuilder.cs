namespace AutoPBR.Preview.Blocks;

internal static class VanillaBlockCubeBuilder
{
    internal static readonly string[] FaceNames = ["north", "south", "west", "east", "up", "down"];

    private static readonly string[] FaceOrder = FaceNames;

    internal static MergedJavaBlockModel Build(
        IReadOnlyDictionary<string, string> faceTextureKeys,
        IReadOnlyDictionary<string, string> textures)
    {
        var faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase);
        foreach (var faceName in FaceOrder)
        {
            if (!faceTextureKeys.TryGetValue(faceName, out var textureKey))
            {
                continue;
            }

            faces[faceName] = new ModelFace
            {
                TextureKey = textureKey.StartsWith('#') ? textureKey : "#" + textureKey,
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
                    To = [16f, 16f, 16f],
                    Faces = faces,
                },
            ],
            Textures = new Dictionary<string, string>(textures, StringComparer.Ordinal),
        };
    }

    internal static Dictionary<string, string> BuildFaceTextureKeys(IReadOnlyDictionary<string, string> slotToZipPath)
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var face in FaceOrder)
        {
            if (slotToZipPath.ContainsKey(face))
            {
                keys[face] = face;
            }
        }

        return keys;
    }
}
