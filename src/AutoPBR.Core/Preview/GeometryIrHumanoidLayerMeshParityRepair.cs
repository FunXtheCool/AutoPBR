using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Canonical 64×64 biped UV origins for repaired <c>HumanoidModel.createMesh</c> hosts (zombie family).
/// </summary>
internal static class GeometryIrHumanoidLayerMeshParityRepair
{
    public static void ApplyCanonicalHumanoidUv(JsonArray rootChildren)
    {
        SetPrimaryCuboidUv(rootChildren, "head", 0, 0);
        SetPrimaryCuboidUv(rootChildren, "body", 16, 16);
        SetPrimaryCuboidUv(rootChildren, "left_arm", 40, 16);
        SetPrimaryCuboidUv(rootChildren, "right_arm", 40, 16);
        SetPrimaryCuboidUv(rootChildren, "left_leg", 0, 16);
        SetPrimaryCuboidUv(rootChildren, "right_leg", 0, 16);
    }

    private static void SetPrimaryCuboidUv(JsonArray parts, string partId, int u, int v)
    {
        if (!TryFindPartById(parts, partId, out var part) || part is null ||
            part["cuboids"] is not JsonArray cuboids ||
            cuboids.Count == 0 ||
            cuboids[0] is not JsonObject cuboid)
        {
            return;
        }

        cuboid["uvOrigin"] = new JsonArray(u, v);
    }

    private static bool TryFindPartById(JsonArray parts, string partId, out JsonObject? found)
    {
        found = null;
        foreach (var n in parts)
        {
            if (n is not JsonObject part)
            {
                continue;
            }

            if (string.Equals((string?)part["id"], partId, StringComparison.Ordinal))
            {
                found = part;
                return true;
            }

            if (part["children"] is JsonArray kids && TryFindPartById(kids, partId, out found))
            {
                return true;
            }
        }

        return false;
    }
}
