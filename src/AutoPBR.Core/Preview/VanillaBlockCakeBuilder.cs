namespace AutoPBR.Core.Preview;

/// <summary>
/// Vanilla cake bites=0: single inset slab (26.1.2 <c>cake.json</c>).
/// </summary>
internal static class VanillaBlockCakeBuilder
{
    internal static MergedJavaBlockModel Build(IReadOnlyDictionary<string, string> textures)
    {
        return new MergedJavaBlockModel
        {
            Elements =
            [
                new ModelElement
                {
                    From = [1f, 0f, 1f],
                    To = [15f, 8f, 15f],
                    Faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["down"] = Face("#bottom"),
                        ["up"] = Face("#top"),
                        ["north"] = Face("#side"),
                        ["south"] = Face("#side"),
                        ["west"] = Face("#side"),
                        ["east"] = Face("#side"),
                    },
                },
            ],
            Textures = new Dictionary<string, string>(textures, StringComparer.Ordinal),
        };
    }

    private static ModelFace Face(string textureKey) =>
        new() { TextureKey = textureKey, Uv = [0f, 0f, 16f, 16f] };
}
