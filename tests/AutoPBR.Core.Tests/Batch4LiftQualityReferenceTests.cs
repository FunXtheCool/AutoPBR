using System.Text.Json;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Batch 4 (object/NPC/zombie attachment models): reference_java bakes vs committed 26.1.2 IR shards.
/// </summary>
[Trait(GeometryIrTestTierSupport.DiagnosticCategory, "LiftQuality")]
public sealed class Batch4LiftQualityReferenceTests
{
    private static readonly string[] Batch4JvmNames =
    [
        "net.minecraft.client.model.monster.zombie.BabyDrownedModel",
        "net.minecraft.client.model.monster.zombie.BabyZombieModel",
        "net.minecraft.client.model.monster.zombie.BabyZombieVillagerModel",
        "net.minecraft.client.model.monster.zombie.DrownedModel",
        "net.minecraft.client.model.monster.zombie.ZombieModel",
        "net.minecraft.client.model.monster.zombie.ZombieVillagerModel",
        "net.minecraft.client.model.npc.BabyVillagerModel",
        "net.minecraft.client.model.npc.VillagerModel",
        "net.minecraft.client.model.object.armorstand.ArmorStandArmorModel",
        "net.minecraft.client.model.object.armorstand.ArmorStandModel",
        "net.minecraft.client.model.object.banner.BannerFlagModel",
        "net.minecraft.client.model.object.banner.BannerModel",
        "net.minecraft.client.model.object.bell.BellModel",
        "net.minecraft.client.model.object.boat.AbstractBoatModel",
        "net.minecraft.client.model.object.boat.BoatModel",
        "net.minecraft.client.model.object.boat.RaftModel",
        "net.minecraft.client.model.object.book.BookModel",
        "net.minecraft.client.model.object.cart.MinecartModel",
        "net.minecraft.client.model.object.chest.ChestModel",
        "net.minecraft.client.model.object.crystal.EndCrystalModel",
        "net.minecraft.client.model.object.equipment.ElytraModel",
        "net.minecraft.client.model.object.equipment.ShieldModel",
        "net.minecraft.client.model.object.leash.LeashKnotModel",
        "net.minecraft.client.model.object.projectile.ArrowModel",
        "net.minecraft.client.model.object.projectile.ShulkerBulletModel",
        "net.minecraft.client.model.object.projectile.TridentModel",
        "net.minecraft.client.model.object.projectile.WindChargeModel",
        "net.minecraft.client.model.object.skull.DragonHeadModel",
        "net.minecraft.client.model.object.skull.PiglinHeadModel",
        "net.minecraft.client.model.object.skull.SkullModel",
        "net.minecraft.client.model.player.PlayerEarsModel",
    ];

    public static IEnumerable<object[]> Batch4Cases() => Batch4JvmNames.Select(j => new object[] { j });

    [Theory]
    [MemberData(nameof(Batch4Cases))]
    public void Batch4_reference_cuboids_match_committed_ok_shard(string jvm)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        var irPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        Assert.True(File.Exists(referencePath), $"missing reference bake: {jvm}");
        Assert.True(File.Exists(irPath), $"missing IR shard: {jvm}");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(irPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        Assert.Equal("reference_java", reference.RootElement.GetProperty("extractionStatus").GetString());

        using var ir = JsonDocument.Parse(File.ReadAllText(irPath));
        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement, ir.RootElement, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message ?? jvm);
    }

    [Fact]
    public void Batch4_quality_report_marks_all_reference_cuboids_match()
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var reportPath = Path.Combine(root, "docs", "generated", "geometry-lift-quality-26.1.2.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        var byJvm = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var entry in report.RootElement.GetProperty("entries").EnumerateArray())
        {
            var name = entry.GetProperty("officialJvmName").GetString();
            if (!string.IsNullOrEmpty(name))
            {
                byJvm[name] = entry;
            }
        }

        foreach (var jvm in Batch4JvmNames)
        {
            var irPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
            if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(irPath, out var status) ||
                !string.Equals(status, "ok", StringComparison.Ordinal))
            {
                continue;
            }

            if (!byJvm.TryGetValue(jvm, out var row))
            {
                continue;
            }

            Assert.True(
                row.TryGetProperty("referenceCuboidsMatch", out var match) &&
                match.ValueKind == JsonValueKind.True,
                $"{jvm}: referenceCuboidsMatch={match}");
        }
    }
}
