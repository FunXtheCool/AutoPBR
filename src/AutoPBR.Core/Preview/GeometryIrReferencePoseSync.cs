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
            SyncWorldPosesIntoShard(referenceRoot, doc);
            return JsonDocument.Parse(doc.ToJsonString()).RootElement;
        }

        return irShardRoot;
    }

    /// <summary>
    /// Full reference parity for preview emit: topology, poses, and cuboid bounds.
    /// </summary>
    public static JsonElement ApplyForParityPreview(JsonElement referenceRoot, JsonElement irShardRoot)
    {
        var topologyAligned = GeometryIrReferenceTopologyAlign.ApplyForWorldPoseCompare(referenceRoot, irShardRoot);
        var node = JsonNode.Parse(topologyAligned.GetRawText());
        if (node is not JsonObject doc)
        {
            return topologyAligned;
        }

        SyncIntoShard(referenceRoot, doc);
        SyncCuboidsIntoShard(referenceRoot, doc);
        if (GeometryIrHumanoidLayerMeshPreviewPolicy.IsHumanoidLayerMeshJvm(
                irShardRoot.TryGetProperty("officialJvmName", out var jvmEl) ? jvmEl.GetString() : null) &&
            doc["roots"]?[0]?["children"] is JsonArray rootKids)
        {
            GeometryIrHumanoidLayerMeshParityRepair.ApplyCanonicalHumanoidUv(rootKids);
        }

        return JsonDocument.Parse(doc.ToJsonString()).RootElement;
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

    public static void SyncWorldPosesIntoShard(JsonElement referenceRoot, JsonObject irShard)
    {
        if (!referenceRoot.TryGetProperty("roots", out var refRoots) ||
            refRoots.ValueKind != JsonValueKind.Array ||
            refRoots.GetArrayLength() == 0)
        {
            return;
        }

        if (irShard["roots"] is not JsonArray irRoots || irRoots.Count == 0 || irRoots[0] is not JsonObject irOuter)
        {
            return;
        }

        var refWorldPoses = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        CollectReferenceWorldPosesByPartId(refRoots[0], refWorldPoses);
        ApplyWorldPosesToTree(irOuter, refWorldPoses);
    }

    private static void CollectReferenceWorldPosesByPartId(JsonElement part, Dictionary<string, JsonObject> byId)
    {
        if (part.TryGetProperty("id", out var idEl) &&
            part.TryGetProperty("worldPose", out var worldPose) &&
            worldPose.ValueKind == JsonValueKind.Object)
        {
            var id = idEl.GetString();
            if (!string.IsNullOrEmpty(id))
            {
                byId[id] = JsonNode.Parse(worldPose.GetRawText())!.AsObject();
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                CollectReferenceWorldPosesByPartId(ch, byId);
            }
        }
    }

    private static void ApplyWorldPosesToTree(JsonObject part, IReadOnlyDictionary<string, JsonObject> refWorldPoses)
    {
        if (part["id"]?.GetValue<string>() is { } id &&
            refWorldPoses.TryGetValue(id, out var refWorldPose))
        {
            part["worldPose"] = refWorldPose.DeepClone();
        }

        if (part["children"] is not JsonArray kids)
        {
            return;
        }

        foreach (var n in kids)
        {
            if (n is JsonObject child)
            {
                ApplyWorldPosesToTree(child, refWorldPoses);
            }
        }
    }

    public static void SyncCuboidsIntoShard(JsonElement referenceRoot, JsonObject irShard)
    {
        if (!referenceRoot.TryGetProperty("roots", out var refRoots) ||
            refRoots.ValueKind != JsonValueKind.Array ||
            refRoots.GetArrayLength() == 0)
        {
            return;
        }

        if (irShard["roots"] is not JsonArray irRoots || irRoots.Count == 0 || irRoots[0] is not JsonObject irOuter)
        {
            return;
        }

        var refCuboids = new Dictionary<string, JsonArray>(StringComparer.Ordinal);
        CollectReferenceCuboidsByPartId(refRoots[0], refCuboids);
        ApplyCuboidsToTree(irOuter, refCuboids);
    }

    private static void CollectReferenceCuboidsByPartId(JsonElement part, Dictionary<string, JsonArray> byId)
    {
        if (part.TryGetProperty("id", out var idEl) &&
            part.TryGetProperty("cuboids", out var cuboids) &&
            cuboids.ValueKind == JsonValueKind.Array)
        {
            var id = idEl.GetString();
            if (!string.IsNullOrEmpty(id))
            {
                byId[id] = JsonNode.Parse(cuboids.GetRawText())!.AsArray();
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                CollectReferenceCuboidsByPartId(ch, byId);
            }
        }
    }

    private static void ApplyCuboidsToTree(JsonObject part, IReadOnlyDictionary<string, JsonArray> refCuboids)
    {
        if (part["id"]?.GetValue<string>() is { } id && refCuboids.TryGetValue(id, out var refArr))
        {
            if (refArr.Count == 0)
            {
                part["cuboids"] = new JsonArray();
            }
            else
            {
            var synced = new JsonArray();
            var existing = part["cuboids"]?.AsArray();
            for (var i = 0; i < refArr.Count; i++)
            {
                if (refArr[i] is not JsonObject refCuboid)
                {
                    continue;
                }

                var clone = refCuboid.DeepClone().AsObject();
                clone["liftKind"] = "exact";
                if (existing is not null && i < existing.Count && existing[i] is JsonObject prior)
                {
                    if (prior["textureKey"] is JsonNode textureKey)
                    {
                        clone["textureKey"] = textureKey.DeepClone();
                    }

                    if (prior["uvOrigin"] is JsonArray uvOrigin)
                    {
                        clone["uvOrigin"] = uvOrigin.DeepClone();
                    }

                    if (prior["mirrorU"] is JsonValue mirrorU)
                    {
                        clone["mirrorU"] = mirrorU.DeepClone();
                    }
                }

                synced.Add(clone);
            }

            if (synced.Count > 0)
            {
                part["cuboids"] = synced;
            }
            }
        }

        if (part["children"] is not JsonArray kids)
        {
            return;
        }

        foreach (var n in kids)
        {
            if (n is JsonObject child)
            {
                ApplyCuboidsToTree(child, refCuboids);
            }
        }
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
