using System.Numerics;

namespace AutoPBR.Preview.Blocks;

/// <summary>
/// Synthetic door pair for texture preview: two full-height thin panels stacked on Y (32 texels total).
/// Lower panel (Y 0–16) uses <c>#bottom</c>; upper panel (Y 16–32) uses <c>#top</c> via <see cref="ModelElement.LocalToParent"/>.
/// Face UVs follow vanilla <c>door_bottom_left</c> / <c>door_top_left</c> templates (26.1.2).
/// </summary>
internal static class VanillaBlockDoorHalfBuilder
{
    private const float PanelDepth = 3f;
    private const float PanelHeight = 16f;

    internal static MergedJavaBlockModel BuildPair(IReadOnlyDictionary<string, string> textures)
    {
        return new MergedJavaBlockModel
        {
            Elements =
            [
                BuildPanelElement(isUpper: false, "#bottom"),
                BuildPanelElement(isUpper: true, "#top"),
            ],
            Textures = new Dictionary<string, string>(textures, StringComparer.Ordinal),
        };
    }

    private static ModelElement BuildPanelElement(bool isUpper, string textureKey)
    {
        var faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
        {
            ["north"] = new ModelFace { TextureKey = textureKey, Uv = [3f, 0f, 0f, 16f] },
            ["south"] = new ModelFace { TextureKey = textureKey, Uv = [0f, 0f, 3f, 16f] },
            ["west"] = new ModelFace { TextureKey = textureKey, Uv = [0f, 0f, 16f, 16f] },
            ["east"] = new ModelFace { TextureKey = textureKey, Uv = [16f, 0f, 0f, 16f] },
        };

        if (isUpper)
        {
            faces["up"] = new ModelFace
            {
                TextureKey = textureKey,
                Uv = [0f, 3f, 16f, 0f],
                RotationDegrees = 90,
            };
        }
        else
        {
            faces["down"] = new ModelFace
            {
                TextureKey = textureKey,
                Uv = [16f, 13f, 0f, 16f],
                RotationDegrees = 90,
            };
        }

        return new ModelElement
        {
            Name = isUpper ? "door_top" : "door_bottom",
            From = [0f, 0f, 0f],
            To = [PanelDepth, PanelHeight, 16f],
            LocalToParent = isUpper ? Matrix4x4.CreateTranslation(0f, PanelHeight, 0f) : Matrix4x4.Identity,
            Faces = faces,
        };
    }
}
