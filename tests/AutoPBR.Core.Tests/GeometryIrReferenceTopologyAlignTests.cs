using System.Text.Json;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.Shared;

namespace AutoPBR.Core.Tests;

public sealed class GeometryIrReferenceTopologyAlignTests
{
    [Theory]
    [InlineData("net.minecraft.client.model.animal.axolotl.AdultAxolotlModel")]
    [InlineData("net.minecraft.client.model.animal.axolotl.BabyAxolotlModel")]
    [InlineData("net.minecraft.client.model.animal.equine.BabyDonkeyModel")]
    [InlineData("net.minecraft.client.model.animal.rabbit.AdultRabbitModel")]
    [InlineData("net.minecraft.client.model.animal.rabbit.BabyRabbitModel")]
    [InlineData("net.minecraft.client.model.animal.rabbit.RabbitModel")]
    [InlineData("net.minecraft.client.model.monster.dragon.EnderDragonModel")]
    public void Composed_flat_pilot_passes_reference_hierarchy_gate(string jvmName)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvmName}.json");
        var refPath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvmName}.json");
        if (!File.Exists(refPath) ||
            !GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(jvmName, "ok", shard.RootElement, repo);
        Assert.True(entry.ReferenceHierarchyMatch, entry.ReferenceHierarchyMessage);
    }

    [Fact]
    public void Sniffer_body_reparents_under_bone_for_world_pose_gate()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var jvm = "net.minecraft.client.model.animal.sniffer.SnifferModel";
        var refPath = Path.Combine(repo, "tools/MinecraftGeometryReference/reference-output", $"{jvm}.json");
        var shardPath = Path.Combine(repo, "docs/generated/geometry/26.1.2", $"{jvm}.json");
        if (!File.Exists(refPath) || !File.Exists(shardPath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(refPath));
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var aligned = GeometryIrReferenceTopologyAlign.ApplyForWorldPoseCompare(reference.RootElement, repaired);
        var synced = GeometryIrReferencePoseSync.ApplyForComparisons(reference.RootElement, aligned);
        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(
            reference.RootElement, synced, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);

        using var index = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(repo, "docs/generated/geometry-index-26.1.2.json")));
        var entry = GeometryIrLiftQualityReport.AnalyzeShard(
            jvm,
            "ok",
            shard.RootElement,
            repo,
            GeometryAssemblyParityPilots.Load(repo, "26.1.2"),
            GeometryJavapPoseOracle.Context.TryCreate(repo, "26.1.2"));
        Assert.True(entry.ReferenceWorldPoseMatch == true, entry.ReferenceWorldPoseCompareMessage);
        Assert.True(entry.ReferenceHierarchyMatch, entry.ReferenceHierarchyMessage);
    }
}
