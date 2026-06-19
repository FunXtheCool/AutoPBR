using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

internal static partial class GeometryIrPartTreeRepair
{
    private readonly record struct BoatCuboidPivotSpec(
        float PivotX,
        float PivotY,
        float PivotZ,
        bool ApplyWhenPartRotationZero = false);

    private static readonly Dictionary<string, BoatCuboidPivotSpec> BoatFamilyCuboidPivotByPartId =
        new(StringComparer.Ordinal)
        {
            ["bottom"] = new(0f, -1f, -1.5f),
            ["back"] = new(-4f, -4f, 0f),
            ["front"] = new(0f, -4f, 0f),
            ["right"] = new(0f, -4f, 0f),
            ["left_paddle"] = new(0f, 1f, 4f),
            ["right_paddle"] = new(0f, 1f, 4f),
        };

    private static readonly Dictionary<string, BoatCuboidPivotSpec> MinecartCuboidPivotByPartId =
        new(StringComparer.Ordinal)
        {
            ["bottom"] = new(0f, 0f, 0f),
            ["front"] = new(0f, 0f, 0f),
            ["back"] = new(0f, 0f, 0f),
            ["left"] = new(0f, 0f, 0f),
            ["right"] = new(0f, 0f, 0f),
        };

    private static bool ShouldRepairObjectBoatFamily(string? officialJvmName) =>
        ShouldRepairObjectEntityHullPartPose(officialJvmName);

