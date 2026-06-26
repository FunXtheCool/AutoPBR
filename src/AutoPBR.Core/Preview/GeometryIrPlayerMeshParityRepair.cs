using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Repairs known <c>HumanoidModel</c> / <c>PlayerModel.createMesh</c> wide-lift regressions where overlay shells
/// (hat/jacket) lift correctly but core head/body cuboids bind to wrong bytecode islands with stacked PartPose offsets.
/// </summary>
internal static class GeometryIrPlayerMeshParityRepair
{
    public static void Apply(JsonArray rootChildren)
    {
        if (!UsesPlayerWideMeshOverlayKit(rootChildren))
        {
            return;
        }

        RepairMisLiftedCoreLayout(rootChildren);
        RepairLegRootOffsets(rootChildren);
        RepairArmRootOffsets(rootChildren);
        RepairPlayerMeshCanonicalUv(rootChildren);
    }

    /// <summary>
    /// <c>HumanoidModel.createMesh</c> can lift an inflated head shell while the hat child keeps the reference box.
    /// </summary>
    public static void ApplyHumanoidHatHeadCanonicalBounds(JsonArray rootChildren)
    {
        if (UsesPlayerWideMeshOverlayKit(rootChildren) ||
            !TryFindPartById(rootChildren, "head", out var head) || head is null ||
            !TryReadPoseTranslation(head, out _, out var headY, out _) ||
            headY < 10 ||
            head["children"] is not JsonArray headKids ||
            !TryFindPartById(headKids, "hat", out var hat) || hat is null ||
            !TryGetFirstCuboid(hat, out var hatCuboid) ||
            !TryGetFirstCuboid(head, out var headCuboid) ||
            !IsCanonicalHumanoidHeadCuboid(hatCuboid))
        {
            return;
        }

        if (IsCanonicalHumanoidHeadCuboid(headCuboid))
        {
            return;
        }

        CopyCuboidBounds(hatCuboid, headCuboid, new JsonArray(0, 0));
        headCuboid.Remove("inflate");
    }

    private static bool IsCanonicalHumanoidHeadCuboid(JsonObject cuboid)
    {
        if (cuboid["from"] is not JsonArray from || cuboid["to"] is not JsonArray to ||
            from.Count < 3 || to.Count < 3)
        {
            return false;
        }

        return Math.Abs(from[0]!.GetValue<double>() + 4) < 0.05 &&
               Math.Abs(from[1]!.GetValue<double>() + 8) < 0.05 &&
               Math.Abs(from[2]!.GetValue<double>() + 4) < 0.05 &&
               Math.Abs(to[0]!.GetValue<double>() - 4) < 0.05 &&
               Math.Abs(to[1]!.GetValue<double>()) < 0.05 &&
               Math.Abs(to[2]!.GetValue<double>() - 4) < 0.05;
    }

    private static void RepairPlayerMeshCanonicalUv(JsonArray rootChildren)
    {
        SetPrimaryCuboidUv(rootChildren, "head", 0, 0);
        SetPrimaryCuboidUv(rootChildren, "body", 16, 16);
        SetPrimaryCuboidUv(rootChildren, "left_arm", 32, 48);
        SetPrimaryCuboidUv(rootChildren, "right_arm", 40, 16);
        SetPrimaryCuboidUv(rootChildren, "left_leg", 16, 48);
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

    private static bool UsesPlayerWideMeshOverlayKit(JsonArray rootChildren) =>
        TryFindPartById(rootChildren, "jacket", out _) &&
        TryFindPartById(rootChildren, "left_pants", out _);

    /// <summary>
    /// Wide lift can bind shrunk head/body shells at Y≈15/18 while hat/jacket retain reference-sized boxes.
    /// </summary>
    private static void RepairMisLiftedCoreLayout(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "head", out var head) || head is null ||
            !TryFindPartById(rootChildren, "body", out var body) || body is null ||
            !TryReadPoseTranslation(head, out _, out var headY, out _) ||
            !TryReadPoseTranslation(body, out _, out var bodyY, out _) ||
            headY < 10 || bodyY < 10)
        {
            return;
        }

