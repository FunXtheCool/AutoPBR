using System.Numerics;

namespace AutoPBR.Preview.Blocks;

/// <summary>Vanilla cake with one bite taken (26.1.2 <c>cake_slice1.json</c>).</summary>
internal static class VanillaBlockCakeSliceBuilder
{
    internal static MergedJavaBlockModel Build(IReadOnlyDictionary<string, string> textures)
    {
        return new MergedJavaBlockModel
        {
            Elements =
            [
                new ModelElement
                {
                    From = [3f, 0f, 1f],
                    To = [15f, 8f, 15f],
                    Faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["down"] = Face("#bottom"),
                        ["up"] = Face("#top"),
                        ["north"] = Face("#side"),
                        ["south"] = Face("#side"),
                        ["west"] = Face("#inside"),
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
