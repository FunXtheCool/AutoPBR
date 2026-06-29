namespace AutoPBR.Core.Preview;

/// <summary>
/// Vanilla cactus block model: inset side panels (26.1.2 <c>cactus.json</c>).
/// </summary>
internal static class VanillaBlockCactusBuilder
{
    internal static MergedJavaBlockModel Build(IReadOnlyDictionary<string, string> textures)
    {
        return new MergedJavaBlockModel
        {
            Elements =
            [
                new ModelElement
                {
                    From = [0f, 0f, 0f],
                    To = [16f, 16f, 16f],
                    Faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["down"] = Face("#bottom", [0f, 0f, 16f, 16f]),
                        ["up"] = Face("#top", [0f, 0f, 16f, 16f]),
                    },
                },
                new ModelElement
                {
                    From = [0f, 0f, 1f],
                    To = [16f, 16f, 15f],
                    Faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["north"] = Face("#side", [0f, 0f, 16f, 16f]),
                        ["south"] = Face("#side", [0f, 0f, 16f, 16f]),
                    },
                },
                new ModelElement
                {
                    From = [1f, 0f, 0f],
                    To = [15f, 16f, 16f],
                    Faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["west"] = Face("#side", [0f, 0f, 16f, 16f]),
                        ["east"] = Face("#side", [0f, 0f, 16f, 16f]),
                    },
                },
            ],
            Textures = new Dictionary<string, string>(textures, StringComparer.Ordinal),
        };
    }

    private static ModelFace Face(string textureKey, float[] uv) =>
        new() { TextureKey = textureKey, Uv = uv };
}
