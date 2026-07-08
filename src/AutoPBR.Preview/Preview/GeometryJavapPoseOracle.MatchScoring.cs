using System.Text.Json;

namespace AutoPBR.Preview;

public static partial class GeometryJavapPoseOracle
{
    public static CompareResult CompareShardToOracle(
        JsonElement shardRoot,
        IReadOnlyDictionary<string, PartPose> oracleByPartId,
        double tolerance = DefaultPoseTolerance)
    {
        var irById = CollectIrPosesByPartId(shardRoot);
        var comparableOracle = oracleByPartId
            .Where(static kv => !IsOracleOnlySyntheticPart(kv.Key, kv.Value))
            .ToArray();
        if (comparableOracle.Length == 0)
        {
            return new CompareResult(false, "oracle: no part bindings parsed", 0, irById.Count);
        }

        if (irById.Count < comparableOracle.Length)
        {
            return new CompareResult(
                false,
                $"pose part count oracle={comparableOracle.Length} ir={irById.Count}",
                comparableOracle.Length,
                irById.Count);
        }

        foreach (var (id, expected) in comparableOracle)
        {
            if (!irById.TryGetValue(id, out var actual))
            {
                return new CompareResult(
                    false,
                    $"missing IR pose for part '{id}'",
                    comparableOracle.Length,
                    irById.Count);
            }

            if (!PoseNear(expected, actual, tolerance))
            {
                return new CompareResult(
                    false,
                    $"part '{id}': oracle T=({expected.Tx:R},{expected.Ty:R},{expected.Tz:R}) R=({expected.Rx:R},{expected.Ry:R},{expected.Rz:R}) " +
                    $"ir T=({actual.Tx:R},{actual.Ty:R},{actual.Tz:R}) R=({actual.Rx:R},{actual.Ry:R},{actual.Rz:R})",
                    comparableOracle.Length,
                    irById.Count);
            }
        }

        return new CompareResult(true, null, comparableOracle.Length, irById.Count);
    }

    private static bool IsOracleOnlySyntheticPart(string partId, PartPose pose) =>
        string.Equals(partId, "root", StringComparison.Ordinal) ||
        (partId.EndsWith("_ear", StringComparison.Ordinal) &&
         pose is { Tx: 0, Ty: 0, Tz: 0, Rx: 0, Ry: 0, Rz: 0 });

    private static Dictionary<string, PartPose> CollectIrPosesByPartId(JsonElement shardRoot)
    {
        var byId = new Dictionary<string, PartPose>(StringComparer.Ordinal);
        if (!shardRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return byId;
        }

        foreach (var root in roots.EnumerateArray())
        {
            WalkIrPartPoses(root, byId);
        }

        return byId;
    }

    private static void WalkIrPartPoses(JsonElement part, Dictionary<string, PartPose> byId)
    {
        if (part.TryGetProperty("id", out var idEl) &&
            part.TryGetProperty("pose", out var pose) &&
            pose.ValueKind == JsonValueKind.Object)
        {
            var id = idEl.GetString();
            if (!string.IsNullOrEmpty(id) && !string.Equals(id, "root", StringComparison.Ordinal))
            {
                byId[id] = ReadIrPose(pose);
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                WalkIrPartPoses(ch, byId);
            }
        }
    }

    private static PartPose ReadIrPose(JsonElement pose)
    {
        static double At(JsonElement arr, int i) =>
            arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > i ? arr[i].GetDouble() : 0;

        var t = pose.GetProperty("translation");
        var r = pose.TryGetProperty("rotationEulerRad", out var rot) ? rot : default;
        return new PartPose(At(t, 0), At(t, 1), At(t, 2), At(r, 0), At(r, 1), At(r, 2));
    }

    private static bool PoseNear(PartPose a, PartPose b, double tolerance)
    {
        return Near(a.Tx, b.Tx, tolerance) &&
               Near(a.Ty, b.Ty, tolerance) &&
               Near(a.Tz, b.Tz, tolerance) &&
               Near(a.Rx, b.Rx, tolerance) &&
               Near(a.Ry, b.Ry, tolerance) &&
               Near(a.Rz, b.Rz, tolerance);
    }

    private static bool Near(double a, double b, double tolerance) => Math.Abs(a - b) <= tolerance;
}
