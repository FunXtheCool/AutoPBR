using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Player geometry IR is lifted with the wide-arm factory default (<c>createMesh(..., false)</c>).
/// Alex / slim skins need 3px arms and matching sleeve overlays at preview emit time.
/// </summary>
internal static class GeometryIrPlayerArmVariant
{
    private static readonly (string PartId, double[] From, double[] To)[] SlimArmCuboids =
    [
        ("left_arm", [-1, -2, -2], [2, 10, 2]),
        ("left_sleeve", [-1, -2, -2], [2, 10, 2]),
        ("right_arm", [-2, -2, -2], [1, 10, 2]),
        ("right_sleeve", [-2, -2, -2], [1, 10, 2]),
    ];

    public static JsonElement ApplySlimArmsIfNeeded(string? builderMethod, JsonElement geometryRoot)
    {
        if (!string.Equals(builderMethod, "PlayerSlim", StringComparison.OrdinalIgnoreCase))
        {
            return geometryRoot;
        }

        var node = JsonNode.Parse(geometryRoot.GetRawText());
        if (node is not JsonObject doc || doc["roots"] is not JsonArray roots)
        {
            return geometryRoot;
        }

        foreach (var root in roots.OfType<JsonObject>())
        {
            if (root["children"] is JsonArray kids)
            {
                PatchSlimArmsInForest(kids);
            }
        }

        return JsonDocument.Parse(doc.ToJsonString()).RootElement;
    }

    private static void PatchSlimArmsInForest(JsonArray parts)
    {
        foreach (var n in parts)
        {
            if (n is not JsonObject part)
            {
                continue;
            }

            var id = (string?)part["id"];
            foreach (var (partId, from, to) in SlimArmCuboids)
            {
                if (string.Equals(id, partId, StringComparison.Ordinal))
                {
                    ReplacePrimaryCuboid(part, from, to);
                    break;
                }
            }

            if (part["children"] is JsonArray kids)
            {
                PatchSlimArmsInForest(kids);
            }
        }
    }

    private static void ReplacePrimaryCuboid(JsonObject part, double[] from, double[] to)
    {
        if (part["cuboids"] is not JsonArray cuboids || cuboids.Count == 0)
        {
            return;
        }

        if (cuboids[0] is not JsonObject cuboid)
        {
            return;
        }

        cuboid["from"] = new JsonArray(from[0], from[1], from[2]);
        cuboid["to"] = new JsonArray(to[0], to[1], to[2]);
    }
}
