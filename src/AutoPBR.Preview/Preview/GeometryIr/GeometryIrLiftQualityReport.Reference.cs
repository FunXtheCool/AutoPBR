using System.Text.Json;

namespace AutoPBR.Preview.GeometryIr;

/// <summary>Metrics for geometry IR shards (lift quality baseline / regression).</summary>
public static partial class GeometryIrLiftQualityReport
{
    private static void WalkMetrics(
        JsonElement part,
        int depth,
        ref int cuboids,
        ref int maxDepth,
        Dictionary<string, int> warnings)
    {
        maxDepth = Math.Max(maxDepth, depth);
        if (part.TryGetProperty("cuboids", out var cuboidArr) && cuboidArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cuboidArr.EnumerateArray())
            {
                cuboids++;
                if (c.TryGetProperty("liftWarnings", out var wArr) && wArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var w in wArr.EnumerateArray())
                    {
                        var code = w.GetString();
                        if (string.IsNullOrEmpty(code))
                        {
                            continue;
                        }

                        warnings[code] = warnings.GetValueOrDefault(code) + 1;
                    }
                }
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                WalkMetrics(ch, depth + 1, ref cuboids, ref maxDepth, warnings);
            }
        }
    }

    private static (bool? Match, string? Message) CompareReferenceWorldPoses(
        JsonElement referenceRoot,
        JsonElement shardRoot,
        string officialJvmName)
    {
        var repairedIr = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvmName, shardRoot);
        var repairedCmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(
            referenceRoot, repairedIr, tolerance: 0.05);
        if (repairedCmp.IsMatch)
        {
            return (true, null);
        }

        var topologyAligned = GeometryIrReferenceTopologyAlign.ApplyForWorldPoseCompare(referenceRoot, repairedIr);
        var poseSynced = GeometryIrReferencePoseSync.ApplyForComparisons(referenceRoot, topologyAligned);
        var alignedCmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(
            referenceRoot, poseSynced, tolerance: 0.05);
        if (alignedCmp.IsMatch)
        {
            return (true, null);
        }

        var rawCmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(
            referenceRoot, shardRoot, tolerance: 0.05);
        var message = alignedCmp.Message ?? repairedCmp.Message;
        if (rawCmp.IsMatch)
        {
            message = string.IsNullOrEmpty(message)
                ? "raw IR tree world pose match; parity-repaired tree diverges"
                : $"{message}; raw IR tree world pose match";
        }

        return (false, message);
    }

    private static JsonElement BuildIrRootForReferenceParityCompare(
        JsonElement referenceRoot,
        JsonElement shardRoot,
        string officialJvmName)
    {
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvmName, shardRoot);
        if (!shardRoot.TryGetProperty("liftSummary", out var liftSummary) ||
            !liftSummary.TryGetProperty("poseApproxCount", out var poseApproxEl) ||
            poseApproxEl.ValueKind != JsonValueKind.Number ||
            poseApproxEl.GetInt32() != 0)
        {
            return repaired;
        }

        return GeometryIrReferencePoseSync.ApplyForComparisons(referenceRoot, repaired);
    }

    private static bool TryCompareReferenceParityMesh(
        JsonElement referenceRoot,
        JsonElement shardRoot,
        string officialJvmName,
        out GeometryIrReferenceComparer.CompareResult cmp)
    {
        cmp = default;
        if (!shardRoot.TryGetProperty("extractionStatus", out var st) ||
            !string.Equals(st.GetString(), "ok", StringComparison.Ordinal))
        {
            return false;
        }

        var (atlasW, atlasH) = ResolveParityAtlasForQualityReport(officialJvmName);
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var mesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test", profile, officialJvmName, atlasW, atlasH, out _, shardRoot);
        if (mesh is null)
        {
            return false;
        }

        cmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(referenceRoot, mesh, tolerance: 0.08);
        return true;
    }

    private static (int W, int H) ResolveParityAtlasForQualityReport(string officialJvmName) =>
        officialJvmName switch
        {
            "net.minecraft.client.model.animal.fish.CodModel" => (32, 32),
            "net.minecraft.client.model.animal.fish.SalmonModel" => (32, 32),
            "net.minecraft.client.model.animal.chicken.ChickenModel" => (64, 32),
            "net.minecraft.client.model.animal.chicken.BabyChickenModel" => (64, 32),
            "net.minecraft.client.model.animal.cow.CowModel" => (64, 64),
            "net.minecraft.client.model.animal.cow.ColdCowModel" => (64, 64),
            "net.minecraft.client.model.animal.pig.PigModel" => (64, 64),
            "net.minecraft.client.model.ambient.BatModel" => (64, 64),
            "net.minecraft.client.model.monster.creeper.CreeperModel" => (64, 32),
            _ => (64, 64)
        };
}
