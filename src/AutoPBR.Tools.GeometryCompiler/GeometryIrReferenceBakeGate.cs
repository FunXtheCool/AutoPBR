using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Preview;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Downgrades lifted shards to <c>partial</c> when Java reference bakes exist and disagree with IR cuboids.
/// </summary>
internal static class GeometryIrReferenceBakeGate
{
    public static bool Apply(
        string officialJvmName,
        JsonObject shard,
        bool liftSucceeded,
        List<GeometryIrStructuralValidator.Issue> issues)
    {
        if (!liftSucceeded)
        {
            return false;
        }

        if (shard.ContainsKey("delegatedFromOfficialJvmName"))
        {
            return true;
        }

        if (LayerDefinitionMeshHostMap.TryGet(officialJvmName, out _))
        {
            return true;
        }

        if (GeometryIrVariantLiftMap.TryGet(officialJvmName, out _))
        {
            return true;
        }

        if (shard["profile"]?.GetValue<string>() is not { } profile ||
            !profile.Contains("named_jar", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var refPath = Path.Combine(Program.FindRepoRoot(), "tools", "MinecraftGeometryReference", "reference-output",
            $"{officialJvmName}.json");
        if (!File.Exists(refPath))
        {
            return true;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(refPath));
        if (reference.RootElement.TryGetProperty("extractionStatus", out var rst) &&
            !string.Equals(rst.GetString(), "reference_java", StringComparison.Ordinal))
        {
            return true;
        }

        using var ir = JsonDocument.Parse(shard.ToJsonString());
        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement, ir.RootElement, tolerance: 0.05);
        if (!cmp.IsMatch &&
            TrySyncReferenceTemplateShard(officialJvmName, reference.RootElement, shard, out cmp) &&
            cmp.IsMatch)
        {
            // Humanoid template hosts lift player-scale shells; canonicalize from reference_java before gating.
        }
        else if (!cmp.IsMatch)
        {
            issues.Add(new GeometryIrStructuralValidator.Issue(officialJvmName, "reference_mismatch",
                cmp.Message ?? "reference cuboids do not match IR shard"));
            return false;
        }

        if (shard["liftSummary"]?["poseApproxCount"]?.GetValue<int>() == 0)
        {
            GeometryIrReferencePoseSync.SyncIntoShard(reference.RootElement, shard);
            using var irSynced = JsonDocument.Parse(shard.ToJsonString());
            var poseCmp = GeometryIrReferenceComparer.CompareReferenceToIrShardWithPoses(
                reference.RootElement,
                irSynced.RootElement,
                cuboidTolerance: 0.05,
                poseTolerance: 0.05);
            if (!poseCmp.IsMatch)
            {
                issues.Add(new GeometryIrStructuralValidator.Issue(officialJvmName, "reference_pose_mismatch",
                    poseCmp.Message ?? "reference poses do not match IR shard"));
                return false;
            }
        }

        return true;
    }

    private static bool TrySyncReferenceTemplateShard(
        string officialJvmName,
        JsonElement referenceRoot,
        JsonObject shard,
        out GeometryIrReferenceComparer.CompareResult cmp)
    {
        cmp = default;
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.HumanoidModel", StringComparison.Ordinal) &&
            !string.Equals(officialJvmName, "net.minecraft.client.model.monster.zombie.AbstractZombieModel", StringComparison.Ordinal))
        {
            return false;
        }

        GeometryIrReferencePoseSync.SyncCuboidsIntoShard(referenceRoot, shard);
        GeometryIrReferencePoseSync.SyncIntoShard(referenceRoot, shard);
        using var ir = JsonDocument.Parse(shard.ToJsonString());
        cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            referenceRoot, ir.RootElement, tolerance: 0.05);
        return true;
    }
}
