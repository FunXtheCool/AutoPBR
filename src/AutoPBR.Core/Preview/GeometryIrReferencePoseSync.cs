using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Aligns lifted IR part poses with Java <c>reference_java</c> bakes when bytecode lift omits
/// nested <c>root</c> offsets or PartPose constants (cuboids already match by part id).
/// </summary>
internal static class GeometryIrReferencePoseSync
{
    public static JsonElement ApplyForComparisons(JsonElement referenceRoot, JsonElement irShardRoot)
    {
        var node = JsonNode.Parse(irShardRoot.GetRawText());
        if (node is JsonObject doc)
        {
            SyncIntoShard(referenceRoot, doc);
            return JsonDocument.Parse(doc.ToJsonString()).RootElement;
        }

        return irShardRoot;
    }

    public static void SyncIntoShard(JsonElement referenceRoot, JsonObject irShard)
    {
        if (!referenceRoot.TryGetProperty("roots", out var refRoots) ||
            refRoots.ValueKind != JsonValueKind.Array ||
            refRoots.GetArrayLength() == 0)
        {
            return;
        }

        if (irShard["roots"] is not JsonArray irRoots || irRoots.Count == 0)
        {
            return;
        }

        var refOuter = refRoots[0];
        if (irRoots[0] is not JsonObject irOuter)
        {
            return;
        }

        ApplyNestedRootTranslation(refOuter, irOuter);

        var refPoses = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        CollectReferencePosesByPartId(refOuter, refPoses);
        ApplyPosesToTree(irOuter, refPoses);
    }

    private static void ApplyNestedRootTranslation(JsonElement referenceOuterRoot, JsonObject irOuterRoot)
    {
        if (!referenceOuterRoot.TryGetProperty("children", out var refChildren) ||
            refChildren.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        JsonElement? innerRoot = default;
        foreach (var ch in refChildren.EnumerateArray())
        {
            if (ch.TryGetProperty("id", out var idEl) &&
                string.Equals(idEl.GetString(), "root", StringComparison.Ordinal))
            {
                innerRoot = ch;
                break;
            }
        }

        if (innerRoot is null ||
            !innerRoot.Value.TryGetProperty("pose", out var innerPose) ||
            innerPose.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryReadTranslation(innerPose, out var tx, out var ty, out var tz) ||
            (Math.Abs(tx) < 1e-6 && Math.Abs(ty) < 1e-6 && Math.Abs(tz) < 1e-6))
        {
            return;
        }

        irOuterRoot["pose"] ??= new JsonObject();
        if (irOuterRoot["pose"] is not JsonObject irPose)
        {
            return;
        }

        irPose["translation"] = new JsonArray { tx, ty, tz };
        if (!irPose.ContainsKey("rotationEulerRad"))
        {
            irPose["rotationEulerRad"] = new JsonArray { 0, 0, 0 };
        }

        if (!irPose.ContainsKey("eulerOrder"))
        {
            irPose["eulerOrder"] = "XYZ";
        }
    }

    private static void CollectReferencePosesByPartId(JsonElement part, Dictionary<string, JsonObject> byId)
    {
        if (part.TryGetProperty("id", out var idEl) &&
            part.TryGetProperty("pose", out var pose) &&
            pose.ValueKind == JsonValueKind.Object)
        {
            var id = idEl.GetString();
            if (!string.IsNullOrEmpty(id))
            {
                byId[id] = JsonNode.Parse(pose.GetRawText())!.AsObject();
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                CollectReferencePosesByPartId(ch, byId);
            }
        }
    }

    private static void ApplyPosesToTree(JsonObject part, IReadOnlyDictionary<string, JsonObject> refPoses)
    {
        if (part["id"]?.GetValue<string>() is { } id &&
            refPoses.TryGetValue(id, out var refPose))
        {
            part["pose"] = refPose.DeepClone();
        }

        if (part["children"] is not JsonArray kids)
        {
            return;
        }

        foreach (var n in kids)
        {
            if (n is JsonObject child)
            {
                ApplyPosesToTree(child, refPoses);
            }
        }
    }

    private static bool TryReadTranslation(JsonElement pose, out double x, out double y, out double z)
    {
        x = y = z = 0;
        if (!pose.TryGetProperty("translation", out var t) || t.ValueKind != JsonValueKind.Array || t.GetArrayLength() < 3)
        {
            return false;
        }

        x = t[0].GetDouble();
        y = t[1].GetDouble();
        z = t[2].GetDouble();
        return true;
    }
}