    private static bool ShouldRepairObjectEntityHullPartPose(string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        return officialJvmName.Contains(".object.boat.", StringComparison.Ordinal) ||
               officialJvmName.Contains(".object.cart.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Java boat factories bake hull panel <c>PartPose.ROTATION_*</c> as part euler; vanilla entity preview matches
    /// CleanRoom <see cref="CleanRoomEntityModelRuntime"/> cuboid pivots. Hoist part rotation onto cuboids before emit.
    /// </summary>
    private static void RepairObjectBoatFamilyHullPartPose(JsonObject shardDoc)
    {
        if (shardDoc["roots"] is not JsonArray roots)
        {
            return;
        }

        var pivotTable = shardDoc["officialJvmName"]?.GetValue<string>() is { } jvm &&
                         jvm.Contains(".object.cart.", StringComparison.Ordinal)
            ? MinecartCuboidPivotByPartId
            : BoatFamilyCuboidPivotByPartId;
        var officialJvm = shardDoc["officialJvmName"]?.GetValue<string>();
        var skipBottomHullRepair = !string.IsNullOrWhiteSpace(officialJvm) &&
            officialJvm.Contains(".object.boat.RaftModel", StringComparison.Ordinal);

        foreach (var root in roots)
        {
            if (root is JsonObject rootObj && rootObj["children"] is JsonArray rootKids)
            {
                RepairObjectEntityHullPartPoseRecursive(rootKids, pivotTable, skipBottomHullRepair);
            }
        }
    }

    private static void RepairObjectEntityHullPartPoseRecursive(
        JsonArray parts,
        Dictionary<string, BoatCuboidPivotSpec> pivotTable,
        bool skipBottomHullRepair)
    {
        foreach (var node in parts)
        {
            if (node is not JsonObject part)
            {
                continue;
            }

            var partId = part["id"]?.GetValue<string>() ?? "";
            if (skipBottomHullRepair &&
                string.Equals(partId, "bottom", StringComparison.Ordinal))
            {
                if (part["children"] is JsonArray bottomKids)
                {
                    RepairObjectEntityHullPartPoseRecursive(bottomKids, pivotTable, skipBottomHullRepair);
                }

                continue;
            }

            if (pivotTable.TryGetValue(partId, out var pivotSpec) &&
                part["pose"] is JsonObject pose &&
                part["cuboids"] is JsonArray cuboids &&
                cuboids.Count > 0 &&
                TryReadPoseRotation(pose, out var rx, out var ry, out var rz) &&
                (Math.Abs(rx) > 1e-5 || Math.Abs(ry) > 1e-5 || Math.Abs(rz) > 1e-5 || pivotSpec.ApplyWhenPartRotationZero))
            {
                foreach (var cuboidNode in cuboids)
                {
                    if (cuboidNode is not JsonObject cuboid)
                    {
                        continue;
                    }

                    cuboid["cuboidRotationEulerRad"] = new JsonArray { rx, ry, rz };
                    cuboid["rotationPivot"] = new JsonArray { pivotSpec.PivotX, pivotSpec.PivotY, pivotSpec.PivotZ };
                }

                pose["rotationEulerRad"] = new JsonArray { 0, 0, 0 };
            }

            if (part["children"] is JsonArray kids)
            {
                RepairObjectEntityHullPartPoseRecursive(kids, pivotTable, skipBottomHullRepair);
            }
        }
    }

    private static bool TryReadPoseRotation(JsonObject pose, out double rx, out double ry, out double rz)
    {
        rx = ry = rz = 0;
        if (pose["rotationEulerRad"] is not JsonArray rot || rot.Count < 3)
        {
            return false;
        }

        rx = rot[0]?.GetValue<double>() ?? 0;
        ry = rot[1]?.GetValue<double>() ?? 0;
        rz = rot[2]?.GetValue<double>() ?? 0;
        return true;
    }

    /// <summary>
    /// Direction-mask chest lifts anchor bottom/lid at x=0; reference_java bakes x=1 inset on single-body layers.
    /// Single-body layers use plain <c>addBox</c> (all faces); strip mistaken direction masks from bytecode bleed.
    /// </summary>
    private static void RepairObjectChestModelCuboidOrigins(JsonObject shardDoc)
    {
        var officialJvm = shardDoc["officialJvmName"]?.GetValue<string>();
        if (officialJvm is null ||
            !officialJvm.StartsWith("net.minecraft.client.model.object.chest.ChestModel", StringComparison.Ordinal))
        {
            return;
        }

        if (shardDoc["roots"] is not JsonArray roots)
        {
            return;
        }

        var factoryMethod = shardDoc["factoryMethod"]?.GetValue<string>();
        if (string.Equals(factoryMethod, "createSingleBodyLayer", StringComparison.Ordinal))
        {
            RepairChestSingleBodyFaceMasks(roots);
        }

        if (!string.Equals(officialJvm, "net.minecraft.client.model.object.chest.ChestModel", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var root in roots)
        {
            if (root is not JsonObject rootObj || rootObj["children"] is not JsonArray rootKids)
            {
                continue;
            }

            if (string.Equals(factoryMethod, "createDoubleBodyLeftLayer", StringComparison.Ordinal))
            {
                continue;
            }

            RepairChestPartCuboidOrigin(rootKids, "bottom", dx: 1f);
            RepairChestPartCuboidOrigin(rootKids, "lid", dx: 1f);
            RepairChestLockCuboidOrigin(rootKids, factoryMethod);
        }
    }

    private static void RepairChestPartCuboidOrigin(JsonArray rootKids, string partId, float dx)
    {
        if (!TryFindPartById(rootKids, partId, out var partNode) || partNode is not JsonObject part ||
            part["cuboids"] is not JsonArray cuboids ||
            cuboids.Count == 0 ||
            cuboids[0] is not JsonObject cuboid ||
            cuboid["from"] is not JsonArray from ||
            cuboid["to"] is not JsonArray to ||
            from.Count < 3 ||
            to.Count < 3)
        {
            return;
        }

        var fromX = from[0]?.GetValue<double>() ?? 0;
        if (fromX >= 0.5)
        {
            return;
        }

        from[0] = fromX + dx;
        to[0] = (to[0]?.GetValue<double>() ?? 0) + dx;
    }

    private static void RepairChestLockCuboidOrigin(JsonArray rootKids, string? factoryMethod)
    {
        if (!string.Equals(factoryMethod, "createSingleBodyLayer", StringComparison.Ordinal))
        {
            return;
        }

        if (!TryFindPartById(rootKids, "lock", out var partNode) || partNode is not JsonObject part ||
            part["cuboids"] is not JsonArray cuboids ||
            cuboids.Count == 0 ||
            cuboids[0] is not JsonObject cuboid ||
            cuboid["from"] is not JsonArray from ||
            cuboid["to"] is not JsonArray to ||
            from.Count < 3 ||
            to.Count < 3)
        {
            return;
        }

        var fromX = from[0]?.GetValue<double>() ?? 0;
        if (fromX is >= 6 and <= 9)
        {
            return;
        }

        from[0] = 7;
        from[1] = -2;
        from[2] = 14;
        to[0] = 9;
        to[1] = 2;
        to[2] = 15;
    }

    private static void RepairChestSingleBodyFaceMasks(JsonArray roots)
    {
        foreach (var root in roots)
        {
            if (root is JsonObject rootObj)
            {
                RepairChestSingleBodyFaceMasksRecursive(rootObj);
            }
        }
    }

    private static void RepairChestSingleBodyFaceMasksRecursive(JsonObject part)
    {
        if (part["cuboids"] is JsonArray cuboids)
        {
            foreach (var cuboidNode in cuboids)
            {
                if (cuboidNode is JsonObject cuboid)
                {
                    cuboid.Remove("faceMask");
                }
            }
        }

        if (part["children"] is not JsonArray kids)
        {
            return;
        }

        foreach (var child in kids)
        {
            if (child is JsonObject childObj)
            {
                RepairChestSingleBodyFaceMasksRecursive(childObj);
            }
        }
    }
}