        if (!TryFindPartById(rootChildren, "hat", out var hat) || hat is null ||
            !TryFindPartById(rootChildren, "jacket", out var jacket) || jacket is null ||
            !TryGetFirstCuboid(hat, out var hatCuboid) ||
            !TryGetFirstCuboid(jacket, out var jacketCuboid) ||
            !TryGetFirstCuboid(head, out var headCuboid) ||
            !TryGetFirstCuboid(body, out var bodyCuboid))
        {
            return;
        }

        WritePoseTranslation(head, 0, 0, 0);
        WritePoseTranslation(body, 0, 0, 0);
        CopyCuboidBounds(hatCuboid, headCuboid, new JsonArray(0, 0));
        CopyCuboidBounds(jacketCuboid, bodyCuboid, new JsonArray(16, 16));
    }

    /// <summary>
    /// Lift sometimes binds <c>right_leg</c> at a stale island offset while <c>left_leg</c> keeps the reference pose.
    /// </summary>
    private static void RepairLegRootOffsets(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "left_leg", out var leftLeg) || leftLeg is null ||
            !TryFindPartById(rootChildren, "right_leg", out var rightLeg) || rightLeg is null ||
            !TryReadPoseTranslation(leftLeg, out var lx, out var ly, out var lz) ||
            !TryReadPoseTranslation(rightLeg, out var rx, out var ry, out var rz))
        {
            return;
        }

        if (ly < 11 || ly > 14)
        {
            return;
        }

        if (Math.Abs(ry - ly) < 0.5 && Math.Abs(rx + lx) < 0.5)
        {
            return;
        }

        WritePoseTranslation(rightLeg, -lx, ly, lz);
    }

    /// <summary>
    /// Wide <c>createMesh</c> lift sometimes binds <c>right_arm</c> at origin while <c>left_arm</c> keeps PartPose offset.
    /// </summary>
    private static void RepairArmRootOffsets(JsonArray rootChildren)
    {
        if (!TryFindPartById(rootChildren, "left_arm", out var leftArm) || leftArm is null ||
            !TryFindPartById(rootChildren, "right_arm", out var rightArm) || rightArm is null ||
            !TryReadPoseTranslation(leftArm, out var lx, out var ly, out var lz) ||
            !TryReadPoseTranslation(rightArm, out var rx, out _, out _))
        {
            return;
        }

        if (Math.Abs(rx) > 0.05 || Math.Abs(lx) < 0.05)
        {
            return;
        }

        WritePoseTranslation(rightArm, -lx, ly, lz);
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

    private static bool TryGetFirstCuboid(JsonObject part, out JsonObject cuboid)
    {
        cuboid = null!;
        if (part["cuboids"] is not JsonArray cuboids || cuboids.Count == 0 || cuboids[0] is not JsonObject first)
        {
            return false;
        }

        cuboid = first;
        return cuboid["from"] is JsonArray && cuboid["to"] is JsonArray;
    }

    private static void CopyCuboidBounds(JsonObject source, JsonObject target, JsonArray? uvOriginOverride)
    {
        target["from"] = source["from"]?.DeepClone();
        target["to"] = source["to"]?.DeepClone();
        if (uvOriginOverride is not null)
        {
            target["uvOrigin"] = uvOriginOverride;
        }
    }

    private static bool TryReadPoseTranslation(JsonObject part, out double x, out double y, out double z)
    {
        x = y = z = 0;
        if (part["pose"]?["translation"] is not JsonArray tr || tr.Count < 3)
        {
            return false;
        }

        x = tr[0]?.GetValue<double>() ?? 0;
        y = tr[1]?.GetValue<double>() ?? 0;
        z = tr[2]?.GetValue<double>() ?? 0;
        return true;
    }

    private static void WritePoseTranslation(JsonObject part, double x, double y, double z)
    {
        if (part["pose"] is not JsonObject pose)
        {
            pose = new JsonObject();
            part["pose"] = pose;
        }

        pose["translation"] = new JsonArray(x, y, z);
        pose["setupAnimPivot"] = new JsonArray(x, y, z);
    }
}
