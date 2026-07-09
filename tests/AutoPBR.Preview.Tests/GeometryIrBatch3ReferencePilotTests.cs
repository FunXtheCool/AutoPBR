using System.Text.Json;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>T1 pilots from lift-quality batch 3 (monsters/effects) with reference_java bakes.</summary>
public sealed class GeometryIrBatch3ReferencePilotTests
{
    private static readonly string[] HardestPilotJvmNames =
    [
        "net.minecraft.client.model.monster.dragon.EnderDragonModel",
        "net.minecraft.client.model.monster.ghast.GhastModel",
        "net.minecraft.client.model.monster.silverfish.SilverfishModel",
        "net.minecraft.client.model.monster.slime.MagmaCubeModel",
    ];

    private static readonly string[] HumanoidDelegatePilotJvmNames =
    [
        "net.minecraft.client.model.monster.enderman.EndermanModel",
        "net.minecraft.client.model.monster.skeleton.SkeletonModel",
        "net.minecraft.client.model.monster.skeleton.BoggedModel",
    ];

    public static IEnumerable<object[]> HumanoidDelegatePilotCases() =>
        HumanoidDelegatePilotJvmNames.Select(j => new object[] { j });

    public static IEnumerable<object[]> HardestPilotCases() =>
        HardestPilotJvmNames.Select(j => new object[] { j });

    [Theory]
    [MemberData(nameof(HardestPilotCases))]
    public void Batch3_hardest_pilot_reference_cuboids_match_committed_ok_shard(string jvm)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            return;
        }

        using var ir = JsonDocument.Parse(File.ReadAllText(shardPath));
        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement, ir.RootElement, tolerance: 0.08);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [MemberData(nameof(HumanoidDelegatePilotCases))]
    public void Batch3_humanoid_delegate_pilot_reference_cuboids_match_committed_ok_shard(string jvm) =>
        Batch3_hardest_pilot_reference_cuboids_match_committed_ok_shard(jvm);
}
