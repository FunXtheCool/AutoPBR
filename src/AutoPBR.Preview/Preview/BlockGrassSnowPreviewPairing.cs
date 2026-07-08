using AutoPBR.Core.Models;

namespace AutoPBR.Preview;

/// <summary>
/// Vanilla <c>grass_block_snow</c> pack JSON keeps <c>grass_block_top</c> on the up face; in-world snow is a layer above.
/// Preview appends a <c>snow_height2</c>-style cap so pack JSON stays authoritative.
/// </summary>
internal static class BlockGrassSnowPreviewPairing
{
    internal const string SnowCapTextureKey = "snow_cap";

    /// <summary>Thickness in block pixels (0–16 space); matches vanilla <c>snow_height2</c> / <c>layers=1</c>.</summary>
    internal const float SnowCapThickness = 2f;

    internal static bool IsGrassBlockSnowTexturePath(string archivePath) =>
        archivePath.Replace('\\', '/').TrimStart('/')
            .EndsWith("/grass_block_snow.png", StringComparison.OrdinalIgnoreCase);

    internal static bool TryAppendSnowCapForGrassBlockSnow(
        string textureArchivePath,
        string textureNamespace,
        ref MergedJavaBlockModel merged)
    {
        if (!IsGrassBlockSnowTexturePath(textureArchivePath) || AlreadyHasSnowCap(merged))
        {
            return false;
        }

        var textures = new Dictionary<string, string>(merged.Textures, StringComparer.Ordinal);
        textures[SnowCapTextureKey] = $"{textureNamespace}:block/snow";

        var capFaces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
        {
            ["down"] = Face("#" + SnowCapTextureKey, [0f, 0f, 16f, 16f]),
            ["up"] = Face("#" + SnowCapTextureKey, [0f, 0f, 16f, 16f]),
            ["north"] = Face("#" + SnowCapTextureKey, [0f, 14f, 16f, 16f]),
            ["south"] = Face("#" + SnowCapTextureKey, [0f, 14f, 16f, 16f]),
            ["west"] = Face("#" + SnowCapTextureKey, [0f, 14f, 16f, 16f]),
            ["east"] = Face("#" + SnowCapTextureKey, [0f, 14f, 16f, 16f]),
        };

        var elements = new List<ModelElement>(merged.Elements.Count + 1);
        elements.AddRange(merged.Elements);
        elements.Add(new ModelElement
        {
            Name = "snow_cap",
            From = [0f, 16f, 0f],
            To = [16f, 16f + SnowCapThickness, 16f],
            Faces = capFaces,
        });

        merged = new MergedJavaBlockModel
        {
            Elements = elements,
            Textures = textures,
            UsesLivingEntityRendererColumnYFlip = merged.UsesLivingEntityRendererColumnYFlip,
        };
        return true;
    }

    private static bool AlreadyHasSnowCap(MergedJavaBlockModel merged)
    {
        foreach (var element in merged.Elements)
        {
            if (element.To.Length < 2 || element.To[1] <= 16f + 1e-3f)
            {
                continue;
            }

            foreach (var face in element.Faces.Values)
            {
                if (!merged.Textures.TryGetValue(face.TextureKey.TrimStart('#'), out var texRef))
                {
                    continue;
                }

                if (texRef.Contains("/snow", StringComparison.OrdinalIgnoreCase) &&
                    !texRef.Contains("grass_block_snow", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ModelFace Face(string textureKey, float[] uv) =>
        new() { TextureKey = textureKey, Uv = uv };
}
